using BIDashboardBackend.Services;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Models;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Features.Ingest;
using BIDashboardBackend.Features.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Http;
using System.Linq.Expressions;
using Moq;
using FluentAssertions;

namespace BIDashboardBackend.Tests.Services
{
    [TestFixture]
    public class IngestServiceTests
    {
        private Mock<IDatasetRepository> _mockRepository;
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private IngestService _service;

        [SetUp]
        public void Setup()
        {
            _mockRepository = new Mock<IDatasetRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();

            // 使用真實的 CsvSniffer 實體
            var csvSniffer = new CsvSniffer();

            _service = new IngestService(
                _mockRepository.Object,
                _mockUnitOfWork.Object,
                _mockBackgroundJobClient.Object,
                csvSniffer  // 真實物件
            );
        }

        #region CreateDatasetAsync 測試

        [Test]
        public async Task CreateDatasetAsync_WithValidInput_ShouldReturnCreateDatasetResult()
        {
            // Arrange
            var datasetName = "測試資料集";
            var userId = 1L;
            var description = "測試描述";
            var expectedDatasetId = 1L;

            _mockRepository.Setup(x => x.GetDatasetCountByUserAsync(userId))
                          .ReturnsAsync(0); // 用戶目前有 0 個資料集
            _mockRepository.Setup(x => x.CreateDatasetAsync(datasetName, userId))
                          .ReturnsAsync(expectedDatasetId);

            // Act
            var result = await _service.CreateDatasetAsync(datasetName, userId, description);

            // Assert
            result.Should().NotBeNull();
            result.DatasetId.Should().Be(expectedDatasetId);
            result.Name.Should().Be(datasetName);
            result.Description.Should().Be(description);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            
            _mockRepository.Verify(x => x.GetDatasetCountByUserAsync(userId), Times.Once);
            _mockRepository.Verify(x => x.CreateDatasetAsync(datasetName, userId), Times.Once);
        }

        [Test]
        public async Task CreateDatasetAsync_WithEmptyName_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetName = "";
            var userId = 1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CreateDatasetAsync(datasetName, userId));
            
            exception.Message.Should().Be("資料集名稱不能為空 (Parameter 'datasetName')");
            exception.ParamName.Should().Be("datasetName");
        }

        [Test]
        public async Task CreateDatasetAsync_WithWhitespaceName_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetName = "   ";
            var userId = 1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CreateDatasetAsync(datasetName, userId));
            
            exception.Message.Should().Be("資料集名稱不能為空 (Parameter 'datasetName')");
            exception.ParamName.Should().Be("datasetName");
        }

        [Test]
        public async Task CreateDatasetAsync_WithInvalidUserId_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetName = "測試資料集";
            var userId = 0L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CreateDatasetAsync(datasetName, userId));
            
            exception.Message.Should().Be("用戶 ID 必須大於 0 (Parameter 'userId')");
            exception.ParamName.Should().Be("userId");
        }

        [Test]
        public async Task CreateDatasetAsync_WithNegativeUserId_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetName = "測試資料集";
            var userId = -1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CreateDatasetAsync(datasetName, userId));
            
            exception.Message.Should().Be("用戶 ID 必須大於 0 (Parameter 'userId')");
            exception.ParamName.Should().Be("userId");
        }

        [Test]
        public async Task CreateDatasetAsync_WithMaxDatasetsReached_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var datasetName = "測試資料集";
            var userId = 1L;
            var description = "測試描述";

            _mockRepository.Setup(x => x.GetDatasetCountByUserAsync(userId))
                          .ReturnsAsync(2); // 用戶已達到最大限制（2 個資料集）

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.CreateDatasetAsync(datasetName, userId, description));
            
            exception.Message.Should().Be("每個用戶最多只能創建 2 個資料集，您目前已有 2 個資料集");
            
            _mockRepository.Verify(x => x.GetDatasetCountByUserAsync(userId), Times.Once);
            _mockRepository.Verify(x => x.CreateDatasetAsync(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        }

        [Test]
        public async Task CreateDatasetAsync_WithOneDataset_ShouldAllowSecondDataset()
        {
            // Arrange
            var datasetName = "第二個資料集";
            var userId = 1L;
            var description = "測試描述";
            var expectedDatasetId = 2L;

            _mockRepository.Setup(x => x.GetDatasetCountByUserAsync(userId))
                          .ReturnsAsync(1); // 用戶目前有 1 個資料集，還可以創建 1 個
            _mockRepository.Setup(x => x.CreateDatasetAsync(datasetName, userId))
                          .ReturnsAsync(expectedDatasetId);

            // Act
            var result = await _service.CreateDatasetAsync(datasetName, userId, description);

            // Assert
            result.Should().NotBeNull();
            result.DatasetId.Should().Be(expectedDatasetId);
            result.Name.Should().Be(datasetName);
            result.Description.Should().Be(description);
            
            _mockRepository.Verify(x => x.GetDatasetCountByUserAsync(userId), Times.Once);
            _mockRepository.Verify(x => x.CreateDatasetAsync(datasetName, userId), Times.Once);
        }

        #endregion

        #region UploadCsvAsync 測試

        [Test]
        public async Task UploadCsvAsync_WithValidFile_ShouldReturnUploadResult()
        {
            // Arrange
            var csvContent = "name,age\nJohn,25\nJane,30\nBob,35"; // 有效的 CSV 資料
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var mockFile = new Mock<IFormFile>();
            
            mockFile.Setup(f => f.Length).Returns(csvBytes.Length);
            mockFile.Setup(f => f.FileName).Returns("test.csv");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(csvBytes));

            var userId = 1L;
            var datasetId = 1L;
            var batchId = 1L;
            var totalRows = 3L; // 實際的資料行數

            _mockRepository.Setup(x => x.GetBatchCountByDatasetAsync(datasetId))
                          .ReturnsAsync(0); // 資料集目前有 0 個批次
            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.CreateBatchAsync(datasetId, "test.csv", totalRows))
                          .ReturnsAsync(batchId);
            
            _mockRepository.Setup(x => x.UpsertColumnsAsync(batchId, It.IsAny<IEnumerable<DatasetColumn>>()))
                          .ReturnsAsync(2);
            
            _mockRepository.Setup(x => x.BulkCopyRowsAsync(batchId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(totalRows);

            // Act
            var result = await _service.UploadCsvAsync(mockFile.Object, userId, datasetId);

            // Assert
            result.Should().NotBeNull();
            result.BatchId.Should().Be(batchId);
            result.FileName.Should().Be("test.csv");
            result.TotalRows.Should().Be(totalRows);
            result.Status.Should().Be("Pending");

            _mockRepository.Verify(x => x.GetBatchCountByDatasetAsync(datasetId), Times.Once);
            _mockUnitOfWork.Verify(x => x.BeginAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
            _mockRepository.Verify(x => x.CreateBatchAsync(datasetId, "test.csv", totalRows), Times.Once);
            _mockRepository.Verify(x => x.UpsertColumnsAsync(batchId, It.IsAny<IEnumerable<DatasetColumn>>()), Times.Once);
            _mockRepository.Verify(x => x.BulkCopyRowsAsync(batchId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UploadCsvAsync_WithEmptyFile_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(0);
            var userId = 1L;
            var datasetId = 1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UploadCsvAsync(mockFile.Object, userId, datasetId));
            
            exception.Message.Should().Be("檔案為空");
        }

        [Test]
        public async Task UploadCsvAsync_WithInvalidDatasetId_ShouldThrowArgumentException()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            var userId = 1L;
            var datasetId = 0L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.UploadCsvAsync(mockFile.Object, userId, datasetId));
            
            exception.Message.Should().Be("資料集 ID 必須大於 0 (Parameter 'datasetId')");
            exception.ParamName.Should().Be("datasetId");
        }

        [Test]
        public Task UploadCsvAsync_WithException_ShouldRollbackTransaction()
        {
            // Arrange
            var csvContent = "name,age\nJohn,25\nJane,30"; // 有效的 CSV 資料
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var mockFile = new Mock<IFormFile>();
            
            mockFile.Setup(f => f.Length).Returns(csvBytes.Length);
            mockFile.Setup(f => f.FileName).Returns("test.csv");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(csvBytes));

            var userId = 1L;
            var datasetId = 1L;

            _mockRepository.Setup(x => x.GetBatchCountByDatasetAsync(datasetId))
                          .ReturnsAsync(0); // 資料集目前有 0 個批次
            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            // Mock Repository 拋出異常
            _mockRepository.Setup(x => x.CreateBatchAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>()))
                          .ThrowsAsync(new Exception("資料庫錯誤"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(() => 
                _service.UploadCsvAsync(mockFile.Object, userId, datasetId));

            _mockRepository.Verify(x => x.GetBatchCountByDatasetAsync(datasetId), Times.Once);
            _mockUnitOfWork.Verify(x => x.BeginAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
            return Task.CompletedTask;
        }

        [Test]
        public async Task UploadCsvAsync_WithMaxBatchesReached_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var csvContent = "name,age\nJohn,25\nJane,30";
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var mockFile = new Mock<IFormFile>();
            
            mockFile.Setup(f => f.Length).Returns(csvBytes.Length);
            mockFile.Setup(f => f.FileName).Returns("test.csv");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(csvBytes));

            var userId = 1L;
            var datasetId = 1L;

            _mockRepository.Setup(x => x.GetBatchCountByDatasetAsync(datasetId))
                          .ReturnsAsync(5); // 資料集已達到最大限制（5 個批次）

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UploadCsvAsync(mockFile.Object, userId, datasetId));
            
            exception.Message.Should().Be("每個資料集最多只能上傳 5 個檔案，此資料集目前已有 5 個檔案");
            
            _mockRepository.Verify(x => x.GetBatchCountByDatasetAsync(datasetId), Times.Once);
            _mockRepository.Verify(x => x.CreateBatchAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        }

        [Test]
        public async Task UploadCsvAsync_WithFourBatches_ShouldAllowFifthBatch()
        {
            // Arrange
            var csvContent = "name,age\nJohn,25\nJane,30";
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var mockFile = new Mock<IFormFile>();
            
            mockFile.Setup(f => f.Length).Returns(csvBytes.Length);
            mockFile.Setup(f => f.FileName).Returns("test.csv");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(csvBytes));

            var userId = 1L;
            var datasetId = 1L;
            var batchId = 5L;
            var totalRows = 2L;

            _mockRepository.Setup(x => x.GetBatchCountByDatasetAsync(datasetId))
                          .ReturnsAsync(4); // 資料集目前有 4 個批次，還可以上傳 1 個
            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.CreateBatchAsync(datasetId, "test.csv", totalRows))
                          .ReturnsAsync(batchId);
            
            _mockRepository.Setup(x => x.UpsertColumnsAsync(batchId, It.IsAny<IEnumerable<DatasetColumn>>()))
                          .ReturnsAsync(2);
            
            _mockRepository.Setup(x => x.BulkCopyRowsAsync(batchId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(totalRows);

            // Act
            var result = await _service.UploadCsvAsync(mockFile.Object, userId, datasetId);

            // Assert
            result.Should().NotBeNull();
            result.BatchId.Should().Be(batchId);
            result.FileName.Should().Be("test.csv");
            result.TotalRows.Should().Be(totalRows);
            result.Status.Should().Be("Pending");
            
            _mockRepository.Verify(x => x.GetBatchCountByDatasetAsync(datasetId), Times.Once);
            _mockRepository.Verify(x => x.CreateBatchAsync(datasetId, "test.csv", totalRows), Times.Once);
        }

        #endregion

        #region UpsertMappingsAsync 測試

        [Test]
        public async Task UpsertMappingsAsync_WithValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name", SystemField = SystemField.Name },
                    new SourceToSystemField { SourceColumn = "age", SystemField = SystemField.Age }
                }
            };

            var batch = new DatasetBatch { Id = 1L, DatasetId = 1L };
            var availableColumns = new HashSet<string> { "name", "age", "email" };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ReturnsAsync(batch);
            
            _mockRepository.Setup(x => x.GetAvailableSourceColumnsAsync(request.BatchId))
                          .ReturnsAsync(availableColumns);
            
            _mockRepository.Setup(x => x.UpsertMappingsAsync(request.BatchId, It.IsAny<IEnumerable<DatasetMapping>>()))
                          .ReturnsAsync(2);
            
            _mockRepository.Setup(x => x.SetBatchStatusAsync(request.BatchId, "Mapped", null))
                          .ReturnsAsync(1);

            // Act
            await _service.UpsertMappingsAsync(request);

            // Assert
            _mockUnitOfWork.Verify(x => x.BeginAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
            _mockRepository.Verify(x => x.GetBatchAsync(request.BatchId), Times.Once);
            _mockRepository.Verify(x => x.GetAvailableSourceColumnsAsync(request.BatchId), Times.Once);
            _mockRepository.Verify(x => x.UpsertMappingsAsync(request.BatchId, It.IsAny<IEnumerable<DatasetMapping>>()), Times.Once);
            _mockRepository.Verify(x => x.SetBatchStatusAsync(request.BatchId, "Mapped", null), Times.Once);
            // 注意：無法直接驗證 Hangfire 的擴展方法 Enqueue，因為它是擴展方法
        }

        [Test]
        public Task UpsertMappingsAsync_WithEmptyMappings_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>()
            };

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpsertMappingsAsync(request));
            
            exception.Message.Should().Be("對應設定不能為空");
            return Task.CompletedTask;
        }

        [Test]
        public Task UpsertMappingsAsync_WithNonExistentBatch_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 999L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name", SystemField = SystemField.Name }
                }
            };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ReturnsAsync((DatasetBatch?)null);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpsertMappingsAsync(request));
            
            exception.Message.Should().Be($"找不到批次 ID: {request.BatchId}");
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            return Task.CompletedTask;
        }

        [Test]
        public Task UpsertMappingsAsync_WithInvalidSourceColumns_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "invalid_column", SystemField = SystemField.Name }
                }
            };

            var batch = new DatasetBatch { Id = 1L, DatasetId = 1L };
            var availableColumns = new HashSet<string> { "name", "age" };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ReturnsAsync(batch);
            
            _mockRepository.Setup(x => x.GetAvailableSourceColumnsAsync(request.BatchId))
                          .ReturnsAsync(availableColumns);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpsertMappingsAsync(request));
            
            exception.Message.Should().Be("以下欄位不存在於資料中: invalid_column");
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            return Task.CompletedTask;
        }

        [Test]
        public Task UpsertMappingsAsync_WithDuplicateSourceColumns_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name", SystemField = SystemField.Name },
                    new SourceToSystemField { SourceColumn = "NAME", SystemField = SystemField.Age } // 重複的來源欄位（不區分大小寫）
                }
            };

            var batch = new DatasetBatch { Id = 1L, DatasetId = 1L };
            var availableColumns = new HashSet<string> { "name", "NAME", "age" };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ReturnsAsync(batch);
            
            _mockRepository.Setup(x => x.GetAvailableSourceColumnsAsync(request.BatchId))
                          .ReturnsAsync(availableColumns);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpsertMappingsAsync(request));
            
            exception.Message.Should().Be("來源欄位不能重複對應: name");
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            return Task.CompletedTask;
        }

        [Test]
        public Task UpsertMappingsAsync_WithDuplicateSystemFields_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name1", SystemField = SystemField.Name },
                    new SourceToSystemField { SourceColumn = "name2", SystemField = SystemField.Name } // 重複的系統欄位
                }
            };

            var batch = new DatasetBatch { Id = 1L, DatasetId = 1L };
            var availableColumns = new HashSet<string> { "name1", "name2" };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ReturnsAsync(batch);
            
            _mockRepository.Setup(x => x.GetAvailableSourceColumnsAsync(request.BatchId))
                          .ReturnsAsync(availableColumns);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpsertMappingsAsync(request));
            
            exception.Message.Should().Be("系統欄位不能重複對應: Name");
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            return Task.CompletedTask;
        }

        [Test]
        public Task UpsertMappingsAsync_WithException_ShouldRollbackTransaction()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1L,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name", SystemField = SystemField.Name }
                }
            };

            _mockUnitOfWork.Setup(x => x.BeginAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);
            
            _mockRepository.Setup(x => x.GetBatchAsync(request.BatchId))
                          .ThrowsAsync(new Exception("資料庫錯誤"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(() => 
                _service.UpsertMappingsAsync(request));

            _mockUnitOfWork.Verify(x => x.BeginAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
            return Task.CompletedTask;
        }

        #endregion

        #region GetColumnsAsync 測試

        [Test]
        public async Task GetColumnsAsync_WithValidBatchId_ShouldReturnColumns()
        {
            // Arrange
            var batchId = 1L;
            var expectedColumns = new List<DatasetColumn>
            {
                new DatasetColumn { Id = 1, SourceName = "name", DataType = "string" },
                new DatasetColumn { Id = 2, SourceName = "age", DataType = "int" }
            };

            _mockRepository.Setup(x => x.GetColumnsAsync(batchId))
                          .ReturnsAsync(expectedColumns);

            // Act
            var result = await _service.GetColumnsAsync(batchId);

            // Assert
            result.Should().BeEquivalentTo(expectedColumns);
            _mockRepository.Verify(x => x.GetColumnsAsync(batchId), Times.Once);
        }

        #endregion

        #region GetColumnMappingInfoAsync 測試

        [Test]
        public async Task GetColumnMappingInfoAsync_WithValidBatchId_ShouldReturnMappingInfo()
        {
            // Arrange
            var batchId = 1L;
            var expectedColumns = new List<DatasetColumnWithMapping>
            {
                new DatasetColumnWithMapping { SourceName = "name", DataType = "string", MappedSystemField = SystemField.Name },
                new DatasetColumnWithMapping { SourceName = "age", DataType = "int", MappedSystemField = SystemField.Age }
            };

            _mockRepository.Setup(x => x.GetColumnsWithMappingAsync(batchId))
                          .ReturnsAsync(expectedColumns);

            // Act
            var result = await _service.GetColumnMappingInfoAsync(batchId);

            // Assert
            result.Should().NotBeNull();
            result.SystemFields.Should().NotBeNull();
            result.DataColumns.Should().BeEquivalentTo(expectedColumns);
            _mockRepository.Verify(x => x.GetColumnsWithMappingAsync(batchId), Times.Once);
        }

        #endregion

        #region GetUploadHistoryAsync 測試

        [Test]
        public async Task GetUploadHistoryAsync_WithValidParameters_ShouldReturnHistory()
        {
            // Arrange
            var userId = 1L;
            var datasetId = 1L;
            var limit = 10;
            var offset = 0;
            var expectedHistory = new List<UploadHistoryDto>
            {
                new UploadHistoryDto { BatchId = 1, SourceFilename = "test1.csv", Status = "Completed" },
                new UploadHistoryDto { BatchId = 2, SourceFilename = "test2.csv", Status = "Pending" }
            };

            _mockRepository.Setup(x => x.GetUploadHistoryAsync(userId, datasetId, limit, offset))
                          .ReturnsAsync(expectedHistory);

            // Act
            var result = await _service.GetUploadHistoryAsync(userId, datasetId, limit, offset);

            // Assert
            result.Should().BeEquivalentTo(expectedHistory);
            _mockRepository.Verify(x => x.GetUploadHistoryAsync(userId, datasetId, limit, offset), Times.Once);
        }

        [Test]
        public async Task GetUploadHistoryAsync_WithInvalidDatasetId_ShouldThrowArgumentException()
        {
            // Arrange
            var userId = 1L;
            var datasetId = 0L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.GetUploadHistoryAsync(userId, datasetId));
            
            exception.Message.Should().Be("資料集 ID 必須大於 0 (Parameter 'datasetId')");
            exception.ParamName.Should().Be("datasetId");
        }

        #endregion

        #region GetBatchDetailsAsync 測試

        [Test]
        public async Task GetBatchDetailsAsync_WithValidBatchId_ShouldReturnDetails()
        {
            // Arrange
            var batchId = 1L;
            var userId = 1L;
            var expectedDetails = new UploadHistoryDto
            {
                BatchId = batchId,
                SourceFilename = "test.csv",
                Status = "Completed",
                TotalRows = 100
            };

            _mockRepository.Setup(x => x.GetBatchDetailsAsync(batchId, userId))
                          .ReturnsAsync(expectedDetails);

            // Act
            var result = await _service.GetBatchDetailsAsync(batchId, userId);

            // Assert
            result.Should().BeEquivalentTo(expectedDetails);
            _mockRepository.Verify(x => x.GetBatchDetailsAsync(batchId, userId), Times.Once);
        }

        #endregion

        #region DeleteBatchAsync 測試

        [Test]
        public async Task DeleteBatchAsync_WithValidInput_ShouldReturnSuccess()
        {
            // Arrange
            var batchId = 1L;
            var userId = 1L;
            var datasetId = 1L;

            _mockRepository.Setup(x => x.DeleteBatchAsync(batchId, userId))
                          .ReturnsAsync((true, datasetId));

            // Act
            var result = await _service.DeleteBatchAsync(batchId, userId);

            // Assert
            result.success.Should().BeTrue();
            result.datasetId.Should().Be(datasetId);
            _mockRepository.Verify(x => x.DeleteBatchAsync(batchId, userId), Times.Once);
        }

        [Test]
        public async Task DeleteBatchAsync_WithInvalidBatchId_ShouldThrowArgumentException()
        {
            // Arrange
            var batchId = 0L;
            var userId = 1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.DeleteBatchAsync(batchId, userId));
            
            exception.Message.Should().Be("批次 ID 必須大於 0 (Parameter 'batchId')");
            exception.ParamName.Should().Be("batchId");
        }

        [Test]
        public async Task DeleteBatchAsync_WithInvalidUserId_ShouldThrowArgumentException()
        {
            // Arrange
            var batchId = 1L;
            var userId = 0L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.DeleteBatchAsync(batchId, userId));
            
            exception.Message.Should().Be("用戶 ID 必須大於 0 (Parameter 'userId')");
            exception.ParamName.Should().Be("userId");
        }

        #endregion

        #region DeleteDatasetAsync 測試

        [Test]
        public async Task DeleteDatasetAsync_WithValidInput_ShouldReturnTrue()
        {
            // Arrange
            var datasetId = 1L;
            var userId = 1L;

            _mockRepository.Setup(x => x.DeleteDatasetAsync(datasetId, userId))
                          .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteDatasetAsync(datasetId, userId);

            // Assert
            result.Should().BeTrue();
            _mockRepository.Verify(x => x.DeleteDatasetAsync(datasetId, userId), Times.Once);
        }

        [Test]
        public async Task DeleteDatasetAsync_WithInvalidDatasetId_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetId = 0L;
            var userId = 1L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.DeleteDatasetAsync(datasetId, userId));
            
            exception.Message.Should().Be("資料集 ID 必須大於 0 (Parameter 'datasetId')");
            exception.ParamName.Should().Be("datasetId");
        }

        [Test]
        public async Task DeleteDatasetAsync_WithInvalidUserId_ShouldThrowArgumentException()
        {
            // Arrange
            var datasetId = 1L;
            var userId = 0L;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                _service.DeleteDatasetAsync(datasetId, userId));
            
            exception.Message.Should().Be("用戶 ID 必須大於 0 (Parameter 'userId')");
            exception.ParamName.Should().Be("userId");
        }

        #endregion
    }
}
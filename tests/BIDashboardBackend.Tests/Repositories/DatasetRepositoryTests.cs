using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Repositories;
using BIDashboardBackend.Interfaces;
using Moq;
using FluentAssertions;

namespace BIDashboardBackend.Tests.Repositories
{
    [TestFixture]
    public class DatasetRepositoryTests
    {
        private Mock<ISqlRunner> _mockSqlRunner;
        private DatasetRepository _repository;

        [SetUp]
        public void Setup()
        {
            _mockSqlRunner = new Mock<ISqlRunner>();
            _repository = new DatasetRepository(_mockSqlRunner.Object);
        }

        #region CreateDatasetAsync 測試

        [Test]
        public async Task CreateDatasetAsync_WithValidInput_ShouldReturnDatasetId()
        {
            // Arrange
            var datasetName = "測試資料集";
            var ownerId = 1L;
            var expectedDatasetId = 1L;

            _mockSqlRunner.Setup(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedDatasetId);

            // Act
            var result = await _repository.CreateDatasetAsync(datasetName, ownerId);

            // Assert
            result.Should().Be(expectedDatasetId);
            _mockSqlRunner.Verify(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task CreateDatasetAsync_WithNullOwnerId_ShouldStillCreateDataset()
        {
            // Arrange
            var datasetName = "測試資料集";
            long? ownerId = null;
            var expectedDatasetId = 1L;

            _mockSqlRunner.Setup(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedDatasetId);

            // Act
            var result = await _repository.CreateDatasetAsync(datasetName, ownerId);

            // Assert
            result.Should().Be(expectedDatasetId);
            _mockSqlRunner.Verify(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        #endregion

        #region CreateBatchAsync 測試

        [Test]
        public async Task CreateBatchAsync_WithValidInput_ShouldReturnBatchId()
        {
            // Arrange
            var datasetId = 1L;
            var fileName = "test.csv";
            var totalRows = 100L;
            var expectedBatchId = 1L;

            _mockSqlRunner.Setup(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedBatchId);

            // Act
            var result = await _repository.CreateBatchAsync(datasetId, fileName, totalRows);

            // Assert
            result.Should().Be(expectedBatchId);
            _mockSqlRunner.Verify(x => x.ScalarAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        #endregion

        #region SetBatchStatusAsync 測試

        [Test]
        public async Task SetBatchStatusAsync_WithValidInput_ShouldUpdateStatus()
        {
            // Arrange
            var batchId = 1L;
            var newStatus = "Completed";
            var errorMessage = "處理完成";
            var expectedRowsAffected = 1;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.SetBatchStatusAsync(batchId, newStatus, errorMessage);

            // Assert
            result.Should().Be(expectedRowsAffected);
            _mockSqlRunner.Verify(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task SetBatchStatusAsync_WithNonExistentBatch_ShouldReturnZero()
        {
            // Arrange
            var batchId = 999L;
            var newStatus = "Completed";
            var expectedRowsAffected = 0;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.SetBatchStatusAsync(batchId, newStatus, null);

            // Assert
            result.Should().Be(expectedRowsAffected);
        }

        #endregion

        #region UpsertColumnsAsync 測試

        [Test]
        public async Task UpsertColumnsAsync_WithValidInput_ShouldInsertColumns()
        {
            // Arrange
            var batchId = 1L;
            var columns = new List<DatasetColumn>
            {
                new DatasetColumn { BatchId = batchId, SourceName = "name", DataType = "string" },
                new DatasetColumn { BatchId = batchId, SourceName = "age", DataType = "int" }
            };
            var expectedRowsAffected = 2;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.UpsertColumnsAsync(batchId, columns);

            // Assert
            result.Should().Be(expectedRowsAffected);
            _mockSqlRunner.Verify(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        #endregion

        #region UpsertMappingsAsync 測試

        [Test]
        public async Task UpsertMappingsAsync_WithValidInput_ShouldInsertMappings()
        {
            // Arrange
            var batchId = 1L;
            var mappings = new List<DatasetMapping>
            {
                new DatasetMapping { BatchId = batchId, SourceColumn = "name", SystemField = SystemField.Name },
                new DatasetMapping { BatchId = batchId, SourceColumn = "age", SystemField = SystemField.Age }
            };
            var expectedRowsAffected = 2;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.UpsertMappingsAsync(batchId, mappings);

            // Assert
            result.Should().Be(expectedRowsAffected);
            _mockSqlRunner.Verify(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
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
                new DatasetColumn { Id = 1, BatchId = batchId, SourceName = "name", DataType = "string" },
                new DatasetColumn { Id = 2, BatchId = batchId, SourceName = "age", DataType = "int" }
            };

            _mockSqlRunner.Setup(x => x.QueryAsync<DatasetColumn>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedColumns);

            // Act
            var result = await _repository.GetColumnsAsync(batchId);

            // Assert
            result.Should().BeEquivalentTo(expectedColumns);
            _mockSqlRunner.Verify(x => x.QueryAsync<DatasetColumn>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task GetColumnsAsync_WithNonExistentBatchId_ShouldReturnEmptyList()
        {
            // Arrange
            var nonExistentBatchId = 999L;
            var emptyColumns = new List<DatasetColumn>();

            _mockSqlRunner.Setup(x => x.QueryAsync<DatasetColumn>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(emptyColumns);

            // Act
            var result = await _repository.GetColumnsAsync(nonExistentBatchId);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region GetAvailableSourceColumnsAsync 測試

        [Test]
        public async Task GetAvailableSourceColumnsAsync_WithValidBatchId_ShouldReturnColumnNames()
        {
            // Arrange
            var batchId = 1L;
            var expectedColumnNames = new HashSet<string> { "name", "age", "email" };

            _mockSqlRunner.Setup(x => x.QueryAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedColumnNames.ToList());

            // Act
            var result = await _repository.GetAvailableSourceColumnsAsync(batchId);

            // Assert
            result.Should().BeEquivalentTo(expectedColumnNames);
            _mockSqlRunner.Verify(x => x.QueryAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        #endregion

        #region GetBatchAsync 測試

        [Test]
        public async Task GetBatchAsync_WithValidBatchId_ShouldReturnBatch()
        {
            // Arrange
            var batchId = 1L;
            var expectedBatch = new DatasetBatch
            {
                Id = batchId,
                DatasetId = 1L,
                SourceFilename = "test.csv",
                TotalRows = 100L,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _mockSqlRunner.Setup(x => x.QueryAsync<DatasetBatch>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(new List<DatasetBatch> { expectedBatch });

            // Act
            var result = await _repository.GetBatchAsync(batchId);

            // Assert
            result.Should().BeEquivalentTo(expectedBatch);
            _mockSqlRunner.Verify(x => x.QueryAsync<DatasetBatch>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task GetBatchAsync_WithNonExistentBatchId_ShouldReturnNull()
        {
            // Arrange
            var nonExistentBatchId = 999L;

            _mockSqlRunner.Setup(x => x.QueryAsync<DatasetBatch>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(new List<DatasetBatch>());

            // Act
            var result = await _repository.GetBatchAsync(nonExistentBatchId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetColumnsWithMappingAsync 測試

        [Test]
        public async Task GetColumnsWithMappingAsync_WithValidBatchId_ShouldReturnColumnsWithMapping()
        {
            // Arrange
            var batchId = 1L;
            var expectedColumns = new List<DatasetColumnWithMapping>
            {
                new DatasetColumnWithMapping { SourceName = "name", DataType = "string", MappedSystemField = SystemField.Name },
                new DatasetColumnWithMapping { SourceName = "age", DataType = "int", MappedSystemField = SystemField.Age }
            };

            _mockSqlRunner.Setup(x => x.QueryAsync<DatasetColumnWithMapping>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedColumns);

            // Act
            var result = await _repository.GetColumnsWithMappingAsync(batchId);

            // Assert
            result.Should().BeEquivalentTo(expectedColumns);
            _mockSqlRunner.Verify(x => x.QueryAsync<DatasetColumnWithMapping>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
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

            // Mock for main query
            _mockSqlRunner.Setup(x => x.QueryAsync<UploadHistoryDto>(It.Is<string>(s => s.Contains("dataset_batches")), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedHistory);
            
            // Mock for GetBatchColumnsWithMappingAsync calls
            _mockSqlRunner.Setup(x => x.QueryAsync<UploadHistoryColumnDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(new List<UploadHistoryColumnDto>());

            // Act
            var result = await _repository.GetUploadHistoryAsync(userId, datasetId, limit, offset);

            // Assert
            result.Should().BeEquivalentTo(expectedHistory);
            _mockSqlRunner.Verify(x => x.QueryAsync<UploadHistoryDto>(It.Is<string>(s => s.Contains("dataset_batches")), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
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

            _mockSqlRunner.Setup(x => x.FirstOrDefaultAsync<UploadHistoryDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedDetails);
            
            // Mock QueryAsync for GetBatchColumnsWithMappingAsync
            _mockSqlRunner.Setup(x => x.QueryAsync<UploadHistoryColumnDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(new List<UploadHistoryColumnDto>());

            // Act
            var result = await _repository.GetBatchDetailsAsync(batchId, userId);

            // Assert
            result.Should().BeEquivalentTo(expectedDetails);
            _mockSqlRunner.Verify(x => x.FirstOrDefaultAsync<UploadHistoryDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
            _mockSqlRunner.Verify(x => x.QueryAsync<UploadHistoryColumnDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task GetBatchDetailsAsync_WithUnauthorizedUser_ShouldReturnNull()
        {
            // Arrange
            var batchId = 1L;
            var userId = 2L; // 不同的用戶ID

            _mockSqlRunner.Setup(x => x.FirstOrDefaultAsync<UploadHistoryDto>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync((UploadHistoryDto?)null);

            // Act
            var result = await _repository.GetBatchDetailsAsync(batchId, userId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region DeleteBatchAsync 測試

        [Test]
        public async Task DeleteBatchAsync_WithValidInput_ShouldDeleteBatchAndReturnSuccess()
        {
            // Arrange
            var batchId = 1L;
            var userId = 1L;
            var datasetId = 1L;

            _mockSqlRunner.Setup(x => x.ScalarAsync<long?>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(datasetId);
            
            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(1);

            // Act
            var result = await _repository.DeleteBatchAsync(batchId, userId);

            // Assert
            result.success.Should().BeTrue();
            result.datasetId.Should().Be(datasetId);
            _mockSqlRunner.Verify(x => x.ScalarAsync<long?>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
            _mockSqlRunner.Verify(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task DeleteBatchAsync_WithUnauthorizedUser_ShouldReturnFailure()
        {
            // Arrange
            var batchId = 1L;
            var userId = 2L; // 不同的用戶ID

            _mockSqlRunner.Setup(x => x.ScalarAsync<long?>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync((long?)null);

            // Act
            var result = await _repository.DeleteBatchAsync(batchId, userId);

            // Assert
            result.success.Should().BeFalse();
            result.datasetId.Should().BeNull();
        }

        #endregion

        #region DeleteDatasetAsync 測試

        [Test]
        public async Task DeleteDatasetAsync_WithValidInput_ShouldDeleteDatasetAndReturnTrue()
        {
            // Arrange
            var datasetId = 1L;
            var userId = 1L;
            var expectedRowsAffected = 1;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.DeleteDatasetAsync(datasetId, userId);

            // Assert
            result.Should().BeTrue();
            _mockSqlRunner.Verify(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()), Times.Once);
        }

        [Test]
        public async Task DeleteDatasetAsync_WithUnauthorizedUser_ShouldReturnFalse()
        {
            // Arrange
            var datasetId = 1L;
            var userId = 2L; // 不同的用戶ID
            var expectedRowsAffected = 0;

            _mockSqlRunner.Setup(x => x.ExecAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int?>()))
                         .ReturnsAsync(expectedRowsAffected);

            // Act
            var result = await _repository.DeleteDatasetAsync(datasetId, userId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
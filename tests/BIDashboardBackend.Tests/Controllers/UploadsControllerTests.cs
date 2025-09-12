using BIDashboardBackend.Controllers;
using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Caching;
using BIDashboardBackend.Features.Jobs;
using BIDashboardBackend.Models;
using BIDashboardBackend.Enums;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq.Expressions;
using Moq;
using FluentAssertions;

namespace BIDashboardBackend.Tests.Controllers
{
    [TestFixture]
    public class UploadsControllerTests
    {
        private Mock<IIngestService> _mockIngestService;
        private Mock<CacheKeyBuilder> _mockCacheKeyBuilder;
        private Mock<ICacheService> _mockCacheService;
        private Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private Mock<IEtlJob> _mockEtlJob;
        private UploadsController _controller;

        [SetUp]
        public void Setup()
        {
            _mockIngestService = new Mock<IIngestService>();
            _mockCacheKeyBuilder = new Mock<CacheKeyBuilder>();
            _mockCacheService = new Mock<ICacheService>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
            _mockEtlJob = new Mock<IEtlJob>();

            _controller = new UploadsController(
                _mockIngestService.Object,
                _mockCacheKeyBuilder.Object,
                _mockCacheService.Object,
                _mockBackgroundJobClient.Object
            );

            // 設定預設的用戶認證
            SetupUserAuthentication();
        }

        private void SetupUserAuthentication(long userId = 1)
        {
            var claims = new List<Claim>
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region CreateDataset 測試

        [Test]
        public async Task CreateDataset_WithValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new CreateDatasetDto
            {
                Name = "測試資料集",
                Description = "測試描述"
            };
            var expectedResult = new CreateDatasetResultDto
            {
                DatasetId = 1,
                Name = "測試資料集",
                Description = "測試描述",
                CreatedAt = DateTime.UtcNow
            };

            _mockIngestService.Setup(x => x.CreateDatasetAsync(request.Name, 1, request.Description))
                            .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateDataset(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
            _mockIngestService.Verify(x => x.CreateDatasetAsync(request.Name, 1, request.Description), Times.Once);
        }

        [Test]
        public async Task CreateDataset_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new CreateDatasetDto { Name = "測試資料集" };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.CreateDataset(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().Be("無效的用戶認證");
        }

        [Test]
        public async Task CreateDataset_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateDatasetDto { Name = "" }; // 空名稱
            _mockIngestService.Setup(x => x.CreateDatasetAsync("", 1, null))
                            .ThrowsAsync(new ArgumentException("資料集名稱不能為空"));

            // Act
            var result = await _controller.CreateDataset(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("資料集名稱不能為空");
        }

        #endregion

        #region UploadCsv 測試

        [Test]
        public async Task UploadCsv_WithValidFile_ShouldReturnOk()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("test.csv");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

            var request = new UploadCsvDto { File = mockFile.Object };
            var datasetId = 1L;
            var expectedResult = new UploadResultDto
            {
                BatchId = 1,
                FileName = "test.csv",
                TotalRows = 100,
                Status = "Pending"
            };

            _mockIngestService.Setup(x => x.UploadCsvAsync(mockFile.Object, 1, datasetId))
                            .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.UploadCsv(request, datasetId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
        }

        [Test]
        public async Task UploadCsv_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            var request = new UploadCsvDto { File = mockFile.Object };
            var datasetId = 1L;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.UploadCsv(request, datasetId);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task UploadCsv_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(0); // 空檔案
            var request = new UploadCsvDto { File = mockFile.Object };
            var datasetId = 1L;

            _mockIngestService.Setup(x => x.UploadCsvAsync(mockFile.Object, 1, datasetId))
                            .ThrowsAsync(new InvalidOperationException("檔案為空"));

            // Act
            var result = await _controller.UploadCsv(request, datasetId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("檔案為空");
        }

        #endregion

        #region UpsertMappings 測試

        [Test]
        public async Task UpsertMappings_WithValidRequest_ShouldReturnNoContent()
        {
            // Arrange
            var request = new UpsertMappingsRequestDto
            {
                BatchId = 1,
                Mappings = new List<SourceToSystemField>
                {
                    new SourceToSystemField { SourceColumn = "name", SystemField = Enums.SystemField.Name }
                }
            };

            _mockIngestService.Setup(x => x.UpsertMappingsAsync(request))
                            .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpsertMappings(request);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockIngestService.Verify(x => x.UpsertMappingsAsync(request), Times.Once);
        }

        #endregion

        #region GetColumns 測試

        [Test]
        public async Task GetColumns_WithValidBatchId_ShouldReturnOk()
        {
            // Arrange
            var batchId = 1L;
            var expectedColumns = new List<Models.DatasetColumn>
            {
                new Models.DatasetColumn { Id = 1, SourceName = "name", DataType = "string" },
                new Models.DatasetColumn { Id = 2, SourceName = "age", DataType = "int" }
            };

            _mockIngestService.Setup(x => x.GetColumnsAsync(batchId))
                            .ReturnsAsync(expectedColumns);

            // Act
            var result = await _controller.GetColumns(batchId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedColumns);
        }

        #endregion

        #region GetColumnMappingInfo 測試

        [Test]
        public async Task GetColumnMappingInfo_WithValidBatchId_ShouldReturnOk()
        {
            // Arrange
            var batchId = 1L;
            var expectedInfo = new ColumnMappingInfoDto
            {
                SystemFields = new Dictionary<SystemField, SystemFieldInfo.SystemFieldProp>(),
                DataColumns = new List<DatasetColumnWithMapping>()
            };

            _mockIngestService.Setup(x => x.GetColumnMappingInfoAsync(batchId))
                            .ReturnsAsync(expectedInfo);

            // Act
            var result = await _controller.GetColumnMappingInfo(batchId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedInfo);
        }

        #endregion

        #region GetUploadHistory 測試

        [Test]
        public async Task GetUploadHistory_WithValidParameters_ShouldReturnOk()
        {
            // Arrange
            var datasetId = 1L;
            var limit = 10;
            var offset = 0;
            var expectedHistory = new List<UploadHistoryDto>
            {
                new UploadHistoryDto { BatchId = 1, SourceFilename = "test1.csv", Status = "Completed" },
                new UploadHistoryDto { BatchId = 2, SourceFilename = "test2.csv", Status = "Pending" }
            };

            _mockIngestService.Setup(x => x.GetUploadHistoryAsync(1, datasetId, limit, offset))
                            .ReturnsAsync(expectedHistory);

            // Act
            var result = await _controller.GetUploadHistory(datasetId, limit, offset);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedHistory);
        }

        [Test]
        public async Task GetUploadHistory_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.GetUploadHistory(1);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task GetUploadHistory_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var datasetId = 0L; // 無效的資料集 ID
            _mockIngestService.Setup(x => x.GetUploadHistoryAsync(1, datasetId, 50, 0))
                            .ThrowsAsync(new ArgumentException("資料集 ID 必須大於 0"));

            // Act
            var result = await _controller.GetUploadHistory(datasetId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetBatchDetails 測試

        [Test]
        public async Task GetBatchDetails_WithValidBatchId_ShouldReturnOk()
        {
            // Arrange
            var batchId = 1L;
            var expectedDetails = new UploadHistoryDto
            {
                BatchId = batchId,
                SourceFilename = "test.csv",
                Status = "Completed",
                TotalRows = 100
            };

            _mockIngestService.Setup(x => x.GetBatchDetailsAsync(batchId, 1))
                            .ReturnsAsync(expectedDetails);

            // Act
            var result = await _controller.GetBatchDetails(batchId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedDetails);
        }

        [Test]
        public async Task GetBatchDetails_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var batchId = 1L;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.GetBatchDetails(batchId);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task GetBatchDetails_WithNotFoundBatch_ShouldReturnNotFound()
        {
            // Arrange
            var batchId = 999L;
            _mockIngestService.Setup(x => x.GetBatchDetailsAsync(batchId, 1))
                            .ReturnsAsync((UploadHistoryDto?)null);

            // Act
            var result = await _controller.GetBatchDetails(batchId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"找不到批次 ID: {batchId}");
        }

        #endregion

        #region DeleteBatch 測試

        [Test]
        public async Task DeleteBatch_WithValidBatchId_ShouldReturnOk()
        {
            // Arrange
            var batchId = 1L;
            var datasetId = 1L;
            _mockIngestService.Setup(x => x.DeleteBatchAsync(batchId, 1))
                            .ReturnsAsync((true, datasetId));

            _mockBackgroundJobClient.Setup(x => x.Enqueue<IEtlJob>(It.IsAny<Expression<Action<IEtlJob>>>()))
                                  .Verifiable();

            // Act
            var result = await _controller.DeleteBatch(batchId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value;
            response.Should().NotBeNull();
            
            _mockIngestService.Verify(x => x.DeleteBatchAsync(batchId, 1), Times.Once);
            _mockBackgroundJobClient.Verify(x => x.Enqueue<IEtlJob>(It.IsAny<Expression<Action<IEtlJob>>>()), Times.Once);
        }

        [Test]
        public async Task DeleteBatch_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var batchId = 1L;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.DeleteBatch(batchId);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task DeleteBatch_WithNotFoundBatch_ShouldReturnNotFound()
        {
            // Arrange
            var batchId = 999L;
            _mockIngestService.Setup(x => x.DeleteBatchAsync(batchId, 1))
                            .ReturnsAsync((false, (long?)null));

            // Act
            var result = await _controller.DeleteBatch(batchId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"找不到批次 ID: {batchId} 或您沒有權限刪除該批次");
        }

        [Test]
        public async Task DeleteBatch_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var batchId = 0L; // 無效的批次 ID
            _mockIngestService.Setup(x => x.DeleteBatchAsync(batchId, 1))
                            .ThrowsAsync(new ArgumentException("批次 ID 必須大於 0"));

            // Act
            var result = await _controller.DeleteBatch(batchId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Test]
        public async Task DeleteBatch_WithEtlJobFailure_ShouldStillReturnOk()
        {
            // Arrange
            var batchId = 1L;
            var datasetId = 1L;
            _mockIngestService.Setup(x => x.DeleteBatchAsync(batchId, 1))
                            .ReturnsAsync((true, datasetId));

            _mockBackgroundJobClient.Setup(x => x.Enqueue<IEtlJob>(It.IsAny<Expression<Action<IEtlJob>>>()))
                                  .Throws(new Exception("ETL Job 排程失敗"));

            // Act
            var result = await _controller.DeleteBatch(batchId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            // ETL Job 失敗不應該影響刪除操作的成功
        }

        #endregion

        #region DeleteDataset 測試

        [Test]
        public async Task DeleteDataset_WithValidDatasetId_ShouldReturnOk()
        {
            // Arrange
            var datasetId = 1L;
            _mockIngestService.Setup(x => x.DeleteDatasetAsync(datasetId, 1))
                            .ReturnsAsync(true);

            _mockCacheKeyBuilder.Setup(x => x.MetricPrefix(datasetId))
                              .Returns("metric:dataset:1:");
            _mockCacheService.Setup(x => x.RemoveByPrefixAsync("metric:dataset:1:"))
                            .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value;
            response.Should().NotBeNull();
            
            _mockIngestService.Verify(x => x.DeleteDatasetAsync(datasetId, 1), Times.Once);
            _mockCacheService.Verify(x => x.RemoveByPrefixAsync("metric:dataset:1:"), Times.Once);
        }

        [Test]
        public async Task DeleteDataset_WithInvalidUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var datasetId = 1L;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal() // 沒有認證資訊
                }
            };

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task DeleteDataset_WithNotFoundDataset_ShouldReturnNotFound()
        {
            // Arrange
            var datasetId = 999L;
            _mockIngestService.Setup(x => x.DeleteDatasetAsync(datasetId, 1))
                            .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"找不到資料集 ID: {datasetId} 或您沒有權限刪除該資料集");
        }

        [Test]
        public async Task DeleteDataset_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var datasetId = 0L; // 無效的資料集 ID
            _mockIngestService.Setup(x => x.DeleteDatasetAsync(datasetId, 1))
                            .ThrowsAsync(new ArgumentException("資料集 ID 必須大於 0"));

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Test]
        public async Task DeleteDataset_WithCacheFailure_ShouldStillReturnOk()
        {
            // Arrange
            var datasetId = 1L;
            _mockIngestService.Setup(x => x.DeleteDatasetAsync(datasetId, 1))
                            .ReturnsAsync(true);

            _mockCacheKeyBuilder.Setup(x => x.MetricPrefix(datasetId))
                              .Returns("metric:dataset:1:");
            _mockCacheService.Setup(x => x.RemoveByPrefixAsync("metric:dataset:1:"))
                            .ThrowsAsync(new Exception("快取清除失敗"));

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            // 快取清除失敗不應該影響刪除操作的成功
        }

        #endregion
    }
}

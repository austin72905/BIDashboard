using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Features.Ingest;
using BIDashboardBackend.Features.Jobs;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.Services;
using Hangfire;
using System.Data;

namespace BIDashboardBackend.Services
{
    public sealed class IngestService : IIngestService
    {
        private readonly IDatasetRepository _repo;
        private readonly IUnitOfWork _uow;
        private readonly CsvSniffer _sniffer;
        private readonly IBackgroundJobClient _jobs;
        private readonly IFileValidationService _fileValidation;
        private readonly IDataSanitizationService _dataSanitization;
        private readonly ILogger<IngestService> _logger;

        public IngestService(
            IDatasetRepository repo, 
            IUnitOfWork uow, 
            IBackgroundJobClient jobs, 
            CsvSniffer sniffer,
            IFileValidationService fileValidation,
            IDataSanitizationService dataSanitization,
            ILogger<IngestService> logger)
        {
            _repo = repo;
            _uow = uow;
            _sniffer = sniffer;
            _jobs = jobs;
            _fileValidation = fileValidation;
            _dataSanitization = dataSanitization;
            _logger = logger;
        }

        /// <summary>
        /// 創建新的資料集
        /// </summary>
        /// <param name="datasetName">資料集名稱</param>
        /// <param name="userId">用戶 ID</param>
        /// <param name="description">資料集描述（可選）</param>
        /// <returns>創建結果</returns>
        public async Task<CreateDatasetResultDto> CreateDatasetAsync(string datasetName, long userId, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(datasetName))
                throw new ArgumentException("資料集名稱不能為空", nameof(datasetName));

            if (userId <= 0)
                throw new ArgumentException("用戶 ID 必須大於 0", nameof(userId));

            // 檢查用戶的資料集數量限制（每個用戶最多 2 個資料集）
            const int maxDatasetsPerUser = 2;
            var currentCount = await _repo.GetDatasetCountByUserAsync(userId);
            if (currentCount >= maxDatasetsPerUser)
            {
                throw new InvalidOperationException($"每個用戶最多只能創建 {maxDatasetsPerUser} 個資料集，您目前已有 {currentCount} 個資料集");
            }

            // 使用 Repository 創建資料集
            var datasetId = await _repo.CreateDatasetAsync(datasetName, ownerId: userId);
            
            return new CreateDatasetResultDto
            {
                DatasetId = datasetId,
                Name = datasetName,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
        }


        public async Task<UploadResultDto> UploadCsvAsync(IFormFile file, long userId, long datasetId)
        {
            if (file.Length == 0) throw new InvalidOperationException("檔案為空");
            if (datasetId <= 0) throw new ArgumentException("資料集 ID 必須大於 0", nameof(datasetId));

            _logger.LogInformation("開始處理 CSV 檔案上傳: {FileName}, 用戶: {UserId}, 資料集: {DatasetId}", 
                file.FileName, userId, datasetId);

            // 1. 執行完整的檔案安全驗證
            var validationResult = await _fileValidation.ValidateCsvFileAsync(file);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("檔案驗證失敗: {FileName}, 錯誤: {Error}", file.FileName, validationResult.ErrorMessage);
                throw new InvalidOperationException($"檔案驗證失敗: {validationResult.ErrorMessage}");
            }

            // 記錄驗證警告
            foreach (var warning in validationResult.Warnings)
            {
                _logger.LogWarning("檔案驗證警告: {FileName}, 警告: {Warning}", file.FileName, warning);
            }

            // 2. 檢查資料集的批次數量限制（每個資料集最多 5 個批次）
            const int maxBatchesPerDataset = 5;
            var currentBatchCount = await _repo.GetBatchCountByDatasetAsync(datasetId);
            if (currentBatchCount >= maxBatchesPerDataset)
            {
                throw new InvalidOperationException($"每個資料集最多只能上傳 {maxBatchesPerDataset} 個檔案，此資料集目前已有 {currentBatchCount} 個檔案");
            }

            await _uow.BeginAsync();

            try
            {
                // 3. 清理檔案名稱
                var sanitizedFileName = _dataSanitization.SanitizeFieldValue(file.FileName);
                
                // 4. 提取有哪些表頭、每個column可能的型別和總行數
                long totalRows;
                IEnumerable<DatasetColumn> columns;
                await using (var stream = file.OpenReadStream())
                {
                    (totalRows, columns) = await _sniffer.ProbeAsync(stream, batchId: 0);
                }

                // 5. 清理和驗證欄位名稱
                var sanitizedColumns = new List<DatasetColumn>();
                foreach (var column in columns)
                {
                    var sanitizedSourceName = _dataSanitization.SanitizeColumnName(column.SourceName);
                    
                    // 創建新的 DatasetColumn 物件，使用清理後的名稱
                    var sanitizedColumn = new DatasetColumn
                    {
                        BatchId = column.BatchId,
                        SourceName = sanitizedSourceName,
                        DataType = column.DataType,
                        SampleValue = column.SampleValue,
                        CreatedAt = column.CreatedAt,
                        UpdatedAt = column.UpdatedAt
                    };
                    
                    sanitizedColumns.Add(sanitizedColumn);
                    
                    _logger.LogDebug("欄位名稱清理: {OriginalName} -> {SanitizedName}", 
                        column.SourceName, sanitizedSourceName);
                }

                // 6. 插入 DataBatch
                var batchId = await _repo.CreateBatchAsync(datasetId, sanitizedFileName, totalRows);
                
                // 7. 插入 DataColumn
                foreach (var col in sanitizedColumns) 
                    col.GetType().GetProperty("BatchId")?.SetValue(col, batchId);

                await _repo.UpsertColumnsAsync(batchId, sanitizedColumns);

                // 8. 插入 DataRow（重新開啟 stream 並進行資料清理）
                await using (var stream = file.OpenReadStream())
                {
                    await _repo.BulkCopyRowsAsync(batchId, stream, CancellationToken.None);
                }
                
                await _uow.CommitAsync();

                _logger.LogInformation("CSV 檔案上傳成功: {FileName}, BatchId: {BatchId}, 總行數: {TotalRows}", 
                    sanitizedFileName, batchId, totalRows);

                return new UploadResultDto
                {
                    BatchId = batchId,
                    FileName = sanitizedFileName,
                    TotalRows = totalRows,
                    Status = "Pending"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV 檔案上傳失敗: {FileName}, 用戶: {UserId}", file.FileName, userId);
                await _uow.RollbackAsync();
                throw;
            }
        }


        public async Task UpsertMappingsAsync(UpsertMappingsRequestDto request)
        {
            /*
                mapping 用戶上傳的欄位 跟系統欄位

                需要判斷是否有該欄位，還有是否真的有該系統欄位

                mapping 需要有唯一性，不能有重複的欄位

                之後要把該batch 的 status 改為 mapped
            
            */
            
            if (request.Mappings.Count == 0)
                throw new InvalidOperationException("對應設定不能為空");

            await _uow.BeginAsync();
            
            try
            {
                // 1. 檢查 Batch 是否存在
                var batch = await _repo.GetBatchAsync(request.BatchId);
                if (batch == null)
                    throw new InvalidOperationException($"找不到批次 ID: {request.BatchId}");

                // 2. 檢查來源欄位是否存在
                var availableColumns = await _repo.GetAvailableSourceColumnsAsync(request.BatchId);
                var invalidColumns = request.Mappings
                    .Where(m => !availableColumns.Contains(m.SourceColumn))
                    .Select(m => m.SourceColumn)
                    .ToList();

                if (invalidColumns.Any())
                    throw new InvalidOperationException($"以下欄位不存在於資料中: {string.Join(", ", invalidColumns)}");

                // 3. 檢查是否有重複的來源欄位
                var duplicateColumns = request.Mappings
                    .GroupBy(m => m.SourceColumn, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1 )
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateColumns.Any())
                    throw new InvalidOperationException($"來源欄位不能重複對應: {string.Join(", ", duplicateColumns)}");

                // 4. 檢查是否有重複的系統欄位
                var duplicateSystemFields = request.Mappings
                    .GroupBy(m => m.SystemField)
                    .Where(g => g.Count() > 1 && g.Key!=SystemField.None)
                    .Select(g => g.Key.ToString())
                    .ToList();

                if (duplicateSystemFields.Any())
                    throw new InvalidOperationException($"系統欄位不能重複對應: {string.Join(", ", duplicateSystemFields)}");

                // 5. 檢查系統欄位是否有效（SystemField 是 enum，.NET 會自動檢查）
                var invalidSystemFields = request.Mappings
                    .Where(m => !Enum.IsDefined(typeof(SystemField), m.SystemField))
                    .Select(m => m.SystemField.ToString())
                    .ToList();

                if (invalidSystemFields.Any())
                    throw new InvalidOperationException($"無效的系統欄位: {string.Join(", ", invalidSystemFields)}");

                // 6. 執行對應設定
                await _repo.UpsertMappingsAsync(request.BatchId, request.Mappings.ConvertAll(m => new DatasetMapping
                {
                    BatchId = request.BatchId,
                    SourceColumn = m.SourceColumn,
                    SystemField = m.SystemField
                }));

                // 7. 更新批次狀態為 "Mapped"
                await _repo.SetBatchStatusAsync(request.BatchId, "Mapped", null);

                await _uow.CommitAsync();


                //enqueue ETL job
                _jobs.Enqueue<IEtlJob>(j => j.ProcessBatch(batch.DatasetId, request.BatchId));

            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        /*
            上傳歷史紀錄，以batch為單位，顯示該batch 上傳檔案的名稱、column，不需要顯示實際上傳內容
            點進去單一筆，會顯示該batch的mapping情況
        
         */
        
        public Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId)
            => _repo.GetColumnsAsync(batchId);

        /// <summary>
        /// 獲取用戶的上傳歷史紀錄
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="limit">限制筆數，預設 50</param>
        /// <param name="offset">偏移量，預設 0</param>
        /// <returns>上傳歷史紀錄列表</returns>
        public Task<IReadOnlyList<UploadHistoryDto>> GetUploadHistoryAsync(long userId, long datasetId, int limit = 50, int offset = 0)
        {
            if (datasetId <= 0)
                throw new ArgumentException("資料集 ID 必須大於 0", nameof(datasetId));
                
            return _repo.GetUploadHistoryAsync(userId, datasetId, limit, offset);
        }

        /// <summary>
        /// 獲取指定批次的詳細資訊（包含欄位和映射）
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <param name="userId">用戶 ID（用於權限驗證）</param>
        /// <returns>批次詳細資訊</returns>
        public Task<UploadHistoryDto?> GetBatchDetailsAsync(long batchId, long userId)
            => _repo.GetBatchDetailsAsync(batchId, userId);

        /// <summary>
        /// 取得欄位對應資訊，包含系統欄位字典和資料欄位（含映射資訊），並提供型別相容性檢查
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>包含系統欄位、資料欄位和映射資訊的完整資訊</returns>
        public async Task<ColumnMappingInfoDto> GetColumnMappingInfoAsync(long batchId)
        {
            // 1. 使用 JOIN 查詢取得資料欄位及其映射資訊
            var dataColumnsWithMapping = await _repo.GetColumnsWithMappingAsync(batchId);
            
            // 2. 取得系統欄位字典
            var systemFields = SystemFieldInfo.SystemFieldDict;
            
            return new ColumnMappingInfoDto
            {
                SystemFields = systemFields,
                DataColumns = dataColumnsWithMapping
            };
        }

        /// <summary>
        /// 刪除指定的批次
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <param name="userId">用戶 ID（用於權限驗證）</param>
        /// <returns>刪除結果，包含datasetId（如果成功）</returns>
        public async Task<(bool success, long? datasetId)> DeleteBatchAsync(long batchId, long userId)
        {
            if (batchId <= 0)
                throw new ArgumentException("批次 ID 必須大於 0", nameof(batchId));

            if (userId <= 0)
                throw new ArgumentException("用戶 ID 必須大於 0", nameof(userId));

            // 使用 Repository 刪除批次
            return await _repo.DeleteBatchAsync(batchId, userId);
        }
        
        /// <summary>
        /// 刪除指定的資料集
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="userId">用戶 ID（用於權限驗證）</param>
        /// <returns>刪除結果</returns>
        public async Task<bool> DeleteDatasetAsync(long datasetId, long userId)
        {
            if (datasetId <= 0)
                throw new ArgumentException("資料集 ID 必須大於 0", nameof(datasetId));

            if (userId <= 0)
                throw new ArgumentException("用戶 ID 必須大於 0", nameof(userId));

            // 使用 Repository 刪除資料集
            return await _repo.DeleteDatasetAsync(datasetId, userId);
        }
        
        
    }
}

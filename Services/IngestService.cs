using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Features.Ingest;
using BIDashboardBackend.Features.Jobs;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using Hangfire;

namespace BIDashboardBackend.Services
{
    public sealed class IngestService : IIngestService
    {
        private readonly IDatasetRepository _repo;
        private readonly IUnitOfWork _uow;
        private readonly CsvSniffer _sniffer;
        //private readonly IBackgroundJobClient _jobs;


        public IngestService(IDatasetRepository repo, IUnitOfWork uow, CsvSniffer sniffer)
        {
            _repo = repo;
            _uow = uow;
            _sniffer = sniffer;
            //_jobs = jobs;
        }


        public async Task<UploadResultDto> UploadCsvAsync(IFormFile file)
        {
            if (file.Length == 0) throw new InvalidOperationException("檔案為空");

            await _uow.BeginAsync();

            try
            {
                // 1. 插入 Dataset
                var datasetName = Path.GetFileNameWithoutExtension(file.FileName);
                var datasetId = await _repo.CreateDatasetAsync(datasetName);

                // 2. 提取有哪些表頭、每個column可能的型別和總行數
                long totalRows;
                IEnumerable<DatasetColumn> columns;
                await using (var stream = file.OpenReadStream())
                {
                    (totalRows, columns) = await _sniffer.ProbeAsync(stream, batchId: 0);
                }

                // 3. 插入 DataBatch
                var batchId = await _repo.CreateBatchAsync(datasetId, file.FileName, totalRows);
                
                // 4. 插入 DataColumn
                foreach (var col in columns) 
                    col.GetType().GetProperty("BatchId")?.SetValue(col, batchId);

                await _repo.UpsertColumnsAsync(batchId, columns);

                // 5. 插入 DataRow - 重新開啟 stream
                await using (var stream = file.OpenReadStream())
                {
                    await _repo.BulkCopyRowsAsync(batchId, stream, CancellationToken.None);
                }
                
                await _uow.CommitAsync();

                return new UploadResultDto
                {
                    BatchId = batchId,
                    FileName = file.FileName,
                    TotalRows = totalRows,
                    Status = "Pending"
                };
            }
            catch
            {
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
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateColumns.Any())
                    throw new InvalidOperationException($"來源欄位不能重複對應: {string.Join(", ", duplicateColumns)}");

                // 4. 檢查是否有重複的系統欄位
                var duplicateSystemFields = request.Mappings
                    .GroupBy(m => m.SystemField)
                    .Where(g => g.Count() > 1)
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
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }
        
        public Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId)
            => _repo.GetColumnsAsync(batchId);

        /// <summary>
        /// 取得欄位對應資訊，包含系統欄位字典和資料欄位，並提供型別相容性檢查
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>包含系統欄位、資料欄位和型別警告的完整資訊</returns>
        public async Task<ColumnMappingInfoDto> GetColumnMappingInfoAsync(long batchId)
        {
            // 1. 取得資料欄位
            var dataColumns = await _repo.GetColumnsAsync(batchId);
            
            // 2. 取得系統欄位字典
            var systemFields = SystemFieldInfo.SystemFieldDict;
                     
            
            return new ColumnMappingInfoDto
            {
                SystemFields = systemFields,
                DataColumns = dataColumns
            };
        }
        
        
    }
}

using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
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
            await _uow.BeginAsync();
            await _repo.UpsertMappingsAsync(request.BatchId, request.Mappings.ConvertAll(m => new DatasetMapping
            {
                BatchId = request.BatchId,
                SourceColumn = m.SourceColumn,
                SystemField = m.SystemField
            }));
            await _uow.CommitAsync();
        }

        public Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId)
            => _repo.GetColumnsAsync(batchId);
    }
}

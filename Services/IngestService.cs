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
        private readonly IBackgroundJobClient _jobs;


        public IngestService(IDatasetRepository repo, IUnitOfWork uow, CsvSniffer sniffer, IBackgroundJobClient jobs)
        {
            _repo = repo;
            _uow = uow;
            _sniffer = sniffer;
            _jobs = jobs;
        }


        public async Task<UploadResultDto> UploadCsvAsync(IFormFile file)
        {
            if (file.Length == 0) throw new InvalidOperationException("Empty file");


            await _uow.BeginAsync();
            await using var stream = file.OpenReadStream();
            var (totalRows, columns) = await _sniffer.ProbeAsync(stream, batchId: 0);

            stream.Position = 0;
            var batchId = await _repo.CreateBatchAsync(file.FileName, totalRows);
            foreach (var col in columns) col.GetType().GetProperty("BatchId")?.SetValue(col, batchId);
            await _repo.UpsertColumnsAsync(batchId, columns);

            stream.Position = 0;
            await _repo.BulkCopyRowsAsync(batchId, stream, CancellationToken.None);
            await _uow.CommitAsync();

            // 匯入資料（COPY/batch insert）可在背景或此處進行；此處示意背景做 ETL
            _jobs.Enqueue<IEtlJob>(job => job.RunEtlForBatchAsync(batchId, CancellationToken.None));

            return new UploadResultDto
            {
                BatchId = batchId,
                FileName = file.FileName,
                TotalRows = totalRows,
                Status = "Pending"
            };
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

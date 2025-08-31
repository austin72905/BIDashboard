using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Models;

namespace BIDashboardBackend.Interfaces
{
    public interface IIngestService
    {
        Task<UploadResultDto> UploadCsvAsync(IFormFile file);
        Task UpsertMappingsAsync(UpsertMappingsRequestDto request);
        Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId);
    }
}

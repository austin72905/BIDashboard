using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.Interfaces
{
    public interface IIngestService
    {
        Task<UploadResultDto> UploadCsvAsync(IFormFile file);
        Task UpsertMappingsAsync(UpsertMappingsRequestDto request);
    }
}

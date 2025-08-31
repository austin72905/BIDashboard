using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.Interfaces
{
    public interface IMetricService
    {
        Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId, CancellationToken ct = default);
        Task<GenderShareDto> GetGenderShareAsync(long datasetId, CancellationToken ct = default);
    }
}

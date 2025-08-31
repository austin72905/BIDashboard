using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.Interfaces
{
    public interface IMetricService
    {
        Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId);
        Task<GenderShareDto> GetGenderShareAsync(long datasetId);
    }
}

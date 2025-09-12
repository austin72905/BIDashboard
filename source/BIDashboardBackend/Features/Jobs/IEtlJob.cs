namespace BIDashboardBackend.Features.Jobs
{
    public interface IEtlJob
    {
        Task ProcessBatch(long datasetId, long batchId);
    }
}

namespace BIDashboardBackend.Features.Jobs
{
    public interface IEtlJob
    {
        Task RunEtlForBatchAsync(long batchId, CancellationToken ct = default);
    }
}

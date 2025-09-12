using System.Data;

namespace BIDashboardBackend.Interfaces
{
    public interface IDbSession : IAsyncDisposable
    {
        IDbConnection Connection { get; }
        IDbTransaction? Transaction { get; }

        Task OpenAsync();
        Task BeginAsync(IsolationLevel level = IsolationLevel.ReadCommitted);
        Task CommitAsync();
        Task RollbackAsync();
    }
}

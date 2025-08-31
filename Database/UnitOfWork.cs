using BIDashboardBackend.Interfaces;

namespace BIDashboardBackend.Database
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly IDbSession _session;
        public UnitOfWork(IDbSession session) => _session = session;

        public Task BeginAsync() => _session.BeginAsync();
        public Task CommitAsync() => _session.CommitAsync();
        public Task RollbackAsync() => _session.RollbackAsync();
    }
}

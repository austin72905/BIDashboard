using BIDashboardBackend.Interfaces;
using Npgsql;
using System.Data;

namespace BIDashboardBackend.Database
{
    public sealed class DbSession : IDbSession
    {
        private readonly string _connStr;
        private NpgsqlConnection? _conn;
        private NpgsqlTransaction? _tx;

        public DbSession(string connStr) => _connStr = connStr;

        public IDbConnection Connection => _conn ?? throw new InvalidOperationException("Connection not opened.");
        public IDbTransaction? Transaction => _tx;

        public async Task OpenAsync()
        {
            if (_conn != null) return;
            _conn = new NpgsqlConnection(_connStr);
            await _conn.OpenAsync();
        }

        public async Task BeginAsync(IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (_conn == null) await OpenAsync();
            if (_tx != null) throw new InvalidOperationException("Transaction already started.");
            _tx = await ((NpgsqlConnection)Connection).BeginTransactionAsync(level);
        }

        public async Task CommitAsync()
        {
            if (_tx == null) return;
            await _tx.CommitAsync();
            await _tx.DisposeAsync();
            _tx = null;
        }

        public async Task RollbackAsync()
        {
            if (_tx == null) return;
            await _tx.RollbackAsync();
            await _tx.DisposeAsync();
            _tx = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_tx is not null) await _tx.DisposeAsync();
            _tx = null;
            if (_conn is not null) await _conn.DisposeAsync();
            _conn = null;
        }
    }
}

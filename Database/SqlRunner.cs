using BIDashboardBackend.Interfaces;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace BIDashboardBackend.Database
{
    public sealed class SqlRunner : ISqlRunner
    {
        private readonly IDbSession _session;
        private readonly int _defaultTimeoutSec;

        public SqlRunner(IDbSession session, int defaultTimeoutSec = 30)
        {
            _session = session;
            _defaultTimeoutSec = defaultTimeoutSec;
        }

        private CommandDefinition Def(string sql, object? args, int? timeoutSec)
            => new CommandDefinition(sql, args, _session.Transaction, timeoutSec ?? _defaultTimeoutSec);

        public async Task<int> ExecAsync(string sql, object? args = null, int? timeoutSec = null)
        {
            await _session.OpenAsync();
            return await _session.Connection.ExecuteAsync(Def(sql, args, timeoutSec));
        }

        public async Task<T?> ScalarAsync<T>(string sql, object? args = null, int? timeoutSec = null)
        {
            await _session.OpenAsync();
            return await _session.Connection.ExecuteScalarAsync<T>(Def(sql, args, timeoutSec));
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? args = null, int? timeoutSec = null)
        {
            await _session.OpenAsync();
            var rows = await _session.Connection.QueryAsync<T>(Def(sql, args, timeoutSec));
            return rows.AsList();
        }

        public async Task<T?> FirstOrDefaultAsync<T>(string sql, object? args = null, int? timeoutSec = null)
        {
            await _session.OpenAsync();
            return await _session.Connection.QueryFirstOrDefaultAsync<T>(Def(sql, args, timeoutSec));
        }
    }
}

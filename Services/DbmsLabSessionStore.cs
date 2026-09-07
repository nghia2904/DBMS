using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Services;

public sealed class DbmsLabSessionStore : IDisposable
{
    private readonly DbHelper _db;
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();
    private readonly Timer _cleanupTimer;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(10);

    public DbmsLabSessionStore(DbHelper db)
    {
        _db = db;
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, SessionTimeout, SessionTimeout);
    }

    public async Task<SessionState> CreateAsync(DbmsLabSession session)
    {
        var connection = _db.GetConnection();
        var transferred = false;
        try
        {
            await connection.OpenAsync();
            var isolation = session.ProblemType switch
            {
                "DirtyRead" when session.TransactionName == "T2" && session.Mode == "Loi" => IsolationLevel.ReadUncommitted,
                "UnrepeatableRead" when session.Mode == "KhacPhuc" => IsolationLevel.RepeatableRead,
                "PhantomRead" when session.Mode == "KhacPhuc" => IsolationLevel.Serializable,
                _ => IsolationLevel.ReadCommitted
            };
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolation);
            var state = new SessionState(session, connection, transaction);
            _sessions.AddOrUpdate(session.SessionId, state, (_, old) =>
            {
                old.Dispose();
                return state;
            });
            transferred = true;
            return state;
        }
        finally
        {
            if (!transferred)
            {
                await connection.DisposeAsync();
            }
        }
    }

    public bool TryGet(Guid id, out SessionState? state) => _sessions.TryGetValue(id, out state);

    public void Remove(Guid id)
    {
        if (_sessions.TryRemove(id, out var state))
        {
            state.Dispose();
        }
    }

    private void CleanupExpiredSessions(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow - SessionTimeout;
        foreach (var pair in _sessions)
        {
            if (pair.Value.LastUsed < cutoff && _sessions.TryRemove(pair.Key, out var expired))
            {
                expired.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        foreach (var id in _sessions.Keys)
        {
            Remove(id);
        }
    }

    public sealed class SessionState : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        public DbmsLabSession Info { get; }
        public SqlConnection Connection { get; }
        public SqlTransaction Transaction { get; }
        public DateTimeOffset LastUsed { get; private set; } = DateTimeOffset.UtcNow;
        public int? LastQuantity { get; set; }
        public decimal? LastPrice { get; set; }
        public bool IsActive { get; private set; } = true;

        public SessionState(DbmsLabSession info, SqlConnection connection, SqlTransaction transaction)
        {
            Info = info;
            Connection = connection;
            Transaction = transaction;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            await _gate.WaitAsync();
            try
            {
                if (!IsActive)
                {
                    throw new InvalidOperationException("Giao tác đã kết thúc.");
                }
                LastUsed = DateTimeOffset.UtcNow;
                return await action();
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Complete(bool commit)
        {
            if (!IsActive)
            {
                return;
            }
            if (commit)
            {
                Transaction.Commit();
            }
            else
            {
                Transaction.Rollback();
            }
            IsActive = false;
        }

        public void Dispose()
        {
            if (IsActive)
            {
                try
                {
                    Transaction.Rollback();
                }
                catch (InvalidOperationException)
                {
                    // The transaction may already have been completed by SQL Server.
                }
                IsActive = false;
            }
            Transaction.Dispose();
            Connection.Dispose();
            _gate.Dispose();
        }
    }
}

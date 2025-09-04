using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;

namespace BIDashboardBackend.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ISqlRunner _sql;
        public UserRepository(ISqlRunner sql)
        {
            _sql = sql;
        }
        public async Task<User> CreateAsync(User user)
        {
            const string sql = @"
                INSERT INTO users (email, display_name, uid, last_login_at, created_at, updated_at)
                VALUES (@Email, @DisplayName, @Uid, @LastLoginAt, NOW(), NOW())
                RETURNING id, email, display_name AS DisplayName, uid AS Uid,
                          last_login_at AS LastLoginAt, created_at AS CreatedAt, updated_at AS UpdatedAt;
                ";
            var created = await _sql.FirstOrDefaultAsync<User>(sql, new
            {
                user.Email,
                user.DisplayName,
                Uid = user.Uid,
                user.LastLoginAt
            });
            if (created is null)
                throw new Exception("Insert failed, no row returned.");
            return created;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT id, email, display_name AS DisplayName, uid AS Uid,
                       last_login_at AS LastLoginAt, created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM users
                WHERE email = @Email
                LIMIT 1;";
            return await _sql.FirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<User?> GetByFirebaseUidAsync(string firebaseUid)
        {
            const string sql = @"
                SELECT id, email, display_name AS DisplayName, uid AS Uid,
                       last_login_at AS LastLoginAt, created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM users
                WHERE uid = @Uid
                LIMIT 1;";
            return await _sql.FirstOrDefaultAsync<User>(sql, new { Uid = firebaseUid });
        }

        public async Task UpdateAsync(User user)
        {
            const string sql = @"
                UPDATE users
                SET    email = @Email,
                       display_name = @DisplayName,
                       uid = @Uid,
                       last_login_at = @LastLoginAt,
                       updated_at = NOW()
                WHERE  id = @Id;";
            var rows = await _sql.ExecAsync(sql, new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                Uid = user.Uid,
                user.LastLoginAt
            });
            if (rows == 0)
                throw new KeyNotFoundException($"User not found: id={user.Id}");
        }
    }

}

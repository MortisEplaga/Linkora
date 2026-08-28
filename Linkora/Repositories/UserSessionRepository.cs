namespace Linkora.Repositories
{
    public class UserSessionRepository : SqlRepositoryBase, IUserSessionRepository
    {
        public UserSessionRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<int> StartSessionAsync(int userId) => (await QueryAsync<int>(@"INSERT INTO UserSessions (UserId, LoginAt) OUTPUT INSERTED.Id VALUES (@UserId, SYSUTCDATETIME())", r => r.GetInt32(0), p => p.AddWithValue("@UserId", userId)))[0];
        public async Task CloseSessionAsync(int sessionId) => await ExecuteAsync("UPDATE UserSessions SET LogoutAt = SYSUTCDATETIME() WHERE Id = @Id AND LogoutAt IS NULL", p => p.AddWithValue("@Id", sessionId));
        public async Task CloseOpenSessionsAsync(int userId) => await ExecuteAsync("UPDATE UserSessions SET LogoutAt = SYSUTCDATETIME() WHERE UserId = @UserId AND LogoutAt IS NULL", p => p.AddWithValue("@UserId", userId));
        public async Task<int> DeleteOldSessionsAsync(int retentionDays = 30) => await ExecuteAsync("DELETE FROM UserSessions WHERE LoginAt < DATEADD(day, -@Days, SYSUTCDATETIME())", p => p.AddWithValue("@Days", retentionDays));
    }
}
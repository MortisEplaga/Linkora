namespace Linkora.Repositories
{
    public class SupportRepository : SqlRepositoryBase, ISupportRepository
    {
        public SupportRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<int> CreateRequestAsync(string name, string email, string? phone, string message, string? userId)
        {
            var result = await QueryAsync<int>(
                @"INSERT INTO SupportRequests (Name, Email, Phone, Message, CreatedAt, Status, UserId)
                  OUTPUT INSERTED.Id
                  VALUES (@Name, @Email, @Phone, @Message, @CreatedAt, @Status, @UserId)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@Email", email);
                    p.AddWithValue("@Phone", (object?)phone ?? DBNull.Value);
                    p.AddWithValue("@Message", message);
                    p.AddWithValue("@CreatedAt", DateTime.UtcNow);
                    p.AddWithValue("@Status", "New");
                    p.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
                });

            return result[0];
        }
    }
}
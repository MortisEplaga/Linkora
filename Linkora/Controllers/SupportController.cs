using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
public class SupportController : ControllerBase
{
    private readonly string _connectionString;

    public SupportController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactSupport([FromBody] SupportRequestDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid data" });

        string userId = User.Identity.IsAuthenticated
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        const string sql = @"
            INSERT INTO SupportRequests (Name, Email, Phone, Message, CreatedAt, Status, UserId)
            VALUES (@Name, @Email, @Phone, @Message, @CreatedAt, @Status, @UserId);
            SELECT SCOPE_IDENTITY();";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", model.Name);
        command.Parameters.AddWithValue("@Email", model.Email);
        command.Parameters.AddWithValue("@Phone", model.Phone ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Message", model.Message);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@Status", "New");
        command.Parameters.AddWithValue("@UserId", userId ?? (object)DBNull.Value);

        var newId = await command.ExecuteScalarAsync();

        return Ok(new { success = true, id = newId });
    }
}

public class SupportRequestDto
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Message { get; set; }
}
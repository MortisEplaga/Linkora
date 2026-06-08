using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Linkora.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly string _connectionString;

        public ProfileController(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IActionResult> Edit()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();
            ViewBag.User = user;
            return View("~/Views/Account/ProfileEdit.cshtml");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Save([FromBody] ProfileSaveDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.UserName))
            {
                errors.Add("Username is required");
            }
            else if (dto.UserName != user.UserName)
            {
                var existing = await _userRepository.GetByUsernameAsync(dto.UserName);
                if (existing != null) errors.Add("Username already taken");
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone) && dto.Phone != user.PhoneNumber)
            {
                var byPhone = await _userRepository.GetByPhoneAsync(dto.Phone);
                if (byPhone != null && byPhone.Id != userId)
                    errors.Add("Phone already used by another account");
            }

            if (errors.Any())
                return BadRequest(new { errors });

            string? newHash = null;
            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                    return BadRequest(new { errors = new[] { "Current password is required" } });

                if (Hash(dto.CurrentPassword) != user.PasswordHash)
                    return BadRequest(new { errors = new[] { "Current password is incorrect" } });

                if (dto.NewPassword.Length < 8 ||
                    !dto.NewPassword.Any(char.IsUpper) ||
                    !dto.NewPassword.Any(char.IsLower) ||
                    !dto.NewPassword.Any(char.IsDigit))
                    return BadRequest(new { errors = new[] { "Password must be at least 8 characters with uppercase, lowercase and digit" } });

                newHash = Hash(dto.NewPassword);
            }

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = newHash != null
                ? "UPDATE Users SET UserName = @U, PhoneNumber = @P, PasswordHash = @H WHERE Id = @Id"
                : "UPDATE Users SET UserName = @U, PhoneNumber = @P WHERE Id = @Id";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@U", dto.UserName);
            cmd.Parameters.AddWithValue("@P", (object?)dto.Phone ?? DBNull.Value);
            if (newHash != null) cmd.Parameters.AddWithValue("@H", newHash);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { success = true });
        }

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }
    }

    public class ProfileSaveDto
    {
        public string UserName { get; set; } = "";
        public string? Phone { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}
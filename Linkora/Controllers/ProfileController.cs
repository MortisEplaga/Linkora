using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Linkora.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserRepository _userRepository;
        private static readonly int[] AllowedDurations = { 1, 3, 7, 14, 30 };
        private static readonly string[] AllowedSubscriptionTypes = { "Free", "Standard", "Premium" };

        public ProfileController(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
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
        public async Task<IActionResult> Save([FromBody] ProfileSaveDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.UserName))
                errors.Add("Username is required");
            else if (dto.UserName != user.UserName)
            {
                var existing = await _userRepository.GetByUsernameAsync(dto.UserName);
                if (existing != null) errors.Add("Username already taken");
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone) && dto.Phone != user.Phone)
            {
                var byPhone = await _userRepository.GetByPhoneAsync(dto.Phone);
                if (byPhone != null && byPhone.Id != userId)
                    errors.Add("Phone already used by another account");
            }

            int? duration = null;
            if (dto.PreferredAdDuration.HasValue)
                if (!AllowedDurations.Contains(dto.PreferredAdDuration.Value))
                    errors.Add("Invalid ad duration value");
                else
                    duration = dto.PreferredAdDuration.Value;

            string? subscriptionType = null;
            if (!string.IsNullOrWhiteSpace(dto.SubscriptionType))
                if (!AllowedSubscriptionTypes.Contains(dto.SubscriptionType))
                    errors.Add("Invalid subscription type");
                else
                    subscriptionType = dto.SubscriptionType;

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

            await _userRepository.UpdateProfileAsync(userId, dto.UserName, dto.Phone, duration, newHash, subscriptionType);

            return Ok(new { success = true });
        }
        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> AdDurationPref()
        {
            if (!User.Identity!.IsAuthenticated)
                return Json(new { days = 30 });

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _userRepository.GetByIdAsync(userId);
            return Json(new { days = user?.PreferredAdDuration ?? 30 });
        }
    }
}
using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private static readonly int[] AllowedDurations = { 1, 3, 7, 14, 30 };
        private static readonly string[] AllowedSubscriptionTypes = { "Free", "Standard", "Premium" };

        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public ProfileController(IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }
        public async Task<IActionResult> Edit()
        {
            var user = await _userRepository.GetByIdAsync(User.GetUserId());
            if (user == null) return NotFound();
            ViewBag.User = user;
            return View("~/Views/Account/ProfileEdit.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] ProfileSaveDto dto)
        {
            var userId = User.GetUserId();
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

                if (user.PasswordHash == null || !_passwordHasher.Verify(dto.CurrentPassword, user.PasswordHash))
                    return BadRequest(new { errors = new[] { "Current password is incorrect" } });
                if (dto.NewPassword.Length < 8 ||
                    !dto.NewPassword.Any(char.IsUpper) ||
                    !dto.NewPassword.Any(char.IsLower) ||
                    !dto.NewPassword.Any(char.IsDigit))
                    return BadRequest(new { errors = new[] { "Password must be at least 8 characters with uppercase, lowercase and digit" } });

                newHash = _passwordHasher.Hash(dto.NewPassword);
            }

            if (_passwordHasher.IsLegacyHash(user.PasswordHash) && string.IsNullOrWhiteSpace(dto.NewPassword))
                newHash = _passwordHasher.Hash(dto.CurrentPassword);

            await _userRepository.UpdateProfileAsync(userId, dto.UserName, dto.Phone, duration, newHash, subscriptionType);

            return Ok(new { success = true });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> AdDurationPref()
        {
            if (!User.Identity!.IsAuthenticated) return Json(new { days = 30 });

            return Json(new { days = (await _userRepository.GetByIdAsync(User.GetUserId()))?.PreferredAdDuration ?? 30 });
        }
    }
}
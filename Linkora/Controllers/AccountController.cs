using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Linkora.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;

        public AccountController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ── Login ──
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null || user.PasswordHash != Hash(password))
            {
                ViewBag.Error = "Invalid username or password";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            await SignInAsync(user);
            return Redirect(returnUrl ?? "/");
        }

        // ── Register ──
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirm)
        {
            if (password != confirm)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            var existing = await _userRepository.GetByUsernameAsync(username);
            if (existing != null)
            {
                ViewBag.Error = "Username already taken";
                return View();
            }

            var user = new User { UserName = username, Email = email };
            var id = await _userRepository.CreateAsync(user, Hash(password));
            user.Id = id;

            await SignInAsync(user);
            return RedirectToAction("Index", "Home");
        }

        // ── Google OAuth: Step 1 — redirect to Google ──
        // Called when user clicks the Google button
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            // After Google authenticates, middleware will handle /Account/GoogleCallback
            // and then redirect here to /Account/GoogleSignedIn
            var redirectUrl = Url.Action("GoogleSignedIn", "Account", new { returnUrl });

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            // Challenge triggers the Google OAuth flow.
            // The middleware will catch /Account/GoogleCallback automatically.
            return Challenge(properties, "Google");
        }

        // ── Google OAuth: Step 2 — after middleware validated the callback ──
        // At this point HttpContext.User already contains Google claims
        public async Task<IActionResult> GoogleSignedIn(string? returnUrl = null)
        {
            // Read the Google identity that middleware already validated
            var result = await HttpContext.AuthenticateAsync("Cookies");

            // If somehow not authenticated, fall back to login
            if (!result.Succeeded || result.Principal == null)
                return RedirectToAction("Login");

            var claims = result.Principal.Claims.ToList();
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var googleName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var avatarUrl = claims.FirstOrDefault(c => c.Type == "picture")?.Value
                          ?? claims.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value;

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            // Find or create user in our DB
            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                var baseUsername = SanitizeUsername(googleName ?? email.Split('@')[0]);
                var username = await _userRepository.EnsureUniqueUsernameAsync(baseUsername);

                user = new User
                {
                    UserName = username,
                    Email = email,
                    AvatarImagePath = avatarUrl,
                };

                var id = await _userRepository.CreateGoogleUserAsync(user);
                user.Id = id;
            }
            else if (string.IsNullOrEmpty(user.AvatarImagePath) && !string.IsNullOrEmpty(avatarUrl))
            {
                await _userRepository.UpdateAvatarAsync(user.Id, avatarUrl);
                user.AvatarImagePath = avatarUrl;
            }

            // Re-issue our own cookie with full user info
            await HttpContext.SignOutAsync("Cookies");
            await SignInAsync(user);

            return Redirect(returnUrl ?? "/");
        }

        // ── Logout ──
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            return RedirectToAction("Index", "Home");
        }

        // ── Helpers ──
        private async Task SignInAsync(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.UserName),
                new(ClaimTypes.Role,           user.Role ?? "user"),
            };
            if (!string.IsNullOrEmpty(user.AvatarImagePath))
                claims.Add(new Claim("Avatar", user.AvatarImagePath));

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("Cookies", principal);
        }

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        private static string SanitizeUsername(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in raw)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch == ' ' || ch == '_') sb.Append('_');
            }
            var result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "user" : result;
        }
    }
}
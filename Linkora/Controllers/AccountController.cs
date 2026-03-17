using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

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
            if (user.AvatarImagePath != null)
                claims.Add(new Claim("Avatar", user.AvatarImagePath)); var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("Cookies", principal);
        }

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
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
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IEmailService _emailService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;

        public AccountController(IUserRepository userRepository, IUserSessionRepository userSessionRepository, IEmailService emailService, IPasswordHasher passwordHasher, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _userSessionRepository = userSessionRepository;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
                user = await _userRepository.GetByEmailAsync(username);
            if (user == null)
                user = await _userRepository.GetByPhoneAsync(username);

            if (user == null || user.PasswordHash == null || !_passwordHasher.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid username or password";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            if (_passwordHasher.IsLegacyHash(user.PasswordHash))
            {
                var upgradedHash = _passwordHasher.Hash(password);
                await _userRepository.UpdatePasswordHashAsync(user.Id, upgradedHash);
            }

            if (!user.EmailConfirmed)
            {
                ViewBag.Error = "Please confirm your email address before signing in. Check your inbox.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            await SignInAsync(user);
            return Redirect(returnUrl ?? "/");
        }
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string username,
                                                  string email,
                                                  string password,
                                                  string confirm,
                                                  string? phone = null,
                                                  bool isCompany = false)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                ViewBag.Error = "Username is required";
                return View();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Email is required";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required";
                return View();
            }

            if (password != confirm)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (password.Length < 8 ||
                !password.Any(char.IsUpper) ||
                !password.Any(char.IsLower) ||
                !password.Any(char.IsDigit))
            {
                ViewBag.Error = "Password must be at least 8 characters and contain uppercase, lowercase and a digit";
                return View();
            }

            if (await _userRepository.GetByUsernameAsync(username) != null)
            {
                ViewBag.Error = "Username already taken";
                return View();
            }

            if (await _userRepository.GetByEmailAsync(email) != null)
            {
                ViewBag.Error = "Email already registered";
                return View();
            }

            var token = Guid.NewGuid().ToString("N");

            var user = new User
            {
                UserName = username,
                Email = email,
                Phone = phone,
                IsCompany = isCompany,
                ConfirmationToken = token,
            };

            await _userRepository.CreateAsync(user, _passwordHasher.Hash(password));

            var confirmUrl = Url.Action("ConfirmEmail", "Account", new { token }, Request.Scheme)!;

            try { await _emailService.SendConfirmationEmailAsync(email, username, confirmUrl); }
            catch (Exception ex) { Console.Error.WriteLine(ex); }

            return RedirectToAction(nameof(RegisterConfirmation), new { email });
        }
        public IActionResult RegisterConfirmation(string email)
        {
            ViewBag.Email = email;
            return View();
        }
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(ConfirmEmailResult), new { success = false });

            var user = await _userRepository.GetByConfirmationTokenAsync(token);

            if (user == null)
                return RedirectToAction(nameof(ConfirmEmailResult), new { success = false });

            await _userRepository.ConfirmEmailAsync(token);

            var confirmed = await _userRepository.GetByIdAsync(user.Id);
            if (confirmed != null)
                await SignInAsync(confirmed);

            return RedirectToAction(nameof(ConfirmEmailResult), new { success = true });
        }
        public IActionResult ConfirmEmailResult(bool success)
        {
            ViewBag.Success = success;
            return View();
        }
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("GoogleSignedIn", "Account", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Google");
        }
        public async Task<IActionResult> GoogleSignedIn(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync("Cookies");

            if (!result.Succeeded || result.Principal == null)
                return RedirectToAction("Login");

            var claims = result.Principal.Claims.ToList();
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var googleName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var avatarUrl = claims.FirstOrDefault(c => c.Type == "picture")?.Value
                          ?? claims.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value;

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                var baseUsername = SanitizeUsername(googleName ?? email.Split('@')[0]);
                var username = await _userRepository.EnsureUniqueUsernameAsync(baseUsername);

                user = new User
                {
                    UserName = username,
                    Email = email,
                    AvatarUrl = avatarUrl,
                    EmailConfirmed = true,
                };

                var id = await _userRepository.CreateGoogleUserAsync(user);
                user.Id = id;
            }
            else if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(avatarUrl))
            {
                await _userRepository.UpdateAvatarAsync(user.Id, avatarUrl);
                user.AvatarUrl = avatarUrl;
            }

            await HttpContext.SignOutAsync("Cookies");
            await SignInAsync(user);

            return Redirect(returnUrl ?? "/");
        }
        public async Task<IActionResult> Logout()
        {
            if (int.TryParse(User.FindFirst("SessionId")?.Value, out int sessionId)) await _userSessionRepository.CloseSessionAsync(sessionId);
            await HttpContext.SignOutAsync("Cookies");
            return RedirectToAction("Index", "Home");
        }
        private async Task SignInAsync(User user)
        {
            var sessionId = await _userSessionRepository.StartSessionAsync(user.Id);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.UserName),
                new Claim(ClaimTypes.Role,           user.Role ?? "user"),
                new Claim("SessionId",               sessionId.ToString()),
            };
            if (!string.IsNullOrEmpty(user.AvatarUrl)) claims.Add(new Claim("Avatar", user.AvatarUrl));

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("Cookies", principal);
        }
        private static string SanitizeUsername(string raw)
        {
            var sb = new StringBuilder();
            foreach (var ch in raw)
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch == ' ' || ch == '_') sb.Append('_');
            var result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "user" : result;
        }
        [HttpPost]
        [Route("Account/FacebookLogin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginModel model)
        {
            var returnUrl = model.ReturnUrl ?? Url.Content("~/");
            if (string.IsNullOrEmpty(model.AccessToken))
                return BadRequest("Access token required");

            using var http = new HttpClient();
            var graphUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={model.AccessToken}";
            var response = await http.GetAsync(graphUrl);
            if (!response.IsSuccessStatusCode)
                return BadRequest("Invalid Facebook token");

            var fbUser = await response.Content.ReadFromJsonAsync<FacebookUserInfo>();
            if (fbUser == null || string.IsNullOrEmpty(fbUser.Id))
                return BadRequest("Could not retrieve user info");

            User? user = null;

            if (!string.IsNullOrEmpty(fbUser.Email))
                user = await _userRepository.GetByEmailAsync(fbUser.Email);

            if (user == null)
            {
                string baseName;
                if (!string.IsNullOrEmpty(fbUser.Email))
                    baseName = fbUser.Email.Split('@')[0];
                else
                    baseName = $"fb_{fbUser.Id}";
                baseName = SanitizeUsername(fbUser.Name ?? baseName);
                var username = await _userRepository.EnsureUniqueUsernameAsync(baseName);

                var avatarUrl = $"https://graph.facebook.com/{fbUser.Id}/picture?type=large";

                user = new User
                {
                    UserName = username,
                    Email = !string.IsNullOrEmpty(fbUser.Email) ? fbUser.Email : null,
                    EmailConfirmed = !string.IsNullOrEmpty(fbUser.Email),
                    AvatarUrl = avatarUrl,
                    IsCompany = false,
                };

                await _userRepository.CreateExternalUserAsync(user);

                user = await _userRepository.GetByEmailAsync(user.Email) ?? await _userRepository.GetByUsernameAsync(username);
                if (user == null)
                    return BadRequest("Failed to create user");
            }
            else
                if (string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var avatarUrl = $"https://graph.facebook.com/{fbUser.Id}/picture?type=large";
                    await _userRepository.UpdateAvatarAsync(user.Id, avatarUrl);
                    user.AvatarUrl = avatarUrl;
                }

            if (string.IsNullOrEmpty(user.FacebookId))
            {
                await _userRepository.UpdateFacebookIdAsync(user.Id, fbUser.Id);
                user.FacebookId = fbUser.Id;
            }
            await SignInAsync(user);
            return LocalRedirect(returnUrl);
        }
        [HttpPost, IgnoreAntiforgeryToken]
        [Route("Account/FacebookDataDeletion")]
        public async Task<IActionResult> FacebookDataDeletion()
        {
            var form = await Request.ReadFormAsync();
            var signedRequest = form["signed_request"].FirstOrDefault();
            if (string.IsNullOrEmpty(signedRequest))
                return BadRequest("Missing signed_request");

            var facebookUserId = DecodeSignedRequest(signedRequest);
            if (string.IsNullOrEmpty(facebookUserId))
                return BadRequest("Invalid signed_request");

            var user = await _userRepository.GetByFacebookIdAsync(facebookUserId);
            if (user == null)
                return Ok(new { url = Url.Action("DeletionStatus", "Account", new { code = "not_found" }, Request.Scheme), confirmation_code = "not_found" });

            var confirmationCode = Guid.NewGuid().ToString("N");

            await _userRepository.MarkForDeletionAsync(user.Id, confirmationCode);

            var statusUrl = Url.Action("DeletionStatus", "Account", new { code = confirmationCode }, Request.Scheme);

            return Ok(new { url = statusUrl, confirmation_code = confirmationCode });
        }
        private string? DecodeSignedRequest(string signedRequest)
        {
            try
            {
                var parts = signedRequest.Split('.');
                if (parts.Length != 2) return null;

                var encodedSig = parts[0];
                var payload = parts[1];

                var appSecret = _configuration["Facebook:AppSecret"];
                if (string.IsNullOrEmpty(appSecret))
                {
                    Console.Error.WriteLine("Facebook:AppSecret is not configured");
                    return null;
                }

                var sig = Base64UrlDecode(encodedSig);
                var expectedSig = ComputeHmacSha256(payload, appSecret);

                if (!CryptographicOperations.FixedTimeEquals(sig, expectedSig)) return null;

                var payloadPadded = payload.Replace('-', '+').Replace('_', '/');
                while (payloadPadded.Length % 4 != 0) payloadPadded += "=";
                var jsonBytes = Convert.FromBase64String(payloadPadded);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (data != null && data.TryGetValue("user_id", out var userId)) return userId.ToString();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            return null;
        }
        private static byte[] Base64UrlDecode(string input)
        {
            var s = input.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }
        private static byte[] ComputeHmacSha256(string payload, string secret) => new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(payload));
        [HttpGet]
        [Route("Account/DeletionStatus")]
        public async Task<IActionResult> DeletionStatus(string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest();

            if (code == "not_found")
            {
                ViewBag.Message = "No data associated with this request.";
                return View();
            }

            var user = await _userRepository.GetByDeletionCodeAsync(code);
            if (user == null)
            {
                ViewBag.Message = "Invalid or expired request code.";
                return View();
            }

            ViewBag.Message = $"Your data deletion request for user '{user.UserName}' has been received and will be processed within 30 days.";

            return View();
        }
        [HttpGet]
        public IActionResult ForgotPassword() => View();
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email, string? lang = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email address";
                return View();
            }

            var resolvedLang = lang ?? Request.GetLang();
            if (resolvedLang != "lv" && resolvedLang != "ru") resolvedLang = "en";

            var user = await _userRepository.GetByEmailAsync(email.Trim());
            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                var expiry = DateTime.UtcNow.AddHours(1);
                await _userRepository.SetPasswordResetTokenAsync(user.Id, token, expiry);

                var resetUrl = Url.Action("ResetPassword", "Account", new { token }, Request.Scheme)!;
                try { await _emailService.SendPasswordResetEmailAsync(user.Email!, user.UserName, resetUrl, resolvedLang); }
                catch (Exception ex) { Console.Error.WriteLine(ex); }
            }

            ViewBag.Sent = true;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null)
            {
                ViewBag.Invalid = true;
                return View();
            }

            ViewBag.Token = token;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string password, string confirm)
        {
            ViewBag.Token = token;

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null)
            {
                ViewBag.Invalid = true;
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password != confirm)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (password.Length < 8 ||
                !password.Any(char.IsUpper) ||
                !password.Any(char.IsLower) ||
                !password.Any(char.IsDigit))
            {
                ViewBag.Error = "Password must be at least 8 characters with uppercase, lowercase and digit";
                return View();
            }

            await _userRepository.UpdatePasswordHashAsync(user.Id, _passwordHasher.Hash(password));
            await _userRepository.ClearPasswordResetTokenAsync(user.Id);

            ViewBag.Success = true;
            return View();
        }
    }
}
using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    [Authorize]
    public class PointsController(IPointsLedgerRepository ledgerRepository, IUserRepository userRepository, IConfiguration configuration) : Controller
    {
        private readonly IPointsLedgerRepository _ledgerRepository = ledgerRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IConfiguration _configuration = configuration;
        public async Task<IActionResult> Progress()
        {
            var userId = User.GetUserId();
            ViewBag.Summary = await _ledgerRepository.GetSummaryAsync(userId);
            ViewBag.History = await _ledgerRepository.GetHistoryAsync(userId, 20);
            ViewBag.Referral = await GetReferralInfoAsync(userId);
            return View();
        }
        private async Task<ReferralInfo> GetReferralInfoAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var code = ReferralCode.Clean(user?.UserName);
            var (earned, pending) = await _ledgerRepository.GetReferralPointsAsync(userId);

            var baseUrl = _configuration["PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";

            return new ReferralInfo
            {
                Code = code ?? "",
                Link = code == null ? "" : ReferralCode.BuildLink(baseUrl!, code),
                InvitedCount = await _userRepository.GetReferralCountAsync(userId),
                EarnedPoints = earned,
                PendingPoints = pending,
            };
        }
    }
}
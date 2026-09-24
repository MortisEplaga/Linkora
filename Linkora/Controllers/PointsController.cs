using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    [Authorize]
    public class PointsController(IPointsLedgerRepository ledgerRepository) : Controller
    {
        private readonly IPointsLedgerRepository _ledgerRepository = ledgerRepository;

        public async Task<IActionResult> Progress()
        {
            var userId = User.GetUserId();
            ViewBag.Summary = await _ledgerRepository.GetSummaryAsync(userId);
            ViewBag.History = await _ledgerRepository.GetHistoryAsync(userId, 20);
            return View();
        }
    }
}
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Route("[controller]")]
    public class SubscriptionController : Controller
    {
        private readonly ISubscriptionRepository _subscriptionRepository;

        public SubscriptionController(ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionRepository = subscriptionRepository;
        }

        [HttpPost("Toggle/{sellerId:int}")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Toggle(int sellerId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (userId == sellerId)
                return BadRequest(new { error = "Cannot subscribe to yourself" });

            var subscribed = await _subscriptionRepository.ToggleAsync(userId, sellerId);
            var count = await _subscriptionRepository.GetSubscriberCountAsync(sellerId);

            return Json(new { subscribed, count });
        }

        [HttpGet("State/{sellerId:int}")]
        public async Task<IActionResult> State(int sellerId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool subscribed = false;

            if (userIdStr != null && int.TryParse(userIdStr, out var userId))
                subscribed = await _subscriptionRepository.IsSubscribedAsync(userId, sellerId);

            var count = await _subscriptionRepository.GetSubscriberCountAsync(sellerId);

            return Json(new { subscribed, count });
        }
    }
}
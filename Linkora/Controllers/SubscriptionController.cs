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

        [HttpGet("Index")]
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var following = await _subscriptionRepository.GetFollowingAsync(userId);
            return View(following);
        }

        [HttpPost("Toggle/{followingId:int}")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Toggle(int followingId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (userId == followingId)
                return BadRequest(new { error = "Cannot subscribe to yourself" });

            var subscribed = await _subscriptionRepository.ToggleAsync(userId, followingId);
            var count = await _subscriptionRepository.GetSubscriberCountAsync(followingId);

            return Json(new { subscribed, count });
        }

        [HttpGet("State/{followingId:int}")]
        public async Task<IActionResult> State(int followingId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool subscribed = false;

            if (userIdStr != null && int.TryParse(userIdStr, out var userId))
                subscribed = await _subscriptionRepository.IsSubscribedAsync(userId, followingId);

            var count = await _subscriptionRepository.GetSubscriberCountAsync(followingId);

            return Json(new { subscribed, count });
        }
    }
}
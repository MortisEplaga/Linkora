using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> Index() => View(await _subscriptionRepository.GetFollowingAsync(User.GetUserId()));

        [HttpPost("Toggle/{followingId:int}")]
        [Authorize]
        public async Task<IActionResult> Toggle(int followingId)
        {
            var userId = User.GetUserId();

            if (userId == followingId)
                return BadRequest(new { error = "Cannot subscribe to yourself" });

            var subscribed = await _subscriptionRepository.ToggleAsync(userId, followingId);
            var count = await _subscriptionRepository.GetSubscriberCountAsync(followingId);

            return Json(new { subscribed, count });
        }

        [HttpGet("State/{followingId:int}")]
        public async Task<IActionResult> State(int followingId)
        {
            bool subscribed = false;
            if (User.TryGetUserId(out int userId)) subscribed = await _subscriptionRepository.IsSubscribedAsync(userId, followingId);
            var count = await _subscriptionRepository.GetSubscriberCountAsync(followingId);

            return Json(new { subscribed, count });
        }
    }
}
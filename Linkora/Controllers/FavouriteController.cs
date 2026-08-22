using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    public class FavouriteController : Controller
    {
        private readonly IFavouriteRepository _favouriteRepository;

        public FavouriteController(IFavouriteRepository favouriteRepository)
        {
            _favouriteRepository = favouriteRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int productId, bool can)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var active = await _favouriteRepository.ToggleAsync(productId, int.Parse(userId), can);
            return Json(new { active });
        }

        [HttpGet]
        public async Task<IActionResult> UserItems()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Json(new { favs = Array.Empty<int>(), cart = Array.Empty<int>() });

            var (favs, cart) = await _favouriteRepository.GetUserItemIdsAsync(int.Parse(userId));
            return Json(new { favs, cart });
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "favs")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return RedirectToAction("Login", "Account");

            var (favs, cart) = await _favouriteRepository.GetUserItemsAsync(int.Parse(userId));

            ViewBag.Favs = favs;
            ViewBag.Cart = cart;
            ViewBag.Tab = tab;
            return View();
        }
    }
}
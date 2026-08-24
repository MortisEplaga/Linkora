using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;

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
            if (!User.TryGetUserId(out int userId)) return Unauthorized();

            var active = await _favouriteRepository.ToggleAsync(productId, userId, can);
            return Json(new { active });
        }

        [HttpGet]
        public async Task<IActionResult> UserItems()
        {
            if (!User.TryGetUserId(out int userId)) return Json(new { favs = Array.Empty<int>(), cart = Array.Empty<int>() });

            var (favs, cart) = await _favouriteRepository.GetUserItemIdsAsync(userId);
            return Json(new { favs, cart });
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "favs")
        {
            if (!User.TryGetUserId(out int userId)) return RedirectToAction("Login", "Account");

            var (favs, cart) = await _favouriteRepository.GetUserItemsAsync(userId);

            ViewBag.Favs = favs;
            ViewBag.Cart = cart;
            ViewBag.Tab = tab;
            return View();
        }
    }
}
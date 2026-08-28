using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    public class SellerController : Controller
    {
        private readonly ISellerRepository _sellerRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SellerController(ISellerRepository sellerRepository, IHttpContextAccessor httpContextAccessor)
        {
            _sellerRepository = sellerRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> Index(int id, int? categoryId, string sort = "new", int page = 1)
        {
            var seller = await _sellerRepository.GetByIdAsync(id);
            if (seller == null) return NotFound();

            var (reviewCount, reviewAvg) = await _sellerRepository.GetRatingAsync(id);
            var categories = await _sellerRepository.GetCategoriesAsync(id, Request.GetLang());
            var pagedResult = await _sellerRepository.GetProductsPagedAsync(id, categoryId, sort, page);
            var reviews = await _sellerRepository.GetReviewsAsync(id);

            ViewBag.Seller = seller;
            ViewBag.ReviewCount = reviewCount;
            ViewBag.ReviewAvg = reviewAvg;
            ViewBag.Categories = categories;
            ViewBag.Products = pagedResult.Items;
            ViewBag.Reviews = reviews;
            ViewBag.Sort = sort;
            ViewBag.CategoryId = categoryId;
            ViewBag.Page = pagedResult.CurrentPage;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.Total = pagedResult.Total;

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Rating(int id)
        {
            var (count, avg) = await _sellerRepository.GetRatingAsync(id);
            if (count > 0)
                return Json(new { count, avg = Math.Round(avg, 1) });
            return Json(new { count = 0, avg = 0.0 });
        }
    }
}
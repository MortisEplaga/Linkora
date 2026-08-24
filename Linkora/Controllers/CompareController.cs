using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    [Authorize]
    public class CompareController : Controller
    {
        private readonly ICompareRepository _compareRepository;

        public CompareController(ICompareRepository compareRepository)
        {
            _compareRepository = compareRepository;
        }
        public async Task<IActionResult> Index()
        {
            var data = await _compareRepository.GetCompareDataAsync(User.GetUserId(), Request.Cookies["lang"] ?? "en");

            ViewBag.Products = data.Products;
            ViewBag.AllParamIds = data.AllParamIds;
            ViewBag.ParamLabels = data.ParamLabels;
            ViewBag.ParamMatrix = data.ParamMatrix;

            return View();
        }
    }
}
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        private string GetLang() => Request.Cookies["lang"] ?? "en";

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var lang = GetLang();

            var data = await _compareRepository.GetCompareDataAsync(userId, lang);

            ViewBag.Products = data.Products;
            ViewBag.AllParamIds = data.AllParamIds;
            ViewBag.ParamLabels = data.ParamLabels;
            ViewBag.ParamMatrix = data.ParamMatrix;

            return View();
        }
    }
}
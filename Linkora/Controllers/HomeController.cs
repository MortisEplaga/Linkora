using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Category", new { id = 3711 });
        }
    }
}
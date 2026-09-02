using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Linkora.Controllers
{
    public class CategoryController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IConfiguration _configuration;

        public CategoryController(ICategoryRepository categoryRepository, IProductRepository productRepository, IConfiguration configuration)
        {
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(int id, string sort = "new", string? q = null, string? city = null, int page = 1)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return NotFound();

            var breadcrumb = await _categoryRepository.GetBreadcrumbAsync(id);
            var children = await _categoryRepository.GetChildrenAsync(id);
            var parameters = await _categoryRepository.GetParametersAsync(breadcrumb.Select(c => c.Id));
            parameters = parameters.Where(p => p.Param.Type != 7).ToList();

            var filters = new Dictionary<int, List<string>>();
            var rangeFrom = new Dictionary<int, decimal>();
            var rangeTo = new Dictionary<int, decimal>();

            foreach (var key in Request.Query.Keys)
            {
                if (!key.StartsWith("p_")) continue;
                var parts = key.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int paramId))
                {
                    var vals = Request.Query[key].Where(v => !string.IsNullOrEmpty(v)).ToList();
                    if (vals.Count > 0) filters[paramId] = vals;
                }
                else if (parts.Length == 3 && int.TryParse(parts[1], out int rangeId))
                {
                    var raw = Request.Query[key].FirstOrDefault();
                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dval))
                        if (parts[2] == "from") rangeFrom[rangeId] = dval;
                        else if (parts[2] == "to") rangeTo[rangeId] = dval;
                }
            }

            int? priceParamId = parameters.FirstOrDefault(p => p.Param.Name == "Price")?.Param.Id;

            var result = await _productRepository.GetByCategoryAsync(id, includeDescendants: true, sort, filters, rangeFrom, rangeTo, priceParamId, city, q, page);

            ViewBag.City = city;
            ViewBag.Category = category;
            ViewBag.Breadcrumb = breadcrumb;
            ViewBag.Children = children;
            ViewBag.Parameters = parameters;
            ViewBag.Products = result.Items;
            ViewBag.Page = result.CurrentPage;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.Total = result.Total;
            ViewBag.Sort = sort;
            ViewBag.Search = q;
            ViewBag.HasPriceSort = priceParamId.HasValue;
            ViewBag.Filters = filters;
            ViewBag.RangeFrom = rangeFrom;
            ViewBag.RangeTo = rangeTo;
            ViewBag.GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"];
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> All() => Json((await _categoryRepository.GetAllAsync()).Select(c => new { c.Id, c.ParentId, c.Name, nameEn = c.NameEn ?? c.Name }));
    }
}
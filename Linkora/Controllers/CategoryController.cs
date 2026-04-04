using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    public class Category : Controller
    {
        private readonly ICategoryRepository _categoryRepository;

        private readonly IProductRepository _productRepository;

        public Category(ICategoryRepository categoryRepository, IProductRepository productRepository)
        {
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
        }

        public async Task<IActionResult> Index(int id, string sort = "new", string? q = null, string? city = null)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return NotFound();

            var breadcrumb = await _categoryRepository.GetBreadcrumbAsync(id);
            var children = await _categoryRepository.GetChildrenAsync(id);
            var parameters = await _categoryRepository.GetParametersAsync(breadcrumb.Select(c => c.Id));
            var descendantIds = await _categoryRepository.GetDescendantIdsAsync(id);

            // Парсим фильтры из query string
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
                    if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal dval))
                    {
                        if (parts[2] == "from") rangeFrom[rangeId] = dval;
                        else if (parts[2] == "to") rangeTo[rangeId] = dval;
                    }
                }
            }

            int? priceParamId = parameters.FirstOrDefault(p => p.Param.Name == "Price")?.Param.Id;

            var products = await _productRepository.GetByCategoryAsync(
                descendantIds, sort, filters, rangeFrom, rangeTo, priceParamId, city, q);
            ViewBag.City = city;
            ViewBag.Category = category;
            ViewBag.Breadcrumb = breadcrumb;
            ViewBag.Children = children;
            ViewBag.Parameters = parameters;
            ViewBag.Products = products;
            ViewBag.Sort = sort;
            ViewBag.Search = q;
            ViewBag.HasPriceSort = priceParamId.HasValue;
            // Передаём активные фильтры во view для предзаполнения
            ViewBag.Filters = filters;
            ViewBag.RangeFrom = rangeFrom;
            ViewBag.RangeTo = rangeTo;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> All()
        {
            var all = await _categoryRepository.GetAllAsync();
            var result = all
                .Where(c => c.Type == 1 || c.Type == null)
                .Select(c => new { c.Id, c.ParentId, c.Name });
            return Json(result);
        }
    }
}

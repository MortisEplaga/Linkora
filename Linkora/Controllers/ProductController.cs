using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    public class ProductController(ICategoryRepository categoryRepository,
        IAddressRepository addressRepository,
        IProductRepository productRepository,
        IConfiguration configuration,
        IMessageRepository messageRepository) : Controller
    {
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IAddressRepository _addressRepository = addressRepository;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly IMessageRepository _messageRepository = messageRepository;

        private readonly IConfiguration _configuration = configuration;
        private static Dictionary<int, string> ParseParamsJson(string? json)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(json)) return result;
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw == null) return result;
            foreach (var (k, v) in raw)
                if (int.TryParse(k, out var pid) && !string.IsNullOrWhiteSpace(v))
                    result[pid] = v;
            return result;
        }

        [HttpGet]
        public async Task<IActionResult> Cities()
        {
            var list = await _addressRepository.GetCitiesAsync();
            return Json(list.Select(x => new { id = x.Id, name = x.Name }));
        }

        [HttpGet]
        public async Task<IActionResult> Streets(int cityId)
        {
            var list = await _addressRepository.GetStreetsAsync(cityId);
            return Json(list.Select(x => new { id = x.Id, name = x.Name }));
        }

        [HttpGet]
        public async Task<IActionResult> Houses(int streetId)
        {
            var list = await _addressRepository.GetHousesAsync(streetId);
            return Json(list.Select(x => new { id = x.Id, name = x.Name }));
        }
        public IActionResult Create() => View();

        [HttpGet]
        public async Task<IActionResult> Parameters(int categoryId)
        {
            var breadcrumb = await _categoryRepository.GetBreadcrumbAsync(categoryId);
            var parameters = await _categoryRepository.GetParametersAsync(breadcrumb.Select(c => c.Id));
            return Json(parameters.Select(p => new
            {
                id = p.Param.Id,
                name = p.Param.Name,
                type = p.Param.Type,
                options = p.Options,
                min = p.Min,
                max = p.Max,
                step = p.Step
            }));
        }
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            // Не считаем просмотры самого владельца
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null || product.UserId?.ToString() != userId)
                await _productRepository.IncrementViewCountAsync(id);

            var similar = product.CategoryId.HasValue
                ? await _productRepository.GetSimilarAsync(product.CategoryId.Value, id)
                : new List<Product>();

            // Параметры с значениями
            var paramValues = await _productRepository.GetParamValuesAsync(id);
            List<Parameter> paramDefs = new();
            if (paramValues.Count > 0 && product.CategoryId.HasValue)
            {
                var breadcrumb = await _categoryRepository.GetBreadcrumbAsync(product.CategoryId.Value);
                paramDefs = await _categoryRepository.GetParametersAsync(breadcrumb.Select(c => c.Id));
            }

            ViewBag.Product = product;
            ViewBag.Similar = similar;
            ViewBag.ParamValues = paramValues;
            ViewBag.ParamDefs = paramDefs;
            return View();
        }
        [Authorize]
        public async Task<IActionResult> My(string tab = "active")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var products = await _productRepository.GetByUserAsync(userId, tab);
            var counts = await _productRepository.GetCountsByStatusAsync(userId); // должен быть этот вызов
            ViewBag.Products = products;
            ViewBag.Tab = tab;
            ViewBag.StatusCounts = counts; // передаём словарь
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (product.UserId != userId) return Forbid();
            ViewBag.Product = product;
            return View();
        }

        [Authorize]
        [HttpPost]
        [Authorize, HttpPost]
        public async Task<IActionResult> Edit(int id, string title, string? description,
    int? qty, string? address, int? categoryId,
    string? paramsJson, List<IFormFile>? photos, bool replaceMedia = false)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var existing = await _productRepository.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.UserId != userId) return Forbid();

            var totalBytes = photos?.Sum(f => f.Length) ?? 0;
            if (totalBytes > 52_428_800)
                return BadRequest("Total media size exceeds 50 MB");

            var paramValues = ParseParamsJson(paramsJson);
            bool wasArchived = existing.Status == ProductStatus.Archived;

            // Если загружены новые файлы — удаляем старые и сохраняем новые
            if (photos?.Count > 0)
            {
                await _productRepository.DeleteMediaAsync(id);
                var media = await SaveUploadedFiles(photos);
                await _productRepository.SaveMediaAsync(id, media);
            }

            // Всегда берём первый элемент из ProductMedia как аватар
            var currentMedia = await _productRepository.GetMediaAsync(id);
            string? newAvatar = currentMedia.FirstOrDefault()?.FilePath ?? existing.AvatarImagePath;

            await _productRepository.UpdateAsync(new Product
            {
                Id = id,
                UserId = userId,
                Name = title,
                Description = description,
                Qty = qty,
                Address = address,
                CategoryId = categoryId,
                AvatarImagePath = newAvatar,
            }, paramValues);

            if (wasArchived)
                await _productRepository.ReactivateProductAsync(id, userId);

            return Ok();
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM Products WHERE Id=@Id AND UserId=@U", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@U", userId);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        [HttpGet]
        public async Task<IActionResult> ParamValues(int productId)
        {
            var values = await _productRepository.GetParamValuesAsync(productId);
            return Json(values);
        }
        // Controllers/ProductController.cs
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CompleteDeal(int id, int otherUserId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            if (product.UserId != userId) return Forbid();
            if (product.Status != ProductStatus.Active)
                return BadRequest("Только активные объявления можно завершить");

            var success = await _productRepository.CompleteDealAsync(id, userId);
            if (!success) return BadRequest("Не удалось завершить сделку");

            return Ok();
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetConversationPartners(int productId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var partners = await _messageRepository.GetConversationPartnersAsync(productId, userId);
            return Ok(partners.Select(p => new { p.Id, p.UserName, p.AvatarImagePath, p.IsCompany }));
        }
        [Authorize]
        [HttpPost]
        [Authorize, HttpPost]
        public async Task<IActionResult> Create(
    string title, string? description, int? qty,
    string? address, int? categoryId,
    List<IFormFile>? photos, string? paramsJson)
        {
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required");

            // Проверяем суммарный размер
            var totalBytes = photos?.Sum(f => f.Length) ?? 0;
            if (totalBytes > 52_428_800)
                return BadRequest("Total media size exceeds 50 MB");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var media = photos?.Count > 0 ? await SaveUploadedFiles(photos) : new();

            var paramValues = ParseParamsJson(paramsJson);

            var newId = await _productRepository.CreateAsync(new Product
            {
                UserId = userId,
                Name = title,
                Description = description,
                Qty = qty,
                Address = address,
                CategoryId = categoryId,
                AvatarImagePath = media.FirstOrDefault()?.FilePath,
            }, paramValues);

            // Синхронизируем аватар с первым элементом ProductMedia
            if (media.Count > 0)
            {
                var firstMedia = media[0];
                await _productRepository.SaveMediaAsync(newId, media);
            }
            return Ok(new { id = newId });
        }
        private async Task<List<ProductMedia>> SaveUploadedFiles(List<IFormFile> files)
        {
            var result = new List<ProductMedia>();
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "products");
            Directory.CreateDirectory(folder);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var isVideo = new[] { ".mp4", ".webm", ".mov", ".avi" }.Contains(ext);
                var name = $"{Guid.NewGuid()}{ext}";
                await using var stream = System.IO.File.Create(Path.Combine(folder, name));
                await file.CopyToAsync(stream);
                result.Add(new ProductMedia
                {
                    FilePath = $"/img/products/{name}",
                    MediaType = isVideo ? "video" : "image",
                });
            }
            return result;
        }
    }
}
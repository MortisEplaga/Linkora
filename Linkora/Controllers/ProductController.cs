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
        IMessageRepository messageRepository,
        INotificationRepository notificationRepository) : Controller
    {
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IAddressRepository _addressRepository = addressRepository;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly IMessageRepository _messageRepository = messageRepository;
        private readonly INotificationRepository _notificationRepository = notificationRepository;
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
        [HttpPost]
        public async Task<IActionResult> ResolveSelectOption([FromBody] ResolveSelectOptionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest("Text is required");

            var lang = Request.Cookies["lang"] ?? "en";
            var col = lang switch
            {
                "lv" => "ValueLV",
                "ru" => "ValueRU",
                _ => "Value"
            };
            var trimmed = dto.Text.Trim();

            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
            await conn.OpenAsync();

            await using var findCmd = new SqlCommand($@"
                SELECT Id FROM SelectOptions
                WHERE CategoryId = @ParamId
                  AND LTRIM(RTRIM({col})) = LTRIM(RTRIM(@Text))", conn);
            findCmd.Parameters.AddWithValue("@ParamId", dto.ParamId);
            findCmd.Parameters.AddWithValue("@Text", trimmed);
            var existingId = await findCmd.ExecuteScalarAsync();

            if (existingId != null)
                return Json(new { id = (int)existingId, created = false });

            if (!dto.CreateIfNotFound)
                return Json(new { id = 0, created = false });
            await using var insCmd = new SqlCommand(@"
                INSERT INTO SelectOptions (CategoryId, Value, ValueLV, ValueRU, IsConf)
                OUTPUT INSERTED.Id
                VALUES (@ParamId, @Text, @Text, @Text, false)", conn);
            insCmd.Parameters.AddWithValue("@ParamId", dto.ParamId);
            insCmd.Parameters.AddWithValue("@Text", trimmed);
            var newId = (int)(await insCmd.ExecuteScalarAsync())!;

            return Json(new { id = newId, created = true });
        }
        [HttpGet]
        public async Task<IActionResult> GetSelectOptions([FromQuery] int paramId)
        {
            var lang = Request.Cookies["lang"] ?? "en";
            var col = lang switch
            {
                "lv" => "ValueLV",
                "ru" => "ValueRU",
                _ => "Value"
            };

            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand($@"
        SELECT Id, {col} 
        FROM SelectOptions 
        WHERE CategoryId = @ParamId and IsConf = 1", conn);
            cmd.Parameters.AddWithValue("@ParamId", paramId);

            var options = new List<object>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                options.Add(new
                {
                    id = reader.GetInt32(0),
                    text = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }

            return Json(options);
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyRecaptcha([FromBody] RecaptchaDto dto)
        {
            var secret = _configuration["Recaptcha:SecretKey"]!;
            using var http = new HttpClient();
            var resp = await http.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={dto.Token}",
                null);
            var json = await resp.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonDocument.Parse(json);
            var success = result.RootElement.GetProperty("success").GetBoolean();
            return Ok(new { success });
        }

        public class RecaptchaDto { public string Token { get; set; } = ""; }
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
                options = p.Options.Select(o => new { id = o.Id, text = o.Text }),
                min = p.Min,
                max = p.Max,
                step = p.Step
            }));
        } 

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null || product.UserId?.ToString() != userId)
                await _productRepository.IncrementViewCountAsync(id);

            var similar = product.CategoryId.HasValue
                ? await _productRepository.GetSimilarAsync(product.CategoryId.Value, id)
                : new List<Product>();

            var lang = Request.Cookies["lang"] ?? "en";
            var paramValues = await _productRepository.GetParamDisplayValuesAsync(id, lang);
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
            ViewBag.RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"];
            return View();
        }
        [Authorize]
        public async Task<IActionResult> My(string tab = "active")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var products = await _productRepository.GetByUserAsync(userId, tab);
            var counts = await _productRepository.GetCountsByStatusAsync(userId); 
            ViewBag.Products = products;
            ViewBag.Tab = tab;
            ViewBag.StatusCounts = counts; 
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (product.UserId != userId) return Forbid();

            string? categoryName = null;
            if (product.CategoryId.HasValue)
            {
                var cat = await _categoryRepository.GetByIdAsync(product.CategoryId.Value);
                categoryName = cat?.Name;
            }

            ViewBag.Product = product;
            ViewBag.CategoryName = categoryName;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MediaFiles(int productId)
        {
            var media = await _productRepository.GetMediaAsync(productId);
            return Json(media.Select(m => new { filePath = m.FilePath, mediaType = m.MediaType }));
        }

        [Authorize]
        [HttpPost]
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, string title, string? description,
    int? qty, string? address, int? categoryId,
    string? paramsJson, List<IFormFile>? photos,
    string? keepMediaJson = null, bool replaceMedia = false, int? publishDays = null)
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

            var keepPaths = string.IsNullOrEmpty(keepMediaJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(keepMediaJson) ?? new();

            var currentMedia = await _productRepository.GetMediaAsync(id);
            var toDelete = currentMedia.Where(m => !keepPaths.Contains(m.FilePath)).ToList();

            if (toDelete.Any())
            {
                foreach (var m in toDelete)
                {
                    var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", m.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                }
                await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
                await conn.OpenAsync();
                var ids = string.Join(",", toDelete.Select(m => m.Id));
                await using var cmd = new SqlCommand($"DELETE FROM ProductMedia WHERE Id IN ({ids})", conn);
                await cmd.ExecuteNonQueryAsync();
            }

            if (photos?.Count > 0)
            {
                var newMedia = await SaveUploadedFiles(photos);
                await _productRepository.SaveMediaAsync(id, newMedia);
            }

            var refreshedMedia = await _productRepository.GetMediaAsync(id);
            string? newAvatar = refreshedMedia.FirstOrDefault()?.FilePath ?? existing.AvatarImagePath;

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

            if (publishDays.HasValue && new[] { 7, 14, 30, 60, 90 }.Contains(publishDays.Value))
            {
                await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(@"
            UPDATE Products
            SET PublishDurationDays = @D,
                ExpiresAt = DATEADD(DAY, @D, GETDATE())
            WHERE Id = @Id AND UserId = @UserId", conn);
                cmd.Parameters.AddWithValue("@D", publishDays.Value);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            if (wasArchived)
                await _productRepository.ReactivateProductAsync(id, userId);

            return Ok();
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var isAdmin = User.IsInRole("admin");

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            if (product.UserId != userId && !isAdmin) return Forbid();

            await _productRepository.DeleteAsync(id);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> ParamValues(int productId)
        {
            var lang = Request.Cookies["lang"] ?? "en";
            var paramValues = await _productRepository.GetParamDisplayValuesAsync(productId, lang);
            return Json(paramValues);
        }
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
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(
    string title, string? description, int? qty,
    string? address, int? categoryId,
    List<IFormFile>? photos, string? paramsJson,
    int? publishDays = null)
        {
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required");

            var totalBytes = photos?.Sum(f => f.Length) ?? 0;
            if (totalBytes > 52_428_800)
                return BadRequest("Total media size exceeds 50 MB");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            int duration = 30;
            if (publishDays.HasValue && new[] { 7, 14, 30, 60, 90 }.Contains(publishDays.Value))
            {
                duration = publishDays.Value;
            }
            else
            {
                await using var conn = new Microsoft.Data.SqlClient.SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")!);
                await conn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT PreferredAdDuration FROM Users WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", userId);
                var pref = await cmd.ExecuteScalarAsync();
                if (pref != null && pref != DBNull.Value)
                    duration = (int)pref;
            }

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
            }, paramValues, duration);

            if (media.Count > 0)
                await _productRepository.SaveMediaAsync(newId, media);

            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            await _notificationRepository.NotifySubscribersAsync(userId, newId, title, userName);

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
        [HttpGet]
        public async Task<IActionResult> CategoryRules(int categoryId)
        {
            var breadcrumb = await _categoryRepository.GetBreadcrumbAsync(categoryId);
            var catIds = breadcrumb.Select(c => c.Id).ToList();
            var rules = await _productRepository.GetCategoryRulesAsync(catIds);
            return Json(rules);
        }
    }
}
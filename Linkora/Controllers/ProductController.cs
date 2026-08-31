using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    public class ProductController(ICategoryRepository categoryRepository,
        IAddressRepository addressRepository,
        IProductRepository productRepository,
        IConfiguration configuration,
        IMessageRepository messageRepository,
        INotificationService notifications,
        IUserRepository userRepository,
        ISelectOptionRepository selectOptionRepository,
        IMediaStorageService mediaStorage,
        IGeocodingService geocodingService) : Controller
    {
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IAddressRepository _addressRepository = addressRepository;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly IMessageRepository _messageRepository = messageRepository;
        private readonly INotificationService _notifications = notifications;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ISelectOptionRepository _selectOptionRepository = selectOptionRepository;
        private readonly IConfiguration _configuration = configuration;
        private readonly IMediaStorageService _mediaStorage = mediaStorage;
        private readonly IGeocodingService _geocodingService = geocodingService;

        private static int PromotionPoints(string? promotionType) => promotionType switch
        {
            "Highlight" => 1,
            "Top" => 2,
            "Vip" => 3,
            _ => 0
        };

        private static Dictionary<int, string> ParseParamsJson(string? json)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(json)) return result;
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw == null) return result;
            foreach (var (k, v) in raw) if (int.TryParse(k, out var pid) && !string.IsNullOrWhiteSpace(v)) result[pid] = v;
            return result;
        }

        public async Task<IActionResult> ResolveSelectOption([FromBody] ResolveSelectOptionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Text is required");

            var existingId = await _selectOptionRepository.FindIdAsync(dto.ParamId, dto.Text, Request.GetLang());

            if (existingId.HasValue) return Json(new { id = existingId.Value, created = false });

            if (!dto.CreateIfNotFound) return Json(new { id = 0, created = false });

            var newId = await _selectOptionRepository.CreateAsync(dto.ParamId, dto.Text);
            return Json(new { id = newId, created = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetSelectOptions([FromQuery] int paramId) => Json((await _selectOptionRepository.GetConfirmedAsync(paramId, Request.GetLang())).Select(o => new { id = o.Id, text = o.Text }));

        [HttpPost]
        public async Task<IActionResult> VerifyRecaptcha([FromBody] RecaptchaDto dto)
        {
            var secret = _configuration["Recaptcha:SecretKey"]!;
            using var http = new HttpClient();
            var resp = await http.PostAsync($"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={dto.Token}", null);
            var json = await resp.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonDocument.Parse(json);
            var success = result.RootElement.GetProperty("success").GetBoolean();
            return Ok(new { success });
        }

        public class RecaptchaDto { public string Token { get; set; } = ""; }

        [HttpGet]
        public async Task<IActionResult> Cities() => Json((await _addressRepository.GetCitiesAsync()).Select(x => new { id = x.Id, name = x.Name }));

        [HttpGet]
        public async Task<IActionResult> Streets(int cityId) => Json((await _addressRepository.GetStreetsAsync(cityId)).Select(x => new { id = x.Id, name = x.Name }));

        [HttpGet]
        public async Task<IActionResult> Houses(int streetId) => Json((await _addressRepository.GetHousesAsync(streetId)).Select(x => new { id = x.Id, name = x.Name }));

        public async Task<IActionResult> Create()
        {
            if (User.Identity!.IsAuthenticated)
            {
                var user = await _userRepository.GetByIdAsync(User.GetUserId());
                ViewBag.UserHomeAddress = user?.HomeAddress;
            }
            return View();
        }
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
                colorOptions = p.ColorOptions.Select(c => new { id = c.Id, name = c.Name, hex = c.HexValue, isMain = c.IsMain }),
                min = p.Min,
                max = p.Max,
                step = p.Step
            }));
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            if (!User.TryGetUserId(out int currentUserId) || product.UserId != currentUserId) await _productRepository.IncrementViewCountAsync(id);
            var similar = product.CategoryId.HasValue ? await _productRepository.GetSimilarAsync(product.CategoryId.Value, id) : [];

            var paramValues = await _productRepository.GetParamDisplayValuesAsync(id, Request.GetLang());
            List<Parameter> paramDefs = [];
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
        public async Task<IActionResult> My(string tab = "Active")
        {
            var userId = User.GetUserId();
            var counts = await _productRepository.GetCountsByStatusAsync(userId);

            if (tab == "Purchased")
            {
                var purchased = await _productRepository.GetPurchasedByUserAsync(userId);
                var purchasedCount = await _productRepository.GetPurchasedConversationCountAsync(userId);
                counts["Purchased"] = purchasedCount;

                ViewBag.Products = purchased;
                ViewBag.Tab = tab;
                ViewBag.StatusCounts = counts;
                return View();
            }

            var products = await _productRepository.GetByUserAsync(userId, tab);
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
            var userId = User.GetUserId();
            if (product.UserId != userId) return Forbid();
            if (await _userRepository.IsBannedAsync(userId)) return Forbid();

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
        public async Task<IActionResult> MediaFiles(int productId) => Json((await _productRepository.GetMediaAsync(productId)).Select(m => new { filePath = m.FilePath, mediaType = m.MediaType }));

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(MediaStorageService.MaxTotalBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MediaStorageService.MaxTotalBytes)]
        public async Task<IActionResult> Edit(int id, string title, string? description,
                                              int? qty, string? address, int? categoryId,
                                              string? paramsJson, List<IFormFile>? photos,
                                              string? keepMediaJson = null, bool replaceMedia = false,
                                              int? publishDays = null, string? promotionType = null)
        {
            var totalBytes = photos?.Sum(f => f.Length) ?? 0;
            if (totalBytes > MediaStorageService.MaxTotalBytes) return BadRequest("Total media size exceeds 50 MB");

            var userId = User.GetUserId();
            if (await _userRepository.IsBannedAsync(userId)) return Forbid();
            var existing = await _productRepository.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.UserId != userId) return Forbid();

            decimal? lat = existing.Lat, lng = existing.Lng;
            if (!string.Equals(existing.Address ?? "", address ?? "", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    lat = null;
                    lng = null;
                }
                else
                {
                    var geocoded = await _geocodingService.GeocodeAsync(address);
                    if (geocoded.HasValue) (lat, lng) = geocoded.Value;
                    else { lat = null; lng = null; }
                }
            }

            var paramValues = ParseParamsJson(paramsJson);
            var oldParamValues = await _productRepository.GetParamValuesAsync(id);
            var priceParamId = await _productRepository.GetPriceParamIdAsync(id);
            bool wasArchived = existing.Status == ProductStatus.Archived;

            var keepPaths = string.IsNullOrEmpty(keepMediaJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(keepMediaJson) ?? [];

            var currentMedia = await _productRepository.GetMediaAsync(id);
            var toDelete = currentMedia.Where(m => !keepPaths.Contains(m.FilePath)).ToList();

            if (toDelete.Any()) await _productRepository.DeleteSpecificMediaAsync(toDelete.Select(m => m.Id));

            if (photos?.Count > 0)
            {
                var newMedia = await _mediaStorage.SaveUploadedFilesAsync(photos);
                await _productRepository.SaveMediaAsync(id, newMedia);
            }

            var refreshedMedia = await _productRepository.GetMediaAsync(id);
            string? newAvatar = refreshedMedia.FirstOrDefault()?.FilePath ?? existing.AvatarUrl;
            var oldPoints = PromotionPoints(existing.PromotionType);
            var newPoints = PromotionPoints(promotionType);

            await _productRepository.UpdateAsync(new Product
            {
                Id = id,
                UserId = userId,
                Name = title,
                Description = description,
                Qty = qty,
                Address = address,
                CategoryId = categoryId,
                AvatarUrl = newAvatar,
                Lat = lat,
                Lng = lng
            }, paramValues, promotionType ?? "None");

            await _productRepository.RecalculateModerationScoreAsync(id);

            if (newPoints != oldPoints) await _userRepository.AdjustPromotionPointsAsync(userId, newPoints - oldPoints);

            var changes = new List<object>();
            if (!string.Equals(existing.Name, title, StringComparison.Ordinal)) changes.Add(new { type = "title_changed" });

            if (!string.Equals(existing.Address ?? "", address ?? "", StringComparison.Ordinal))
                changes.Add(new
                {
                    type = "address_changed",
                    oldAddress = existing.Address ?? "—",
                    newAddress = address ?? "—"
                });
            if (existing.Qty != qty)
                changes.Add(new
                {
                    type = "qty_changed",
                    oldQty = existing.Qty?.ToString() ?? "—",
                    newQty = qty?.ToString() ?? "—"
                });
            if (!string.Equals(existing.Description ?? "", description ?? "", StringComparison.Ordinal)) changes.Add(new { type = "description_updated" });
            if (priceParamId.HasValue)
            {
                paramValues.TryGetValue(priceParamId.Value, out var newPriceStr);
                decimal? newPrice = decimal.TryParse(newPriceStr,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var np) ? np : null;
                if (existing.Price != newPrice)
                    changes.Add(new
                    {
                        type = "price_changed",
                        oldPrice = existing.Price?.ToString("N2") ?? "—",
                        newPrice = newPrice?.ToString("N2") ?? "—"
                    });
            }

            var otherChanged = paramValues
                .Where(kv => kv.Key != priceParamId && (!oldParamValues.TryGetValue(kv.Key, out var ov) || ov != kv.Value))
                .Count()
                + oldParamValues
                .Where(kv => kv.Key != priceParamId && !paramValues.ContainsKey(kv.Key))
                .Count();
            if (otherChanged > 0) changes.Add(new { type = "characteristics_updated" });

            if (changes.Any())
            {
                var favUserIds = await _productRepository.GetFavouriteSubscriberIdsAsync(id, userId);
                if (favUserIds.Any())
                {
                    var payload = new
                    {
                        type = "favourite_updated",
                        changes = changes.Take(3).ToList()
                    };
                    foreach (var favUid in favUserIds) await _notifications.CreateAsync(favUid, null, id, System.Text.Json.JsonSerializer.Serialize(payload));
                }
            }

            if (publishDays.HasValue && new[] { 7, 14, 30, 60, 90 }.Contains(publishDays.Value)) await _productRepository.UpdatePublishDurationAsync(id, userId, publishDays.Value);

            if (wasArchived) await _productRepository.ReactivateProductAsync(id, userId);

            return Ok();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();
            if (await _userRepository.IsBannedAsync(userId)) return Forbid();
            var isAdmin = User.IsInRole("admin");

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            if (product.UserId != userId && !isAdmin) return Forbid();

            await _productRepository.DeleteAsync(id);
            return Ok();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Republish(int id)
        {
            var userId = User.GetUserId();
            if (await _userRepository.IsBannedAsync(userId)) return Forbid();

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            if (product.UserId != userId) return Forbid();

            var success = await _productRepository.ReactivateProductAsync(id, userId);
            if (!success) return BadRequest("Не удалось возобновить публикацию.");

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> ParamValues(int productId) => Json(await _productRepository.GetParamDisplayValuesAsync(productId, Request.GetLang()));

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CompleteDeal(int id, int otherUserId)
        {
            var userId = User.GetUserId();
            if (await _userRepository.IsBannedAsync(userId)) return Forbid();
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            if (product.UserId != userId) return Forbid();
            if (product.Status != ProductStatus.Active)
                return BadRequest("Только активные объявления можно завершить");

            var success = await _productRepository.CompleteDealAsync(id, userId, otherUserId);
            if (!success) return BadRequest("Не удалось завершить сделку");

            var soldMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "deal_sold" });
            await _notifications.CreateAsync(userId, otherUserId, id, soldMsg);

            var boughtMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "deal_bought" });
            await _notifications.CreateAsync(otherUserId, userId, id, boughtMsg);

            var subIds = await _productRepository.GetSubscriberIdsExcludingAsync(userId, otherUserId);
            if (subIds.Any())
            {
                var subSoldMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "subscription_sold" });
                foreach (var subId in subIds)
                    await _notifications.CreateAsync(subId, userId, id, subSoldMsg);
            }

            return Ok();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetConversationPartners(int productId)
        {
            var userId = User.GetUserId();
            var partners = await _messageRepository.GetConversationPartnersAsync(productId, userId);
            return Ok(partners.Select(p => new { p.Id, p.UserName, p.AvatarUrl, p.IsCompany }));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(string title, string? description, int? qty,
                                                string? address, int? categoryId,
                                                List<IFormFile>? photos, string? paramsJson,
                                                int? publishDays = null, string? promotionType = null,
                                                bool useHomeAddress = false)
        {
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required");

            var totalBytes = photos?.Sum(f => f.Length) ?? 0;
            if (totalBytes > MediaStorageService.MaxTotalBytes) return BadRequest("Total media size exceeds 50 MB");

            var userId = User.GetUserId();
            var currentUser = await _userRepository.GetByIdAsync(userId);
            if (currentUser == null) return NotFound();
            if (currentUser.Role == "banned") return Forbid();

            decimal? lat = null, lng = null;

            if (useHomeAddress && !string.IsNullOrWhiteSpace(currentUser.HomeAddress))
            {
                address = currentUser.HomeAddress;
                lat = currentUser.HomeLat;
                lng = currentUser.HomeLng;
            }
            else if (!string.IsNullOrWhiteSpace(address))
            {
                var geocoded = await _geocodingService.GeocodeAsync(address);
                if (geocoded.HasValue) (lat, lng) = geocoded.Value;
            }

            int duration = 30;
            if (publishDays.HasValue && new[] { 7, 14, 30, 60, 90 }.Contains(publishDays.Value)) duration = publishDays.Value;
            else if (currentUser.PreferredAdDuration.HasValue) duration = currentUser.PreferredAdDuration.Value;

            var media = photos?.Count > 0 ? await _mediaStorage.SaveUploadedFilesAsync(photos) : [];
            var paramValues = ParseParamsJson(paramsJson);

            var newId = await _productRepository.CreateAsync(new Product
            {
                UserId = userId,
                Name = title,
                Description = description,
                Qty = qty,
                Address = address,
                CategoryId = categoryId,
                AvatarUrl = media.FirstOrDefault()?.FilePath,
                Lat = lat,
                Lng = lng,
            }, paramValues, duration, promotionType ?? "None");

            var points = PromotionPoints(promotionType);
            if (points > 0) await _userRepository.AdjustPromotionPointsAsync(userId, points);

            if (media.Count > 0) await _productRepository.SaveMediaAsync(newId, media);

            await _productRepository.RecalculateModerationScoreAsync(newId);

            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            await _notifications.NotifySubscribersAsync(userId, newId, title, userName);

            return Ok(new { id = newId });
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
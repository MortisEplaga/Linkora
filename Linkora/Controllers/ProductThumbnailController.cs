using Linkora.Services;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers;

public class ProductThumbnailController : Controller
{
    [HttpGet("/product-thumbnail/{name}")]
    public IActionResult Get(string name)
    {
        var original = "/img/products/" + name;
        if (ProductThumbnails.Url(original) == original) return NotFound();
        var thumbnail = ProductThumbnails.ExistingPath(name);
        if (thumbnail == null)
        {
            if (!System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "products", name))) return NotFound();
            return Redirect(original);
        }
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return PhysicalFile(thumbnail, "image/webp", enableRangeProcessing: false);
    }
}

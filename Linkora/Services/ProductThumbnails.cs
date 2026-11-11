using SkiaSharp;

namespace Linkora.Services;

public static class ProductThumbnails
{
    private const string Prefix = "/img/products/";
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
    private static readonly SemaphoreSlim GenerationLimit = new(2);
    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "products");

    public static string Url(string? original)
    {
        if (string.IsNullOrEmpty(original)) return "/img/no-photo.svg";
        if (!original.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return original;
        var name = original[Prefix.Length..];
        return IsImageName(name) ? "/product-thumbnail/" + Uri.EscapeDataString(name) : original;
    }

    private static bool IsImageName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." &&
        name.IndexOfAny(['/', '\\', ':', '?', '#']) < 0 &&
        Extensions.Contains(Path.GetExtension(name));

    private static string ThumbnailPath(string name) => Path.Combine(Folder, "thumbs", name + ".webp");

    public static string? ExistingPath(string name)
    {
        if (!IsImageName(name) || !File.Exists(Path.Combine(Folder, name))) return null;
        var path = ThumbnailPath(name);
        return File.Exists(path) ? path : null;
    }

    public static async Task<string?> GenerateAsync(string name, CancellationToken ct = default)
    {
        if (!IsImageName(name)) return null;
        var source = Path.Combine(Folder, name);
        var target = ThumbnailPath(name);
        if (!File.Exists(source)) return null;
        if (File.Exists(target)) return target;
        await GenerationLimit.WaitAsync(ct);
        try
        {
            if (!File.Exists(source)) return null;
            if (File.Exists(target)) return target;
            ct.ThrowIfCancellationRequested();

            using var codec = SKCodec.Create(source);
            if (codec == null) return null;
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 40_000_000) return null;
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap == null) return null;
            ct.ThrowIfCancellationRequested();

            var scale = Math.Min(1d, 480d / Math.Max(info.Width, info.Height));
            var width = Math.Max(1, (int)Math.Round(info.Width * scale));
            var height = Math.Max(1, (int)Math.Round(info.Height * scale));
            var origin = codec.EncodedOrigin;
            var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            using var thumbnail = new SKBitmap(swapped ? height : width, swapped ? width : height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(thumbnail))
            using (var image = SKImage.FromBitmap(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var transform = Orientation(origin, width, height);
                canvas.SetMatrix(in transform);
                canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                canvas.Flush();
            }
            using var encoded = thumbnail.Encode(SKEncodedImageFormat.Webp, 75);
            if (encoded == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = File.Create(temp)) encoded.SaveTo(stream);
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(source)) return null;
                File.Move(temp, target, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return target;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not generate thumbnail for {name}: {ex.Message}");
            return null;
        }
        finally { GenerationLimit.Release(); }
    }

    private static SKMatrix Orientation(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => SKMatrix.Identity
    };

    public static void Delete(string? original)
    {
        if (string.IsNullOrEmpty(original) || !original.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return;
        var name = original[Prefix.Length..];
        if (!IsImageName(name)) return;
        var thumbnail = ThumbnailPath(name);
        if (File.Exists(thumbnail)) File.Delete(thumbnail);
    }
}

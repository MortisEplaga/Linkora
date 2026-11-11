using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

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

    public static async Task<string?> EnsureAsync(string name, CancellationToken ct = default)
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

            var info = await Image.IdentifyAsync(source, ct);
            if (info == null || (long)info.Width * info.Height > 40_000_000) return null;
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, source, ct);
            image.Mutate(x =>
            {
                x.AutoOrient();
                if (image.Width > 480 || image.Height > 480)
                    x.Resize(new ResizeOptions { Size = new Size(480, 480), Mode = ResizeMode.Max });
            });
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = File.Create(temp))
                    await image.SaveAsync(stream, new WebpEncoder { Quality = 75, SkipMetadata = true }, ct);

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

    public static void Delete(string? original)
    {
        if (string.IsNullOrEmpty(original) || !original.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return;
        var name = original[Prefix.Length..];
        if (!IsImageName(name)) return;
        var thumbnail = ThumbnailPath(name);
        if (File.Exists(thumbnail)) File.Delete(thumbnail);
    }
}

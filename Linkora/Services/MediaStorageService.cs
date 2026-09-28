using Linkora.Models;
using System.Net;
using System.Net.Sockets;

namespace Linkora.Services
{
    public record RemoteImageResult(ProductMedia? Media, string? Error, long Bytes);

    public interface IMediaStorageService
    {
        Task<List<ProductMedia>> SaveUploadedFilesAsync(List<IFormFile> files, CancellationToken ct = default);
        Task<string?> SaveAvatarAsync(IFormFile file, CancellationToken ct = default);
        /// <summary>
        /// Скачивает изображение по прямой ссылке (или копирует уже лежащий на сервере файл /img/...)
        /// и сохраняет его в wwwroot/img/products. Файл проходит те же проверки, что и загружаемый вручную.
        /// </summary>
        Task<RemoteImageResult> DownloadImageAsync(string url, CancellationToken ct = default);
    }
    public sealed class MediaStorageService(IHttpClientFactory httpClientFactory) : IMediaStorageService
    {
        public const long MaxSingleFileBytes = 10L * 1024 * 1024;   // 10 МБ
        public const long MaxTotalBytes = 52_428_800L;         // 50 МБ

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };
        private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".avi"
        };
        private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/pjpeg", "image/png",
            "image/gif", "image/webp", "image/bmp"
        };
        private static readonly HashSet<string> AllowedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4", "video/webm", "video/quicktime",
            "video/x-msvideo", "video/avi", "video/msvideo"
        };

        public async Task<List<ProductMedia>> SaveUploadedFilesAsync(List<IFormFile> files, CancellationToken ct = default)
        {
            var result = new List<ProductMedia>();
            if (files is null || files.Count == 0) return result;

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "products");
            Directory.CreateDirectory(folder);

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                if (file.Length == 0) continue;
                if (file.Length > MaxSingleFileBytes) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(ext) && !AllowedVideoExtensions.Contains(ext)) continue;

                var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
                var allowedMimes = AllowedVideoExtensions.Contains(ext) ? AllowedVideoMimeTypes : AllowedImageMimeTypes;
                if (!allowedMimes.Contains(contentType)) continue;

                var header = new byte[16];
                int totalRead = 0;
                await using (var rs = file.OpenReadStream())
                {
                    while (totalRead < header.Length)
                    {
                        var n = await rs.ReadAsync(header.AsMemory(totalRead), ct);
                        if (n == 0) break;
                        totalRead += n;
                    }
                }
                if (totalRead == 0) continue;
                if (totalRead < header.Length) Array.Resize(ref header, totalRead);

                if (HasExecutableOrScriptSignature(header)) continue;
                if (!ValidateContentSignature(header, ext, out var isVideo)) continue;

                var name = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(folder, name);
                await using (var fs = File.Create(fullPath))
                {
                    await file.CopyToAsync(fs, ct);
                }

                result.Add(new ProductMedia
                {
                    FilePath = $"/img/products/{name}",
                    MediaType = isVideo ? "video" : "image"
                });
            }
            return result;
        }
        public async Task<string?> SaveAvatarAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file is null || file.Length == 0 || file.Length > MaxSingleFileBytes) return null;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext)) return null;

            var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
            if (!AllowedImageMimeTypes.Contains(contentType)) return null;

            var header = new byte[16];
            int totalRead = 0;
            await using (var rs = file.OpenReadStream())
            {
                while (totalRead < header.Length)
                {
                    var n = await rs.ReadAsync(header.AsMemory(totalRead), ct);
                    if (n == 0) break;
                    totalRead += n;
                }
            }
            if (totalRead == 0) return null;
            if (totalRead < header.Length) Array.Resize(ref header, totalRead);

            if (HasExecutableOrScriptSignature(header)) return null;
            if (!ValidateContentSignature(header, ext, out _)) return null;

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "avatars");
            Directory.CreateDirectory(folder);

            var name = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, name);
            await using (var fs = File.Create(fullPath)) await file.CopyToAsync(fs, ct);

            return $"/img/avatars/{name}";
        }

        public async Task<RemoteImageResult> DownloadImageAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return new(null, "empty url", 0);
            url = url.Trim();
            if (url.Length > 2048) return new(null, "url is too long", 0);

            // Ссылка на уже существующий файл на этом сервере (/img/products/...):
            // копируем файл напрямую, без HTTP-запроса к самому себе.
            if (url.StartsWith('/')) return CopyLocalImage(url);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return new(null, "only absolute http(s) urls are supported", 0);

            try
            {
                if (!await IsPublicHostAsync(uri.Host, ct)) return new(null, "host is not allowed", 0);
            }
            catch (OperationCanceledException) { return new(null, "download timed out", 0); }
            catch { return new(null, "host could not be resolved", 0); }

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            byte[] data;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("Linkora-Import/1.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) return new(null, $"download failed (HTTP {(int)response.StatusCode})", 0);

                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (contentType != null && !AllowedImageMimeTypes.Contains(contentType)) return new(null, "unsupported content type", 0);

                await using var src = await response.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    total += read;
                    if (total > MaxSingleFileBytes) return new(null, $"file is larger than {MaxSingleFileBytes / (1024 * 1024)} MB", 0);
                    ms.Write(buffer, 0, read);
                }
                data = ms.ToArray();
            }
            catch (OperationCanceledException) { return new(null, "download timed out", 0); }
            catch { return new(null, "download failed", 0); }

            var saved = SaveValidatedImage(data);
            return new(saved.Media, saved.Error, saved.Media == null ? 0 : data.LongLength);
        }

        private static RemoteImageResult CopyLocalImage(string relativeUrl)
        {
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var root = Path.GetFullPath(Path.Combine(baseDir, "img", "products"));
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return new(null, "local path is not allowed", 0);
            if (!File.Exists(fullPath)) return new(null, "local file not found", 0);

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext)) return new(null, "unsupported image format", 0);

            byte[] data;
            try { data = File.ReadAllBytes(fullPath); }
            catch { return new(null, "local file not readable", 0); }
            if (data.Length > MaxSingleFileBytes) return new(null, $"file is larger than {MaxSingleFileBytes / (1024 * 1024)} MB", 0);

            var saved = SaveValidatedImage(data);
            return new(saved.Media, saved.Error, saved.Media == null ? 0 : data.LongLength);
        }

        private static (ProductMedia? Media, string? Error) SaveValidatedImage(byte[] data)
        {
            if (data.Length == 0) return (null, "empty file");

            var header = data.Length <= 16 ? data : data[..16];
            if (HasExecutableOrScriptSignature(header)) return (null, "unsupported image format");

            var ext = DetectImageExtension(header);
            if (ext == null || !ValidateContentSignature(header, ext, out _)) return (null, "unsupported image format");

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "products");
            Directory.CreateDirectory(folder);

            var name = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, name);
            File.WriteAllBytes(fullPath, data);

            return (new ProductMedia { FilePath = $"/img/products/{name}", MediaType = "image" }, null);
        }

        private static string? DetectImageExtension(byte[] data) =>
            MatchesPrefix(data, [0xFF, 0xD8, 0xFF]) ? ".jpg" :
            MatchesPrefix(data, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? ".png" :
            MatchesPrefix(data, [0x47, 0x49, 0x46, 0x38]) ? ".gif" :
            MatchesPrefix(data, [0x52, 0x49, 0x46, 0x46]) && data.Length >= 12
                && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50 ? ".webp" :
            MatchesPrefix(data, [0x42, 0x4D]) ? ".bmp" :
            null;

        /// <summary>Защита от SSRF: хост должен разрешаться в публичный адрес (не loopback/приватную сеть).</summary>
        private static async Task<bool> IsPublicHostAsync(string host, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;

            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0) return false;
            foreach (var address in addresses)
                if (!IsPublicAddress(address)) return false;
            return true;
        }

        private static bool IsPublicAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return false;
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal) return false;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 0 || b[0] == 10 || b[0] == 127) return false;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;   // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return false;                // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return false;                // 169.254.0.0/16
            }
            return true;
        }

        private static bool MatchesPrefix(byte[] data, byte[] prefix, int offset = 0)
        {
            if (data.Length < offset + prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[offset + i] != prefix[i]) return false;
            return true;
        }
        private static bool HasExecutableOrScriptSignature(byte[] data)
        {
            if (MatchesPrefix(data, [0x4D, 0x5A])) return true; // MZ (PE/DLL/EXE)
            if (MatchesPrefix(data, [0x7F, 0x45, 0x4C, 0x46])) return true; // ELF
            if (MatchesPrefix(data, [0xFE, 0xED, 0xFA])) return true; // Mach-O (BE)
            if (MatchesPrefix(data, [0xCE, 0xFA, 0xED, 0xFE])) return true; // Mach-O (LE)
            if (MatchesPrefix(data, [0xCF, 0xFA, 0xED, 0xFE])) return true; // Mach-O 64 (LE)
            if (MatchesPrefix(data, [0xCA, 0xFE, 0xBA, 0xBE])) return true; // Java class
            if (MatchesPrefix(data, [0x23, 0x21])) return true; // shebang #!
            if (MatchesPrefix(data, [0x3C, 0x3F, 0x70, 0x68, 0x70])) return true; // <?php
            if (MatchesPrefix(data, [0x3C, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74])) return true; // <script
            if (MatchesPrefix(data, [0x3C, 0x25, 0x40])) return true; // <%@ (ASP)
            if (MatchesPrefix(data, [0x3C, 0x68, 0x74, 0x6D, 0x6C])) return true; // <html
            return false;
        }
        private static bool ValidateContentSignature(byte[] data, string ext, out bool isVideo)
        {
            isVideo = false;

            if (AllowedVideoExtensions.Contains(ext))
            {
                isVideo = true;
                return ext switch
                {
                    ".mp4" or ".mov" => data.Length >= 8
                                         && data[4] == 0x66 && data[5] == 0x74
                                         && data[6] == 0x79 && data[7] == 0x70,    // "ftyp" по смещению 4
                    ".webm" => MatchesPrefix(data, [0x1A, 0x45, 0xDF, 0xA3]),
                    ".avi" => MatchesPrefix(data, [0x52, 0x49, 0x46, 0x46])           // "RIFF"
                               && data.Length >= 12
                               && data[8] == 0x41 && data[9] == 0x56
                               && data[10] == 0x49 && data[11] == 0x20,                // "AVI "
                    _ => false
                };
            }

            if (AllowedImageExtensions.Contains(ext))
                return ext switch
                {
                    ".jpg" or ".jpeg" => MatchesPrefix(data, [0xFF, 0xD8, 0xFF]),
                    ".png" => MatchesPrefix(data, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                    ".gif" => MatchesPrefix(data, [0x47, 0x49, 0x46, 0x38, 0x37, 0x61]) // GIF87a
                               || MatchesPrefix(data, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]), // GIF89a
                    ".webp" => MatchesPrefix(data, [0x52, 0x49, 0x46, 0x46])             // "RIFF"
                               && data.Length >= 12
                               && data[8] == 0x57 && data[9] == 0x45
                               && data[10] == 0x42 && data[11] == 0x50,                  // "WEBP"
                    ".bmp" => MatchesPrefix(data, [0x42, 0x4D]),
                    _ => false
                };

            return false;
        }
    }
}
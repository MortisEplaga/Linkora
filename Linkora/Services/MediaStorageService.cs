using Linkora.Models;

namespace Linkora.Services
{
    public interface IMediaStorageService
    {
        Task<List<ProductMedia>> SaveUploadedFilesAsync(List<IFormFile> files, CancellationToken ct = default);
    }

    public sealed class MediaStorageService : IMediaStorageService
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

            var folder = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "img", "products");
            Directory.CreateDirectory(folder);

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                if (file.Length == 0) continue;
                if (file.Length > MaxSingleFileBytes) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(ext) &&
                    !AllowedVideoExtensions.Contains(ext))
                    continue;

                var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
                var allowedMimes = AllowedVideoExtensions.Contains(ext)
                    ? AllowedVideoMimeTypes
                    : AllowedImageMimeTypes;
                if (!allowedMimes.Contains(contentType))
                    continue;

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
                await using (var fs = System.IO.File.Create(fullPath))
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
using ClosedXML.Excel;
using Linkora.Models;
using Linkora.Repositories;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Linkora.Services
{
    public record ImportError(int Row, string Message);
    public record ImportResult(int Created, List<ImportError> Errors, List<int> ProductIds);

    public interface IListingImportService
    {
        Task<(byte[] Data, string ContentType, string FileName)> BuildTemplateAsync(int categoryId, string format, string lang);
        Task<ImportResult> ImportAsync(int userId, string userName, IFormFile file, string lang);
    }

    public class ListingImportService(ICategoryRepository categories, ISelectOptionRepository selectOptions, IProductRepository products, IGeocodingService geocoding, IPromotionRepository promotions, IPointsLedgerRepository points, INotificationService notifications, IUserRepository users) : IListingImportService
    {
        public const int MaxRows = 200;
        public const long MaxFileBytes = 5 * 1024 * 1024;
        private static readonly Regex ParamHeader = new(@"\[p(\d+)\]\s*$", RegexOptions.Compiled);
        private static readonly string[] FixedColumns = ["title", "description", "qty", "price", "address", "publish_days"];
        private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase) { "true", "yes", "1", "да", "jā", "ja" };
        private static readonly HashSet<string> FalseValues = new(StringComparer.OrdinalIgnoreCase) { "false", "no", "0", "нет", "nē", "ne" };

        private async Task<(Category Category, List<Parameter> Params)?> LoadAsync(int categoryId)
        {
            var category = await categories.GetByIdAsync(categoryId);
            if (category == null) return null;
            var breadcrumb = await categories.GetBreadcrumbAsync(categoryId);
            var parameters = await categories.GetParametersAsync(breadcrumb.Select(c => c.Id));
            return (category, parameters.OrderBy(p => p.Param.Id).ToList());
        }

        private static List<string> BuildHeaders(Category category, List<Parameter> parameters)
        {
            var headers = new List<string> { "title", "description", "qty" };
            if (category.HasPrice == true) headers.Add("price");
            headers.Add("address");
            headers.Add("publish_days");
            headers.AddRange(parameters.Select(p => $"{p.Param.Name} [p{p.Param.Id}]"));
            return headers;
        }

        private static IEnumerable<string> OptionTexts(Parameter p) => p.Param.Type switch
        {
            2 or 4 or 8 => p.Options.Select(o => o.Text),
            6 => p.ColorOptions.Select(o => o.Name),
            3 => ["TRUE", "FALSE"],
            _ => []
        };

        public async Task<(byte[] Data, string ContentType, string FileName)> BuildTemplateAsync(int categoryId, string format, string lang)
        {
            var loaded = await LoadAsync(categoryId) ?? throw new ArgumentException("Category not found");
            var (category, parameters) = loaded;
            var headers = BuildHeaders(category, parameters);
            var fileBase = $"template_{categoryId}";

            if (format == "csv")
            {
                var sb = new StringBuilder();
                sb.Append("#category=").Append(categoryId).Append('\n');
                sb.Append(string.Join(';', headers.Select(CsvEscape))).Append('\n');
                return (new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", fileBase + ".csv");
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Listings");
            var lists = wb.Worksheets.Add("Lists");
            var meta = wb.Worksheets.Add("Meta");
            meta.Cell(1, 1).Value = "category";
            meta.Cell(1, 2).Value = categoryId;
            meta.Visibility = XLWorksheetVisibility.Hidden;

            for (int i = 0; i < headers.Count; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int paramOffset = headers.Count - parameters.Count;
            for (int i = 0; i < parameters.Count; i++)
            {
                var texts = OptionTexts(parameters[i]).ToList();
                if (texts.Count == 0) continue;
                int listCol = i + 1;
                for (int r = 0; r < texts.Count; r++) lists.Cell(r + 1, listCol).Value = texts[r];
                lists.Cell(0 + 1, listCol).Address.ToString();
                var range = lists.Range(1, listCol, texts.Count, listCol);

                if (parameters[i].Param.Type is 2 or 6 or 3)
                    ws.Range(2, paramOffset + i + 1, MaxRows + 1, paramOffset + i + 1).CreateDataValidation().List(range);
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileBase + ".xlsx");
        }

        private static string CsvEscape(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                else if (ch == '"') quoted = true;
                else if (ch == ';') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result;
        }

        private static (int CategoryId, List<string> Headers, List<List<string>> Rows) ReadFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                var lines = new List<string>();
                string? l;
                while ((l = reader.ReadLine()) != null) if (!string.IsNullOrWhiteSpace(l)) lines.Add(l);
                if (lines.Count < 2 || !lines[0].StartsWith("#category=")) throw new InvalidDataException("Missing #category line");
                var catId = int.Parse(lines[0]["#category=".Length..].Trim(), CultureInfo.InvariantCulture);
                return (catId, ParseCsvLine(lines[1]), lines.Skip(2).Select(ParseCsvLine).ToList());
            }

            if (ext == ".xlsx")
            {
                using var wb = new XLWorkbook(stream);
                var meta = wb.Worksheet("Meta");
                var ws = wb.Worksheet("Listings");
                var catId = (int)meta.Cell(1, 2).GetDouble();
                int lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
                var headers = Enumerable.Range(1, lastCol).Select(c => ws.Cell(1, c).GetString().Trim()).ToList();
                var rows = new List<List<string>>();
                foreach (var row in ws.RowsUsed().Skip(1))
                    rows.Add(Enumerable.Range(1, lastCol).Select(c => row.Cell(c).GetFormattedString().Trim()).ToList());
                return (catId, headers, rows);
            }

            throw new InvalidDataException("Unsupported file type");
        }

        public async Task<ImportResult> ImportAsync(int userId, string userName, IFormFile file, string lang)
        {
            var errors = new List<ImportError>();
            (int CategoryId, List<string> Headers, List<List<string>> Rows) data;
            try { data = ReadFile(file); }
            catch (Exception ex) { return new ImportResult(0, [new ImportError(0, "Invalid file: " + ex.Message)], []); }

            if (data.Rows.Count == 0) return new ImportResult(0, [new ImportError(0, "No rows")], []);
            if (data.Rows.Count > MaxRows) return new ImportResult(0, [new ImportError(0, $"Max {MaxRows} rows")], []);

            var loaded = await LoadAsync(data.CategoryId);
            if (loaded == null) return new ImportResult(0, [new ImportError(0, "Category not found")], []);
            var (category, parameters) = loaded.Value;

            var user = await users.GetByIdAsync(userId);
            int duration = user?.PreferredAdDuration is int d && AdDurations.IsAccepted(d) ? d : AdDurations.Default;

            var col = new Dictionary<string, int>();
            var paramCol = new Dictionary<int, int>();
            for (int i = 0; i < data.Headers.Count; i++)
            {
                var h = data.Headers[i];
                var m = ParamHeader.Match(h);
                if (m.Success) paramCol[int.Parse(m.Groups[1].Value)] = i;
                else col[h.ToLowerInvariant()] = i;
            }
            if (!col.ContainsKey("title")) return new ImportResult(0, [new ImportError(1, "Column 'title' not found")], []);

            string Cell(List<string> row, string name) => col.TryGetValue(name, out var i) && i < row.Count ? row[i].Trim() : "";

            var prepared = new List<(Product Product, Dictionary<int, string> Params, int Duration)>();
            var newOptions = new Dictionary<(int ParamId, string Text), int>();

            for (int r = 0; r < data.Rows.Count; r++)
            {
                var row = data.Rows[r];
                int line = r + 3; 
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                int before = errors.Count;

                var title = Cell(row, "title");
                if (title.Length == 0) errors.Add(new(line, "title is required"));
                if (title.Length > 500) errors.Add(new(line, "title is too long"));

                int qty = 1;
                var qtyText = Cell(row, "qty");
                if (qtyText.Length > 0 && (!int.TryParse(qtyText, out qty) || qty < 1)) errors.Add(new(line, "qty must be a positive integer"));

                decimal? price = null;
                if (category.HasPrice == true)
                {
                    var priceText = Cell(row, "price").Replace(',', '.');
                    if (priceText.Length > 0)
                    {
                        if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var pv) || pv < 0) errors.Add(new(line, "invalid price"));
                        else price = pv;
                    }
                }

                int rowDuration = duration;
                var daysText = Cell(row, "publish_days");
                if (daysText.Length > 0)
                    if (!int.TryParse(daysText, out var days) || !AdDurations.IsAccepted(days)) errors.Add(new(line, $"publish_days must be one of: {AdDurations.OptionsHint}"));
                    else rowDuration = days;

                var paramValues = new Dictionary<int, string>();
                foreach (var p in parameters)
                {
                    if (!paramCol.TryGetValue(p.Param.Id, out var ci) || ci >= row.Count) continue;
                    var raw = row[ci].Trim();
                    if (raw.Length == 0) continue;

                    switch (p.Param.Type)
                    {
                        case 2:
                            {
                                var o = p.Options.FirstOrDefault(x => x.Text.Equals(raw, StringComparison.OrdinalIgnoreCase));
                                if (o == null) errors.Add(new(line, $"{p.Param.Name}: unknown value '{raw}'"));
                                else paramValues[p.Param.Id] = o.Id.ToString();
                                break;
                            }
                        case 4:
                            {
                                var ids = new List<string>();
                                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                {
                                    var o = p.Options.FirstOrDefault(x => x.Text.Equals(part, StringComparison.OrdinalIgnoreCase));
                                    if (o == null) errors.Add(new(line, $"{p.Param.Name}: unknown value '{part}'"));
                                    else ids.Add(o.Id.ToString());
                                }
                                if (ids.Count > 0) paramValues[p.Param.Id] = string.Join(',', ids);
                                break;
                            }
                        case 8:
                            {
                                var o = p.Options.FirstOrDefault(x => x.Text.Equals(raw, StringComparison.OrdinalIgnoreCase));
                                if (o != null) paramValues[p.Param.Id] = o.Id.ToString();
                                else paramValues[p.Param.Id] = "new:" + raw; 
                                break;
                            }
                        case 6:
                            {
                                var o = p.ColorOptions.FirstOrDefault(x => x.Name.Equals(raw, StringComparison.OrdinalIgnoreCase));
                                if (o == null) errors.Add(new(line, $"{p.Param.Name}: unknown color '{raw}'"));
                                else paramValues[p.Param.Id] = o.Id.ToString();
                                break;
                            }
                        case 3:
                            if (TrueValues.Contains(raw)) paramValues[p.Param.Id] = "true";
                            else if (!FalseValues.Contains(raw)) errors.Add(new(line, $"{p.Param.Name}: expected TRUE/FALSE"));
                            break;
                        case 5:
                            {
                                if (!decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n)) errors.Add(new(line, $"{p.Param.Name}: not a number"));
                                else if ((p.Min.HasValue && n < p.Min) || (p.Max.HasValue && n > p.Max)) errors.Add(new(line, $"{p.Param.Name}: must be between {p.Min} and {p.Max}"));
                                else paramValues[p.Param.Id] = n.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                        default:
                            if (raw.Length > 100) errors.Add(new(line, $"{p.Param.Name}: max 100 characters"));
                            else paramValues[p.Param.Id] = raw;
                            break;
                    }
                }

                if (errors.Count > before) continue;

                var address = Cell(row, "address");
                if (address.Length > 50) { errors.Add(new(line, "address max 50 characters")); continue; }
                if (address.Length == 0 && !string.IsNullOrWhiteSpace(user?.HomeAddress)) address = user!.HomeAddress!;
                decimal? lat = null, lng = null;
                if (address.Length > 0)
                {
                    var g = await geocoding.GeocodeAsync(address);
                    if (g.HasValue) (lat, lng) = g.Value;
                }

                prepared.Add((new Product
                {
                    UserId = userId,
                    Name = title,
                    Description = Cell(row, "description"),
                    Qty = qty,
                    Address = address.Length > 0 ? address : null!,
                    CategoryId = category.Id,
                    Price = price,
                    Lat = lat,
                    Lng = lng,
                }, paramValues, rowDuration));
            }

            if (errors.Count > 0) return new ImportResult(0, errors, []);
            if (prepared.Count == 0) return new ImportResult(0, [new ImportError(0, "No rows")], []);

            var subscription = await promotions.GetActiveAsync(userId);
            var createdIds = new List<int>();

            foreach (var (product, paramValues, rowDuration) in prepared)
            {
                foreach (var key in paramValues.Where(kv => kv.Value.StartsWith("new:")).Select(kv => kv.Key).ToList())
                {
                    var text = paramValues[key][4..];
                    if (!newOptions.TryGetValue((key, text.ToLowerInvariant()), out var optId))
                    {
                        optId = await selectOptions.FindIdAsync(key, text, lang) ?? await selectOptions.CreateAsync(key, text);
                        newOptions[(key, text.ToLowerInvariant())] = optId;
                    }
                    paramValues[key] = optId.ToString();
                }

                product.SubscriptionBoostLevel = subscription?.Tier;
                product.SubscriptionBoostExpiresAt = subscription?.ExpiresAt;

                var id = await products.CreateAsync(product, paramValues, rowDuration);
                await points.RecordListingPostedAsync(userId, id);
                await products.RecalculateModerationScoreAsync(id);
                await notifications.NotifySubscribersAsync(userId, id, product.Name, userName);
                createdIds.Add(id);
            }

            return new ImportResult(createdIds.Count, [], createdIds);
        }
    }
}
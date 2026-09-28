namespace Linkora.Models
{
    /// <summary>
    /// Полностью подготовленное к записи объявление из файла импорта
    /// (значение, параметры, срок публикации и уже скачанные на сервер медиафайлы).
    /// </summary>
    public record ImportListing(Product Product, Dictionary<int, string> ParamValues, int PublishDurationDays, List<ProductMedia> Media);

    /// <summary>
    /// Объявление пользователя, выгружаемое в CSV/XLSX.
    /// ParamValues уже содержит отображаемые значения (тексты опций, TRUE/FALSE и т.д.).
    /// </summary>
    public record ExportListing(int Id, string Title, string? Description, int? Qty, string? Address, decimal? Price,
                                int PublishDurationDays, Dictionary<int, string> ParamValues, List<string> PhotoUrls);
}

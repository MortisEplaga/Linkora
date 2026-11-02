using System.Text.Json;

namespace Linkora.Services
{
    public static class NotificationEmailFormatter
    {
        public static (string Subject, string Body) Format(string message)
        {
            const string subject = "Уведомление Vena";

            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    return (subject, type switch
                    {
                        "deal_sold" => "Ваше объявление было куплено.",
                        "deal_bought" => "Вы совершили покупку.",
                        "review_received" => "Вы получили новый отзыв.",
                        "product_approved" => "Ваше объявление одобрено.",
                        "rejected_reason" => "Ваше объявление было отклонено.",
                        "report_on_product" => "На ваше объявление поступила жалоба.",
                        "user_banned" => "Ваш аккаунт заблокирован.",
                        "user_unbanned" => "Блокировка вашего аккаунта снята.",
                        "favourite_updated" => "Объявление из вашего избранного было изменено.",
                        "favourite_archived_ban" => "Объявление из вашего избранного перенесено в архив.",
                        "subscription_sold" or "subscription_seller_banned" => "Изменение по отслеживаемому продавцу.",
                        "shipment_requested" => "Покупатель запросил доставку вашего объявления через пакомат.",
                        "shipment_accepted" => "Продавец подтвердил запрос на доставку через пакомат.",
                        "shipment_declined" => "Продавец отклонил запрос на доставку через пакомат.",
                        "shipment_arrived" => "Посылка прибыла в пакомат. Заберите её в течение срока хранения.",
                        "shipment_delivered" => "Посылка доставлена получателю.",
                        "shipment_returned" => "Посылка возвращена отправителю.",
                        "shipment_expired" => "Срок сдачи посылки в пакомат истёк, отправление отменено.",
                        "shipment_accept_expired" => "Оплата доставки через пакомат не поступила в срок. Запрос снова открыт.",
                        "shipment_cancelled" => "Отправление отменено перевозчиком.",
                        "listing_expiring_soon" => FormatExpiring(doc),
                        _ => "У вас новое уведомление на Vena.",
                    });
                }
            }
            catch (JsonException) { }

            return (subject, "У вас новое уведомление на Vena.");
        }
        private static string FormatExpiring(JsonDocument doc)
        {
            var name = doc.RootElement.TryGetProperty("productName", out var n) ? n.GetString() : null;
            return string.IsNullOrEmpty(name)
                ? "Срок публикации одного из ваших объявлений истекает через 24 часа."
                : $"Срок публикации объявления «{name}» истекает через 24 часа.";
        }
    }
}
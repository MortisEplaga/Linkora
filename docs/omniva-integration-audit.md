# Аудит интеграции с пакоматами Omniva

**Дата:** 2026-11-02 (3-е обновление: тарифы, XL, сверка кодов с PDF) · **Ветка:** `arena/d43e06b1-linkora` · **База:** `ad74927`
**Метод:** статический ревью всех файлов интеграции, SQL-миграции, платёжного контура, Razor/JS-UI, локализации и юридических текстов; сверка с [OMX API Manual](https://www.omniva.ee/wp-content/uploads/sites/7/2025/08/OMX-API-Manual-for-Customers_nov.pdf), [Omniva Developer site](https://developer.omniva.ee/), публичным фидом `https://www.omniva.ee/locations.json` и приложенным документом **«Possible events for event request»** (`possible-events-for-event-request.pdf`, 2 стр.).
**Ограничение:** .NET SDK в песочнице отсутствует, `dotnet build` не выполнялся — компиляция не проверена (P0.8).

---

## Короткий ответ

Каркас написан аккуратно (идемпотентность, `partnerShipmentId`, курсор событий, фильтрованные уникальные индексы, ретраи, полная локализация на 3 языка, обновлённые §15–17 правил), но **до боевого запуска ещё не готово**.

Закрыто по итогам трёх оборотов:

- ✅ Справочник пакоматов (`locations.json` + джоба синхронизации); работает и без OMX-кредов.
- ✅ Каталог кодов событий — зашит в код; **сверен с PDF** (полный список, а не 14 кодов из первого письма).
- ✅ **Тарифы** — админский редактор `/Admin/ShippingTariffs` + идемпотентный SQL-скрипт с матрицей 3 страны × 4 размера. Цены из договора вносит оператор.
- ✅ Размер XS–L уходит в OMX как габариты, **XL убран из всех доступных списков**.
- ✅ `paidByReceiver` убран из payload.
- ✅ Контакт отправителя валидируется; телефоны нормализуются по стране; `fileData`/`filedata` для этикетки.

Остаются незакрытыми (не зависят от договора): конфигурация и секреты, захардкоженный хост колбэков оплаты, возвраты/reconciliation, админский контур по посылкам, тесты/CI, несколько расхождений с правилами портала.

| Подсистема | Готовность |
|---|---|
| Схема БД / миграция | ✅ хорошо (нужен прогон на стейдже) |
| Тарифы | ✅ админка + заготовки строк; **цены нужно внести** |
| Справочник пакоматов | ✅ парсер + джоба; нужен доступ приложения к `www.omniva.ee` |
| Регистрация посылки в OMX | 🟡 поля сверены с мануалом; габариты XS/S/M/L подтвердить |
| Этикетка PDF | ✅ `fileData` / `filedata` |
| Отмена у Omniva | 🟡 `POST /shipments/cancel` подтверждён, тело ответа уточнить |
| Поллинг событий | ✅ полный каталог из PDF, дефолтная карта статусов |
| Оплата доставки | 🟡 дубли платежей, захардкоженный `vena.lv` |
| Возвраты / отмена после оплаты | ❌ не реализовано, противоречит §17 |
| Админка / поддержка по посылкам | ❌ отсутствует |
| UI покупателя / продавца | 🟡 страны и лимиты не доделаны |
| Конфигурация / секреты | 🟡 шаблон удалён; ключи описаны ниже |
| Тесты / CI / сборка | ❌ отсутствуют |

---

## 1. Что сделано хорошо

- Чистая слоистая архитектура, все зависимости зарегистрированы в `Program.cs`.
- Продуманная идемпотентность: детерминированный `partnerShipmentId`, `TryBeginRegistrationAsync` с проверкой `Attempts`, уникальные индексы, курсор `CarrierSyncState`, дедупликация событий, защита `LastEventAt < @At`.
- Разделение transient / permanent ошибок, 5 джоб, отдельный DI-scope на проход.
- Валидация, rate limiting, анти-CSRF, проверки владельца и роли `admin`.
- Локализация `ship_*` / `notif_shipment_*` / `admin_tariff_*` полная во всех трёх языках.
- Обновлены правила §15–17 и политика §5.
- SQL-скрипты идемпотентные, с историей `HIS_Products` и `AcceptedAt`.

---

## 2. Закрыто в 3-м обороте

| Было | Стало |
|---|---|
| Таблица тарифов пуста, `GetAllAsync`/`SaveAsync` не использовались | `AdminController.ShippingTariffs` + `SaveShippingTariff` (`[Authorize]`, роль `admin`, `[ValidateAntiForgeryToken]`), новый `Views/Admin/ShippingTariffs.cshtml` (матрица LV/LT/EE × XS/S/M/L, цена Omniva, наценка, макс. вес, `IsActive`, расчётная цена), пункт в сайдбаре, 11 ключей i18n. `Database/2026-11-02_omniva_tariffs.sql` создаёт 12 неактивных строк-заготовок (не перезаписывает введённые цены), выключает доставку у старых XL-объявлений и удаляет XL-тариф |
| `ParcelSizes.All` включал XL | `["XS","S","M","L"]` — модель, `_DeliveryFields.cshtml` (через общий список), `Seller.cshtml`, `product-form.js`, `shipping.js`, `OmnivaParcelDimensions`. `RegisterShipmentAsync` отклоняет любой неизвестный размер кодом `PARCEL_SIZE_INVALID` |
| Карта кодов была по короткому списку из 14 кодов, `Returned` недостижим | `OmnivaEventCodes.cs` переписан по PDF: все коды основных событий, события возврата, уведомления, 18 кодов международных отправлений; сохранены 5 legacy-алиасов из первого письма. `TRT_SHIPMENT_RETURN` и `TRT_SHIPMENT_RETURNED_BY_CUSTOMER` → `Returned` (уведомление `shipment_returned` наконец достижимо), `TRT_SHIPMENT_DELIVERED` / `TRT_SHIPMENT_EMI` → `Delivered`, `TRT_SHIPMENT_STORED_IN_PM` / `TRT_SHIPMENT_ARRIVED_TO_FINAL_PO` → `ArrivedAtLocker`. Информационные коды (SMS/напоминания, «movement stopped/restored», смена контакта, часть таможенных) намеренно **не** маппятся: они пишутся в `ShipmentEvents`, но не откатывают статус и не спамят уведомлениями |
| `paidByReceiver = false` в каждом запросе регистрации | Поле убрано. Интеграция остаётся `/business-to-client` (b2c), где платит отправитель; опция «платит получатель» не предлагается |
| `Linkora/appsettings.example.json` | Удалён по требованию (2-й оборот) |

**Важное расхождение, найденное в PDF:** в официальном каталоге нет `TRT_DELIVERED`, `TRT_ARRIVED_AT_PARCEL_MACHINE`, `TRT_ARRIVED_AT_SORTING_CENTRE`, `TRT_SENT_TO_DESTINATION`, `TRT_CUSTOMS_PROCESSING` — вместо них `TRT_SHIPMENT_DELIVERED`, `TRT_SHIPMENT_STORED_IN_PM`, `TRT_SHIPMENT_EDD` и т.п. Коды из первого письма оставлены как legacy-алиасы, чтобы уже поставленные в очередь события не потерялись.

---

## 3. P0 — что ещё блокирует запуск

| # | Что | Где | Последствие | Что сделать |
|---|---|---|---|---|
| 1 | **Конфигурация не внесена** | `Linkora/appsettings.json` в `.gitignore`, файла нет; шаблон удалён по требованию | Конструктор `OmnivaService` не падает без кредов: справочник пакоматов грузится, а OMX-методы возвращают `CONFIG_MISSING` (transient, ретраи). Но регистрация/этикетка/трекинг не работают; `MakeCommerce:TestMode` по умолчанию `true` → тестовый шлюз | Ключи: `Omniva:BaseUrl`, `CustomerCode`, `Username`, `Password`; опционально `IntegrationAgentId`, `LocationsUrl`, `EventStatusMap`, `Shipping:HandoverDays` (3), `MaxRegistrationAttempts` (10), `AcceptPaymentHours` (24). Секреты — в env/secret store |
| 2 | **Нет доступа приложения к `www.omniva.ee`** | `ParcelLockerSyncService` | В закрытом контуре справочник останется пустым | Разрешить исходящий HTTPS (или своё зеркало + `Omniva:LocationsUrl`) |
| 3 | **Цены не внесены** | `/Admin/ShippingTariffs` | Заготовки созданы неактивными с нулевой ценой → `Price`/`Accept` отдают `tariff_missing`, пока оператор не введёт цены и не включит `IsActive` | Внести тарифы из договора и активировать 12 комбинаций |
| 4 | **Callback'ы оплаты ведут на прод** | `PaymentsController.StartTransactionAsync`: `var host = "vena.lv";//Request.Host.Value;` | На стейдже `return_url`/`notification_url` указывают на прод | Брать `PublicBaseUrl`, fallback `Request.Scheme://Request.Host` |
| 5 | **Нет возвратов и отмены после оплаты** | `OnPaymentCompletedAsync` пишет «manual refund is required»; `MarkRegistrationFailedAsync`, `MarkHandoverExpiredAsync` деньги не возвращают | §17.4–17.7 и §15.17 обещают возврат; по факту только лог | Возврат через MakeCommerce + админская операция + регламент |
| 6 | **Нет reconciliation «оплачено, но посылки нет»** | `MarkCompletedAsync` до `OnPaymentCompletedAsync`; повтор колбэка → `already_completed` | При сбое между ними — `Paid` без `Shipments`, ретрай не подхватит | Джоб «Paid без Shipment» + реентерабельный колбэк |
| 7 | **Нет админки/поддержки по посылкам** | `GetEventsAsync`, `GetOpenShipmentsAsync`, `GetRequestByPaymentAsync` не используются | Не видно `Failed`/`ErrorCode`, нет истории событий, нельзя повторить регистрацию | Экран «Доставки» + алерты |
| 8 | **Нет тестов, CI, сборка не проверена** | тестового проекта и `.github` нет; `dotnet` в песочнице отсутствует | Опечатка или расхождение схемы ответа всплывёт только в бою | `dotnet build` в CI, unit-тесты на парсинг/маппинг/переходы, мок Omniva, контрактные тесты в sandbox, E2E |
| 9 | **Расхождения с правилами и UI** | `_ShipmentRequestModal.cshtml`, `shipping.js` | §18.2 — нет активного принятия правил перед покупкой (у промо есть); §15.4.1 — не показываются макс. вес/габариты; §15.7 — не показывается срок кода; страна в UI жёстко `LV` при поддержке LV/LT/EE | Чекбокс согласия, лимиты тарифа и `HandoverDeadline`, селектор страны |
| 10 | **Мёртвый код** | `Shipping/ProductSettings`, `GetOpenShipmentsAsync`, `GetRequestByPaymentAsync`, `GetEventsAsync` | Ложное ощущение «реализовано» | Подключить или удалить |
| 11 | **Многоинстансовость** | 5 `BackgroundService` без распределённой блокировки | Параллельный поллинг, двойные уведомления, повторные отмены | Один инстанс или distributed lock |

---

## 4. P1 — оставшиеся дефекты и решения

| Где | Проблема | Рекомендация |
|---|---|---|
| `OmnivaParcelDimensions` | Габариты XS (5×38×64 см) и S/M/L (9/19/39 × 38 × 64 см) взяты из публичных описаний; в OMX это метры | Подтвердить таблицу по договору; при расхождении править один класс |
| `ParcelRules.MaxWeightKg = 100` | В форме объявления «до 100 кг», у Omniva для пакоматов до 30 кг | Заготовки тарифов уже ставят `MaxWeightKg = 30`, и `Accept` проверяет его; но подсказку в UI стоит привести к 30 кг |
| `OmnivaEventCodes` | `TRT_SHIPMENT_CANCELLED` отображается как `Failed` (в enum нет `Cancelled`), событие шлёт `shipment_cancelled` | При желании завести отдельный статус `Cancelled` (enum + CHECK-констрейнт + i18n + UI) |
| `PaymentsController.InitiateShipping` | Каждый клик создаёт новый платёж и перезаписывает `PaymentId` | Двойное списание; переиспользовать/блокировать активный платёж |
| `ShipmentRepository.GetOrCreateShipmentAsync` | `Return` и `Notification` приходят почти одновременно | Гонка → `SqlException 2627` по `UX_Shipments_Request`; нужен `MERGE WITH (HOLDLOCK)` или обработка 2601/2627 |
| `PaymentsController.ProcessPaymentMessageAsync` | Сумма берётся из БД, подписанный `amount` из JSON игнорируется | Сверять `amount`/`currency` из payload |
| `CancelShipmentAsync` | Шлём лишний `customerCode`, считаем любой HTTP 200 без `resultCode` успехом | Сверить точное тело запроса/ответа в sandbox |
| `ShippingController.Label` | Проверяется только `Status == Failed` | Этикетку можно скачать и для `Returned` |
| `ShippingController.Decline` / `Cancel` | Нет `IsBannedAsync` | Выровнять с остальными экшенами |
| `ShipmentEvents.RawJson` | Полный payload с ПДн без срока хранения | Retention / DPA |
| `ProductRepository.GetPromoCountCachedAsync` | Считает `>= Top` (Vip+Top), а `promoTask` выбирает только `= Vip` | Завышенный `Total` и «дыры» в выдаче, в т.ч. с `shipping_only=1` |

---

## 5. Что ещё нужно от Omniva / из договора

**Уже получено:** публичный фид пакоматов, полный каталог событий (PDF), базовые поля OMX из мануала, короткий список кодов из первого письма.

**Реквизиты (подставить):** `Omniva:BaseUrl` (prod и `https://test-omx.omniva.eu`), `CustomerCode`, `Username`, `Password`, `IntegrationAgentId`, тестовые креды.

**Ещё надо:**
- **цены по страна × размер (XS/S/M/L)**, наценка, правила биллинга неиспользованных/просроченных этикеток и возвращённых посылок, порядок сверки счетов со списаниями продавцов;
- подтверждение таблицы габаритов XS–L, макс. веса (по публичным данным 30 кг), формата `partnerShipmentId` (`VENA{id}`) и его идемпотентности при ретраях;
- срок сдачи (3 дня), окно оплаты (24 ч), срок хранения в пакомате, SLA трекинга;
- подтверждение схемы «регистрируем посылки продавцов под одним `customerCode` платформы» (b2c). В c2c, по документации, транспорт всегда оплачивает получатель — мы используем `/business-to-client` и больше не передаём `paidByReceiver` [6](https://www.omniva.ee/en/business/api/);
- точные тела `/cancel` и `/package-labels`, лимиты/429;
- DPA/GDPR (передача ПДн, срок хранения `RawJson`), ответственность/страхование, тестовые баркоды.

---

## 6. Чек-лист внедрения

1. Накатить `Database/2026-10-30_omniva_shipping.sql` через sqlcmd/SSMS (есть `GO`), затем `Database/2026-11-02_omniva_tariffs.sql`. На стейдже проверить `HIS_Products`/триггер и колонки `Payments`.
2. Внести ключи конфигурации (раздел 3, п. 1) в защищённое хранилище; **не** коммитить секреты.
3. Разрешить исходящий доступ к `www.omniva.ee`, дождаться `ParcelLockerSyncService`, проверить `ParcelLockers` (LV/LT/EE, `IsActive=1`).
4. **В `/Admin/ShippingTariffs` ввести цены Omniva + наценку, макс. вес и включить `IsActive` для всех 12 комбинаций.** Пока тариф не активен — `tariff_missing`.
5. Починить `PublicBaseUrl`, дубли платежей, reconciliation, возвраты, согласие с правилами, лимиты и срок кода, селектор страны.
6. Сверить габариты, тела `cancel` и `package-labels` в sandbox; подтвердить b2c-схему и что `paidByReceiver` действительно не нужен.
7. Проверить 5 джоб, режим одного инстанса, мониторинг и алерты.
8. Сквозной тест: объявление → заявка → accept → оплата → регистрация → этикетка → `InTransit` → `ArrivedAtLocker` → `Delivered`; отдельно: decline, cancel, просрочка оплаты, просрочка сдачи, `TRT_SHIPMENT_CANCELLED`, `TRT_SHIPMENT_RETURN`, `TRT_SHIPMENT_STORED_IN_PM`, `429`, дубль колбэка, отказ Omniva, падение БД после успеха регистрации.
9. `dotnet build` + тесты в CI (в песочнице не выполнялись).

---

## 7. Изменения в репозитории

**1-й оборот:** типобезопасный разбор JSON в `OmnivaService`, защита от «застрявшего» курсора событий, перенос XML-комментария, отчёт.

**2-й оборот:** `GetLockersAsync` + `ParcelLockerSyncService`, `OmnivaEventCodes.cs` (14 кодов), дефолтная карта статусов, `HandoverCodes` в SQL просрочки, габариты `measurement`, валидация отправителя, телефоны по стране, `fileData`/`filedata`, уведомление `shipment_cancelled`, мягкий конструктор `OmnivaService`, удалён `appsettings.example.json`.

**3-й оборот:**
- `Linkora/Controllers/AdminController.cs`, `Linkora/Views/Admin/ShippingTariffs.cshtml`, `Linkora/Views/Admin/Sidebar.cshtml`, `Linkora/wwwroot/js/i18n.js` — админский редактор тарифов + 11 ключей en/lv/ru.
- `Database/2026-11-02_omniva_tariffs.sql` — идемпотентные заготовки 12 тарифов, отключение XL-объявлений, удаление XL-тарифа.
- `Linkora/Models/Shipping.cs` — `ShippingCountries`, `ParcelSizes.All` без XL, `ShippingTariffEditDto`.
- `Linkora/Controllers/ShippingController.cs`, `Linkora/Views/Shipments/Seller.cshtml`, `wwwroot/js/{product-form.js,shipping.js}` — единый список размеров без XL.
- `Linkora/Services/OmnivaService.cs` — убран `paidByReceiver`, убран XL из габаритов, отклонение неизвестного размера.
- `Linkora/Models/OmnivaEventCodes.cs` — полный каталог из PDF, `Returned` и legacy-алиасы.

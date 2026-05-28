// ============================================================
//  Linkora i18n — Interface Translation System
//  Usage: add data-i18n="key" to elements
//         add data-i18n-placeholder="key" to inputs
//         add data-i18n-title="key" to elements needing title attr
// ============================================================
(function () {
    const cookieLang = document.cookie.split(';')
        .find(c => c.trim().startsWith('lang='))
        ?.split('=')[1];
    if (cookieLang) localStorage.setItem('lang', cookieLang);
    if (localStorage.getItem('theme') === 'dark')
        document.documentElement.classList.add('dark-theme');
})();

const TRANSLATIONS = {
    en: {
        'btn_close': 'Close',

        // ── Header ──
        'all_categories': 'All categories',
        'search_btn': 'Search',
        'search_placeholder': 'Search...',
        'post_ad_btn': '+ Post an ad',
        'sign_in_btn': 'Sign in',
        'all_regions_btn': 'All regions',

        // ── User dropdown ──
        'my_ads': 'My ads',
        'messages': 'Messages',
        'favourites': 'Favourites',
        'cart': 'Cart',
        'reviews': 'Reviews',
        'settings': 'Settings',
        'logout': 'Logout',

        // ── Auth modal ──
        'auth_sign_in': 'Sign in',
        'auth_register': 'Register',
        'auth_or': 'or',
        'auth_username_ph': 'Username or email',
        'auth_password_ph': 'Password',
        'auth_confirm_ph': 'Confirm password',
        'auth_forgot': 'Forgot password?',
        'auth_private': 'Private',
        'auth_business': 'Business',
        'auth_type_hint': "You won't be able to change this later.",
        'auth_terms': 'I accept the Terms of Service',
        'auth_newsletter': 'I agree to receive newsletters and promotions',
        'auth_rule_len': 'At least 8 characters',
        'auth_rule_upper': 'Uppercase letter',
        'auth_rule_lower': 'Lowercase letter',
        'auth_rule_digit': 'Digit',
        'auth_submit_login': 'Sign in',
        'auth_submit_register': 'Register',
        'auth_no_account': 'No account?',

        // ── Region modal ──
        'region_title': 'Select region',
        'region_search_ph': 'Enter city',
        'all_regions': 'All regions',

        // ── Settings modal ──
        'settings_title': 'Settings',
        'settings_dark': 'Dark mode',
        'settings_language': 'Language',

        // ── Seller modal ──
        'seller_other_listings': 'Other listings →',
        'seller_member_since': 'Member since',
        'seller_show_contact': 'Show contact',
        'company_badge': 'Company',
        'unknown_seller': 'Unknown',

        // ── Category / Filters ──
        'apply_filters': 'Apply filters',
        'reset_filters': 'Reset',
        'sort_newest': 'Newest first',
        'sort_cheapest': 'Cheapest first',
        'sort_expensive': 'Most expensive',
        'more_btn': 'More',
        'hide_btn': 'Hide',
        'save_search': 'Save search',
        'sorting_label': 'Sorting',

        // ── Product Details ──
        'description': 'Description',
        'parameters': 'Parameters',
        'similar_listings': 'Similar listings',
        'write_btn': 'Write',
        'call_btn': 'Call',
        'report_title': 'Report listing',
        'report_reason_label': 'Reason',
        'report_reason_ph': 'Select a reason',
        'report_comment_label': 'Comment (optional)',
        'report_comment_ph': 'Additional details...',
        'send_report_btn': 'Send report',
        'cancel_btn': 'Cancel',
        'msg_ph': 'Your message...',
        'send_btn': 'Send',
        'price_on_request': 'Price on request',
        'link_copied': 'Link copied!',

        // ── My Ads ──
        'my_ads_title': 'My ads',
        'tab_active': 'Active',
        'tab_moderation': 'Under moderation',
        'tab_rejected': 'Rejected',
        'tab_archived': 'Archived',
        'tab_succeeded': 'Succeeded',
        'edit_btn': 'Edit',
        'delete_btn': 'Delete',
        'no_ads_yet': "You have no active ads yet.",
        'select_user': 'Select user',
        'no_partners': 'No partners found',
        'confirm_delete': 'Delete this ad?',

        // ── Favourites ──
        'favourites_tab': 'Favourites',
        'cart_tab': 'Cart',
        'nothing_here': 'Nothing here yet',
        'items_in_cart': 'Items in cart',
        'total_label': 'Total',
        'without_price_note': 'item(s) without price not included',

        // ── Messages ──
        'messages_title': 'Messages',
        'select_conversation': 'Select a conversation',
        'no_conversations': 'No conversations yet',
        'write_message_ph': 'Write a message...',
        'how_rate': 'How would you rate this user?',
        'optional_comment': 'Optional comment',
        'submit_review_btn': 'Submit review',

        // ── Create / Edit ──
        'post_ad_title': 'Post an ad',
        'edit_ad_title': 'Edit ad',
        'basic_info': 'Basic information',
        'title_label': 'Title',
        'title_ph': 'What are you selling?',
        'desc_label': 'Description',
        'desc_ph': 'Describe the item in detail',
        'price_label': 'Price',
        'qty_label': 'Quantity',
        'city_label': 'City',
        'city_ph': 'Start typing...',
        'street_label': 'Street',
        'house_label': 'House',
        'category_section': 'Category',
        'select_category': 'Select category',
        'params_section': 'Parameters',
        'photos_section': 'Photos & Video',
        'photos_click': 'Click to upload photos or video',
        'photos_hint': 'Images & video · Max 50 MB total',
        'publish_btn': 'Publish',
        'save_btn': 'Save',
        'publishing': 'Publishing...',
        'saving': 'Saving...',
        'change_category': 'Change category',

        // ── Breadcrumb ──
        'home': 'Home',

        // ── Seller page ──
        'all_categories_side': 'All categories',
        'subscribe_btn': 'Subscribe',
        'member_since_label': 'Member since',
        'ratings_label': 'Ratings:',
        'ratings_count': 'ratings',

        // ── Reviews modal ──
        'reviews_title': 'Reviews',
        'about_me_tab': 'About me',
        'from_me_tab': 'From me',
        'loading_text': 'Loading...',
        'no_reviews_yet': 'No reviews yet',
        'failed_load': 'Failed to load',

        // ── Privacy / Footer ──
        'privacy': 'Privacy',

        // ── Misc ──
        'address_label': 'Address',
        'for_business': 'For business',
        'career': 'Career',
        'help': 'Help',
        'catalogs': 'Catalogs',
        'login_register': 'Login and registration',
        'place_an_ad': 'Place an ad',

        'compare_title': 'Compare listings',
        'compare_all_params': 'All parameters',
        'compare_show_diff': 'Show differences',
        'compare_empty': 'Your cart is empty — nothing to compare.',
        'compare_group_main': 'Listings',
        'go_to_cart': 'Go to cart',
        'compare_btn': 'Compare',
        'go_to_compare': 'Go to comparison',

        'notifications': 'Notifications',
        'notif_mark_all_read': 'Mark all as read',
        'notif_tab_all': 'All',
        'notif_tab_unread': 'Unread',
        'notif_no_unread': 'No unread notifications',
        'notif_no_notifications': 'No notifications yet',
        'my_subscriptions': 'My subscriptions',
        'no_subscriptions': 'You are not subscribed to anyone yet',
        'unsubscribe_btn': 'Unsubscribe',
        'subscribed_btn': 'Subscribed',
    },

    lv: {
        'btn_close': 'Aizvērt',

        // ── Header ──
        'all_categories': 'Visas kategorijas',
        'search_btn': 'Meklēt',
        'search_placeholder': 'Meklēt...',
        'post_ad_btn': '+ Publicēt sludinājumu',
        'sign_in_btn': 'Ieiet',
        'all_regions_btn': 'Visi reģioni',

        // ── User dropdown ──
        'my_ads': 'Mani sludinājumi',
        'messages': 'Ziņojumi',
        'favourites': 'Izlase',
        'cart': 'Grozs',
        'reviews': 'Atsauksmes',
        'settings': 'Iestatījumi',
        'logout': 'Iziet',

        // ── Auth modal ──
        'auth_sign_in': 'Ieiet',
        'auth_register': 'Reģistrēties',
        'auth_or': 'vai',
        'auth_username_ph': 'Lietotājvārds vai e-pasts',
        'auth_password_ph': 'Parole',
        'auth_confirm_ph': 'Apstiprināt paroli',
        'auth_forgot': 'Aizmirsāt paroli?',
        'auth_private': 'Privāts',
        'auth_business': 'Uzņēmums',
        'auth_type_hint': "Vēlāk to nevarēs mainīt.",
        'auth_terms': 'Es piekrītu lietošanas noteikumiem',
        'auth_newsletter': 'Piekrītu saņemt jaunumus un piedāvājumus',
        'auth_rule_len': 'Vismaz 8 rakstzīmes',
        'auth_rule_upper': 'Lielais burts',
        'auth_rule_lower': 'Mazais burts',
        'auth_rule_digit': 'Cipars',
        'auth_submit_login': 'Ieiet',
        'auth_submit_register': 'Reģistrēties',
        'auth_no_account': 'Nav konta?',

        // ── Region modal ──
        'region_title': 'Izvēlēties reģionu',
        'region_search_ph': 'Ievadiet pilsētu',
        'all_regions': 'Visi reģioni',

        // ── Settings modal ──
        'settings_title': 'Iestatījumi',
        'settings_dark': 'Tumšais režīms',
        'settings_language': 'Valoda',

        // ── Seller modal ──
        'seller_other_listings': 'Citi sludinājumi →',
        'seller_member_since': 'Dalībnieks kopš',
        'seller_show_contact': 'Rādīt kontaktu',
        'company_badge': 'Uzņēmums',
        'unknown_seller': 'Nezināms',

        // ── Category / Filters ──
        'apply_filters': 'Lietot filtrus',
        'reset_filters': 'Atiestatīt',
        'sort_newest': 'Jaunākie vispirms',
        'sort_cheapest': 'Lētākie vispirms',
        'sort_expensive': 'Dārgākie vispirms',
        'more_btn': 'Vairāk',
        'hide_btn': 'Slēpt',
        'save_search': 'Saglabāt meklēšanu',
        'sorting_label': 'Kārtošana',

        // ── Product Details ──
        'description': 'Apraksts',
        'parameters': 'Parametri',
        'similar_listings': 'Līdzīgi sludinājumi',
        'write_btn': 'Rakstīt',
        'call_btn': 'Zvanīt',
        'report_title': 'Ziņot par sludinājumu',
        'report_reason_label': 'Iemesls',
        'report_reason_ph': 'Izvēlieties iemeslu',
        'report_comment_label': 'Komentārs (neobligāti)',
        'report_comment_ph': 'Papildu informācija...',
        'send_report_btn': 'Nosūtīt sūdzību',
        'cancel_btn': 'Atcelt',
        'msg_ph': 'Jūsu ziņojums...',
        'send_btn': 'Nosūtīt',
        'price_on_request': 'Cena pēc pieprasījuma',
        'link_copied': 'Saite nokopēta!',

        // ── My Ads ──
        'my_ads_title': 'Mani sludinājumi',
        'tab_active': 'Aktīvie',
        'tab_moderation': 'Moderācijā',
        'tab_rejected': 'Noraidīti',
        'tab_archived': 'Arhīvā',
        'tab_succeeded': 'Pabeigti',
        'edit_btn': 'Rediģēt',
        'delete_btn': 'Dzēst',
        'no_ads_yet': "Jums vēl nav aktīvu sludinājumu.",
        'select_user': 'Izvēlieties lietotāju',
        'no_partners': 'Partneru nav atrasts',
        'confirm_delete': 'Dzēst šo sludinājumu?',

        // ── Favourites ──
        'favourites_tab': 'Izlase',
        'cart_tab': 'Grozs',
        'nothing_here': 'Šeit vēl nav nekas',
        'items_in_cart': 'Preces grozā',
        'total_label': 'Kopā',
        'without_price_note': 'prece(s) bez cenas nav iekļautas',

        // ── Messages ──
        'messages_title': 'Ziņojumi',
        'select_conversation': 'Izvēlieties sarunu',
        'no_conversations': 'Sarunu vēl nav',
        'write_message_ph': 'Rakstiet ziņojumu...',
        'how_rate': 'Kā jūs vērtētu šo lietotāju?',
        'optional_comment': 'Neobligāts komentārs',
        'submit_review_btn': 'Iesniegt atsauksmi',

        // ── Create / Edit ──
        'post_ad_title': 'Publicēt sludinājumu',
        'edit_ad_title': 'Rediģēt sludinājumu',
        'basic_info': 'Pamatinformācija',
        'title_label': 'Nosaukums',
        'title_ph': 'Ko jūs pārdodat?',
        'desc_label': 'Apraksts',
        'desc_ph': 'Aprakstiet preci sīkāk',
        'price_label': 'Cena',
        'qty_label': 'Daudzums',
        'city_label': 'Pilsēta',
        'city_ph': 'Sāciet rakstīt...',
        'street_label': 'Iela',
        'house_label': 'Mājas nr.',
        'category_section': 'Kategorija',
        'select_category': 'Izvēlēties kategoriju',
        'params_section': 'Parametri',
        'photos_section': 'Fotoattēli un video',
        'photos_click': 'Noklikšķiniet, lai augšupielādētu',
        'photos_hint': 'Attēli un video · Maks. 50 MB',
        'publish_btn': 'Publicēt',
        'save_btn': 'Saglabāt',
        'publishing': 'Publicē...',
        'saving': 'Saglabā...',
        'change_category': 'Mainīt kategoriju',

        // ── Breadcrumb ──
        'home': 'Sākumlapa',

        // ── Seller page ──
        'all_categories_side': 'Visas kategorijas',
        'subscribe_btn': 'Sekot',
        'member_since_label': 'Dalībnieks kopš',
        'ratings_label': 'Vērtējumi:',
        'ratings_count': 'vērtējumi',

        // ── Reviews modal ──
        'reviews_title': 'Atsauksmes',
        'about_me_tab': 'Par mani',
        'from_me_tab': 'No manis',
        'loading_text': 'Ielādē...',
        'no_reviews_yet': 'Atsauksmju vēl nav',
        'failed_load': 'Neizdevās ielādēt',

        // ── Privacy / Footer ──
        'privacy': 'Privātuma politika',

        // ── Misc ──
        'address_label': 'Adrese',
        'for_business': 'Biznesam',
        'career': 'Karjera',
        'help': 'Palīdzība',
        'catalogs': 'Katalogi',
        'login_register': 'Ieeja un reģistrācija',
        'place_an_ad': 'Publicēt sludinājumu',

        'compare_title': 'Sludinājumu salīdzinājums',
        'compare_all_params': 'Visi parametri',
        'compare_show_diff': 'Rādīt atšķirības',
        'compare_empty': 'Grozs ir tukšs — nav ko salīdzināt.',
        'compare_group_main': 'Sludinājumi',
        'go_to_cart': 'Uz grozu',
        'compare_btn': 'Salīdzināt',
        'go_to_compare': 'Uz salīdzinājumu',

        'notifications': 'Paziņojumi',
        'notif_mark_all_read': 'Atzīmēt visus kā lasītus',
        'notif_tab_all': 'Visi',
        'notif_tab_unread': 'Nelasīti',
        'notif_no_unread': 'Nav nelasītu paziņojumu',
        'notif_no_notifications': 'Paziņojumu vēl nav',
        'my_subscriptions': 'Manas abonēšanas',
        'no_subscriptions': 'Jūs vēl neesat abonējis nevienu',
        'unsubscribe_btn': 'Atrakstīties',
        'subscribed_btn': 'Abonēts',
    },

    ru: {
        'btn_close': 'Закрыть',

        // ── Header ──
        'all_categories': 'Все категории',
        'search_btn': 'Поиск',
        'search_placeholder': 'Поиск...',
        'post_ad_btn': '+ Подать объявление',
        'sign_in_btn': 'Войти',
        'all_regions_btn': 'Все регионы',

        // ── User dropdown ──
        'my_ads': 'Мои объявления',
        'messages': 'Сообщения',
        'favourites': 'Избранное',
        'cart': 'Корзина',
        'reviews': 'Отзывы',
        'settings': 'Настройки',
        'logout': 'Выйти',

        // ── Auth modal ──
        'auth_sign_in': 'Войти',
        'auth_register': 'Регистрация',
        'auth_or': 'или',
        'auth_username_ph': 'Имя пользователя или email',
        'auth_password_ph': 'Пароль',
        'auth_confirm_ph': 'Подтвердите пароль',
        'auth_forgot': 'Забыли пароль?',
        'auth_private': 'Частное лицо',
        'auth_business': 'Бизнес',
        'auth_type_hint': 'Позже это изменить будет нельзя.',
        'auth_terms': 'Я принимаю условия использования',
        'auth_newsletter': 'Я согласен получать новости и акции',
        'auth_rule_len': 'Минимум 8 символов',
        'auth_rule_upper': 'Заглавная буква',
        'auth_rule_lower': 'Строчная буква',
        'auth_rule_digit': 'Цифра',
        'auth_submit_login': 'Войти',
        'auth_submit_register': 'Зарегистрироваться',
        'auth_no_account': 'Нет аккаунта?',

        // ── Region modal ──
        'region_title': 'Выберите регион',
        'region_search_ph': 'Введите город',
        'all_regions': 'Все регионы',

        // ── Settings modal ──
        'settings_title': 'Настройки',
        'settings_dark': 'Тёмный режим',
        'settings_language': 'Язык',

        // ── Seller modal ──
        'seller_other_listings': 'Другие объявления →',
        'seller_member_since': 'Участник с',
        'seller_show_contact': 'Показать контакты',
        'company_badge': 'Компания',
        'unknown_seller': 'Неизвестный',

        // ── Category / Filters ──
        'apply_filters': 'Применить фильтры',
        'reset_filters': 'Сбросить',
        'sort_newest': 'Сначала новые',
        'sort_cheapest': 'Сначала дешёвые',
        'sort_expensive': 'Сначала дорогие',
        'more_btn': 'Ещё',
        'hide_btn': 'Скрыть',
        'save_search': 'Сохранить поиск',
        'sorting_label': 'Сортировка',

        // ── Product Details ──
        'description': 'Описание',
        'parameters': 'Характеристики',
        'similar_listings': 'Похожие объявления',
        'write_btn': 'Написать',
        'call_btn': 'Позвонить',
        'report_title': 'Пожаловаться на объявление',
        'report_reason_label': 'Причина',
        'report_reason_ph': 'Выберите причину',
        'report_comment_label': 'Комментарий (необязательно)',
        'report_comment_ph': 'Дополнительные сведения...',
        'send_report_btn': 'Отправить жалобу',
        'cancel_btn': 'Отмена',
        'msg_ph': 'Ваше сообщение...',
        'send_btn': 'Отправить',
        'price_on_request': 'Цена по запросу',
        'link_copied': 'Ссылка скопирована!',

        // ── My Ads ──
        'my_ads_title': 'Мои объявления',
        'tab_active': 'Активные',
        'tab_moderation': 'На модерации',
        'tab_rejected': 'Отклонённые',
        'tab_archived': 'Архив',
        'tab_succeeded': 'Завершённые',
        'edit_btn': 'Редактировать',
        'delete_btn': 'Удалить',
        'no_ads_yet': 'У вас пока нет активных объявлений.',
        'select_user': 'Выберите пользователя',
        'no_partners': 'Партнёры не найдены',
        'confirm_delete': 'Удалить это объявление?',

        // ── Favourites ──
        'favourites_tab': 'Избранное',
        'cart_tab': 'Корзина',
        'nothing_here': 'Здесь пока ничего нет',
        'items_in_cart': 'Товаров в корзине',
        'total_label': 'Итого',
        'without_price_note': 'товар(ы) без цены не учтены',

        // ── Messages ──
        'messages_title': 'Сообщения',
        'select_conversation': 'Выберите диалог',
        'no_conversations': 'Диалогов пока нет',
        'write_message_ph': 'Напишите сообщение...',
        'how_rate': 'Как бы вы оценили этого пользователя?',
        'optional_comment': 'Необязательный комментарий',
        'submit_review_btn': 'Отправить отзыв',

        // ── Create / Edit ──
        'post_ad_title': 'Подать объявление',
        'edit_ad_title': 'Редактировать объявление',
        'basic_info': 'Основная информация',
        'title_label': 'Заголовок',
        'title_ph': 'Что вы продаёте?',
        'desc_label': 'Описание',
        'desc_ph': 'Подробно опишите товар',
        'price_label': 'Цена',
        'qty_label': 'Количество',
        'city_label': 'Город',
        'city_ph': 'Начните вводить...',
        'street_label': 'Улица',
        'house_label': 'Дом',
        'category_section': 'Категория',
        'select_category': 'Выберите категорию',
        'params_section': 'Параметры',
        'photos_section': 'Фото и видео',
        'photos_click': 'Нажмите, чтобы загрузить фото или видео',
        'photos_hint': 'Изображения и видео · Не более 50 МБ',
        'publish_btn': 'Опубликовать',
        'save_btn': 'Сохранить',
        'publishing': 'Публикация...',
        'saving': 'Сохранение...',
        'change_category': 'Изменить категорию',

        // ── Breadcrumb ──
        'home': 'Главная',

        // ── Seller page ──
        'all_categories_side': 'Все категории',
        'subscribe_btn': 'Подписаться',
        'member_since_label': 'Участник с',
        'ratings_label': 'Рейтинг:',
        'ratings_count': 'оценок',

        // ── Reviews modal ──
        'reviews_title': 'Отзывы',
        'about_me_tab': 'Обо мне',
        'from_me_tab': 'От меня',
        'loading_text': 'Загрузка...',
        'no_reviews_yet': 'Пока нет отзывов',
        'failed_load': 'Не удалось загрузить',

        // ── Privacy / Footer ──
        'privacy': 'Конфиденциальность',

        // ── Misc ──
        'address_label': 'Адрес',
        'for_business': 'Для бизнеса',
        'career': 'Карьера',
        'help': 'Помощь',
        'catalogs': 'Каталоги',
        'login_register': 'Вход и регистрация',
        'place_an_ad': 'Подать объявление',

        'compare_title': 'Сравнение объявлений',
        'compare_all_params': 'Все параметры',
        'compare_show_diff': 'Показать различия',
        'compare_empty': 'Корзина пуста — нечего сравнивать.',
        'compare_group_main': 'Объявления',
        'go_to_cart': 'В корзину',
        'compare_btn': 'Сравнить',
        'go_to_compare': 'Перейти к сравнению',

        'notifications': 'Уведомления',
        'notif_mark_all_read': 'Отметить все как прочитанные',
        'notif_tab_all': 'Все',
        'notif_tab_unread': 'Непрочитанные',
        'notif_no_unread': 'Нет непрочитанных уведомлений',
        'notif_no_notifications': 'Уведомлений пока нет',
        'my_subscriptions': 'Мои подписки',
        'no_subscriptions': 'Вы ни на кого не подписаны',
        'unsubscribe_btn': 'Отписаться',
        'subscribed_btn': 'Подписан',
    }
};

// ── Core translation engine ──────────────────────────────────

function t(key) {
    const lang = localStorage.getItem('lang') || 'en';
    const dict = TRANSLATIONS[lang] || TRANSLATIONS['en'];
    return dict[key] || TRANSLATIONS['en'][key] || key;
}

function applyTranslations() {
    const lang = localStorage.getItem('lang') || 'en';
    const dict = TRANSLATIONS[lang] || TRANSLATIONS['en'];

    // textContent replacements
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.dataset.i18n;
        if (dict[key] !== undefined) el.textContent = dict[key];
    });

    // placeholder replacements
    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.dataset.i18nPlaceholder;
        if (dict[key] !== undefined) el.placeholder = dict[key];
    });

    // title attribute replacements
    document.querySelectorAll('[data-i18n-title]').forEach(el => {
        const key = el.dataset.i18nTitle;
        if (dict[key] !== undefined) el.title = dict[key];
    });

    // value replacements (buttons/inputs)
    document.querySelectorAll('[data-i18n-value]').forEach(el => {
        const key = el.dataset.i18nValue;
        if (dict[key] !== undefined) el.value = dict[key];
    });
}

// Apply on DOM ready
document.addEventListener('DOMContentLoaded', applyTranslations);
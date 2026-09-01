function getCurrentLanguage() {
    return localStorage.getItem('lang') || 'en';
}

/* ---------- Generic info-modal language switching ---------- */

const MODAL_TITLES = {
    faqModal: { en: 'FAQ', lv: 'Bieži uzdotie jautājumi', ru: 'FAQ' },
    rulesModal: { en: 'Terms of use', lv: 'Portāla lietošanas noteikumi', ru: 'Правила пользования порталом' },
    policyModal: { en: 'Privacy Policy', lv: 'Privātuma politika', ru: 'Политика конфиденциальности' },
    contactsModal: { en: 'Contacts', lv: 'Kontakti', ru: 'Контакты' },
    supportModal: { en: 'Technical Support', lv: 'Tehniskā palīdzība', ru: 'Техническая поддержка' },
};

const SUPPORT_FORM_TEXT = {
    en: { name: 'Your name', email: 'Email', phone: 'Phone', message: 'Describe the error', send: 'Send' },
    lv: { name: 'Jūsu vārds', email: 'E-pasts', phone: 'Tālrunis', message: 'Aprakstiet kļūdu', send: 'Sūtīt' },
    ru: { name: 'Ваше имя', email: 'Email', phone: 'Телефон', message: 'Опишите ошибку', send: 'Отправить' },
};

function switchInfoLang(lang, btn) {
    const parent = btn.closest('.info-modal');
    parent.querySelectorAll('.info-lang-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    parent.querySelectorAll('.info-lang-content').forEach(el => {
        el.style.display = el.dataset.lang === lang ? '' : 'none';
    });

    var titles = MODAL_TITLES[parent.id];
    if (titles && titles[lang]) {
        const titleEl = parent.querySelector('.info-modal-title');
        if (titleEl) {
            titleEl.removeAttribute('data-i18n');
            titleEl.textContent = titles[lang];
        }
    }
}

function openInfoModalWithLang(overlayId, modalId) {
    document.getElementById(overlayId)?.classList.add('modal-open');
    document.getElementById(modalId)?.classList.add('modal-open');
    document.body.style.overflow = 'hidden';

    const lang = getCurrentLanguage();
    const modal = document.getElementById(modalId);
    if (!modal) return;
    const buttons = modal.querySelectorAll('.info-lang-btn');
    for (const btn of buttons) {
        const btnLang = btn.textContent.trim().toUpperCase();
        if ((lang === 'ru' && btnLang === 'RU') ||
            (lang === 'lv' && btnLang === 'LV') ||
            (lang === 'en' && btnLang === 'EN')) {
            switchInfoLang(lang, btn);
            break;
        }
    }
}

function closeInfoModal(overlayId, modalId) {
    document.getElementById(overlayId)?.classList.remove('modal-open');
    document.getElementById(modalId)?.classList.remove('modal-open');
    document.body.style.overflow = '';
}

/* ---------- FAQ ---------- */

function openFaqModal() { openInfoModalWithLang('faqOverlay', 'faqModal'); }
function closeFaqModal() { closeInfoModal('faqOverlay', 'faqModal'); }

function toggleFaq(el) {
    el.classList.toggle('open');
    const answer = el.nextElementSibling;
    answer.classList.toggle('open');
}

/* ---------- Rules ---------- */

function openRulesModal() { openInfoModalWithLang('rulesOverlay', 'rulesModal'); }
function closeRulesModal() { closeInfoModal('rulesOverlay', 'rulesModal'); }

/* ---------- Policy ---------- */

function openPolicyModal() { openInfoModalWithLang('policyOverlay', 'policyModal'); }
function closePolicyModal() { closeInfoModal('policyOverlay', 'policyModal'); }

/* ---------- Contacts ---------- */

function openContactsModal() { openInfoModalWithLang('contactsOverlay', 'contactsModal'); }
function closeContactsModal() { closeInfoModal('contactsOverlay', 'contactsModal'); }

/* ---------- Support ---------- */

let currentSupportLang = 'en';

function openSupportModal() {
    const afToken = document.getElementById('afToken');
    const supportAfToken = document.getElementById('supportAfToken');
    if (afToken && supportAfToken) supportAfToken.value = afToken.value;

    document.getElementById('supportError').style.display = 'none';
    document.getElementById('supportSuccess').style.display = 'none';
    document.getElementById('supportForm').reset();

    document.getElementById('supportOverlay').classList.add('modal-open');
    document.getElementById('supportModal').classList.add('modal-open');
    document.body.style.overflow = 'hidden';

    const lang = getCurrentLanguage();
    const modal = document.getElementById('supportModal');
    const buttons = modal.querySelectorAll('.info-lang-btn');
    let activeBtn = null;
    for (const btn of buttons) {
        const btnLang = btn.textContent.trim().toUpperCase();
        if ((lang === 'ru' && btnLang === 'RU') ||
            (lang === 'lv' && btnLang === 'LV') ||
            (lang === 'en' && btnLang === 'EN')) {
            activeBtn = btn;
            break;
        }
    }
    switchSupportLang(lang, activeBtn || buttons[buttons.length - 1]);
}

function closeSupportModal() { closeInfoModal('supportOverlay', 'supportModal'); }

function switchSupportLang(lang, btn) {
    currentSupportLang = lang;
    switchInfoLang(lang, btn);

    const dict = SUPPORT_FORM_TEXT[lang] || SUPPORT_FORM_TEXT.en;
    const modal = document.getElementById('supportModal');
    const setPh = (id, val) => { const el = document.getElementById(id); if (el) el.placeholder = val; };
    setPh('supportName', dict.name);
    setPh('supportEmail', dict.email);
    setPh('supportPhone', dict.phone);
    setPh('supportMessage', dict.message);

    if (modal) {
        const submitBtn = modal.querySelector('.auth-submit');
        if (submitBtn) submitBtn.textContent = dict.send;
    }
}

async function submitSupportForm(event) {
    event.preventDefault();
    const errorEl = document.getElementById('supportError');
    const successEl = document.getElementById('supportSuccess');
    errorEl.style.display = 'none';
    successEl.style.display = 'none';

    const payload = {
        name: document.getElementById('supportName').value.trim(),
        email: document.getElementById('supportEmail').value.trim(),
        phone: document.getElementById('supportPhone').value.trim(),
        message: document.getElementById('supportMessage').value.trim()
    };

    if (!payload.name || !payload.email || !payload.message) {
        errorEl.textContent = translate('support_form_required');
        errorEl.style.display = 'block';
        return;
    }

    try {
        const res = await fetch('/api/support/contact', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.getElementById('supportAfToken').value
            },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || 'Request failed');
        }

        successEl.textContent = translate('support_form_success');
        successEl.style.display = 'block';
        document.getElementById('supportForm').reset();
    } catch (err) {
        errorEl.textContent = err.message || translate('support_form_required');
        errorEl.style.display = 'block';
    }
}

/* ---------- Generic notification (OK) modal ---------- */

function showModal(messageKeyOrText) {
    const overlay = document.getElementById('notificationOverlay');
    const modal = document.getElementById('notificationModal');
    const messageEl = document.getElementById('notifMessage');
    const titleEl = document.getElementById('notifTitle');

    if (!overlay || !modal || !messageEl) return;

    const text = (typeof translate === 'function') ? (translate(messageKeyOrText) || messageKeyOrText) : messageKeyOrText;
    messageEl.textContent = text;

    if (titleEl) titleEl.style.display = 'none';

    overlay.style.display = 'block';
    modal.style.display = 'block';
    document.body.style.overflow = 'hidden';
}

function closeNotificationModal(event) {
    if (event && event.target && event.target !== event.currentTarget) return;

    const overlay = document.getElementById('notificationOverlay');
    const modal = document.getElementById('notificationModal');

    if (overlay) overlay.style.display = 'none';
    if (modal) modal.style.display = 'none';
    document.body.style.overflow = '';
}

document.addEventListener('DOMContentLoaded', function () {
    const okBtn = document.getElementById('notifOkBtn');
    if (okBtn) {
        okBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            closeNotificationModal();
        });
    }
});

/* ---------- Auth modal ---------- */

function openAuthModal(tab) {
    switchTab(tab || 'login');
    document.getElementById('authOverlay').classList.add('modal-open');
    document.getElementById('authModal').classList.add('modal-open');
    document.body.style.overflow = 'hidden';
    applyTranslations();
}

function closeAuthModal() {
    document.getElementById('authOverlay').classList.remove('modal-open');
    document.getElementById('authModal').classList.remove('modal-open');
    document.body.style.overflow = '';
}

function switchTab(tab) {
    document.getElementById('tabLogin').style.display = tab === 'login' ? 'block' : 'none';
    document.getElementById('tabRegister').style.display = tab === 'register' ? 'block' : 'none';
    document.querySelectorAll('.auth-tab').forEach((el, i) => {
        el.classList.toggle('auth-tab-active', (tab === 'login' && i === 0) || (tab === 'register' && i === 1));
    });
}

function submitLogin() {
    document.getElementById('fLoginUsername').value = document.getElementById('loginUsername').value;
    document.getElementById('fLoginPassword').value = document.getElementById('modalLoginPassword').value;
    document.getElementById('fLoginReturn').value = window.location.pathname;
    document.getElementById('loginForm').submit();
}

function togglePass(id, btn) {
    const input = document.getElementById(id);
    input.type = input.type === 'password' ? 'text' : 'password';
    btn.style.opacity = input.type === 'text' ? '0.4' : '1';
}

function checkStrength(input, prefix = 'rule') {
    const v = input.value;
    const set = (id, ok) => {
        const el = document.getElementById(id);
        if (el) el.classList.toggle('auth-rule-ok', ok);
    };
    set(`${prefix}-len`, v.length >= 8);
    set(`${prefix}-upper`, /[A-Z]/.test(v));
    set(`${prefix}-lower`, /[a-z]/.test(v));
    set(`${prefix}-digit`, /[0-9]/.test(v));
}
function updateRegisterButton() {
    const getEl = id => document.getElementById(id);
    const usernameEl = getEl('regUsername');
    const emailEl = getEl('regEmail');
    const phoneEl = getEl('regPhone');
    const passwordEl = getEl('modalRegPassword');
    const confirmEl = getEl('modalRegConfirm');
    const checkboxEl = getEl('modalChkTerms');
    const button = getEl('registerBtn');
    const emailError = getEl('emailError');

    if (!button) return;

    if (!usernameEl || !emailEl || !phoneEl || !passwordEl || !confirmEl || !checkboxEl) {
        button.disabled = true;
        return;
    }

    const getVal = el => (el?.value ?? '').toString().trim();

    const emailRegex = /^[^\s\x40]+\x40[^\s\x40]+\.[^\s\x40]+$/;
    const emailVal = getVal(emailEl);
    const isEmailValid = emailVal !== '' && emailRegex.test(emailVal);

    if (emailError) {
        emailError.style.display = (emailVal !== '' && !isEmailValid) ? 'block' : 'none';
    }

    const allFilled =
        getVal(usernameEl) !== '' &&
        emailVal !== '' &&
        getVal(phoneEl) !== '' &&
        isEmailValid &&
        getVal(passwordEl) !== '' &&
        getVal(confirmEl) !== '' &&
        checkboxEl.checked;

    button.disabled = !allFilled;
}

function submitRegister() {
    const termsEl = document.getElementById('modalChkTerms');
    if (!termsEl || !termsEl.checked) {
        alert(translate('auth_terms'));
        return;
    }

    const phoneValue = document.getElementById('regPhone')?.value.trim() ?? '';
    if (!phoneValue) {
        alert(translate('phone_required'));
        return;
    }

    const usernameEl = document.getElementById('regUsername');
    const emailEl = document.getElementById('regEmail');
    const passwordEl = document.getElementById('modalRegPassword');
    const confirmEl = document.getElementById('modalRegConfirm');

    if (!usernameEl || !emailEl || !passwordEl || !confirmEl) {
        alert(translate('registration_incomplete'));
        return;
    }

    document.getElementById('fRegUsername').value = usernameEl.value;
    document.getElementById('fRegEmail').value = emailEl.value;
    document.getElementById('fRegPassword').value = passwordEl.value;
    document.getElementById('fRegConfirm').value = confirmEl.value;
    document.getElementById('fRegPhone').value = phoneValue;
    document.getElementById('registerForm').submit();
}

document.addEventListener('DOMContentLoaded', function () {
    updateRegisterButton();

    ['regUsername', 'regEmail', 'regPhone', 'modalRegPassword', 'modalRegConfirm'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', updateRegisterButton);
    });

    document.getElementById('modalChkTerms')?.addEventListener('change', updateRegisterButton); });

/* ---------- User dropdown ---------- */

function toggleUserMenu() {
    document.getElementById('userDropdown')?.classList.toggle('open');
}

document.addEventListener('click', e => {
    if (!e.target.closest('.user-menu-wrap')) {
        document.getElementById('userDropdown')?.classList.remove('open');
    }
});

/* ---------- Region modal ---------- */

let regionCities = [];
let selectedRegion = null;

async function openRegionModal() {
    if (!regionCities.length) {
        const res = await fetch('/Product/Cities');
        regionCities = await res.json();
    }
    renderRegions(regionCities);
    document.getElementById('regionOverlay').classList.add('modal-open');
    document.getElementById('regionModal').classList.add('modal-open');
    document.body.style.overflow = 'hidden';
    applyTranslations();
}

function closeRegionModal() {
    document.getElementById('regionOverlay').classList.remove('modal-open');
    document.getElementById('regionModal').classList.remove('modal-open');
    document.body.style.overflow = '';
}

function renderRegions(cities) {
    const container = document.getElementById('regionPills');
    container.innerHTML = '';
    const all = document.createElement('button');
    all.className = 'region-pill' + (!selectedRegion ? ' region-pill-active' : '');
    all.textContent = translate('all_regions');
    all.onclick = () => selectRegion(null, translate('all_regions'));
    container.appendChild(all);
    cities.forEach(city => {
        const btn = document.createElement('button');
        btn.className = 'region-pill' + (selectedRegion?.id === city.id ? ' region-pill-active' : '');
        btn.textContent = city.name;
        btn.onclick = () => selectRegion(city, city.name);
        container.appendChild(btn);
    });
}

function filterRegions(val) {
    const filtered = val
        ? regionCities.filter(c => c.name.toLowerCase().includes(val.toLowerCase()))
        : regionCities;
    renderRegions(filtered);
}

function selectRegion(city, label) {
    selectedRegion = city;
    const regionsBtn = document.querySelector('.regions-btn');
    if (regionsBtn) {
        regionsBtn.innerHTML = `<img src="/img/marker_for_adv.svg" alt="" class="regions-icon" />${label}`;
    }
    const regionBtnLabel = document.getElementById('regionBtnLabel');
    if (regionBtnLabel) regionBtnLabel.textContent = label;
    closeRegionModal();
    const path = window.location.pathname;
    if (path.startsWith('/Category/Index') || path.startsWith('/Category')) {
        const url = new URL(window.location.href);
        if (city) url.searchParams.set('city', city.name);
        else url.searchParams.delete('city');
        window.location.href = url.toString();
    }
}

/* ---------- Favourites / cart toggle ---------- */

async function toggleFav(btn, productId, can) {
    const res = await fetch(`/Favourite/Toggle?productId=${productId}&can=${can}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: '{}'
    });
    if (res.status === 401) {
        openAuthModal('login');
        return;
    }
    const data = await res.json();
    btn.classList.toggle('product-card-action-active', data.active);
    if (window.location.pathname.startsWith('/Favourite')) { location.reload(); }
    if (res.ok && typeof updateFavAndCartBadges === 'function') { await updateFavAndCartBadges(); }
}

/* ---------- Seller modal ---------- */

function openSellerModalFromEl(el) {
    const d = el.dataset;
    openSellerModal(
        d.sellerUsername, d.sellerAvatar, d.sellerPhone,
        d.sellerCompany === 'true', d.sellerEmail, d.sellerCreated,
        parseInt(d.sellerId, 10) || 0,
        d.sellerTelegram, d.sellerWhatsapp, d.sellerWebsite
    );
}
function openSellerModal(name, avatar, phone, isCompany, email, date, sellerId, telegramUrl, whatsAppUrl, websiteUrl) {
    const avatarEl = document.getElementById('smAvatar');
    avatarEl.innerHTML = avatar
        ? `<img src="${avatar}" onerror="this.src='/img/no-photo.svg'" />`
        : `<svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>`;

    document.getElementById('smName').textContent = name || translate('unknown_seller');
    const badge = document.getElementById('smBadge');
    badge.innerHTML = isCompany ? `<span class="seller-badge">${translate('company_badge')}</span>` : '';

    const phoneRow = document.getElementById('smPhoneRow');
    const phoneEl = document.getElementById('smPhone');
    if (phone) { phoneEl.textContent = phone; phoneRow.style.display = 'flex'; }
    else { phoneRow.style.display = 'none'; }

    const emailRow = document.getElementById('smEmailRow');
    const emailEl = document.getElementById('smEmail');
    if (email) { emailEl.textContent = email; emailRow.style.display = 'flex'; }
    else { emailRow.style.display = 'none'; }

    const telegramRow = document.getElementById('smTelegramRow');
    const telegramEl = document.getElementById('smTelegram');
    if (telegramUrl) {
        telegramEl.href = telegramUrl;
        telegramEl.textContent = telegramUrl.replace(/^https?:\/\/(t\.me\/|telegram\.me\/)?/i, '@');
        telegramRow.style.display = 'flex';
    } else {
        telegramRow.style.display = 'none';
    }

    const whatsAppRow = document.getElementById('smWhatsAppRow');
    const whatsAppEl = document.getElementById('smWhatsApp');
    if (whatsAppUrl) {
        whatsAppEl.href = whatsAppUrl;
        whatsAppEl.textContent = 'WhatsApp';
        whatsAppRow.style.display = 'flex';
    } else {
        whatsAppRow.style.display = 'none';
    }

    const websiteRow = document.getElementById('smWebsiteRow');
    const websiteEl = document.getElementById('smWebsite');
    if (websiteUrl) {
        websiteEl.href = websiteUrl;
        websiteEl.textContent = websiteUrl.replace(/^https?:\/\//i, '');
        websiteRow.style.display = 'flex';
    } else {
        websiteRow.style.display = 'none';
    }

    const dateRow = document.getElementById('smDateRow');
    const dateEl = document.getElementById('smDate');
    if (date) { dateEl.textContent = translate('seller_member_since') + ' ' + date; dateRow.style.display = 'flex'; }
    else { dateRow.style.display = 'none'; }

    const link = document.getElementById('smLink');
    link.textContent = translate('seller_other_listings');
    if (sellerId) { link.href = '/Seller/Index/' + sellerId; link.style.display = ''; }
    else { link.style.display = 'none'; }

    const ratingEl = document.getElementById('smRating');
    ratingEl.style.display = 'none';
    ratingEl.innerHTML = '';
    if (sellerId) {
        fetch(`/Seller/Rating?id=${sellerId}`)
            .then(r => r.json())
            .then(data => {
                if (!data.count) return;
                const avg = data.avg;
                ratingEl.innerHTML = [1, 2, 3, 4, 5].map(i => {
                    const fill = i <= Math.floor(avg) ? '#f5a623' : 'none';
                    const partial = i === Math.ceil(avg) && avg % 1 > 0;
                    if (partial) {
                        const pct = Math.round((avg % 1) * 100);
                        return `<svg width="18" height="18" viewBox="0 0 24 24"><defs><linearGradient id="sg${i}"><stop offset="${pct}%" stop-color="#f5a623"/><stop offset="${pct}%" stop-color="none"/></linearGradient></defs><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" fill="url(#sg${i})" stroke="#f5a623" stroke-width="2"/></svg>`;
                    }
                    return `<svg width="18" height="18" viewBox="0 0 24 24"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" fill="${fill}" stroke="#f5a623" stroke-width="2"/></svg>`;
                }).join('') + `<span class="sm-rating-text">${avg.toFixed(1)} · ${data.count}</span>`;
                ratingEl.style.display = 'flex';
            })
            .catch(() => { });
    }

    document.getElementById('sellerOverlay').classList.add('open');
    document.getElementById('sellerModal').classList.add('open');
}
function closeSellerModal() {
    document.getElementById('sellerOverlay').classList.remove('open');
    document.getElementById('sellerModal').classList.remove('open');
}

/* ---------- Settings modal ---------- */

function openSettingsModal() {
    if (!document.getElementById('settingsOverlay')) return;
    document.getElementById('userDropdown')?.classList.remove('open');
    if (typeof applySettingsUI === 'function') applySettingsUI();
    document.getElementById('settingsOverlay').classList.add('modal-open');
    document.getElementById('settingsModal').classList.add('modal-open');
    applyTranslations();
}

function closeSettingsModal() {
    if (!document.getElementById('settingsOverlay')) return;
    document.getElementById('settingsOverlay').classList.remove('modal-open');
    document.getElementById('settingsModal').classList.remove('modal-open');
}

function applySettingsUI() {
    const dark = localStorage.getItem('theme') === 'dark';
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) themeToggle.classList.toggle('settings-toggle-on', dark);
}

function toggleTheme() {
    const isDark = document.documentElement.classList.toggle('dark-theme');
    localStorage.setItem('theme', isDark ? 'dark' : 'light');
    const themeToggleBtn = document.getElementById('themeToggle');
    if (themeToggleBtn) themeToggleBtn.classList.toggle('settings-toggle-on', isDark);

    const logo = document.getElementById('siteLogo');
    if (logo) logo.src = isDark ? '/img/DLogo.svg' : '/img/Logo.svg';
}

/* ---------- Reviews modal ---------- */

async function openMyReviewsModal() {
    if (!document.getElementById('myReviewsOverlay')) return;
    document.getElementById('userDropdown')?.classList.remove('open');
    document.getElementById('myReviewsOverlay').classList.add('open');
    document.getElementById('myReviewsModal').classList.add('open');
    applyTranslations();
    await loadReviewTab('about');
}

function closeMyReviewsModal() {
    if (!document.getElementById('myReviewsOverlay')) return;
    document.getElementById('myReviewsOverlay').classList.remove('open');
    document.getElementById('myReviewsModal').classList.remove('open');
}

async function switchReviewTab(tab) {
    const tabAbout = document.getElementById('tabAboutMe');
    const tabFrom = document.getElementById('tabFromMe');
    if (tabAbout) tabAbout.classList.toggle('reviews-tab-active', tab === 'about');
    if (tabFrom) tabFrom.classList.toggle('reviews-tab-active', tab === 'from');
    await loadReviewTab(tab);
}

async function loadReviewTab(tab) {
    const list = document.getElementById('myReviewsList');
    if (!list) return;
    list.innerHTML = `<div class="reviews-empty">${translate('loading_text')}</div>`;
    try {
        const res = await fetch(`/Reviews/My?tab=${tab}`);
        const data = await res.json();
        if (!data.length) {
            list.innerHTML = `<div class="reviews-empty">${translate('no_reviews_yet')}</div>`;
            return;
        }
        list.innerHTML = data.map(r => `
            <div class="review-item">
                <div class="review-author">
                    <img src="${r.avatarUrl || '/img/no-photo.svg'}"
                         class="review-author-avatar"
                         onerror="this.src='/img/no-photo.svg'" />
                    <div>
                        <a class="review-author-name" href="/Seller/Index/${r.userId}"
                           onclick="closeMyReviewsModal()">
                            ${r.userName}
                        </a>
                        <div class="review-date">${r.createdAt}</div>
                    </div>
                </div>
                <div class="review-stars">
                    ${[1, 2, 3, 4, 5].map(i =>
            `<svg width="14" height="14" viewBox="0 0 24 24"
                              fill="${i <= r.rating ? '#f5a623' : 'none'}"
                              stroke="#f5a623" stroke-width="2">
                            <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>
                         </svg>`
        ).join('')}
                </div>
                ${r.comment ? `<div class="review-comment">${r.comment}</div>` : ''}
            </div>`
        ).join('');
    } catch {
        list.innerHTML = `<div class="reviews-empty">${translate('failed_load')}</div>`;
    }
}

/* ---------- Deal review modal ---------- */

let dealReviewTargetId = null;
let dealReviewProductId = null;
let dealReviewRating = 0;

function openDealReviewModal(targetUserId, productId) {
    dealReviewTargetId = targetUserId;
    dealReviewProductId = productId;
    dealReviewRating = 0;
    document.getElementById('dealReviewComment').value = '';
    document.querySelectorAll('#dealReviewStars .star').forEach(s => s.setAttribute('fill', 'none'));
    document.getElementById('dealReviewOverlay').classList.add('open');
    document.getElementById('dealReviewModal').classList.add('open');
    applyTranslations();
}

function closeDealReviewModal() {
    document.getElementById('dealReviewOverlay').classList.remove('open');
    document.getElementById('dealReviewModal').classList.remove('open');
}

function rateDealUser(rating) {
    dealReviewRating = rating;
    document.querySelectorAll('#dealReviewStars .star').forEach((s, idx) => {
        s.setAttribute('fill', idx < rating ? '#f5a623' : 'none');
    });
}

async function submitDealReview() {
    if (!dealReviewRating) return;
    const comment = document.getElementById('dealReviewComment').value.trim();
    const res = await fetch('/Reviews/Create', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: JSON.stringify({
            targetUserId: dealReviewTargetId,
            productId: dealReviewProductId,
            rating: dealReviewRating,
            comment: comment || null
        })
    });
    if (res.ok) closeDealReviewModal();
}

/* ---------- Notification preferences modal ---------- */

async function openNotifPrefsModal() {
    closeSettingsModal();
    const res = await fetch('/Notifications/Preferences');
    if (res.ok) {
        const p = await res.json();
        document.getElementById('prefDeals').checked = p.deals;
        document.getElementById('prefReviews').checked = p.reviews;
        document.getElementById('prefModeration').checked = p.moderation;
        document.getElementById('prefAccount').checked = p.account;
        document.getElementById('prefFavourites').checked = p.favourites;
        document.getElementById('prefNewListings').checked = p.newListings;
    }
    document.getElementById('notifPrefsOverlay').classList.add('modal-open');
    document.getElementById('notifPrefsModal').classList.add('modal-open');
    applyTranslations();
}

function closeNotifPrefsModal() {
    document.getElementById('notifPrefsOverlay').classList.remove('modal-open');
    document.getElementById('notifPrefsModal').classList.remove('modal-open');
}

async function saveNotifPrefs() {
    const payload = {
        deals: document.getElementById('prefDeals').checked,
        reviews: document.getElementById('prefReviews').checked,
        moderation: document.getElementById('prefModeration').checked,
        account: document.getElementById('prefAccount').checked,
        favourites: document.getElementById('prefFavourites').checked,
        newListings: document.getElementById('prefNewListings').checked,
    };
    const res = await fetch('/Notifications/SavePreferences', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: JSON.stringify(payload)
    });
    if (res.ok) closeNotifPrefsModal();
}

/* ---------- Facebook login ---------- */

window.fbAsyncInit = function () {
    FB.init({
        appId: '837576922674717',
        cookie: true,
        xfbml: true,
        version: 'v25.0'
    });
    FB.AppEvents.logPageView();
};

(function (d, s, id) {
    let js, fjs = d.getElementsByTagName(s)[0];
    if (d.getElementById(id)) return;
    js = d.createElement(s);
    js.id = id;
    js.src = "https://connect.facebook.net/en_US/sdk.js";
    fjs.parentNode.insertBefore(js, fjs);
}(document, 'script', 'facebook-jssdk'));

function loginWithFacebook(returnUrl) {
    FB.login(function (response) {
        if (response.authResponse) {
            const accessToken = response.authResponse.accessToken;
            fetch('/Account/FacebookLogin', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.getElementById('afToken')?.value || ''
                },
                body: JSON.stringify({
                    accessToken: accessToken,
                    returnUrl: returnUrl || window.location.pathname
                })
            })
                .then(res => {
                    if (res.redirected) {
                        window.location.href = res.url;
                    } else if (res.ok) {
                        window.location.reload();
                    } else {
                        return res.text().then(err => { throw new Error(err) });
                    }
                })
                .catch(err => {
                    console.error(translate('facebook_login_failed'), err);
                    alert(translate('facebook_login_error'));
                });
        } else {
            console.log(translate('user_cancelled_login'));
        }
    }, { scope: 'public_profile,email' });
}

/* ---------- Scroll-to-top button ---------- */

window.addEventListener("load", function () {
    const scrollBtn = document.getElementById("scrollTopBtn");
    if (!scrollBtn) return;

    function toggleBtn() {
        scrollBtn.classList.toggle("show", document.documentElement.scrollTop > 300);
    }

    window.addEventListener("scroll", toggleBtn);
    scrollBtn.addEventListener("click", function () {
        window.scrollTo({ top: 0, behavior: "smooth" });
    });
    toggleBtn();
});

/* ---------- Notifications Modal & Badges ---------- */

let notifAllData = [];
let notifCurrentTab = 'all';
let notifModalOpen = false;

function toggleNotifModal(e) {
    if (!document.getElementById('notifOverlay')) return;
    e.stopPropagation();
    if (notifModalOpen) {
        closeNotifModal();
    } else {
        openNotifModal();
    }
}

async function openNotifModal() {
    if (!document.getElementById('notifOverlay')) return;
    notifModalOpen = true;
    document.getElementById('notifOverlay').classList.add('open');
    document.getElementById('notifModal').classList.add('open');
    await loadAllNotifications();
}

function closeNotifModal() {
    if (!document.getElementById('notifOverlay')) return;
    notifModalOpen = false;
    document.getElementById('notifOverlay').classList.remove('open');
    document.getElementById('notifModal').classList.remove('open');
}

async function loadAllNotifications() {
    const list = document.getElementById('notifModalList');
    if (!list) return;
    list.innerHTML = '<div class="notif-modal-empty"><svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#ddd" stroke-width="1.5"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>Loading...</div>';
    try {
        const res = await fetch('/Notifications/List');
        notifAllData = await res.json();
        renderNotifList();
        updateNotifBadge(notifAllData.filter(n => !n.isRead).length);
    } catch {
        list.innerHTML = '<div class="notif-modal-empty">Failed to load</div>';
    }
}

function renderNotifList() {
    const list = document.getElementById('notifModalList');
    if (!list) return;
    const unreadCount = notifAllData.filter(n => !n.isRead).length;

    const tabCount = document.getElementById('tabUnreadCount');
    if (tabCount) {
        if (unreadCount > 0) {
            tabCount.textContent = unreadCount;
            tabCount.style.display = 'inline-block';
        } else {
            tabCount.style.display = 'none';
        }
    }
    const markAllBtn = document.getElementById('markAllBtn');
    if (markAllBtn) markAllBtn.style.display = unreadCount > 0 ? '' : 'none';

    const data = notifCurrentTab === 'unread'
        ? notifAllData.filter(n => !n.isRead)
        : notifAllData;

    if (!data.length) {
        list.innerHTML = `
            <div class="notif-modal-empty">
                <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#ddd" stroke-width="1.5">
                    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                    <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                </svg>
                ${notifCurrentTab === 'unread' ? translate('notif_empty_unread') : translate('notif_empty_all')}
            </div>`;
        return;
    }

    list.innerHTML = data.map(n => `
        <div class="notif-item ${n.isRead ? '' : 'unread'}"
             id="notif-item-${n.id}"
             onclick="handleNotifClick(${n.id}, '${n.productId ? '/Product/Details/' + n.productId : ''}')">
            <img class="notif-item-avatar"
                 src="${n.fromUserAvatar || '/img/no-photo.svg'}"
                 onerror="this.src='/img/no-photo.svg'" />
            <div class="notif-item-body">
                <div class="notif-item-msg">${escapeHtml(translateNotificationMessage(n.text, n))}</div>
                <div class="notif-item-time">${n.createdAt}</div>
                ${n.productId && n.productName ? `
                    <div class="notif-item-product">
                        ${n.productImage ? `<img src="${n.productImage}" onerror="this.src='/img/no-photo.svg'" />` : ''}
                        <span>${escapeHtml(n.productName)}</span>
                    </div>` : ''}
            </div>
        </div>`
    ).join('');
}

async function handleNotifClick(id, url) {
    const item = notifAllData.find(n => n.id === id);
    if (!item) return;

    if (!item.isRead) {
        item.isRead = true;
        await fetch(`/Notifications/MarkRead?id=${id}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': document.getElementById('afToken')?.value ?? '' }
        });
        const unreadLeft = notifAllData.filter(n => !n.isRead).length;
        updateNotifBadge(unreadLeft);

        const el = document.getElementById('notif-item-' + id);
        if (el) {
            el.classList.remove('unread');
            if (notifCurrentTab !== 'unread') {
                const tabCount = document.getElementById('tabUnreadCount');
                if (tabCount) {
                    if (unreadLeft > 0) tabCount.textContent = unreadLeft;
                    else tabCount.style.display = 'none';
                }
                const markAllBtn = document.getElementById('markAllBtn');
                if (markAllBtn) markAllBtn.style.display = unreadLeft > 0 ? '' : 'none';
            }
        }
    }

    let notifType = null;
    let reviewId = null;
    try {
        const parsed = JSON.parse(item.text);
        notifType = parsed?.type;
        reviewId = parsed?.reviewId;
    } catch { }

    if (notifType === 'review_received' && reviewId) {
        closeNotifModal();
        window.location.href = `/Seller/Index/${window.currentUserId}?reviewId=${reviewId}`;
        return;
    }

    if (notifType === 'deal_sold' || notifType === 'deal_bought') {
        closeNotifModal();
        await handleDealNotifClick(item);
        return;
    }

    if (url) {
        closeNotifModal();
        window.location.href = url;
    }
}

async function handleDealNotifClick(notif) {
    if (!notif.productId || !notif.fromUserId) return;
    try {
        const res = await fetch(`/Reviews/CanReview?targetUserId=${notif.fromUserId}&productId=${notif.productId}`);
        if (res.ok) {
            const data = await res.json();
            if (data.canReview) {
                openDealReviewModal(notif.fromUserId, notif.productId);
                return;
            }
        }
    } catch { }
    window.location.href = '/Product/Details/' + notif.productId;
}

function switchNotifTab(tab) {
    notifCurrentTab = tab;
    const tabAll = document.getElementById('tabAll');
    const tabUnread = document.getElementById('tabUnread');
    if (tabAll) tabAll.classList.toggle('notif-tab-active', tab === 'all');
    if (tabUnread) tabUnread.classList.toggle('notif-tab-active', tab === 'unread');
    renderNotifList();
}

async function markAllReadModal() {
    await fetch('/Notifications/MarkAllRead', {
        method: 'POST',
        headers: { 'RequestVerificationToken': document.getElementById('afToken')?.value ?? '' }
    });
    notifAllData.forEach(n => n.isRead = true);
    updateNotifBadge(0);
    renderNotifList();
}

function updateNotifBadge(count) {
    const badge = document.getElementById('notifBadge');
    if (!badge) return;
    badge.textContent = count;
    badge.style.display = count > 0 ? 'flex' : 'none';
}

async function updateFavAndCartBadges() {
    try {
        const res = await fetch('/Favourite/UserItems');
        if (!res.ok) return;
        const { favs, cart } = await res.json();

        const favBadge = document.getElementById('favsNavBadge');
        if (favBadge) {
            if (favs && favs.length > 0) {
                favBadge.textContent = favs.length;
                favBadge.style.display = 'inline-flex';
            } else {
                favBadge.style.display = 'none';
            }
        }

        const cartBadge = document.getElementById('cartNavBadge');
        if (cartBadge) {
            if (cart && cart.length > 0) {
                cartBadge.textContent = cart.length;
                cartBadge.style.display = 'inline-flex';
            } else {
                cartBadge.style.display = 'none';
            }
        }
    } catch (e) {
        console.warn("Failed to update fav/cart badges", e);
    }
}

async function updateMsgBadge() {
    try {
        const res = await fetch('/Messages/UnreadCount');
        if (!res.ok) return;
        const { count } = await res.json();
        const badge = document.getElementById('msgNavBadge');
        if (badge) {
            badge.textContent = count;
            badge.style.display = count > 0 ? 'inline-flex' : 'none';
        }
    } catch (e) {
        console.warn('Failed to update message badge', e);
    }
}

/* ---------- Shared Rules Modal (Promo / Subscription) ---------- */

const sharedRulesState = {};

function openSharedRulesModal(prefix) {
    const rulesBody = document.getElementById(`${prefix}RulesBody`);
    const policyBody = document.getElementById(`${prefix}PolicyBody`);

    const rulesSource = document.querySelector('#rulesModal .info-modal-body');
    const policySource = document.querySelector('#policyModal .info-modal-body');

    if (rulesSource && rulesBody) rulesBody.innerHTML = rulesSource.innerHTML;
    if (policySource && policyBody) policyBody.innerHTML = policySource.innerHTML;

    const lang = getCurrentLanguage();

    [rulesBody, policyBody].forEach(body => {
        if (!body) return;
        body.querySelectorAll('.info-lang-content').forEach(el => {
            el.style.display = el.dataset.lang === lang ? '' : 'none';
        });
    });

    const modal = document.getElementById(`${prefix}RulesModal`);
    if (modal) {
        modal.querySelectorAll('.info-lang-btn').forEach(btn => {
            const btnLang = btn.textContent.trim().toLowerCase();
            btn.classList.toggle('active', btnLang === lang);
        });

        const titles = {
            promo: { en: 'Promotion rules', lv: 'Veicināšanas noteikumi', ru: 'Правила продвижения' },
            sub: { en: 'Subscription rules', lv: 'Abonementa noteikumi', ru: 'Правила подписки' }
        };
        const titleEl = modal.querySelector('.info-modal-title');
        if (titleEl && titles[prefix]) {
            titleEl.textContent = titles[prefix][lang] || titles[prefix]['en'];
        }

        const innerTitles = {
            en: { rules: 'Rules', policy: 'Privacy Policy' },
            lv: { rules: 'Noteikumi', policy: 'Privātuma politika' },
            ru: { rules: 'Правила', policy: 'Политика конфиденциальности' }
        };
        const rulesTitleEl = modal.querySelector('[data-i18n="rules_title"]');
        const policyTitleEl = modal.querySelector('[data-i18n="policy_title"]');
        if (rulesTitleEl) rulesTitleEl.textContent = innerTitles[lang].rules;
        if (policyTitleEl) policyTitleEl.textContent = innerTitles[lang].policy;
    }

    const agreeBtn = document.getElementById(`${prefix}AgreeBtn`);
    if (agreeBtn) agreeBtn.disabled = true;

    sharedRulesState[prefix] = { rulesScrolled: false, policyScrolled: false };

    if (rulesBody) rulesBody.scrollTop = 0;
    if (policyBody) policyBody.scrollTop = 0;

    document.getElementById(`${prefix}RulesOverlay`).classList.add('modal-open');
    modal.classList.add('modal-open');
    document.body.style.overflow = 'hidden';

    setTimeout(() => {
        if (rulesBody && rulesBody.scrollHeight <= rulesBody.clientHeight + 10) sharedRulesState[prefix].rulesScrolled = true;
        if (policyBody && policyBody.scrollHeight <= policyBody.clientHeight + 10) sharedRulesState[prefix].policyScrolled = true;
        if (sharedRulesState[prefix].rulesScrolled && sharedRulesState[prefix].policyScrolled) {
            if (agreeBtn) agreeBtn.disabled = false;
        }
    }, 50);
}

function checkSharedRulesScroll(prefix, type) {
    const body = document.getElementById(`${prefix}${type === 'rules' ? 'Rules' : 'Policy'}Body`);
    if (!body) return;

    const atBottom = body.scrollTop + body.clientHeight >= body.scrollHeight - 10;
    if (atBottom) {
        if (type === 'rules') sharedRulesState[prefix].rulesScrolled = true;
        if (type === 'policy') sharedRulesState[prefix].policyScrolled = true;
    }

    if (sharedRulesState[prefix].rulesScrolled && sharedRulesState[prefix].policyScrolled) {
        document.getElementById(`${prefix}AgreeBtn`).disabled = false;
    }
}

function switchSharedRulesLang(prefix, lang, btn) {
    const modal = document.getElementById(`${prefix}RulesModal`);
    if (!modal) return;

    modal.querySelectorAll('.info-lang-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    ['Rules', 'Policy'].forEach(type => {
        const body = document.getElementById(`${prefix}${type}Body`);
        if (body) {
            body.querySelectorAll('.info-lang-content').forEach(el => {
                el.style.display = el.dataset.lang === lang ? '' : 'none';
            });
            body.scrollTop = 0;
        }
    });

    sharedRulesState[prefix] = { rulesScrolled: false, policyScrolled: false };
    const agreeBtn = document.getElementById(`${prefix}AgreeBtn`);
    if (agreeBtn) agreeBtn.disabled = true;

    setTimeout(() => {
        const rBody = document.getElementById(`${prefix}RulesBody`);
        const pBody = document.getElementById(`${prefix}PolicyBody`);
        if (rBody && rBody.scrollHeight <= rBody.clientHeight + 10) sharedRulesState[prefix].rulesScrolled = true;
        if (pBody && pBody.scrollHeight <= pBody.clientHeight + 10) sharedRulesState[prefix].policyScrolled = true;
        if (sharedRulesState[prefix].rulesScrolled && sharedRulesState[prefix].policyScrolled) {
            if (agreeBtn) agreeBtn.disabled = false;
        }
    }, 50);

    const titles = {
        promo: { en: 'Promotion rules', lv: 'Veicināšanas noteikumi', ru: 'Правила продвижения' },
        sub: { en: 'Subscription rules', lv: 'Abonementa noteikumi', ru: 'Правила подписки' }
    };
    const titleEl = modal.querySelector('.info-modal-title');
    if (titleEl && titles[prefix]) {
        titleEl.textContent = titles[prefix][lang] || titles[prefix]['en'];
    }

    const innerTitles = {
        en: { rules: 'Rules', policy: 'Privacy Policy' },
        lv: { rules: 'Noteikumi', policy: 'Privātuma politika' },
        ru: { rules: 'Правила', policy: 'Политика конфиденциальности' }
    };
    const rulesTitleEl = modal.querySelector('[data-i18n="rules_title"]');
    const policyTitleEl = modal.querySelector('[data-i18n="policy_title"]');
    if (rulesTitleEl) rulesTitleEl.textContent = innerTitles[lang].rules;
    if (policyTitleEl) policyTitleEl.textContent = innerTitles[lang].policy;
}

function closeSharedRulesModal(prefix) {
    document.getElementById(`${prefix}RulesOverlay`)?.classList.remove('modal-open');
    document.getElementById(`${prefix}RulesModal`)?.classList.remove('modal-open');
    document.body.style.overflow = '';
}

function confirmSharedRules(prefix, agreed) {
    if (prefix === 'promo' && typeof window.confirmPromoRules === 'function') {
        window.confirmPromoRules(agreed);
    } else if (prefix === 'sub' && typeof window.confirmSubRules === 'function') {
        window.confirmSubRules(agreed);
    }
}

window.openPromoRulesModal = () => openSharedRulesModal('promo');
window.closePromoRulesModal = () => closeSharedRulesModal('promo');

window.isUserAuthenticated = document.documentElement.dataset.authenticated === 'true';
function openMessageModal() {
    document.getElementById('msgOverlay')?.classList.add('open');
    document.getElementById('msgModal')?.classList.add('open');
}

function closeMessageModal() {
    document.getElementById('msgOverlay')?.classList.remove('open');
    document.getElementById('msgModal')?.classList.remove('open');
}

function checkAuthAndOpenMessageModal() {
    if (window.isUserAuthenticated) {
        openMessageModal();
    } else if (typeof openAuthModal === 'function') {
        openAuthModal('login');
    } else {
        console.warn('openAuthModal not found');
    }
}

async function sendMessageToSeller() {
    const textarea = document.querySelector('.msg-modal-textarea');
    const text = textarea?.value.trim();
    if (!text) return;

    const modal = document.getElementById('msgModal');
    const productId = parseInt(modal?.dataset.productId, 10);
    const sellerId = parseInt(modal?.dataset.sellerId, 10);

    const res = await fetch('/Messages/Start', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: JSON.stringify({ productId, sellerId, text })
    });

    if (res.status === 401) { openAuthModal('login'); return; }

    const { conversationId } = await res.json();
    window.location.href = '/Messages/Index/' + conversationId;
}

let reportReasons = [];

async function openReportModal(productId) {
    if (!document.getElementById('reportModal')) return;

    if (!reportReasons.length) {
        const res = await fetch('/api/Report/reasons');
        if (!res.ok) return;
        reportReasons = await res.json();
    }

    const select = document.getElementById('reportReason');
    select.innerHTML = `<option value="">${translate('report_reason_ph')}</option>`;
    reportReasons.forEach(r => {
        const opt = document.createElement('option');
        opt.value = r.id;
        opt.textContent = r.text;
        select.appendChild(opt);
    });

    document.getElementById('reportModal').dataset.productId = productId;
    document.getElementById('reportOverlay').classList.add('modal-open');
    document.getElementById('reportModal').classList.add('modal-open');
}

function closeReportModal() {
    document.getElementById('reportOverlay')?.classList.remove('modal-open');
    document.getElementById('reportModal')?.classList.remove('modal-open');
}

async function submitReport() {
    const reasonId = document.getElementById('reportReason')?.value;
    if (!reasonId) {
        alert(translate('report_select_reason'));
        return;
    }

    const comment = document.getElementById('reportComment')?.value;
    const productId = document.getElementById('reportModal')?.dataset.productId;

    const res = await fetch('/api/Report/create', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: JSON.stringify({
            productId: parseInt(productId),
            reportReasonId: parseInt(reasonId),
            comment: comment || null
        })
    });

    if (res.status === 401) {
        closeReportModal();
        openAuthModal('login');
        return;
    }

    if (res.ok) {
        showModal('report_submitted');
        closeReportModal();
    } else {
        const err = await res.text();
        alert(translate('error') + ': ' + err);
    }
}

let pendingPhone = null;
let recaptchaWidgetId = null;

function togglePhone(btn) {
    const phone = btn.dataset.phone;
    if (!phone) { btn.textContent = 'No phone'; return; }
    if (btn.classList.contains('shown')) {
        btn.innerHTML = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 13.1a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 2.45h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 10.1a16 16 0 0 0 6 6l1.27-.95a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 17.92z"/></svg> ${translate('call_btn')}`;
        btn.classList.remove('shown');
    } else {
        btn.textContent = phone;
        btn.classList.add('shown');
    }
}
function handlePhoneClick(btn) {
    if (window.isUserAuthenticated) {
        togglePhone(btn);
        return;
    }
    if (btn.classList.contains('shown')) return;

    pendingPhone = btn.dataset.phone || null;
    openCaptchaModal();
}
function openCaptchaModal() {
    const modal = document.getElementById('captchaModal');
    if (!modal) return;

    document.getElementById('captchaError').style.display = 'none';
    document.getElementById('captchaOverlay').classList.add('modal-open');
    modal.classList.add('modal-open');

    if (recaptchaWidgetId === null) {
        ensureCaptchaWidget(0);
    } else {
        grecaptcha.reset(recaptchaWidgetId);
    }
}
function ensureCaptchaWidget(attempt) {
    const modal = document.getElementById('captchaModal');
    if (!modal || recaptchaWidgetId !== null) return;

    const sitekey = modal.dataset.sitekey;
    if (!sitekey) return;

    if (typeof grecaptcha === 'undefined' || !grecaptcha.render) {
        if (attempt < 15) setTimeout(() => ensureCaptchaWidget(attempt + 1), 400);
        return;
    }

    recaptchaWidgetId = grecaptcha.render('recaptchaWidget', { sitekey });
}

function closeCaptchaModal() {
    document.getElementById('captchaOverlay')?.classList.remove('modal-open');
    document.getElementById('captchaModal')?.classList.remove('modal-open');
}

async function verifyCaptcha() {
    if (recaptchaWidgetId === null) return;

    const token = grecaptcha.getResponse(recaptchaWidgetId);
    if (!token) {
        const err = document.getElementById('captchaError');
        err.style.display = 'block';
        err.textContent = translate('captcha_required');
        return;
    }

    const res = await fetch('/Product/VerifyRecaptcha', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
        },
        body: JSON.stringify({ token })
    });
    const data = await res.json();

    if (!data.success) {
        const err = document.getElementById('captchaError');
        err.style.display = 'block';
        err.textContent = translate('captcha_failed');
        grecaptcha.reset(recaptchaWidgetId);
        return;
    }

    closeCaptchaModal();

    const btn = document.getElementById('phoneBtn');
    if (btn && pendingPhone) {
        btn.textContent = pendingPhone;
        btn.classList.add('shown');
    }
    pendingPhone = null;
}
function checkAuthAndOpenSellerModalFromEl(el) {
    if (window.isUserAuthenticated) {
        openSellerModalFromEl(el);
    } else if (typeof openAuthModal === 'function') {
        openAuthModal('login');
    }
}

let currentProductId = null;
function openPartnerModal(productId) {
    currentProductId = productId;
    fetch(`/Product/GetConversationPartners?productId=${productId}`)
        .then(res => res.json())
        .then(partners => {
            const list = document.getElementById('partnerList');
            list.innerHTML = '';
            if (partners.length === 0) {
                list.innerHTML = `<div class="partner-item">${translate('no_partners')}</div>`;
                return;
            }
            partners.forEach(p => {
                const item = document.createElement('div');
                item.className = 'partner-item';
                item.onclick = () => selectPartner(p.id);
                item.innerHTML = `
                        <img class="partner-item-avatar" src="${p.avatarUrl || '/img/no-photo.svg'}" onerror="this.src='/img/no-photo.svg'">
                        <span class="partner-item-name">${p.userName}</span>
                        ${p.isCompany ? '<span class="partner-item-badge">Company</span>' : ''}
                    `;
                list.appendChild(item);
            });
        });
    document.getElementById('partnerOverlay').classList.add('modal-open');
    document.getElementById('partnerModal').classList.add('modal-open');
}

function closePartnerModal() {
    document.getElementById('partnerOverlay').classList.remove('modal-open');
    document.getElementById('partnerModal').classList.remove('modal-open');
}

async function selectPartner(otherUserId) {
    closePartnerModal();
    try {
        const res = await fetch(`/Product/CompleteDeal?id=${currentProductId}&otherUserId=${otherUserId}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
            }
        });
        if (res.ok) {
            location.reload();
        } else {
            const err = await res.text();
            alert(translate('error') + ': ' + err);
        }
    } catch (e) {
        console.error(translate('error') + ': ', e);
    }
}
function showToast(message, type = 'success') {
    const toast = document.getElementById('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.className = 'admin-toast show ' + type;
    setTimeout(() => toast.classList.remove('show'), 3000);
}

let rejectReasons = [];
let currentRejectProductId = null;

async function openRejectModal(productId) {
    currentRejectProductId = productId;
    if (!rejectReasons.length) {
        const res = await fetch('/api/Report/reasons');
        if (res.ok) rejectReasons = await res.json();
    }
    const select = document.getElementById('rejectReasonSelect');
    select.innerHTML = `<option value="">${translate('report_reason_ph')}</option>`;
    rejectReasons.forEach(r => {
        const opt = document.createElement('option');
        opt.value = r.id;
        opt.textContent = r.text;
        select.appendChild(opt);
    });
    document.getElementById('rejectComment').value = '';
    document.getElementById('rejectOverlay').classList.add('modal-open');
    document.getElementById('rejectModal').classList.add('modal-open');
}

function closeRejectModal() {
    document.getElementById('rejectOverlay')?.classList.remove('modal-open');
    document.getElementById('rejectModal')?.classList.remove('modal-open');
}

async function submitRejectProduct() {
    const reasonId = document.getElementById('rejectReasonSelect').value;
    if (!reasonId) return;

    const comment = document.getElementById('rejectComment').value;
    const res = await fetch(
        `/Admin/RejectProductWithReason?id=${currentRejectProductId}&reasonId=${reasonId}&comment=${encodeURIComponent(comment)}`,
        { method: 'POST', headers: { 'RequestVerificationToken': afToken } }
    );

    if (res.ok) {
        closeRejectModal();
        document.getElementById('row-' + currentRejectProductId)?.remove();
        showToast(translate('status_updated') + translate('admin_status_rejected'), 'success');
    } else {
        showToast(translate('err_status'), 'error');
    }
}

async function setStatus(id, status, btn) {
    btn.disabled = true;
    const res = await fetch(`/Admin/SetProductStatus?id=${id}&status=${status}`, {
        method: 'POST',
        headers: { 'RequestVerificationToken': afToken }
    });
    if (res.ok) {
        document.getElementById('row-' + id)?.remove();
        showToast(translate('status_updated') + status, 'success');
    } else {
        showToast(translate('err_status'), 'error');
        btn.disabled = false;
    }
}

let deleteId = null;
function confirmDelete(id, name) {
    deleteId = id;
    document.getElementById('confirmText').textContent = translate('confirm_question').replace('{name}', name);
    document.getElementById('confirmOverlay').classList.add('open');
    document.getElementById('confirmOk').onclick = doDelete;
}

function closeConfirm() {
    document.getElementById('confirmOverlay').classList.remove('open');
}

async function doDelete() {
    closeConfirm();
    const res = await fetch(`/Product/Delete?id=${deleteId}`, {
        method: 'POST',
        headers: { 'RequestVerificationToken': afToken }
    });
    if (res.ok) {
        document.getElementById('row-' + deleteId)?.remove();
        showToast(translate('deleted_success'), 'success');
    } else {
        showToast(translate('err_delete'), 'error');
    }
}

document.addEventListener('click', function (e) {
    const rejectBtn = e.target.closest('.js-reject-product');
    if (rejectBtn) {
        const id = rejectBtn.dataset.productId;
        if (id) openRejectModal(parseInt(id, 10));
        return;
    }

    const deleteBtn = e.target.closest('.js-delete-product');
    if (deleteBtn) {
        const id = deleteBtn.dataset.productId;
        const name = deleteBtn.dataset.productName || '';
        confirmDelete(parseInt(id, 10), name);
        return;
    }

    var partnerBtn = e.target.closest('.js-open-partner');
    if (partnerBtn) {
        var productId = partnerBtn.dataset.productId;
        if (productId) {
            openPartnerModal(parseInt(productId, 10));
        }
        return;
    }
});

document.getElementById('confirmOverlay')?.addEventListener('click', e => {
    if (e.target === e.currentTarget) closeConfirm();
});

function openReviews() {
    document.getElementById('reviewsOverlay')?.classList.add('open');
    document.getElementById('reviewsModal')?.classList.add('open');
}

function closeReviews() {
    document.getElementById('reviewsOverlay')?.classList.remove('open');
    document.getElementById('reviewsModal')?.classList.remove('open');
}
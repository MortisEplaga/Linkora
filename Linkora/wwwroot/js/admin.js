const afToken = document.getElementById('afToken')?.value ?? '';

function showToast(msg, type = 'success') {
    const t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.className = `admin-toast ${type} show`;
    setTimeout(() => { t.className = 'admin-toast'; }, 2500);
}

function getJsText(key, dict) {
    const lang = localStorage.getItem('lang') || 'en';
    return dict[lang]?.[key] || dict.en[key] || key;
}
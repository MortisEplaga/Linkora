(function (window) {
    var STORAGE_KEY = 'linkora_icon_cache_v1';
    var ASSET_STORAGE_KEY = 'linkora_asset_cache_v1';
    var memoryCache = {};
    var assetCache = {};

    var ICONS = {
        avatarPlaceholder: '<svg width="{s}" height="{s}" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
        star: '<svg width="{s}" height="{s}" viewBox="0 0 24 24" fill="{fill}" stroke="#f5a623" stroke-width="2"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',
        telegram: '<svg width="{s}" height="{s}" viewBox="0 0 24 24" fill="#0088cc"><path d="M9.036 15.803 8.72 20.09c.46 0 .658-.198.9-.437l2.157-2.06 4.473 3.267c.82.454 1.408.215 1.63-.757l2.95-13.86h.001c.26-1.21-.437-1.684-1.235-1.39L2.32 9.62c-1.184.462-1.166 1.126-.202 1.427l4.792 1.496 11.13-7.02c.524-.34 1-.152.608.19"/></svg>',
        whatsapp: '<svg width="{s}" height="{s}" viewBox="0 0 24 24" fill="#25D366"><path d="M20.52 3.48A11.9 11.9 0 0 0 12.06 0C5.5 0 .17 5.32.17 11.88c0 2.1.55 4.13 1.6 5.94L0 24l6.32-1.65a11.9 11.9 0 0 0 5.72 1.46h.01c6.56 0 11.88-5.32 11.88-11.88 0-3.17-1.24-6.15-3.41-8.45zM12.06 21.7h-.01a9.85 9.85 0 0 1-5.02-1.37l-.36-.21-3.75.98 1-3.66-.24-.38a9.8 9.8 0 0 1-1.5-5.18c0-5.44 4.43-9.87 9.89-9.87 2.64 0 5.12 1.03 6.99 2.9a9.8 9.8 0 0 1 2.89 6.98c0 5.44-4.43 9.81-9.89 9.81z"/></svg>',
        website: '<svg width="{s}" height="{s}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>',
    };

    var ASSETS = {
        logo: '/img/Logo.svg',
        logoDark: '/img/DLogo.svg',
        favicon: '/img/MiniLogo.svg',
        noPhoto: '/img/no-photo.svg',
        searchIcon: '/img/search_for.svg',
        markerIcon: '/img/marker_for_adv.svg',
    };

    const CATEGORY_ICONS = {
        'transport': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-car-front-icon lucide-car-front"><path d="m21 8-2 2-1.5-3.7A2 2 0 0 0 15.646 5H8.4a2 2 0 0 0-1.903 1.257L5 10 3 8"/><path d="M7 14h.01"/><path d="M17 14h.01"/><rect width="18" height="8" x="3" y="10" rx="2"/><path d="M5 18v2"/><path d="M19 18v2"/></svg>`,
        'real estate': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-house-icon lucide-house"><path d="M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8"/><path d="M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>`,
        'job': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-pickaxe-icon lucide-pickaxe"><path d="m14 13-8.381 8.38a1 1 0 0 1-3.001-3L11 9.999"/><path d="M15.973 4.027A13 13 0 0 0 5.902 2.373c-1.398.342-1.092 2.158.277 2.601a19.9 19.9 0 0 1 5.822 3.024"/><path d="M16.001 11.999a19.9 19.9 0 0 1 3.024 5.824c.444 1.369 2.26 1.676 2.603.278A13 13 0 0 0 20 8.069"/><path d="M18.352 3.352a1.205 1.205 0 0 0-1.704 0l-5.296 5.296a1.205 1.205 0 0 0 0 1.704l2.296 2.296a1.205 1.205 0 0 0 1.704 0l5.296-5.296a1.205 1.205 0 0 0 0-1.704z"/></svg>`,
        'services': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-toolbox-icon lucide-toolbox"><path d="M16 12v4"/><path d="M16 6a2 2 0 0 1 1.414.586l4 4A2 2 0 0 1 22 12v7a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 .586-1.414l4-4A2 2 0 0 1 8 6z"/><path d="M16 6V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/><path d="M2 14h20"/><path d="M8 12v4"/></svg>`,
        'personal belongings': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-handbag-icon lucide-handbag"><path d="M2.048 18.566A2 2 0 0 0 4 21h16a2 2 0 0 0 1.952-2.434l-2-9A2 2 0 0 0 18 8H6a2 2 0 0 0-1.952 1.566z"/><path d="M8 11V6a4 4 0 0 1 8 0v5"/></svg>`,
        'for home and garden': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-sprout-icon lucide-sprout"><path d="M14 9.536V7a4 4 0 0 1 4-4h1.5a.5.5 0 0 1 .5.5V5a4 4 0 0 1-4 4 4 4 0 0 0-4 4c0 2 1 3 1 5a5 5 0 0 1-1 3"/><path d="M4 9a5 5 0 0 1 8 4 5 5 0 0 1-8-4"/><path d="M5 21h14"/></svg>`,
        'electronics': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-smartphone-charging-icon lucide-smartphone-charging"><rect width="14" height="20" x="5" y="2" rx="2" ry="2"/><path d="M12.667 8 10 12h4l-2.667 4"/></svg>`,
        'hobbies and recreation': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-palette-icon lucide-palette"><path d="M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z"/><circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/></svg>`,
        'animals': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-bone-icon lucide-bone"><path d="M17 10c.7-.7 1.69 0 2.5 0a2.5 2.5 0 1 0 0-5 .5.5 0 0 1-.5-.5 2.5 2.5 0 1 0-5 0c0 .81.7 1.8 0 2.5l-7 7c-.7.7-1.69 0-2.5 0a2.5 2.5 0 0 0 0 5c.28 0 .5.22.5.5a2.5 2.5 0 1 0 5 0c0-.81-.7-1.8 0-2.5Z"/></svg>`,
        'business and equipment': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-wallet-icon lucide-wallet"><path d="M19 7V4a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v4h-3a2 2 0 0 0 0 4h3a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1"/><path d="M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1v-4"/></svg>`,
        'community': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5.37 10.3a3.15 3.15 0 1 0 0-6.3a3.15 3.15 0 0 0 0 6.3m5.071 4.5a5.368 5.368 0 0 0-10.139 0m18.33 -4.5a3.15 3.15 0 1 0 0-6.3a3.15 3.15 0 0 0 0 6.3m5.069 4.5a5.368 5.368 0 0 0-10.139 0"/></svg>`,
    };
    function loadFromStorage() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            if (raw) memoryCache = JSON.parse(raw);
        } catch (e) { memoryCache = {}; }

        try {
            var rawAssets = localStorage.getItem(ASSET_STORAGE_KEY);
            if (rawAssets) assetCache = JSON.parse(rawAssets);
        } catch (e) { assetCache = {}; }
    }

    function saveIconsToStorage() {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(memoryCache)); } catch (e) { }
    }

    function saveAssetsToStorage() {
        try { localStorage.setItem(ASSET_STORAGE_KEY, JSON.stringify(assetCache)); } catch (e) { }
    }

    function buildKey(name, params) {
        return name + '|' + (params.s || 20) + '|' + (params.fill || '');
    }

    function getIcon(name, params) {
        params = params || {};
        var key = buildKey(name, params);
        if (memoryCache[key]) return memoryCache[key];

        var template = ICONS[name];
        if (!template) return '';

        var svg = template
            .replace(/\{s\}/g, params.s || 20)
            .replace(/\{fill\}/g, params.fill || 'none');

        memoryCache[key] = svg;
        saveIconsToStorage();
        return svg;
    }

    function blobToDataUrl(blob) {
        return new Promise(function (resolve, reject) {
            var reader = new FileReader();
            reader.onloadend = function () { resolve(reader.result); };
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });
    }
    function getAsset(name) {
        var url = ASSETS[name];
        if (!url) return Promise.reject(new Error('Unknown asset: ' + name));

        if (assetCache[name]) return Promise.resolve(assetCache[name]);

        return fetch(url)
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to fetch asset: ' + url);
                return res.blob();
            })
            .then(blobToDataUrl)
            .then(function (dataUrl) {
                assetCache[name] = dataUrl;
                saveAssetsToStorage();
                return dataUrl;
            })
            .catch(function () {
                return url;
            });
    }
    function applyAsset(el, name) {
        if (!el) return;
        var attr = el.tagName === 'LINK' ? 'href' : 'src';
        getAsset(name).then(function (url) { el[attr] = url; });
    }
    function getCategoryIcon(nameEn) {
        if (!nameEn) return null;
        var key = nameEn.toLowerCase().trim();
        if (CATEGORY_ICONS[key]) return CATEGORY_ICONS[key];
        for (var k in CATEGORY_ICONS)
            if (key.indexOf(k) !== -1 || k.indexOf(key) !== -1) return CATEGORY_ICONS[k];
        return null;
    }
    function clearCache() {
        memoryCache = {};
        assetCache = {};
        try {
            localStorage.removeItem(STORAGE_KEY);
            localStorage.removeItem(ASSET_STORAGE_KEY);
        } catch (e) { }
    }

    loadFromStorage();

    window.Icons = {
        get: getIcon,
        getAsset: getAsset,
        applyAsset: applyAsset,
        clearCache: clearCache,
        getCategoryIcon: getCategoryIcon,
        names: Object.keys(ICONS),
        assetNames: Object.keys(ASSETS)
    };
})(window);
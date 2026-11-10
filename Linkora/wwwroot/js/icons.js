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
        faviconDark: '/img/MiniLogoDark.svg',
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
        'agriculture': `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 64 64" fill="currentColor"><path d="M 58.818359 5.921875 C 58.39842 5.9081437 57.994119 5.924701 57.605469 5.9667969 C 55.273568 6.2193721 53.509882 7.4005867 52.347656 8.6425781 C 50.798022 10.298567 50.162109 12.113281 50.162109 12.113281 A 1.0001 1.0001 0 1 0 52.046875 12.78125 C 52.046875 12.78125 52.566025 11.335527 53.806641 10.009766 C 54.898994 8.8424412 56.480818 7.8733911 58.833984 7.984375 C 58.682881 10.213502 58.144837 11.895159 57.306641 13.019531 C 56.340582 14.31542 55.05871 15.072298 53.722656 15.519531 C 51.646215 16.214603 49.901118 16.105608 49.054688 16.021484 C 49.075657 14.006486 48.740567 10.704229 46.886719 7.8359375 A 1.0001 1.0001 0 0 0 45.675781 7.4492188 C 41.05416 9.2974387 39.362151 12.780916 39.078125 15.855469 C 38.794099 18.930021 39.759802 21.738784 39.960938 22.476562 A 1.0001 1.0001 0 0 0 41.535156 23.005859 C 42.816763 22.019499 44.836839 21.154287 46.923828 21.052734 C 48.755975 20.963584 50.592702 21.577295 52.265625 22.994141 C 50.518172 25.306839 47.761058 26.101131 44.871094 26.083984 C 41.93378 26.066554 39.15884 25.203804 37.9375 24.650391 C 38.02354 23.455785 38.208602 18.685623 33.642578 15.107422 A 1.0001 1.0001 0 0 0 32.376953 15.132812 C 29.540042 17.555805 28.328204 20.682545 28.113281 23.685547 C 27.898359 26.688549 28.629365 29.584019 29.623047 31.8125 A 1.0001 1.0001 0 0 0 31.167969 32.179688 C 32.416839 31.163432 34.44554 30.190033 36.767578 30.050781 C 38.791166 29.929427 41.061993 30.611624 43.378906 32.244141 C 41.039385 34.421351 37.548518 35.05994 34.318359 34.958984 C 31.287494 34.864254 29.086995 34.303949 28.310547 34.09375 C 28.328957 33.488356 28.415978 32.291473 28.017578 30.214844 C 27.530517 27.676075 26.429543 24.634553 23.679688 23.179688 A 1.0001 1.0001 0 0 0 22.605469 23.267578 C 18.945971 26.053134 17.962783 29.9924 18.199219 33.486328 C 18.435654 36.980257 19.783722 40.09758 20.822266 41.828125 A 1.0001 1.0001 0 0 0 22.349609 42.056641 C 24.413381 40.194841 26.745218 39.123659 29.203125 39.039062 C 31.387756 38.963873 33.728438 39.827962 36.134766 41.587891 C 33.816795 44.441493 30.165145 45.129665 26.712891 44.927734 C 23.300061 44.72811 20.660242 43.829001 20.123047 43.640625 C 19.896456 42.738372 18.451646 37.356999 13.201172 33.642578 A 1.0001 1.0001 0 0 0 11.806641 33.884766 C 7.0268672 40.688631 9.2301799 47.374366 12.927734 50.839844 C 10.830789 53.425518 9.3144531 55.882813 9.3144531 55.882812 A 1.0001 1.0001 0 1 0 11.068359 56.841797 C 11.068359 56.841797 12.513014 54.51606 14.472656 52.099609 C 16.401712 53.43635 18.849099 54.653864 21.568359 54.902344 C 24.699507 55.188461 28.093643 54.087944 30.369141 50.587891 A 1.0001 1.0001 0 0 0 29.917969 49.121094 C 27.842786 48.251059 24.900817 47.389016 22.009766 47.125 C 19.118714 46.860984 16.180588 47.14006 14.351562 49.048828 C 14.275213 49.128488 14.242975 49.19912 14.167969 49.279297 C 11.369145 46.594569 9.5256624 41.64453 12.855469 36.046875 C 17.087713 39.600626 18.398438 44.679688 18.398438 44.679688 A 1.0001 1.0001 0 0 0 19.025391 45.396484 C 19.025391 45.396484 22.46946 46.684428 26.595703 46.925781 C 30.721946 47.167135 35.771744 46.327406 38.519531 41.888672 A 1.0001 1.0001 0 0 0 38.310547 40.595703 C 35.285227 38.061459 32.131859 36.937862 29.134766 37.041016 C 26.567782 37.129366 24.215375 38.244251 22.105469 39.849609 C 21.291297 38.256964 20.370313 35.93763 20.195312 33.351562 C 20.003214 30.512829 20.712886 27.662478 23.294922 25.380859 C 24.806703 26.525673 25.667924 28.575822 26.054688 30.591797 C 26.489127 32.856278 26.378906 34.841797 26.378906 34.841797 A 1.0001 1.0001 0 0 0 27.089844 35.857422 C 27.089844 35.857422 30.331542 36.836333 34.255859 36.958984 C 38.180177 37.081636 42.969801 36.370261 45.832031 32.689453 A 1.0001 1.0001 0 0 0 45.691406 31.314453 C 42.657044 28.72815 39.44565 27.886939 36.648438 28.054688 C 34.428776 28.1878 32.607205 28.987238 31.109375 29.925781 C 30.446568 28.112593 29.954093 25.997802 30.109375 23.828125 C 30.272706 21.545989 31.22294 19.356263 33.066406 17.443359 C 36.44057 20.775949 36.007812 25.244141 36.007812 25.244141 A 1.0001 1.0001 0 0 0 36.525391 26.251953 C 38.032098 27.063118 41.270502 28.062691 44.859375 28.083984 C 48.448248 28.105274 52.457476 27.01049 54.576172 23.28125 A 1.0001 1.0001 0 0 0 54.431641 22.097656 C 52.188072 19.741415 49.383683 18.93024 46.826172 19.054688 C 44.825995 19.152017 43.072458 19.854334 41.580078 20.720703 C 41.259141 19.519181 40.895844 17.927678 41.070312 16.039062 C 41.286701 13.696675 42.343495 11.36857 45.599609 9.7558594 C 46.914528 12.373536 47.218048 15.836767 47.126953 16.871094 A 1.0001 1.0001 0 0 0 47.957031 17.945312 C 47.957031 17.945312 51.11303 18.502049 54.357422 17.416016 C 55.979618 16.872999 57.657528 15.895143 58.910156 14.214844 C 60.162785 12.534545 60.957031 10.18653 60.957031 7.0429688 A 1.0001 1.0001 0 0 0 60.125 6.0566406 C 59.673877 5.9796263 59.238299 5.9356063 58.818359 5.921875 z M 19.939453 49.058594 C 20.540941 49.036807 21.174575 49.057504 21.828125 49.117188 C 23.932837 49.309393 26.051758 49.896892 27.826172 50.523438 C 26.066487 52.519855 23.971443 53.113147 21.75 52.910156 C 19.548908 52.709025 17.401443 51.662028 15.71875 50.529297 C 15.75057 50.495717 15.764805 50.465091 15.796875 50.431641 C 16.627106 49.565214 18.13499 49.123953 19.939453 49.058594 z"/></svg>`,
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
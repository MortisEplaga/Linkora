(function (window) {
    const MOBILE_BREAKPOINT = 768;

    function setView(v) {
        localStorage.setItem('listingView', v);
        const c = document.getElementById('listingContainer');
        const mapWrap = document.getElementById('listingMap');

        document.getElementById('btnGrid')?.classList.toggle('active', v === 'grid');
        document.getElementById('btnList')?.classList.toggle('active', v === 'list');
        document.getElementById('btnMap')?.classList.toggle('active', v === 'map');

        if (v === 'map' && mapWrap) {
            c.style.display = 'none';
            mapWrap.style.display = 'block';
            if (typeof window.ensureMapsLoaded === 'function') {
                window.ensureMapsLoaded().then(window.loadProductsMap);
            }
        } else {
            c.className = v === 'grid' ? 'listing-grid' : 'listing-list';
            c.style.display = '';
            if (mapWrap) mapWrap.style.display = 'none';
        }
    }

    function initialView() {
        let saved = localStorage.getItem('listingView') || 'grid';
        if (window.innerWidth <= MOBILE_BREAKPOINT && saved === 'list') {
            saved = 'grid';
            localStorage.setItem('listingView', 'grid');
        }
        return saved;
    }

    function toggleSort(e) {
        e.stopPropagation();
        document.getElementById('sortDrop')?.classList.toggle('open');
    }

    function setSortLabel() {
        const sortLabel = document.getElementById('sortLabel');
        if (!sortLabel) return;
        const lang = localStorage.getItem('lang') || 'en';
        const dict = (typeof TRANSLATIONS !== 'undefined' && (TRANSLATIONS[lang] || TRANSLATIONS['en'])) || {};
        const sortMap = { new: dict.sort_newest, cheap: dict.sort_cheapest, expensive: dict.sort_expensive };
        sortLabel.textContent = sortMap[sortLabel.dataset.sort || 'new'] || dict.sort_newest || 'Newest first';
    }

    document.addEventListener('click', e => {
        if (!e.target.closest('.listing-sort-wrap')) {
            document.getElementById('sortDrop')?.classList.remove('open');
        }
    });

    window.addEventListener('resize', () => {
        if (window.innerWidth <= MOBILE_BREAKPOINT && localStorage.getItem('listingView') === 'list') {
            setView('grid');
        }
    });

    window.ListingView = { setView, initialView, toggleSort, setSortLabel };
})(window);
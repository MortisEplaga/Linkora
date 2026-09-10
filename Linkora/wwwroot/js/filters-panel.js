(function (window) {
    let overlayEl = null;

    function ensureOverlay() {
        if (overlayEl) return overlayEl;
        overlayEl = document.createElement('div');
        overlayEl.className = 'catmenu-overlay';
        overlayEl.addEventListener('click', closeFilters);
        document.body.appendChild(overlayEl);
        return overlayEl;
    }

    function openFilters() {
        const panel = document.getElementById('filterForm');
        if (!panel) return;
        panel.classList.add('filters-open');
        ensureOverlay().classList.add('catmenu-overlay-open');
    }

    function closeFilters() {
        document.getElementById('filterForm')?.classList.remove('filters-open');
        overlayEl?.classList.remove('catmenu-overlay-open');
    }

    window.FiltersPanel = { open: openFilters, close: closeFilters };
})(window);
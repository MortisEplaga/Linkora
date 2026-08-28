let allCategories = [];
let categoryMenuMode = 'navigate';
let categoryMenuCallback = null;
let tooltipTimer = null;
let tooltipElement = null;
let activeItem = null;

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
function getIcon(nameEn) {
    if (!nameEn) return null;
    const key = nameEn.toLowerCase().trim();
    if (CATEGORY_ICONS[key]) return CATEGORY_ICONS[key];
    for (const [k, icon] of Object.entries(CATEGORY_ICONS))
        if (key.includes(k) || k.includes(key)) return icon;
    return null;
}
function updateColumnsVisibility() {
    const container = document.getElementById('catMenuColumns');
    if (!container) return;

    const columns = Array.from(container.children);

    const availableWidth = window.innerWidth;
    let totalWidth = 0;

    for (let i = columns.length - 1; i >= 0; i--) {
        const colWidth = 240; 
        totalWidth += colWidth;

        if (totalWidth > availableWidth && i < columns.length - 1) columns[i].classList.add('catmenu-col-hidden');
        else columns[i].classList.remove('catmenu-col-hidden');
    }
}

window.addEventListener('resize', () => {
    updateColumnsVisibility();
});
function openCatMenu(mode = 'navigate', callback = null) {
    categoryMenuMode = mode;
    categoryMenuCallback = callback;

    if (!allCategories.length) {
        fetch('/Category/All')
            .then(r => r.json())
            .then(data => {
                allCategories = data;
                renderCol(0, 3711);
                showMenu();
            });
    } else {
        renderCol(0, 3711);
        showMenu();
    }
}
function showMenu() {
    document.getElementById('catMenu').classList.add('catmenu-open');
    document.getElementById('catMenuOverlay').classList.add('catmenu-overlay-open');
    setTimeout(updateColumnsVisibility, 50);
}
function closeCatMenu() {
    document.getElementById('catMenu').classList.remove('catmenu-open');
    document.getElementById('catMenuOverlay').classList.remove('catmenu-overlay-open');
    clearTimeout(tooltipTimer);
    removeTooltip();
    activeItem = null;
}
function renderCol(colIndex, parentId) {
    const container = document.getElementById('catMenuColumns');

    while (container.children.length > colIndex)
        container.removeChild(container.lastChild);

    const items = allCategories.filter(c => c.parentId === parentId);
    if (!items.length) {
        updateColumnsVisibility();
        return;
    }

    const isTopLevel = (parentId === 3711);
    const col = document.createElement('div');
    col.className = 'catmenu-col';

    if (colIndex > 0) {
        const backItem = document.createElement('div');
        backItem.className = 'catmenu-item catmenu-back-row';

        const backLink = document.createElement('a');
        backLink.className = 'catmenu-link';
        backLink.href = 'javascript:void(0)';

        const backIcon = document.createElement('span');
        backIcon.className = 'catmenu-icon';
        backIcon.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m12 19-7-7 7-7"/><path d="M19 12H5"/></svg>`;

        const backLabel = document.createElement('span');
        backLabel.className = 'catmenu-label';
        backLabel.textContent = translate('catmenu_back');
        backLink.appendChild(backIcon);
        backLink.appendChild(backLabel);
        backItem.appendChild(backLink);

        backItem.addEventListener('click', (e) => {
            e.stopPropagation();
            clearTimeout(tooltipTimer);
            removeTooltip();
            activeItem = null;
            container.removeChild(col);
            updateColumnsVisibility();
        });

        col.appendChild(backItem);
    }

    items.forEach(item => {
        const hasChildren = allCategories.some(c => c.parentId === item.id);
        const icon = isTopLevel ? getIcon(item.nameEn || item.name) : null;

        const el = document.createElement('div');
        el.className = 'catmenu-item';

        const link = document.createElement('a');
        link.className = 'catmenu-link';
        link.href = 'javascript:void(0)';

        if (icon) {
            const iconSpan = document.createElement('span');
            iconSpan.className = 'catmenu-icon';
            iconSpan.innerHTML = icon;
            link.appendChild(iconSpan);
        }

        const labelSpan = document.createElement('span');
        labelSpan.className = 'catmenu-label';
        labelSpan.textContent = item.name;
        link.appendChild(labelSpan);

        el.appendChild(link);

        if (hasChildren) {
            const arrowSpan = document.createElement('span');
            arrowSpan.className = 'catmenu-arrow';
            arrowSpan.textContent = '›';
            el.appendChild(arrowSpan);
        }

        el.addEventListener('click', function (e) {
            e.stopPropagation();

            const isAlreadyActive = el.classList.contains('catmenu-item-active');

            if (isAlreadyActive) {
                clearTimeout(tooltipTimer);
                removeTooltip();

                if (categoryMenuMode === 'navigate') {
                    window.location.href = '/Category/Index/' + item.id;
                    closeCatMenu();
                } else if (categoryMenuMode === 'select') {
                    if (typeof categoryMenuCallback === 'function')
                        categoryMenuCallback({ id: item.id, name: item.name });
                    closeCatMenu();
                }
                return;
            }

            col.querySelectorAll('.catmenu-item').forEach(i => i.classList.remove('catmenu-item-active'));
            el.classList.add('catmenu-item-active');
            activeItem = el;

            clearTimeout(tooltipTimer);
            removeTooltip();

            tooltipTimer = setTimeout(() => {
                if (activeItem === el && el.classList.contains('catmenu-item-active')) {
                    showTooltip(el, translate('catmenu_confirm_hint'));
                }
            }, 1000);

            if (hasChildren) {
                renderCol(colIndex + 1, item.id);
            } else {
                while (container.children.length > colIndex + 1)
                    container.removeChild(container.lastChild);
                updateColumnsVisibility();
            }
        });
        col.appendChild(el);
    });

    container.appendChild(col);
    updateColumnsVisibility();
}

document.getElementById('catMenuColumns').addEventListener('mouseenter', function (e) {
    const item = e.target.closest('.catmenu-item');
    if (!item) return;

    if (activeItem && item !== activeItem) {
        clearTimeout(tooltipTimer);
        removeTooltip();
        activeItem = null;
    }
}, true);
function showTooltip(element, text) {
    removeTooltip();

    const rect = element.getBoundingClientRect();
    tooltipElement = document.createElement('div');
    tooltipElement.className = 'catmenu-tooltip';
    tooltipElement.textContent = text;
    tooltipElement.style.left = (rect.left + rect.width / 2) + 'px';
    tooltipElement.style.top = (rect.bottom + 8) + 'px';
    document.body.appendChild(tooltipElement);
}
function removeTooltip() {
    if (tooltipElement) {
        tooltipElement.remove();
        tooltipElement = null;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const catBtn = document.getElementById('categoriesBtn');
    if (catBtn) {
        catBtn.addEventListener('click', () => {
            openCatMenu('navigate', null);
        });
    }
});
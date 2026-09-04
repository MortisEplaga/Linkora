let allCategories = [];
let categoryMenuMode = 'navigate';
let categoryMenuCallback = null;
let tooltipTimer = null;
let tooltipElement = null;
let activeItem = null;
function getIcon(nameEn) {
    return Icons.getCategoryIcon(nameEn);
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
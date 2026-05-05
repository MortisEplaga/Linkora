let allCategories = [];

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
};

function getIcon(nameEn) {
    if (!nameEn) return null;
    const key = nameEn.toLowerCase().trim();
    if (CATEGORY_ICONS[key]) return CATEGORY_ICONS[key];
    for (const [k, icon] of Object.entries(CATEGORY_ICONS)) {
        if (key.includes(k) || k.includes(key)) return icon;
    }
    return null;
}

async function openCatMenu() {
    if (!allCategories.length) {
        const res = await fetch('/Category/All');
        allCategories = await res.json();
    }
    renderCol(0, 3711);
    document.getElementById('catMenu').classList.add('catmenu-open');
    document.getElementById('catMenuOverlay').classList.add('catmenu-overlay-open');
}

function closeCatMenu() {
    document.getElementById('catMenu').classList.remove('catmenu-open');
    document.getElementById('catMenuOverlay').classList.remove('catmenu-overlay-open');
}

function renderCol(colIndex, parentId) {
    const container = document.getElementById('catMenuColumns');

    while (container.children.length > colIndex) {
        container.removeChild(container.lastChild);
    }

    const items = allCategories.filter(c => c.parentId === parentId);
    if (!items.length) return;

    const isTopLevel = (parentId === 3711);

    const col = document.createElement('div');
    col.className = 'catmenu-col';

    items.forEach(item => {
        const hasChildren = allCategories.some(c => c.parentId === item.id);
        const icon = isTopLevel ? getIcon(item.nameEn || item.name) : null;

        const el = document.createElement('div');
        el.className = 'catmenu-item';
        el.innerHTML = `
            <a href="/Category/Index/${item.id}" class="catmenu-link">
                ${icon ? `<span class="catmenu-icon">${icon}</span>` : ''}
                <span class="catmenu-label">${item.name}</span>
            </a>
            ${hasChildren ? '<span class="catmenu-arrow">›</span>' : ''}
        `;

        if (hasChildren) {
            el.addEventListener('mouseenter', () => {
                col.querySelectorAll('.catmenu-item').forEach(i => i.classList.remove('catmenu-item-active'));
                el.classList.add('catmenu-item-active');
                renderCol(colIndex + 1, item.id);
            });
        }

        el.querySelector('.catmenu-link').addEventListener('click', closeCatMenu);
        col.appendChild(el);
    });

    container.appendChild(col);
}
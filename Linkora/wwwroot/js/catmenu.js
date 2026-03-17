let allCategories = [];

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

    // Удалить все колонки начиная с colIndex
    while (container.children.length > colIndex) {
        container.removeChild(container.lastChild);
    }

    const items = allCategories.filter(c => c.parentId === parentId);
    if (!items.length) return;

    const col = document.createElement('div');
    col.className = 'catmenu-col';

    items.forEach(item => {
        const hasChildren = allCategories.some(c => c.parentId === item.id);
        const el = document.createElement('div');
        el.className = 'catmenu-item';
        el.innerHTML = `
            <a href="/Category/Index/${item.id}" class="catmenu-link">${item.name}</a>
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
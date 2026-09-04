const MAX_BYTES = 52_428_800; // 50 MB
let selectedFiles = [];
let citiesCache = null;

const DURATION_OPTIONS = [1, 3, 7, 14, 30];
const DEFAULT_DURATION = 30;

let pendingPromoType = null;
let pendingPromoBtn = null;
let rulesScrolled = false;
let policyScrolled = false;

function collectParamValues() {
    const params = {};

    document.querySelectorAll('#createParams input[data-param], #createParams textarea[data-param]').forEach(el => {
        if (el.closest('.param-options')) return;
        if (el.type === 'checkbox') {
            if (el.checked) params[el.dataset.param] = "true";
        } else if (el.value) {
            params[el.dataset.param] = el.value;
        }
    });

    document.querySelectorAll('.param-pills[data-param]').forEach(el => {
        const active = el.querySelector('.param-pill-active');
        if (active) params[el.dataset.param] = active.dataset.id;
    });

    document.querySelectorAll('.param-swatches[data-param]').forEach(el => {
        const active = el.querySelector('.param-swatch-row.param-swatch-active');
        if (active) params[el.dataset.param] = active.dataset.id;
    });

    document.querySelectorAll('.param-options[data-param]').forEach(el => {
        const vals = [...el.querySelectorAll('input[type=checkbox]:checked')].map(cb => cb.dataset.id);
        if (vals.length) params[el.dataset.param] = vals.join(',');
    });

    document.querySelectorAll('.free-text-select[data-param]').forEach(wrap => {
        const hidden = wrap.querySelector('.free-text-select-id');
        if (hidden && hidden.value) params[wrap.dataset.param] = hidden.value;
    });

    const priceInput = document.getElementById('adPrice');
    if (priceInput?.dataset.paramId && priceInput.value) {
        params[priceInput.dataset.paramId] = priceInput.value;
    }

    return params;
}

function openCatMenuForEdit() {
    openCatMenu('select', function (category) {
        const preservedValues = collectParamValues();
        document.getElementById('selectedCategoryId').value = category.id;
        document.getElementById('catSelectorText').textContent = category.name;
        loadParams(category.id, preservedValues);
    });
}

async function loadParams(categoryId, existingValues = {}) {
    const res = await fetch(`/Product/Parameters?categoryId=${categoryId}`);
    const data = await res.json();
    const params = data.parameters;
    const hasPrice = data.hasPrice;
    const container = document.getElementById('createParams');
    const section = document.getElementById('paramsSection');
    container.innerHTML = '';
    const priceWrap = document.getElementById('priceFieldWrap');
    const priceInput = document.getElementById('adPrice');
    const qtyWrap = document.getElementById('qtyFieldWrap');

    if (hasPrice) {
        priceInput.value = existingValues.price ?? '';
        priceWrap.style.display = '';
        qtyWrap.style.gridColumn = '';
    } else {
        priceWrap.style.display = 'none';
        priceInput.value = '';
        qtyWrap.style.gridColumn = '1 / -1';
    }

    if (!params.length) { section.style.display = 'none'; return; }
    section.style.display = 'block';

    function estimateHeight(p) {
        const base = 38;
        if (p.type === 3) return base + 32;
        if (p.type === 2) return base + 44;
        if (p.type === 5) return base + 52;
        if (p.type === 4) return base + Math.min(p.options.length, 5) * 32 + (p.options.length > 5 ? 32 : 0);
        return base + 40;
    }

    const col1 = [], col2 = [];
    let h1 = 0, h2 = 0;
    params.forEach(p => {
        const h = estimateHeight(p);
        if (h1 <= h2) { col1.push(p); h1 += h; }
        else { col2.push(p); h2 += h; }
    });

    function buildCol(items, totalOtherHeight) {
        const div = document.createElement('div');
        div.className = 'create-params-col';
        div.style.gap = '0';
        const myHeight = items.reduce((s, p) => s + estimateHeight(p), 0);
        const blocks = items.map(p => buildBlock(p, existingValues));
        blocks.forEach(b => div.appendChild(b));
        if (items.length > 1 && myHeight < totalOtherHeight) {
            const extraPerBlock = (totalOtherHeight - myHeight) / items.length;
            blocks.forEach(b => { b.style.marginBottom = `${24 + extraPerBlock}px`; });
        }
        return div;
    }

    container.appendChild(buildCol(col1, h2));
    container.appendChild(buildCol(col2, h1));

    if (typeof updateTranslations === 'function') {
        updateTranslations();
    }
}

function buildBlock(p, existingValues = {}) {
    const val = existingValues[p.id] ?? '';
    const block = document.createElement('div');
    block.className = 'create-param-block';
    block.innerHTML = `<div class="param-title">${p.name}</div>`;

    if (p.type === 3) {
        const lang = localStorage.getItem('lang') || 'en';
        const dict = TRANSLATIONS[lang] || TRANSLATIONS['en'];
        const yesText = dict['yes'] || 'Yes';

        block.innerHTML += `
            <div class="param-check-row">
                <span>${yesText}</span>
                <input type="checkbox" class="param-check-input" data-param="${p.id}"
                       ${val && String(val).toLowerCase() === 'true' ? 'checked' : ''} />
            </div>`;
    } else if (p.type === 2) {
        const pills = p.options.map(opt =>
            `<label class="param-pill ${String(val) === String(opt.id) ? 'param-pill-active' : ''}"
                    data-id="${opt.id}"
                    onclick="pillClick(this)">${opt.text}</label>`
        ).join('');
        block.innerHTML += `<div class="param-pills" data-param="${p.id}">${pills}</div>`;
    } else if (p.type === 8) {
        var initialText = '';
        if (val) {
            var found = (p.options || []).find(function (o) { return String(o.id) === String(val); });
            if (found) initialText = found.text;
        }
        block.innerHTML += FreeTextSelect.buildHtml(p.id, initialText, val || '', p.options || []);
    } else if (p.type === 6) {
        const selectedVal = val != null ? String(val) : '';
        const FALLBACK_VISIBLE = 5;

        const visibleIds = new Set();
        if (p.colorOptions.some(o => o.isMain)) {
            p.colorOptions.forEach(o => { if (o.isMain) visibleIds.add(String(o.id)); });
        } else {
            p.colorOptions.slice(0, FALLBACK_VISIBLE).forEach(o => visibleIds.add(String(o.id)));
        }
        if (selectedVal) visibleIds.add(selectedVal);

        const rows = p.colorOptions.map(opt => {
            const isActive = selectedVal === String(opt.id);
            const isHidden = !visibleIds.has(String(opt.id));
            return `<label class="param-swatch-row ${isActive ? 'param-swatch-active' : ''}"
                      data-id="${opt.id}"
                      ${isHidden ? 'style="display:none"' : ''}
                      onclick="swatchClick(this)">
            <span class="param-swatch-circle" style="background-color:${opt.hex}"></span>
            <span class="param-swatch-name">${opt.name}</span>
        </label>`;
        }).join('');
        block.innerHTML += `<div class="param-swatches" data-param="${p.id}">${rows}</div>`;

        const hiddenCount = p.colorOptions.filter(o => !visibleIds.has(String(o.id))).length;
        if (hiddenCount > 0) {
            const btn = document.createElement('button');
            btn.className = 'param-show-more';
            btn.setAttribute('data-i18n', 'more_btn');
            btn.setAttribute('data-count', hiddenCount);
            btn.onclick = () => {
                block.querySelectorAll('.param-swatch-row[style="display:none"]')
                    .forEach(el => el.style.display = '');
                btn.style.display = 'none';
            };
            block.appendChild(btn);
            translateDynamicElement(btn);
        }
    } else if (p.type === 4) {
        const selected = val ? String(val).split(',').map(s => s.trim()) : [];
        const LIMIT = 5;
        const checksHtml = p.options.map((opt, i) =>
            `<label class="param-option" ${i >= LIMIT ? 'style="display:none"' : ''}>
                <input type="checkbox" data-param="${p.id}" data-id="${opt.id}"
                       ${selected.includes(String(opt.id)) ? 'checked' : ''} /> ${opt.text}
            </label>`
        ).join('');
        block.innerHTML += `<div class="param-options" data-param="${p.id}">${checksHtml}</div>`;
        if (p.options.length > LIMIT) {
            const btn = document.createElement('button');
            btn.className = 'param-show-more';
            btn.setAttribute('data-i18n', 'more_btn');
            btn.setAttribute('data-count', p.options.length - LIMIT);
            btn.onclick = () => {
                block.querySelectorAll('.param-option[style="display:none"]')
                    .forEach(el => el.style.display = '');
                btn.style.display = 'none';
            };
            block.appendChild(btn);
            translateDynamicElement(btn);
        }
    } else if (p.type === 5) {
        block.innerHTML += `
            <input type="number" class="param-range-input"
                   placeholder="${p.min}–${p.max}" min="${p.min}" max="${p.max}" step="${p.step}"
                   data-param="${p.id}" value="${val}" />`;
    } else if (p.type === 7) {
        block.innerHTML += `
            <input type="text" class="create-input param-text-input"
                   data-param="${p.id}" value="${val}"
                   placeholder="" />`;
    }
    return block;
}

function pillClick(pill) {
    const pills = pill.closest('.param-pills');
    pills.querySelectorAll('.param-pill').forEach(p => p.classList.remove('param-pill-active'));
    pill.classList.add('param-pill-active');
    if (window.ParamRulesEngine) {
        window.ParamRulesEngine.triggerUpdate();
    }
}

function swatchClick(row) {
    const wrap = row.closest('.param-swatches');
    wrap.querySelectorAll('.param-swatch-row').forEach(r => r.classList.remove('param-swatch-active'));
    row.classList.add('param-swatch-active');
    if (window.ParamRulesEngine) ParamRulesEngine.triggerUpdate();
}

async function addressSearch(input, type) {
    const val = input.value.toLowerCase();

    if (type === 'city') {
        if (!citiesCache) {
            const res = await fetch('/Product/Cities');
            citiesCache = await res.json();
        }
        const filtered = citiesCache.filter(c => c.name.toLowerCase().includes(val));
        filtered.sort((a, b) => {
            const an = a.name.toLowerCase().startsWith(val) ? 0 : 1;
            const bn = b.name.toLowerCase().startsWith(val) ? 0 : 1;
            return an - bn || a.name.localeCompare(b.name);
        });
        showDrop('adCityDrop', filtered, (item) => {
            document.getElementById('adCity').value = item.name;
            document.getElementById('adCityId').value = item.id;
            hideDrop('adCityDrop');
            document.getElementById('adStreet').value = '';
            document.getElementById('adStreetId').value = '';
            document.getElementById('adHouse').value = '';
            document.getElementById('adHouseId').value = '';
            document.getElementById('streetField').style.display = 'block';
            document.getElementById('houseField').style.display = 'none';
        });
    } else if (type === 'street') {
        const cityId = document.getElementById('adCityId').value;
        if (!cityId) return;
        const res = await fetch(`/Product/Streets?cityId=${cityId}`);
        const streets = await res.json();
        const filteredS = streets.filter(s => s.name.toLowerCase().includes(val));
        filteredS.sort((a, b) => {
            const an = a.name.toLowerCase().startsWith(val) ? 0 : 1;
            const bn = b.name.toLowerCase().startsWith(val) ? 0 : 1;
            return an - bn || a.name.localeCompare(b.name);
        });
        showDrop('adStreetDrop', filteredS, (item) => {
            document.getElementById('adStreet').value = item.name;
            document.getElementById('adStreetId').value = item.id;
            hideDrop('adStreetDrop');
            document.getElementById('adHouse').value = '';
            document.getElementById('adHouseId').value = '';
            document.getElementById('houseField').style.display = 'block';
        });
    } else if (type === 'house') {
        const streetId = document.getElementById('adStreetId').value;
        if (!streetId) return;
        const res = await fetch(`/Product/Houses?streetId=${streetId}`);
        const houses = await res.json();
        const filteredH = houses.filter(h => h.name.toLowerCase().includes(val));
        filteredH.sort((a, b) => {
            const an = a.name.toLowerCase().startsWith(val) ? 0 : 1;
            const bn = b.name.toLowerCase().startsWith(val) ? 0 : 1;
            return an - bn || a.name.localeCompare(b.name);
        });
        showDrop('adHouseDrop', filteredH, (item) => {
            document.getElementById('adHouse').value = item.name;
            document.getElementById('adHouseId').value = item.id;
            hideDrop('adHouseDrop');
        });
    }
}

function showDrop(dropId, items, onSelect) {
    const drop = document.getElementById(dropId);
    drop.innerHTML = '';
    if (!items.length) { drop.style.display = 'none'; return; }
    items.slice(0, 10).forEach(item => {
        const el = document.createElement('div');
        el.className = 'create-drop-item';
        el.textContent = item.name;
        el.onclick = () => onSelect(item);
        drop.appendChild(el);
    });
    drop.style.display = 'block';
}

function hideDrop(dropId) {
    document.getElementById(dropId).style.display = 'none';
}

document.addEventListener('click', e => {
    ['adCityDrop', 'adStreetDrop', 'adHouseDrop'].forEach(id => {
        const drop = document.getElementById(id);
        if (drop && !drop.contains(e.target)) drop.style.display = 'none';
    });
});

function previewPhotos(input) {
    Array.from(input.files).forEach(file => {
        if (!selectedFiles.find(f => f.name === file.name && f.size === file.size))
            selectedFiles.push(file);
    });
    input.value = '';
    renderPreviews();
}

function renderPreviews() {
    const preview = document.getElementById('photosPreview');
    preview.innerHTML = '';
    let total = 0;

    selectedFiles.forEach((file, idx) => {
        total += file.size;
        const wrap = document.createElement('div');
        wrap.className = 'create-photo-thumb';
        const isVideo = file.type.startsWith('video/');

        if (isVideo) {
            const url = URL.createObjectURL(file);
            wrap.innerHTML = `
                <video src="${url}" class="thumb-video" muted playsinline
                       onmouseenter="this.play()" onmouseleave="this.pause();this.currentTime=0"></video>
                <div class="thumb-video-badge">▶</div>
                <button type="button" onclick="removeFile(${idx})">✕</button>`;
        } else {
            const reader = new FileReader();
            reader.onload = e => {
                wrap.innerHTML = `
                    <img src="${e.target.result}" />
                    <button type="button" onclick="removeFile(${idx})">✕</button>`;
            };
            reader.readAsDataURL(file);
        }
        preview.appendChild(wrap);
    });

    updateSizeBar(total);
}

function removeFile(idx) {
    selectedFiles.splice(idx, 1);
    renderPreviews();
}

function updateSizeBar(totalBytes) {
    const bar = document.getElementById('sizeBar');
    const fill = document.getElementById('sizeFill');
    const label = document.getElementById('sizeLabel');
    if (selectedFiles.length === 0) { bar.style.display = 'none'; return; }
    bar.style.display = 'block';
    const pct = Math.min(totalBytes / MAX_BYTES * 100, 100);
    fill.style.width = pct + '%';
    fill.style.background = pct > 90 ? '#e53e3e' : pct > 70 ? '#f6a623' : '#00b0a3';
    label.textContent = (totalBytes / 1_048_576).toFixed(1) + ' / 50 MB';
}

function selectPubDuration(days, btn) {
    document.querySelectorAll('#pubDurationPills .duration-pill')
        .forEach(p => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('pubDurationValue').value = days;
}

function applyPromotionSelection(type, btn) {
    document.querySelectorAll('#promotionPills .duration-pill').forEach(p => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('promotionValue').value = type;
}

function selectPromotion(type, btn) {
    if (type === 'None') {
        applyPromotionSelection(type, btn);
        return;
    }
    pendingPromoType = type;
    pendingPromoBtn = btn;
    openSharedRulesModal('promo');
}

async function confirmPromoRules(agreed) {
    closeSharedRulesModal('promo');
    if (agreed && pendingPromoType) {
        if (typeof window.onPromoAgree === 'function') {
            await window.onPromoAgree(pendingPromoType, pendingPromoBtn);
        } else {
            applyPromotionSelection(pendingPromoType, pendingPromoBtn);
        }
    }
    pendingPromoType = null;
    pendingPromoBtn = null;
}

async function initDurationUI() {
    let preferred = DEFAULT_DURATION;
    try {
        const res = await fetch('/Profile/AdDurationPref');
        if (res.ok) {
            const data = await res.json();
            if (data.days && DURATION_OPTIONS.includes(data.days)) {
                preferred = data.days;
            }
        }
    } catch (e) { console.warn('Could not load duration preference', e); }

    const hiddenInput = document.getElementById('pubDurationValue');
    if (hiddenInput) hiddenInput.value = preferred;

    const pills = document.querySelectorAll('#pubDurationPills .duration-pill');
    if (pills.length) {
        pills.forEach(btn => {
            const days = parseInt(btn.dataset.days);
            if (days === preferred) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    } else {
        const container = document.getElementById('pubDurationPills');
        if (container) {
            container.innerHTML = '';
            DURATION_OPTIONS.forEach(days => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'duration-pill';
                btn.dataset.days = days;
                btn.textContent = `${days}`;
                btn.onclick = () => selectPubDuration(days, btn);
                if (days === preferred) btn.classList.add('active');
                container.appendChild(btn);
            });
        }
    }
}

function translateDynamicElement(el) {
    const lang = localStorage.getItem('lang') || 'en';
    const dict = TRANSLATIONS[lang] || TRANSLATIONS['en'];

    const key = el.getAttribute('data-i18n');
    if (key && dict[key] !== undefined) {
        let text = dict[key];
        const count = el.getAttribute('data-count');
        if (count !== null) {
            text = `${text} ${count}`;
        }
        el.textContent = text;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('pubDurationPills')) {
        initDurationUI();
    }
});
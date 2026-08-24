async function collectParamValues(container = document) {
    const params = {};

    const priceInput = document.getElementById('adPrice');
    if (priceInput && priceInput.dataset.paramId && priceInput.value) {
        params[priceInput.dataset.paramId] = priceInput.value;
    }

    container.querySelectorAll('input[data-param], textarea[data-param]').forEach(el => {
        if (el.closest('.param-options')) return;

        if (el.type === 'checkbox') {
            if (el.checked) params[el.dataset.param] = "true";
        } else if (el.value) params[el.dataset.param] = el.value;
    });

    container.querySelectorAll('.param-pills[data-param]').forEach(el => {
        const active = el.querySelector('.param-pill-active');
        if (active) params[el.dataset.param] = active.dataset.id;
    });

    container.querySelectorAll('.param-swatches[data-param]').forEach(el => {
        const active = el.querySelector('.param-swatch-row.param-swatch-active');
        if (active) params[el.dataset.param] = active.dataset.id;
    });

    if (typeof FreeTextSelect !== 'undefined') {
        const targetContainer = container.querySelector('#createParams') || container;
        const freeTextValues = await FreeTextSelect.resolveAll(targetContainer);
        Object.assign(params, freeTextValues);
    }

    container.querySelectorAll('.param-options[data-param]').forEach(el => {
        const vals = [...el.querySelectorAll('input[type=checkbox]:checked')].map(cb => cb.dataset.id);
        if (vals.length) params[el.dataset.param] = vals.join(',');
    });

    container.querySelectorAll('.param-range-input[data-param]').forEach(el => {
        if (el.value) params[el.dataset.param] = el.value;
    });

    return params;
}
function selectPill(pill, paramId) {
    const pills = pill.closest('.param-pills');
    const wasActive = pill.classList.contains('param-pill-active');
    pills.querySelectorAll('.param-pill').forEach(p => p.classList.remove('param-pill-active'));
    const input = document.getElementById('pill_' + paramId);

    if (!wasActive) {
        pill.classList.add('param-pill-active');
        if (input) input.value = pill.dataset.id;
    } else if (input) input.value = '';

    if (window.ParamRulesEngine) window.ParamRulesEngine.triggerUpdate();
}
function selectSwatch(row, paramId) {
    const wrap = row.closest('.param-swatches');
    const wasActive = row.classList.contains('param-swatch-active');
    wrap.querySelectorAll('.param-swatch-row').forEach(r => r.classList.remove('param-swatch-active'));
    const input = document.getElementById('swatch_' + paramId);

    if (!wasActive) {
        row.classList.add('param-swatch-active');
        if (input) input.value = row.dataset.id;
    } else if (input) input.value = '';

    if (window.ParamRulesEngine) window.ParamRulesEngine.triggerUpdate();
}
function rangeSync(input, side) {
    const wrap = input.closest('.param-range');
    if (!wrap) return;
    const min = parseFloat(wrap.dataset.min);
    const max = parseFloat(wrap.dataset.max);
    const step = parseFloat(wrap.dataset.step) || 1;
    const from = wrap.querySelector('.param-range-from');
    const to = wrap.querySelector('.param-range-to');

    let val = parseFloat(input.value);
    if (!isNaN(val)) {
        val = Math.round((val - min) / step) * step + min;
        val = Math.min(Math.max(val, min), max);
        input.value = val;
    }

    if (side === 'from' && to.value !== '' && val > parseFloat(to.value)) from.value = to.value;
    if (side === 'to' && from.value !== '' && val < parseFloat(from.value)) to.value = from.value;
}
function toggleOptions(btn) {
    const block = btn.parentElement;
    const hidden = block.querySelectorAll('.param-option-hidden');
    const isHidden = hidden[0]?.style.display === 'none';
    hidden.forEach(el => el.style.display = isHidden ? '' : 'none');

    const lang = localStorage.getItem('lang') || 'en';
    const dict = (typeof TRANSLATIONS !== 'undefined' && (TRANSLATIONS[lang] || TRANSLATIONS['en'])) || {};

    const count = btn.dataset.count || hidden.length;
    btn.textContent = isHidden ? (dict.hide_btn || 'Hide') : (dict.more_btn || 'More') + ' ' + count;
}
function removeType8Chip(link) {
    const chip = link.closest('.search-chip');
    if (chip) chip.remove();
}

function addType8Chip(paramId, id, text) {
    const chipsContainer = document.getElementById('chips_' + paramId);
    if (!chipsContainer) return;

    const exists = chipsContainer.querySelector(`[data-id="${id}"]`);
    if (exists) return;

    const chip = document.createElement('span');
    chip.className = 'search-chip';
    chip.style.margin = '0';
    chip.setAttribute('data-id', id);
    chip.innerHTML = `
        ${text}
        <a class="search-chip-remove" href="#" onclick="removeType8Chip(this); return false;">✕</a>
        <input type="hidden" name="p_${paramId}" value="${id}" />
    `;
    chipsContainer.appendChild(chip);
}

async function handleType8Keydown(e, paramId) {
    if (e.key === 'Enter') {
        e.preventDefault();
        const input = e.target;
        const text = input.value.trim();
        if (!text) return;

        try {
            const res = await fetch('/Product/ResolveSelectOption', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
                },
                body: JSON.stringify({ paramId: paramId, text: text, createIfNotFound: false })
            });
            if (res.ok) {
                const data = await res.json();
                if (data && data.id) {
                    addType8Chip(paramId, data.id, text);
                    input.value = '';
                }
            }
        } catch (err) {
            console.error('Error resolving option:', err);
        }
    }
}
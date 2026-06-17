(function (window) {

    function buildFreeTextSelectHtml(paramId, initialText, initialId, options) {
        var safeText = initialText ? String(initialText).replace(/"/g, '&quot;') : '';
        var optionsJson = JSON.stringify(options || []).replace(/"/g, '&quot;');
        return '' +
            '<div class="free-text-select" data-param="' + paramId + '" data-options="' + optionsJson + '" style="position:relative">' +
            '<input type="text" class="create-input free-text-select-input" ' +
            'data-param="' + paramId + '" ' +
            'value="' + safeText + '" autocomplete="off" ' +
            'oninput="FreeTextSelect.onInput(this)" ' +
            'onfocus="FreeTextSelect.onFocus(this)" />' +
            '<input type="hidden" class="free-text-select-id" value="' + (initialId || '') + '" />' +
            '<div class="create-dropdown free-text-select-drop"></div>' +
            '</div>';
    }

    function getWrap(inputEl) {
        return inputEl.closest('.free-text-select');
    }

    function getOptionsFromWrap(wrap) {
        try {
            return JSON.parse(wrap.dataset.options || '[]');
        } catch (e) {
            return [];
        }
    }

    function onFocus(inputEl) {
        var wrap = getWrap(inputEl);
        renderDrop(wrap, getOptionsFromWrap(wrap), inputEl.value);
    }

    function onInput(inputEl) {
        var wrap = getWrap(inputEl);

        var hidden = wrap.querySelector('.free-text-select-id');
        if (hidden) hidden.value = '';
        renderDrop(wrap, getOptionsFromWrap(wrap), inputEl.value);
    }

    function renderDrop(wrap, options, filterText) {
        var drop = wrap.querySelector('.free-text-select-drop');
        var val = (filterText || '').toLowerCase().trim();
        var filtered = val
            ? options.filter(function (o) { return o.text.toLowerCase().includes(val); })
            : options;

        drop.innerHTML = '';
        if (!filtered.length) { drop.style.display = 'none'; return; }

        filtered.slice(0, 15).forEach(function (opt) {
            var item = document.createElement('div');
            item.className = 'create-drop-item';
            item.textContent = opt.text;
            item.onclick = function () { selectOption(wrap, opt); };
            drop.appendChild(item);
        });
        drop.style.display = 'block';
    }

    function selectOption(wrap, opt) {
        var input = wrap.querySelector('.free-text-select-input');
        var hidden = wrap.querySelector('.free-text-select-id');
        input.value = opt.text;
        hidden.value = opt.id;
        hideDrop(wrap);
    }

    function hideDrop(wrap) {
        var drop = wrap.querySelector('.free-text-select-drop');
        if (drop) drop.style.display = 'none';
    }

    document.addEventListener('click', function (e) {
        document.querySelectorAll('.free-text-select').forEach(function (wrap) {
            if (!wrap.contains(e.target)) hideDrop(wrap);
        });
    });

    async function resolveWrapToId(wrap) {
        var paramId = wrap.dataset.param;
        var input = wrap.querySelector('.free-text-select-input');
        var text = input.value.trim();
        if (!text) return null;

        try {
            var res = await fetch('/Product/ResolveSelectOption', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.getElementById('afToken')?.value ?? ''
                },
                body: JSON.stringify({ paramId: parseInt(paramId), text: text })
            });
            if (!res.ok) return null;
            var data = await res.json();
            return data.id;
        } catch (e) {
            console.error('Failed to resolve select option', e);
            return null;
        }
    }

    async function resolveAll(containerEl) {
        var result = {};
        var wraps = (containerEl || document).querySelectorAll('.free-text-select');
        for (var i = 0; i < wraps.length; i++) {
            var wrap = wraps[i];
            var paramId = wrap.dataset.param;
            var id = await resolveWrapToId(wrap);
            if (id != null) result[paramId] = String(id);
        }
        return result;
    }

    window.FreeTextSelect = {
        buildHtml: buildFreeTextSelectHtml,
        onFocus: onFocus,
        onInput: onInput,
        resolveAll: resolveAll
    };
})(window);
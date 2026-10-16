/* ============================================================
   dependent-options.js — зависимые (каскадные) списки вариантов
   ------------------------------------------------------------
   Данные: dbo.SelectOptionRelations (ParentOptionId -> ChildOptionId).
   Правило: выбранный вариант родительского параметра
   (например «Профессия») оставляет в дочернем параметре
   («Специализация») только связанные с ним варианты.
   Если в родителе ничего не выбрано — показываются все варианты.

   Работает:
     * в форме создания/редактирования объявления (#createParams);
     * в панели фильтров на странице категории (#filterForm);
   с параметрами типов 2 (пилюли), 4 (чекбоксы), 6 (цвета)
   и 8 (свободный ввод с подсказками).

   Скрипт подключается автоматически: его путь лежит в
   dbo.ParameterCustomScripts, а ParamRulesEngine подгружает
   эти пути из /Product/CategoryRules.
   ============================================================ */
(function (window, document) {
    'use strict';

    var HIDDEN_CLASS = 'dependent-option-hidden';

    var relationsByParent = {};   // "parentOptionId" -> ["childOptionId", ...]
    var groups = [];              // [{ parentParamId, childParamId }, ...]
    var loadedCategoryId = null;
    var loading = false;
    var styleInjected = false;
    var applyScheduled = false;

    /* ---------- helpers ---------- */

    function injectStyle() {
        if (styleInjected) return;
        styleInjected = true;
        var style = document.createElement('style');
        style.setAttribute('data-dependent-options', 'true');
        style.textContent = '.' + HIDDEN_CLASS + ' { display: none !important; }';
        document.head.appendChild(style);
    }

    function dict() {
        var lang = 'lv';
        try { lang = localStorage.getItem('lang') || 'lv'; } catch (e) { }
        var translations = (typeof window.TRANSLATIONS !== 'undefined' && window.TRANSLATIONS)
            ? (window.TRANSLATIONS[lang] || window.TRANSLATIONS['lv'] || {})
            : {};
        return {
            more: translations.more_btn || 'More',
            hide: translations.hide_btn || 'Hide'
        };
    }

    function currentCategoryId() {
        if (window.ParamRulesEngine && window.ParamRulesEngine.categoryId) return window.ParamRulesEngine.categoryId;

        var catInput = document.querySelector('#filterForm input[name="id"]');
        if (catInput && catInput.value) return parseInt(catInput.value, 10);

        var selected = document.getElementById('selectedCategoryId');
        if (selected && selected.value) return parseInt(selected.value, 10);

        return null;
    }

    function findBlock(paramId) {
        var byParam = document.querySelector('[data-param="' + paramId + '"]');
        if (byParam) {
            var block = byParam.closest('.create-param-block, .param-block, .create-field, .create-section');
            if (block) return block;
        }

        var byInput = document.getElementById('pill_' + paramId) || document.querySelector('input[name="p_' + paramId + '"]');
        if (byInput) {
            var inputBlock = byInput.closest('.param-block, .create-param-block, .create-field, .create-section');
            if (inputBlock) return inputBlock;
        }

        return null;
    }

    /* Все элементы вариантов дочернего параметра: { el, id } */
    function optionItems(paramId) {
        var block = findBlock(paramId);
        if (!block) return [];

        var items = [];
        var seen = [];

        var push = function (el, id) {
            if (!el || id === undefined || id === null || String(id) === '') return;
            if (seen.indexOf(el) !== -1) return;
            seen.push(el);
            items.push({ el: el, id: String(id) });
        };

        block.querySelectorAll('.param-pill').forEach(function (el) { push(el, el.dataset.id); });
        block.querySelectorAll('.param-swatch-row').forEach(function (el) { push(el, el.dataset.id); });
        block.querySelectorAll('.param-option').forEach(function (el) {
            var input = el.querySelector('input[type="checkbox"]');
            if (input) push(el, input.dataset.id || input.value);
        });

        // форма объявления: чекбоксы лежат в .param-options[data-param]
        document.querySelectorAll('.param-options[data-param="' + paramId + '"] input[type="checkbox"]').forEach(function (input) {
            push(input.closest('label') || input, input.dataset.id);
        });

        return items;
    }

    /* Выбранные значения параметра (массив id вариантов) */
    function selectedIds(paramId) {
        var ids = [];
        var add = function (raw) {
            if (raw === undefined || raw === null) return;
            String(raw).split(',').forEach(function (v) {
                v = v.trim();
                if (v !== '' && ids.indexOf(v) === -1) ids.push(v);
            });
        };

        var pillInput = document.getElementById('pill_' + paramId);
        if (pillInput) add(pillInput.value);

        document.querySelectorAll('input[type="hidden"][name="p_' + paramId + '"]').forEach(function (el) { add(el.value); });

        var block = findBlock(paramId);
        if (block) {
            block.querySelectorAll('.param-pill-active').forEach(function (el) { add(el.dataset.id); });
            block.querySelectorAll('.param-swatch-row.param-swatch-active').forEach(function (el) { add(el.dataset.id); });
            block.querySelectorAll('input[type="checkbox"]:checked').forEach(function (input) { add(input.dataset.id || input.value); });
        }

        document.querySelectorAll('.param-pills[data-param="' + paramId + '"] .param-pill-active').forEach(function (el) { add(el.dataset.id); });
        document.querySelectorAll('.param-options[data-param="' + paramId + '"] input[type="checkbox"]:checked').forEach(function (input) { add(input.dataset.id); });

        var freeTextId = document.querySelector('.free-text-select[data-param="' + paramId + '"] .free-text-select-id');
        if (freeTextId) add(freeTextId.value);

        return ids;
    }

    /* Снять выбор с варианта, который больше не доступен */
    function deselect(el, paramId) {
        if (!el || !el.classList) return;

        if (el.classList.contains('param-pill-active')) {
            el.classList.remove('param-pill-active');
            var pillInput = document.getElementById('pill_' + paramId);
            if (pillInput && String(pillInput.value) === String(el.dataset.id)) pillInput.value = '';
        }

        if (el.classList.contains('param-swatch-active')) {
            el.classList.remove('param-swatch-active');
            var swatchInput = document.getElementById('swatch_' + paramId);
            if (swatchInput && String(swatchInput.value) === String(el.dataset.id)) swatchInput.value = '';
        }

        var input = el.querySelector ? el.querySelector('input[type="checkbox"]') : null;
        if (!input && el.type === 'checkbox') input = el;
        if (input && input.checked) input.checked = false;
    }

    function setVisible(item, visible, paramId) {
        var el = item.el;

        if (visible) {
            if (!el.classList.contains(HIDDEN_CLASS)) return;
            el.classList.remove(HIDDEN_CLASS);
            if (el.dataset.depPrevDisplay !== undefined) {
                el.style.display = el.dataset.depPrevDisplay;
                delete el.dataset.depPrevDisplay;
            }
            return;
        }

        if (!el.classList.contains(HIDDEN_CLASS)) {
            el.dataset.depPrevDisplay = el.style.display || '';
            el.classList.add(HIDDEN_CLASS);
        }
        deselect(el, paramId);
    }

    /* Кнопка «Ещё N»: пересчитать количество скрытых вариантов */
    function updateMoreButton(block) {
        var btn = block.querySelector('.param-show-more');
        if (!btn) return;

        var items = block.querySelectorAll('.param-pill, .param-option, .param-swatch-row');
        var hiddenCount = 0;

        items.forEach(function (el) {
            if (el.classList.contains(HIDDEN_CLASS)) return;
            if (el.classList.contains('param-option-hidden') || el.classList.contains('param-swatch-hidden')) hiddenCount++;
            else if (el.style.display === 'none') hiddenCount++;
        });

        var words = dict();
        btn.dataset.count = hiddenCount;

        if (hiddenCount > 0) {
            btn.style.display = '';
            btn.textContent = words.more + ' ' + hiddenCount;
        } else {
            btn.style.display = 'none';
            btn.textContent = words.hide;
        }
    }

    /* Тип 8 (свободный ввод): ограничить список подсказок */
    function applyFreeText(paramId, allowed) {
        var wrap = document.querySelector('.free-text-select[data-param="' + paramId + '"]');
        if (!wrap) return;

        var hidden = wrap.querySelector('.free-text-select-id');
        var text = wrap.querySelector('.free-text-select-input');

        if (hidden && hidden.value && allowed && !allowed[String(hidden.value)]) {
            hidden.value = '';
            if (text) text.value = '';
        }

        var all = [];
        try { all = JSON.parse(wrap.dataset.allOptions || '[]'); } catch (e) { all = []; }
        if (!all.length) {
            try { all = JSON.parse(wrap.dataset.options || '[]'); } catch (e) { all = []; }
        }
        if (!all.length) return;

        wrap.dataset.allOptions = JSON.stringify(all);
        var filtered = allowed
            ? all.filter(function (o) { return allowed[String(o.id)] === true; })
            : all;
        wrap.dataset.options = JSON.stringify(filtered);

        var drop = wrap.querySelector('.free-text-select-drop');
        if (drop) drop.style.display = 'none';
    }

    /* ---------- применение правил ---------- */

    function applyGroup(group) {
        var parents = selectedIds(group.parentParamId);
        var allowed = null;

        if (parents.length > 0) {
            allowed = {};
            parents.forEach(function (parentId) {
                (relationsByParent[String(parentId)] || []).forEach(function (childId) {
                    allowed[String(childId)] = true;
                });
            });
        }

        optionItems(group.childParamId).forEach(function (item) {
            setVisible(item, !allowed || allowed[item.id] === true, group.childParamId);
        });

        applyFreeText(group.childParamId, allowed);

        var block = findBlock(group.childParamId);
        if (block) updateMoreButton(block);
    }

    function applyAll() {
        if (!groups.length) return;
        groups.forEach(applyGroup);
    }

    function scheduleApply() {
        if (applyScheduled) return;
        applyScheduled = true;
        setTimeout(function () {
            applyScheduled = false;
            applyAll();
        }, 0);
    }

    /* ---------- загрузка связей ---------- */

    function buildIndex(list) {
        relationsByParent = {};
        var groupMap = {};

        (list || []).forEach(function (r) {
            var key = String(r.parentOptionId);
            if (!relationsByParent[key]) relationsByParent[key] = [];
            if (relationsByParent[key].indexOf(String(r.childOptionId)) === -1) {
                relationsByParent[key].push(String(r.childOptionId));
            }
            groupMap[r.parentParamId + ':' + r.childParamId] = {
                parentParamId: r.parentParamId,
                childParamId: r.childParamId
            };
        });

        groups = Object.keys(groupMap).map(function (k) { return groupMap[k]; });
    }

    /* Оставляем только те связи, у которых оба параметра есть на странице */
    function filterGroupsToPage() {
        groups = groups.filter(function (g) {
            return !!(findBlock(g.parentParamId) && findBlock(g.childParamId));
        });
    }

    async function load(categoryId, force) {
        if (loading) return;
        if (!force && categoryId && categoryId === loadedCategoryId) { applyAll(); return; }

        loading = true;
        try {
            var url = '/Product/OptionRelations' + (categoryId ? '?categoryId=' + encodeURIComponent(categoryId) : '');
            var response = await fetch(url);
            if (!response.ok) return;

            var data = await response.json();
            buildIndex(data.relations || data);
            if (!categoryId) filterGroupsToPage();

            loadedCategoryId = categoryId || null;
            applyAll();
        } catch (e) {
            console.error('[DependentOptions]', e);
        } finally {
            loading = false;
        }
    }

    /* ---------- public ---------- */

    window.DependentOptions = {
        load: load,
        apply: applyAll,
        refresh: function (categoryId, force) {
            injectStyle();
            if (categoryId === undefined || categoryId === null) categoryId = currentCategoryId();
            return load(categoryId, force === true);
        },
        reset: function () {
            loadedCategoryId = null;
            relationsByParent = {};
            groups = [];
        }
    };

    function init() {
        injectStyle();

        document.addEventListener('click', scheduleApply, true);
        document.addEventListener('change', scheduleApply, true);
        document.addEventListener('input', scheduleApply, true);

        window.DependentOptions.refresh();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window, document);

(function (window) {
    var _visibilityRules = [];
    var _validationRules = [];

    var ERROR_MESSAGES = {
        en: {
            required: 'This field is required',
            invalid_format: 'Invalid format',
            min_value: 'Minimum value:',
            max_value: 'Maximum value:',
            min_length: 'Minimum length:',
            max_length: 'Maximum length:',
            invalid_vin: 'VIN must be 17 alphanumeric characters (I, O, Q not allowed)',
            invalid_reg_number: 'Format: XX-#### (letters, hyphen, 1–4 digits, first not zero)',
        },
        lv: {
            required: 'Šis lauks ir obligāts',
            invalid_format: 'Nepareizs formāts',
            min_value: 'Minimālā vērtība:',
            max_value: 'Maksimālā vērtība:',
            min_length: 'Minimālais garums:',
            max_length: 'Maksimālais garums:',
            invalid_vin: 'VIN jābūt 17 burtciparu rakstzīmēm (I, O, Q nav atļauti)',
            invalid_reg_number: 'Formāts: XX-#### (burti, defise, 1–4 cipari, pirmais nav nulle)',
        },
        ru: {
            required: 'Это поле обязательно',
            invalid_format: 'Неверный формат',
            min_value: 'Минимальное значение:',
            max_value: 'Максимальное значение:',
            min_length: 'Минимальная длина:',
            max_length: 'Максимальная длина:',
            invalid_vin: 'VIN должен содержать 17 буквенно-цифровых символов (I, O, Q недопустимы)',
            invalid_reg_number: 'Формат: XX-#### (две буквы, дефис, 1–4 цифры, первая не ноль)',
        }
    };

    var getParamValue = function (paramId) {
        var activePill = document.querySelector('[data-param="' + paramId + '"] .param-pill-active, .param-pills[data-param="' + paramId + '"] .param-pill-active'
            + '.param-swatches[data-param="' + paramId + '"] .param-swatch-row.param-swatch-active');

        if (!activePill) {
            var pillInput = document.querySelector('input[type="hidden"]#pill_' + paramId + ', input[type="hidden"][name="p_' + paramId + '"]');
            if (pillInput && pillInput.value) return pillInput.value;
        }
        if (activePill) return activePill.dataset.id || activePill.getAttribute('data-id');

        var cb = document.querySelector('input[type="checkbox"][data-param="' + paramId + '"]');
        if (cb) return cb.checked ? 'true' : '';

        var input = document.querySelector('input[data-param="' + paramId + '"], textarea[data-param="' + paramId + '"], select[data-param="' + paramId + '"]');
        if (input) return input.value.trim();

        var namedInput = document.querySelector('input[name="p_' + paramId + '"], textarea[name="p_' + paramId + '"], select[name="p_' + paramId + '"]');
        if (namedInput) return namedInput.value.trim();

        return '';
    };

    var findParamBlock = function (paramId) {
        var el = document.querySelector('[data-param="' + paramId + '"]');

        if (!el) {
            el = document.querySelector('input#pill_' + paramId + ', input[name="p_' + paramId + '"], textarea[name="p_' + paramId + '"], select[name="p_' + paramId + '"]');
        }

        if (!el) return null;
        return el.closest('.create-param-block, .param-block, .create-field, .create-section');
    };

    var validateSingle = function (paramId) {
        var block = findParamBlock(paramId);
        if (!block || block.style.display === 'none') return true;

        var rules = _validationRules.filter(function (r) { return r.paramId == paramId; });
        var value = getParamValue(paramId);
        var errorText = null;

        var lang = localStorage.getItem('lang') || 'en';
        var msgs = ERROR_MESSAGES[lang] || ERROR_MESSAGES['en'];

        for (var i = 0; i < rules.length; i++) {
            var rule = rules[i];

            if (rule.ruleType === 'required_if' && rule.triggerParamId) {
                var tVal = getParamValue(rule.triggerParamId);
                if (String(tVal) !== String(rule.triggerValue)) continue;
            }

            var key = rule.errorMessageKey;
            var customMsg = (key && msgs[key]) ? msgs[key] : null;

            if ((rule.ruleType === 'required' || rule.ruleType === 'required_if') && !value) {
                errorText = customMsg || msgs.required;
            } else if (rule.ruleType === 'regex' && value) {
                if (!new RegExp(rule.ruleValue).test(value)) {
                    errorText = customMsg || msgs.invalid_format;
                }
            } else if (rule.ruleType === 'min' && value && parseFloat(value) < parseFloat(rule.ruleValue)) {
                errorText = customMsg || (msgs.min_value + " " + rule.ruleValue);
            } else if (rule.ruleType === 'max' && value && parseFloat(value) > parseFloat(rule.ruleValue)) {
                errorText = customMsg || (msgs.max_value + " " + rule.ruleValue);
            }

            if (errorText) break;
        }

        var existingErr = block.querySelector('.param-error');
        if (errorText) {
            if (!existingErr) {
                existingErr = document.createElement('div');
                existingErr.className = 'param-error';
                existingErr.style.cssText = 'color:#cc0000;font-size:12px;margin-top:4px;font-weight:bold;';
                block.appendChild(existingErr);
            }
            existingErr.textContent = errorText;
            return false;
        } else {
            if (existingErr) existingErr.remove();
            return true;
        }
    };

    var applyAllVisibility = function () {
        _visibilityRules.forEach(function (rule) {
            var actual = getParamValue(rule.triggerParamId);
            var expected = rule.triggerValue;
            var conditionMet = false;

            if (rule.triggerOperator === 'eq') conditionMet = String(actual) === String(expected);
            else if (rule.triggerOperator === 'neq') conditionMet = String(actual) !== String(expected);
            else if (rule.triggerOperator === 'contains') conditionMet = String(actual).split(',').indexOf(String(expected)) !== -1;

            var shouldShow = rule.action === 'show' ? conditionMet : !conditionMet;
            var block = findParamBlock(rule.targetParamId);
            if (block) {
                block.style.display = shouldShow ? '' : 'none';
                if (!shouldShow) {
                    var err = block.querySelector('.param-error');
                    if (err) err.remove();
                }
            }
        });
    };

    window.ParamRulesEngine = {
        loadRules: async function (categoryId) {
            try {
                var res = await fetch('/Product/CategoryRules?categoryId=' + categoryId);
                var data = await res.json();
                _visibilityRules = data.visibilityRules || [];
                _validationRules = data.validationRules || [];

                var container = document.getElementById('filterForm');
                if (container) {
                    var handler = function (e) {
                        applyAllVisibility();
                        var pid = e.target.dataset.param;
                        if (pid) validateSingle(pid);
                    };
                    container.addEventListener('input', handler);
                    container.addEventListener('change', handler);
                }
                applyAllVisibility();
            } catch (e) {
                console.error(translate('engine_error'), e);
            }
        },
        triggerUpdate: function () {
            applyAllVisibility();
            _validationRules.forEach(function (r) { validateSingle(r.paramId); });
        },
        validateAll: function () {
            var isValid = true;
            _validationRules.forEach(function (rule) {
                if (!validateSingle(rule.paramId)) isValid = false;
            });
            if (!isValid) {
                var firstErr = document.querySelector('.param-error');
                if (firstErr) firstErr.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
            return isValid;
        }
    };
})(window);
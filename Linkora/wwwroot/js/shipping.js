// Omniva delivery: buyer request window on the product page and the seller's request page.
(function () {
    'use strict';

    function csrf() { return document.getElementById('afToken')?.value ?? ''; }

    /**
     * The cookie scheme redirects anonymous requests to the login page instead of answering 401,
     * so a redirect (followed or not) also means "login required".
     */
    function needsLogin(res) {
        return res.status === 401 || res.redirected || res.type === 'opaqueredirect';
    }

    function jsonHeaders() {
        return { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': csrf() };
    }

    function tr(key) {
        const text = typeof translate === 'function' ? translate(key) : key;
        return text && text !== key ? text : null;
    }

    /** Localized text for an API error code; falls back to the generic message. */
    function errorText(code) {
        return tr('ship_err_' + (code || 'generic')) || tr('ship_err_generic') || 'Error';
    }

    /** Localized template with {0} replaced by `value`; `fallback` is used when there is no translation. */
    function trFormat(key, value, fallback) {
        return (tr(key) || fallback).replace('{0}', value);
    }

    async function readErrorCode(res) {
        try {
            const body = await res.json();
            return body?.error ?? null;
        } catch {
            return null;
        }
    }

    function el(tag, text, className) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text != null) node.textContent = text;
        return node;
    }

    function setValue(id, value) {
        const field = document.getElementById(id);
        if (field) field.value = value ?? '';
    }

    function formatMoney(value) {
        return value == null ? '—' : Number(value).toFixed(2) + ' €';
    }

    function formatDate(value) {
        if (!value) return '';
        const d = new Date(value);
        return isNaN(d) ? '' : d.toLocaleDateString();
    }

    // ---------- Seller page ----------

    let sellerInitialized = false;

    window.initSellerShipments = function () {
        if (sellerInitialized) return;
        sellerInitialized = true;
        document.addEventListener('click', onSellerClick);
    };

    async function onSellerClick(e) {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;

        const action = btn.dataset.action;
        const id = parseInt(btn.dataset.id, 10);

        if (action === 'accept') return openAccept(btn.closest('[data-request-id]'));
        if (action === 'close-accept') return closeAccept();
        if (action === 'submit-accept') return submitAccept();
        if (action === 'decline') return declineRequest(id);
        if (action === 'pay') return payShipment(id);
    }

    const POSTCODE_PATTERN = /\b(LV|LT|EE)[-\s]?(\d{4,5})\b/i;
    /** A whole comma-separated segment that is only a postcode, e.g. "LV-1010". */
    const POSTCODE_SEGMENT = /^\s*(LV|LT|EE)[-\s]?\d{4,5}\s*$/i;

    /**
     * Addresses are stored as "City, street, house" (listing address and profile home address
     * use the same placeholder), so the first segment is the city and the rest is the street.
     * Postcode and country are only known when the address carries a "LV-1234" style code.
     */
    function parseAddress(raw) {
        const text = (raw || '').trim();
        const parsed = { city: '', street: '', postcode: '', country: 'LV' };
        if (!text) return parsed;

        const code = text.match(POSTCODE_PATTERN);
        if (code) {
            parsed.country = code[1].toUpperCase();
            parsed.postcode = code[2];
        }

        const parts = text.split(',').map(s => s.trim()).filter(Boolean).filter(part => !POSTCODE_SEGMENT.test(part));
        if (parts.length > 0) {
            parsed.city = parts[0];
            parsed.street = parts.slice(1).join(', ');
        }
        return parsed;
    }

    function openAccept(card) {
        const requestId = card?.dataset.requestId;
        if (!requestId) return;

        const page = document.querySelector('.ship-page');
        const productSize = (card.dataset.productSize || '').toUpperCase();

        document.getElementById('shipAcceptRequestId').value = requestId;
        // No silent default: the price depends on the size, so the seller has to choose one when the listing has none.
        document.getElementById('shipAcceptSize').value = ['XS', 'S', 'M', 'L'].includes(productSize) ? productSize : '';
        document.getElementById('shipAcceptWeight').value = card.dataset.productWeight || '';

        // The sender is the seller: the listing address is the best guess, the profile is the fallback.
        const address = parseAddress(card.dataset.productAddress || page?.dataset.homeAddress);
        setSender('shipSenderName', page?.dataset.userName);
        setSender('shipSenderPhone', page?.dataset.userPhone);
        setSender('shipSenderEmail', page?.dataset.userEmail);
        setSender('shipSenderStreet', address.street);
        setSender('shipSenderCity', address.city);
        setSender('shipSenderPostcode', address.postcode);
        setSender('shipSenderCountry', address.country);

        hideAcceptError();
        openOverlay('shipAcceptOverlay', 'shipAcceptModal');
        updateAcceptPrice();
    }

    /** Overwrites a sender field on every opening, so values from the previous request never leak in. */
    function setSender(id, value) {
        const field = document.getElementById(id);
        if (!field) return;
        field.value = value ?? '';
        if (field.tagName === 'SELECT' && field.value !== (value ?? '')) field.value = 'LV';
    }

    function closeAccept() { closeOverlay('shipAcceptOverlay', 'shipAcceptModal'); }

    /** Guards against a slow answer for a previously chosen size overwriting the current price. */
    let priceRequestToken = 0;

    async function updateAcceptPrice() {
        const priceEl = document.getElementById('shipAcceptPrice');
        const requestId = document.getElementById('shipAcceptRequestId').value;
        const size = document.getElementById('shipAcceptSize').value;
        const token = ++priceRequestToken;

        if (!size) {
            priceEl.textContent = '—';
            priceEl.dataset.maxWeight = '';
            applyWeightLimit();
            return;
        }

        priceEl.textContent = '…';
        const res = await fetch(`/Shipping/Price?requestId=${encodeURIComponent(requestId)}&size=${encodeURIComponent(size)}`, { headers: { 'Accept': 'application/json' } });
        if (token !== priceRequestToken) return;

        if (!res.ok) {
            priceEl.textContent = '—';
            priceEl.dataset.maxWeight = '';
            applyWeightLimit();
            showAcceptError(errorText(await readErrorCode(res)));
            return;
        }
        const data = await res.json();
        if (token !== priceRequestToken) return;

        priceEl.textContent = formatMoney(data.price);
        priceEl.dataset.maxWeight = data.maxWeightKg ?? '';
        applyWeightLimit();
    }

    /**
     * The carrier limit belongs to the tariff of the chosen size, so the field max and the hint
     * follow the price response instead of the global 100 kg fallback.
     */
    function applyWeightLimit() {
        const input = document.getElementById('shipAcceptWeight');
        const hint = document.getElementById('shipAcceptWeightHint');
        if (!input || !hint) return;

        if (!input.dataset.defaultMax) input.dataset.defaultMax = input.getAttribute('max') || '';

        const limit = parseFloat(document.getElementById('shipAcceptPrice')?.dataset.maxWeight || '');
        const hasLimit = Number.isFinite(limit) && limit > 0;
        input.max = hasLimit ? String(limit) : (input.dataset.defaultMax || '');

        const weight = parseFloat(input.value);
        const exceeded = hasLimit && Number.isFinite(weight) && weight > limit;

        hint.hidden = !hasLimit;
        hint.classList.toggle('ship-weight-hint-error', exceeded);
        hint.textContent = hasLimit ? trFormat('ship_weight_limit', limit, 'Carrier limit for this size: up to {0} kg.') : '';
    }

    async function submitAccept() {
        const requestId = document.getElementById('shipAcceptRequestId').value;
        const size = document.getElementById('shipAcceptSize').value;
        const weightRaw = document.getElementById('shipAcceptWeight').value;
        const weight = weightRaw === '' ? null : parseFloat(weightRaw);
        const maxWeight = parseFloat(document.getElementById('shipAcceptPrice').dataset.maxWeight || '');
        const val = id => document.getElementById(id).value.trim();

        if (!size) return showAcceptError(errorText('size_invalid'));
        if (weight != null && Number.isFinite(maxWeight) && maxWeight > 0 && weight > maxWeight) {
            return showAcceptError(errorText('weight_exceeds_limit'));
        }

        const body = {
            size,
            weightKg: weight,
            senderName: val('shipSenderName'),
            senderPhone: val('shipSenderPhone') || null,
            senderEmail: val('shipSenderEmail') || null,
            senderStreet: val('shipSenderStreet'),
            senderCity: val('shipSenderCity'),
            senderPostcode: val('shipSenderPostcode'),
            senderCountry: document.getElementById('shipSenderCountry').value
        };

        const res = await fetch(`/Shipping/Request/${encodeURIComponent(requestId)}/Accept`, {
            method: 'POST',
            headers: jsonHeaders(),
            body: JSON.stringify(body)
        });

        if (res.ok) return location.reload();
        if (needsLogin(res) && typeof openAuthModal === 'function') return openAuthModal('login');
        showAcceptError(errorText(await readErrorCode(res)));
    }

    async function declineRequest(id) {
        if (!confirm(tr('ship_confirm_decline') || 'Decline this request?')) return;

        const res = await fetch(`/Shipping/Request/${id}/Decline`, { method: 'POST', headers: jsonHeaders() });
        if (res.ok) return location.reload();
        alert(errorText(await readErrorCode(res)));
    }

    async function payShipment(id) {
        const res = await fetch(`/Payments/InitiateShipping?requestId=${id}`, { method: 'POST', headers: { 'RequestVerificationToken': csrf() } });
        if (!res.ok) {
            const code = await readErrorCode(res);
            alert(code ? errorText(code) : (tr('ship_err_generic') || 'Error'));
            return;
        }
        const data = await res.json();
        if (data.redirectUrl) window.location.href = data.redirectUrl;
    }

    function showAcceptError(text) {
        const box = document.getElementById('shipAcceptError');
        box.textContent = text;
        box.hidden = false;
    }

    function hideAcceptError() {
        const box = document.getElementById('shipAcceptError');
        box.textContent = '';
        box.hidden = true;
    }

    // ---------- Buyer request window (product page) ----------

    let buyProductId = null;
    let lockerSearchTimer = null;
    /** Locker chosen in the window; kept across searches so typing a query does not lose the choice. */
    let selectedLocker = null;

    window.openShipmentModal = async function (productId) {
        if (!document.getElementById('shipBuyModal')) return;

        buyProductId = productId;
        document.getElementById('shipBuyProductId').value = productId;
        hideBuyError();

        // A fresh window for a fresh request: no locker or receiver details carried over.
        selectedLocker = null;
        renderLockerSelection();
        setValue('shipLockerSearch', '');
        setValue('shipReceiverName', '');
        setValue('shipReceiverPhone', '');
        setValue('shipReceiverEmail', '');

        const loaded = await loadMyRequests();
        if (loaded === 'unauthorized') {
            if (typeof openAuthModal === 'function') openAuthModal('login');
            return;
        }

        await loadMyContact();
        await loadLockers('');
        openOverlay('shipBuyOverlay', 'shipBuyModal');
    };

    window.closeShipmentModal = function () {
        closeOverlay('shipBuyOverlay', 'shipBuyModal');
    };

    window.onLockerSearchInput = function () {
        clearTimeout(lockerSearchTimer);
        lockerSearchTimer = setTimeout(() => loadLockers(document.getElementById('shipLockerSearch').value.trim()), 300);
    };

    /** Name, phone and e-mail of the logged-in buyer, used to prefill the receiver fields. */
    async function loadMyContact() {
        try {
            const res = await fetch('/Shipping/BuyerContact', { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return;

            const contact = await res.json();
            setValue('shipReceiverName', contact.name);
            setValue('shipReceiverPhone', contact.phone);
            setValue('shipReceiverEmail', contact.email);
        } catch { }
    }

    async function loadLockers(query) {
        const list = document.getElementById('shipLockerList');
        if (!list) return;

        const country = document.getElementById('shipLockerCountry')?.value || 'LV';
        list.textContent = '';

        const res = await fetch(`/Shipping/Lockers?country=${encodeURIComponent(country)}&q=${encodeURIComponent(query)}`, { headers: { 'Accept': 'application/json' } });
        if (!res.ok) {
            list.appendChild(el('p', errorText(await readErrorCode(res)), 'ship-locker-empty'));
            return;
        }

        const lockers = await res.json();
        for (const l of lockers) {
            const item = el('button', null, 'ship-locker-item');
            item.type = 'button';
            item.dataset.lockerId = l.id;
            item.setAttribute('role', 'option');
            item.appendChild(el('span', l.name, 'ship-locker-name'));

            const details = [l.city, l.address].filter(Boolean).join(', ');
            if (details) item.appendChild(el('span', details, 'ship-locker-addr'));

            item.addEventListener('click', () => selectLocker({ id: l.id, name: l.name, city: l.city }));
            list.appendChild(item);
        }

        if (lockers.length === 0) list.appendChild(el('p', tr('ship_no_lockers') || 'No parcel lockers found', 'ship-locker-empty'));
        markSelectedLocker();
    }

    function selectLocker(locker) {
        selectedLocker = locker;
        renderLockerSelection();
        hideBuyError();
    }

    /** The chosen locker is shown as a separate line: a highlighted row inside the list is easy to miss. */
    function renderLockerSelection() {
        const box = document.getElementById('shipLockerSelected');

        if (box) {
            if (!selectedLocker) {
                box.hidden = true;
                box.textContent = '';
            } else {
                const text = [selectedLocker.name, selectedLocker.city].filter(Boolean).join(', ');
                box.hidden = false;
                box.textContent = trFormat('ship_locker_selected', text, 'Selected: {0}');
            }
        }

        markSelectedLocker();
    }

    function markSelectedLocker() {
        document.querySelectorAll('#shipLockerList .ship-locker-item').forEach(item => {
            const active = selectedLocker != null && String(item.dataset.lockerId) === String(selectedLocker.id);
            item.classList.toggle('active', active);
            item.setAttribute('aria-selected', active ? 'true' : 'false');
        });
    }

    async function loadMyRequests() {
        const res = await fetch(`/Shipping/Requests/Buyer?productId=${buyProductId}`, { headers: { 'Accept': 'application/json' } });
        if (needsLogin(res)) return 'unauthorized';

        const box = document.getElementById('shipBuyMyRequests');
        box.textContent = '';
        if (!res.ok) return 'failed';

        const requests = await res.json();
        if (requests.length === 0) return 'ok';

        box.appendChild(el('h3', tr('ship_your_requests') || 'Your requests', 'ship-subtitle'));
        for (const r of requests) {
            const row = el('div', null, 'ship-my-request');
            const statusText = tr('ship_status_' + r.status) || r.status;
            row.appendChild(el('span', statusText, 'ship-badge ship-badge-' + r.status));
            if (r.shipmentStatus) row.appendChild(el('span', tr('ship_state_' + r.shipmentStatus) || r.shipmentStatus, 'ship-my-state'));
            row.appendChild(el('span', (r.lockerName || '') + (r.lockerCity ? ', ' + r.lockerCity : ''), 'ship-my-locker'));
            if (r.barcode) row.appendChild(el('span', (tr('ship_barcode') || 'Barcode') + ': ' + r.barcode, 'ship-my-barcode'));
            if (r.status === 'Requested' || r.status === 'Accepted') {
                const cancel = el('button', tr('ship_cancel_request') || 'Cancel request', 'ship-btn');
                cancel.type = 'button';
                cancel.addEventListener('click', () => cancelShipmentRequest(r.id));
                row.appendChild(cancel);
            }
            box.appendChild(row);
        }
        return 'ok';
    }

    window.submitShipmentRequest = async function () {
        if (!selectedLocker) return showBuyError(errorText('locker_invalid'));

        const body = {
            productId: buyProductId,
            lockerId: selectedLocker.id,
            receiverName: document.getElementById('shipReceiverName').value.trim(),
            receiverPhone: document.getElementById('shipReceiverPhone').value.trim() || null,
            receiverEmail: document.getElementById('shipReceiverEmail').value.trim() || null
        };

        const res = await fetch('/Shipping/Request', { method: 'POST', headers: jsonHeaders(), body: JSON.stringify(body) });
        if (needsLogin(res)) return typeof openAuthModal === 'function' ? openAuthModal('login') : null;
        if (!res.ok) return showBuyError(errorText(await readErrorCode(res)));

        closeShipmentModal();
        showModal('ship_request_sent');
    };

    async function cancelShipmentRequest(id) {
        if (!confirm(tr('ship_confirm_cancel') || 'Cancel this request?')) return;

        const res = await fetch(`/Shipping/Request/${id}/Cancel`, { method: 'POST', headers: jsonHeaders() });
        if (!res.ok) return showBuyError(errorText(await readErrorCode(res)));
        await loadMyRequests();
    }

    function showBuyError(text) {
        const box = document.getElementById('shipBuyError');
        box.textContent = text;
        box.hidden = false;
    }

    function hideBuyError() {
        const box = document.getElementById('shipBuyError');
        box.textContent = '';
        box.hidden = true;
    }

    // ---------- Modal helpers (same markup pattern as the report modal) ----------

    // Named differently from the global showModal(key) in modals.js, which shows the notification modal.
    function openOverlay(overlayId, modalId) {
        document.getElementById(overlayId)?.classList.add('modal-open');
        document.getElementById(modalId)?.classList.add('modal-open');
    }

    function closeOverlay(overlayId, modalId) {
        document.getElementById(overlayId)?.classList.remove('modal-open');
        document.getElementById(modalId)?.classList.remove('modal-open');
    }

    // Price refresh when the seller changes the parcel size; a new delivery country resets the locker choice.
    document.addEventListener('change', e => {
        if (e.target && e.target.id === 'shipAcceptSize') updateAcceptPrice();
        if (e.target && e.target.id === 'shipLockerCountry') {
            selectedLocker = null;
            renderLockerSelection();
            loadLockers(document.getElementById('shipLockerSearch')?.value.trim() || '');
        }
    });
    document.addEventListener('input', e => {
        if (e.target && e.target.id === 'shipLockerSearch') onLockerSearchInput();
        if (e.target && e.target.id === 'shipAcceptWeight') applyWeightLimit();
    });
})();

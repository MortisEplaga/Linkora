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

    function openAccept(card) {
        const requestId = card?.dataset.requestId;
        if (!requestId) return;

        const page = document.querySelector('.ship-page');
        const size = card.dataset.productSize || 'M';
        const weight = card.dataset.productWeight || '';

        document.getElementById('shipAcceptRequestId').value = requestId;
        document.getElementById('shipAcceptSize').value = ['XS', 'S', 'M', 'L'].includes(size) ? size : 'M';
        document.getElementById('shipAcceptWeight').value = weight;

        const phone = document.getElementById('shipSenderPhone');
        const email = document.getElementById('shipSenderEmail');
        if (!phone.value) phone.value = page?.dataset.userPhone || '';
        if (!email.value) email.value = page?.dataset.userEmail || '';

        hideAcceptError();
        openOverlay('shipAcceptOverlay', 'shipAcceptModal');
        updateAcceptPrice();
    }

    function closeAccept() { closeOverlay('shipAcceptOverlay', 'shipAcceptModal'); }

    async function updateAcceptPrice() {
        const priceEl = document.getElementById('shipAcceptPrice');
        const requestId = document.getElementById('shipAcceptRequestId').value;
        const size = document.getElementById('shipAcceptSize').value;

        priceEl.textContent = '…';
        const res = await fetch(`/Shipping/Price?requestId=${encodeURIComponent(requestId)}&size=${encodeURIComponent(size)}`, { headers: { 'Accept': 'application/json' } });
        if (!res.ok) {
            priceEl.textContent = '—';
            showAcceptError(errorText(await readErrorCode(res)));
            return;
        }
        const data = await res.json();
        priceEl.textContent = formatMoney(data.price);
        priceEl.dataset.maxWeight = data.maxWeightKg ?? '';
    }

    async function submitAccept() {
        const requestId = document.getElementById('shipAcceptRequestId').value;
        const weightRaw = document.getElementById('shipAcceptWeight').value;
        const weight = weightRaw === '' ? null : parseFloat(weightRaw);
        const val = id => document.getElementById(id).value.trim();

        const body = {
            size: document.getElementById('shipAcceptSize').value,
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

    window.openShipmentModal = async function (productId) {
        if (!document.getElementById('shipBuyModal')) return;

        buyProductId = productId;
        document.getElementById('shipBuyProductId').value = productId;
        hideBuyError();

        const loaded = await loadMyRequests();
        if (loaded === 'unauthorized') {
            if (typeof openAuthModal === 'function') openAuthModal('login');
            return;
        }

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

    async function loadLockers(query) {
        const select = document.getElementById('shipLockerSelect');
        const res = await fetch(`/Shipping/Lockers?country=LV&q=${encodeURIComponent(query)}`, { headers: { 'Accept': 'application/json' } });
        select.textContent = '';
        if (!res.ok) return;

        const lockers = await res.json();
        for (const l of lockers) {
            const option = el('option', l.name + (l.city ? ', ' + l.city : ''));
            option.value = l.id;
            select.appendChild(option);
        }
        if (lockers.length === 0) select.appendChild(el('option', tr('ship_no_lockers') || 'No parcel lockers found'));
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
        const lockerId = parseInt(document.getElementById('shipLockerSelect').value, 10);
        if (!lockerId) return showBuyError(errorText('locker_invalid'));

        const body = {
            productId: buyProductId,
            lockerId,
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

    // Price refresh when the seller changes the parcel size.
    document.addEventListener('change', e => {
        if (e.target && e.target.id === 'shipAcceptSize') updateAcceptPrice();
    });
    document.addEventListener('input', e => {
        if (e.target && e.target.id === 'shipLockerSearch') onLockerSearchInput();
    });
})();

(function () {
    'use strict';

    function selectAll(el) {
        if (!el) return;
        try {
            if (typeof el.select === 'function') {
                var ro = el.readOnly;
                el.readOnly = false;
                el.focus();
                el.select();
                if (typeof el.setSelectionRange === 'function')
                    el.setSelectionRange(0, (el.value || '').length);
                el.readOnly = ro;
            } else {
                var range = document.createRange();
                range.selectNodeContents(el);
                var sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(range);
            }
        } catch { }
    }

    function legacyCopy(text) {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.style.position = 'fixed';
        ta.style.top = '0';
        ta.style.left = '0';
        ta.style.opacity = '0';
        ta.style.fontSize = '16px';
        document.body.appendChild(ta);
        try {
            ta.focus();
            ta.readOnly = false;
            ta.select();
            ta.setSelectionRange(0, text.length);
            ta.readOnly = true;
            return document.execCommand('copy');
        } catch {
            return false;
        } finally {
            document.body.removeChild(ta);
        }
    }

    function feedback(btn, ok, target) {
        var original = btn.getAttribute('data-um-copy-original');
        if (original === null) {
            original = btn.textContent;
            btn.setAttribute('data-um-copy-original', original);
        }
        if (ok) {
            btn.textContent = btn.getAttribute('data-um-copied-label') || 'Copiado!';
            btn.classList.add('um-copied');
        } else {
            if (target) selectAll(target);
            btn.textContent = 'Segure e copie';
            btn.classList.add('um-copy-failed');
        }
        clearTimeout(btn.__umCopyTimer);
        btn.__umCopyTimer = setTimeout(function () {
            btn.textContent = original;
            btn.classList.remove('um-copied', 'um-copy-failed');
        }, 2200);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target && e.target.closest
            ? e.target.closest('[data-um-copy],[data-um-copy-target]')
            : null;
        if (!btn) return;

        var target = null;
        var text = btn.getAttribute('data-um-copy');
        if (!text) {
            target = document.getElementById(btn.getAttribute('data-um-copy-target') || '');
            text = target ? (target.value || target.textContent || '') : '';
        }
        if (!text) return;

        if (legacyCopy(text)) { feedback(btn, true, target); return; }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(
                function () { feedback(btn, true, target); },
                function () { feedback(btn, false, target); });
            return;
        }
        feedback(btn, false, target);
    });

    window.umCopyToClipboard = async function (text) {
        if (text == null) return false;
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch { }
        try { return legacyCopy(String(text)); } catch { return false; }
    };
})();

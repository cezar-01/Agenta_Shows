(function () {
    var bar = document.getElementById('um-progress');
    if (!bar) return;

    var active = false, settle = null, safety = null;

    function start() {
        if (active) return;
        active = true;
        bar.classList.remove('um-progress--done');
        void bar.offsetWidth;
        bar.classList.add('um-progress--active');
        clearTimeout(safety);
        safety = setTimeout(finish, 10000);
    }

    function finish() {
        if (!active) return;
        active = false;
        clearTimeout(settle);
        clearTimeout(safety);
        bar.classList.remove('um-progress--active');
        bar.classList.add('um-progress--done');
        setTimeout(function () { bar.classList.remove('um-progress--done'); }, 450);
    }

    document.addEventListener('click', function (e) {
        var a = e.target && e.target.closest ? e.target.closest('a[href]') : null;
        if (!a) return;
        var href = a.getAttribute('href') || '';
        if (href && href.charAt(0) !== '#' && a.target !== '_blank' && a.origin === location.origin) {
            start();
        }
    }, true);

    var _push = history.pushState;
    history.pushState = function () { start(); return _push.apply(this, arguments); };
    window.addEventListener('popstate', start);

    var obs = new MutationObserver(function () {
        if (!active) return;
        clearTimeout(settle);
        settle = setTimeout(finish, 350);
    });
    obs.observe(document.body, { childList: true, subtree: true });
})();

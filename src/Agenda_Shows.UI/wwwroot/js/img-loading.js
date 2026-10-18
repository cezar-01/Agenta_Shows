(function () {
    function mark(img) {
        if (!img || img.tagName !== 'IMG') return;
        if (img.complete && img.naturalWidth > 0) {
            img.classList.add('img-loaded');
        }
    }

    document.addEventListener('load', function (e) {
        var t = e.target;
        if (t && t.tagName === 'IMG') { t.classList.add('img-loaded'); }
    }, true);
    document.addEventListener('error', function (e) {
        var t = e.target;
        if (t && t.tagName === 'IMG') { t.classList.add('img-error'); }
    }, true);

    var obs = new MutationObserver(function (muts) {
        for (var i = 0; i < muts.length; i++) {
            var nodes = muts[i].addedNodes;
            for (var j = 0; j < nodes.length; j++) {
                var n = nodes[j];
                if (!n || n.nodeType !== 1) continue;
                if (n.tagName === 'IMG') { mark(n); }
                else if (n.querySelectorAll) {
                    var imgs = n.querySelectorAll('img');
                    for (var k = 0; k < imgs.length; k++) { mark(imgs[k]); }
                }
            }
        }
    });
    obs.observe(document.documentElement, { childList: true, subtree: true });

    var all = document.querySelectorAll('img');
    for (var i = 0; i < all.length; i++) { mark(all[i]); }
})();

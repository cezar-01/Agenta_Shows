window.umGeo = {
    permissionState: function () {
        return new Promise(function (resolve) {
            try {
                if (window.isSecureContext === false) { resolve('insecure'); return; }
                if (!('geolocation' in navigator)) { resolve('unsupported'); return; }
                if (!navigator.permissions || !navigator.permissions.query) { resolve('unknown'); return; }
                navigator.permissions.query({ name: 'geolocation' })
                    .then(function (s) { resolve(s && s.state ? s.state : 'unknown'); })
                    .catch(function () { resolve('unknown'); });
            } catch (e) { resolve('unknown'); }
        });
    },

    get: function (fresh) {
        return new Promise(function (resolve) {
            if (window.isSecureContext === false) {
                resolve({ status: 'insecure', lat: 0, lon: 0 });
                return;
            }
            if (!('geolocation' in navigator)) {
                resolve({ status: 'unsupported', lat: 0, lon: 0 });
                return;
            }
            var done = false;
            var finish = function (r) {
                if (done) return;
                done = true;
                clearTimeout(watchdog);
                resolve(r);
            };
            var watchdog = setTimeout(function () {
                finish({ status: 'timeout', lat: 0, lon: 0 });
            }, 35000);
            var ok = function (pos) {
                finish({ status: 'ok', lat: pos.coords.latitude, lon: pos.coords.longitude });
            };
            navigator.geolocation.getCurrentPosition(
                ok,
                function (err) {
                    if (err.code === 1) { finish({ status: 'denied', lat: 0, lon: 0 }); return; }
                    navigator.geolocation.getCurrentPosition(
                        ok,
                        function (err2) {
                            var status = err2.code === 1 ? 'denied' : err2.code === 3 ? 'timeout' : 'unavailable';
                            finish({ status: status, lat: 0, lon: 0 });
                        },
                        { enableHighAccuracy: true, timeout: 12000, maximumAge: 0 }
                    );
                },
                { enableHighAccuracy: false, timeout: 10000, maximumAge: fresh ? 0 : 300000 }
            );
        });
    },

    // O ponto do celular com precisão alta (o "Perto de mim" e o alfinete do cadastro): chamado so no toque do cliente, com a precisao alta e sem
    // posicao guardada. A margem (accuracy, em metros) vai junto: e ela que o servidor confere.
    preciso: function () {
        return new Promise(function (resolve) {
            if (window.isSecureContext === false) { resolve({ status: 'insecure', lat: 0, lon: 0, acc: 0 }); return; }
            if (!('geolocation' in navigator)) { resolve({ status: 'unsupported', lat: 0, lon: 0, acc: 0 }); return; }
            var done = false;
            var finish = function (r) {
                if (done) return;
                done = true;
                clearTimeout(watchdog);
                resolve(r);
            };
            var watchdog = setTimeout(function () { finish({ status: 'timeout', lat: 0, lon: 0, acc: 0 }); }, 20000);
            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    finish({ status: 'ok', lat: pos.coords.latitude, lon: pos.coords.longitude, acc: pos.coords.accuracy || 0 });
                },
                function (err) {
                    var status = err.code === 1 ? 'denied' : err.code === 3 ? 'timeout' : 'unavailable';
                    finish({ status: status, lat: 0, lon: 0, acc: 0 });
                },
                { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
            );
        });
    }
};

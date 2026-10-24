/* Minimal same-origin service worker required for reliable PWA installation. */
var TANDIS_CACHE = 'tandis-pwa-v2';
var TANDIS_SHELL = [
    '/manifest.json',
    '/images/app-icon-192.png',
    '/images/app-icon-512.png',
    '/offline.html'
];

self.addEventListener('install', function (event) {
    self.skipWaiting();
    event.waitUntil(
        caches.open(TANDIS_CACHE)
            .then(function (cache) { return cache.addAll(TANDIS_SHELL); })
            .catch(function () { })
    );
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (keys) {
            return Promise.all(keys.filter(function (key) {
                return key.indexOf('tandis-pwa-') === 0 && key !== TANDIS_CACHE;
            }).map(function (key) { return caches.delete(key); }));
        }).then(function () { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function (event) {
    var request = event.request;
    if (request.method !== 'GET') return;

    var url = new URL(request.url);
    if (url.origin !== self.location.origin) return;

    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request).catch(function () { return caches.match('/offline.html'); })
        );
        return;
    }

    if (request.destination !== 'style' &&
        request.destination !== 'script' &&
        request.destination !== 'image' &&
        request.destination !== 'font' &&
        request.destination !== 'manifest') return;

    event.respondWith(
        caches.match(request).then(function (cached) {
            return cached || fetch(request).then(function (response) {
                if (!response || !response.ok) return response;
                var copy = response.clone();
                caches.open(TANDIS_CACHE).then(function (cache) { cache.put(request, copy); });
                return response;
            });
        })
    );
});

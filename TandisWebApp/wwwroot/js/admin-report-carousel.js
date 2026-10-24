(function () {
    'use strict';

    var sourceGrid = document.getElementById('adminReportGrid');
    var stage = document.getElementById('adminReportStage');
    var label = document.getElementById('adminReportLabel');
    var dots = document.getElementById('adminReportDots');
    var allGrid = document.getElementById('adminReportAllGrid');
    var overlay = document.getElementById('adminReportAllOverlay');
    var showAll = document.getElementById('btnShowAllAdminReports');
    var closeAll = document.getElementById('btnCloseAllAdminReports');

    if (!sourceGrid || !stage || !label || !dots || !allGrid || !overlay || !showAll || !closeAll) return;

    var links = Array.prototype.slice.call(sourceGrid.querySelectorAll('.admin-report-link'));
    if (!links.length) return;

    var items = links.map(function (link) {
        var card = link.querySelector('.report-card');
        var icon = card ? card.querySelector('.rc-icon') : null;
        var title = card ? card.querySelector('.rc-title') : null;
        return {
            href: link.getAttribute('href') || '#',
            title: title ? title.textContent.trim() : '',
            iconHtml: icon ? icon.innerHTML : '<i class="bi bi-bar-chart-line-fill" aria-hidden="true"></i>'
        };
    });

    var slides = [];
    var dotButtons = [];
    var current = 0;
    var returnFocus = null;
    var touchStartX = 0;
    var touchStartY = 0;
    var trackingTouch = false;

    items.forEach(function (item, index) {
        var slide = document.createElement('a');
        slide.className = 'admin-report-slide';
        slide.setAttribute('data-index', String(index));
        slide.href = item.href;
        slide.setAttribute('aria-label', item.title);
        slide.innerHTML = '<div class="admin-report-icon">' + item.iconHtml + '</div>' +
            '<div class="admin-report-title">' + item.title + '</div>';
        stage.appendChild(slide);
        slides.push(slide);

        var dot = document.createElement('button');
        dot.type = 'button';
        dot.className = 'admin-report-dot';
        dot.setAttribute('aria-label', item.title);
        dot.setAttribute('aria-pressed', 'false');
        dot.addEventListener('click', function () { goTo(index); });
        dots.appendChild(dot);
        dotButtons.push(dot);

        var allLink = links[index].cloneNode(true);
        allGrid.appendChild(allLink);
    });

    function goTo(index) {
        current = (index + items.length) % items.length;
        slides.forEach(function (slide, i) {
            var rel = ((i - current) % items.length + items.length) % items.length;
            var distance = Math.min(rel, items.length - rel);
            var side = rel === 0 ? 0 : (rel <= Math.floor(items.length / 2) ? -1 : 1);
            var transform;
            var opacity;

            if (distance === 0) {
                transform = 'translateX(0) translateZ(0) rotateY(0deg) scale(1)';
                opacity = 1;
            } else if (distance === 1) {
                transform = 'translateX(' + (side * 62) + '%) translateZ(-140px) rotateY(' + (-side * 42) + 'deg)';
                opacity = .75;
            } else if (distance === 2) {
                transform = 'translateX(' + (side * 85) + '%) translateZ(-260px) rotateY(' + (-side * 52) + 'deg)';
                opacity = .45;
            } else if (distance === 3) {
                transform = 'translateX(' + (side * 95) + '%) translateZ(-360px) rotateY(' + (-side * 58) + 'deg)';
                opacity = .22;
            } else {
                transform = 'translateX(0) translateZ(-460px)';
                opacity = 0;
            }

            slide.style.transform = transform;
            slide.style.opacity = String(opacity);
            slide.style.zIndex = String(100 - distance);
            slide.classList.toggle('is-active', distance === 0);
            slide.setAttribute('tabindex', distance === 0 ? '0' : '-1');
            slide.setAttribute('aria-hidden', distance > 3 ? 'true' : 'false');
        });
        dotButtons.forEach(function (dot, i) {
            dot.classList.toggle('is-active', i === current);
            dot.setAttribute('aria-pressed', i === current ? 'true' : 'false');
        });
        label.textContent = items[current].title;
    }

    function next() { goTo(current + 1); }
    function previous() { goTo(current - 1); }

    stage.addEventListener('click', function (event) {
        var slide = event.target.closest && event.target.closest('.admin-report-slide');
        if (!slide) return;
        var index = Number(slide.getAttribute('data-index'));
        if (!isFinite(index) || index === current) return;
        event.preventDefault();
        var relative = ((index - current) % items.length + items.length) % items.length;
        goTo(relative <= Math.floor(items.length / 2) ? current + relative : current - (items.length - relative));
    });

    stage.addEventListener('keydown', function (event) {
        if (!event.target.closest || !event.target.closest('.admin-report-slide')) return;
        if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            event.preventDefault();
            if (event.key === 'ArrowLeft') next();
            else previous();
            var active = stage.querySelector('.admin-report-slide.is-active');
            if (active) active.focus();
        }
    });

    function openAllReports() {
        returnFocus = document.activeElement;
        overlay.hidden = false;
        overlay.setAttribute('aria-hidden', 'false');
        showAll.setAttribute('aria-expanded', 'true');
        document.body.classList.add('admin-report-modal-open');
        requestAnimationFrame(function () {
            overlay.classList.add('is-open');
            closeAll.focus();
        });
    }

    function closeAllReports() {
        overlay.classList.remove('is-open');
        overlay.setAttribute('aria-hidden', 'true');
        showAll.setAttribute('aria-expanded', 'false');
        document.body.classList.remove('admin-report-modal-open');
        window.setTimeout(function () { overlay.hidden = true; }, 240);
        if (returnFocus && document.documentElement.contains(returnFocus)) returnFocus.focus();
    }

    showAll.addEventListener('click', openAllReports);
    closeAll.addEventListener('click', closeAllReports);
    overlay.querySelectorAll('[data-admin-report-close]').forEach(function (node) {
        node.addEventListener('click', closeAllReports);
    });

    document.addEventListener('keydown', function (event) {
        if (overlay.classList.contains('is-open')) {
            if (event.key === 'Escape') {
                event.preventDefault();
                closeAllReports();
                return;
            }
            if (event.key === 'Tab') {
                var focusable = overlay.querySelectorAll('button, a[href]');
                var first = focusable[0];
                var last = focusable[focusable.length - 1];
                if (!first || !last) return;
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first.focus();
                }
            }
            return;
        }
    });

    stage.addEventListener('touchstart', function (event) {
        if (!event.touches || !event.touches.length) return;
        touchStartX = event.touches[0].clientX;
        touchStartY = event.touches[0].clientY;
        trackingTouch = true;
    }, { passive: true });

    stage.addEventListener('touchend', function (event) {
        if (!trackingTouch || !event.changedTouches || !event.changedTouches.length) return;
        trackingTouch = false;
        var dx = event.changedTouches[0].clientX - touchStartX;
        var dy = event.changedTouches[0].clientY - touchStartY;
        if (Math.abs(dx) > 42 && Math.abs(dx) > Math.abs(dy)) {
            // RTL/موبایل: کشیدن به چپ باید کارت بعدی را نشان بدهد.
            if (dx < 0) previous();
            else next();
        }
    }, { passive: true });

    goTo(0);
})();

/* Coach shell enhancements: accessible navigation, motion hooks and touch-friendly cards. */
(function () {
    'use strict';

    var body = document.body;
    if (!body || !body.classList.contains('coach-glass-body')) return;

    var reduceMotion = false;
    if (window.matchMedia) {
        reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function addMotionHooks() {
        var selector = [
            '.coach-banner', '.cstat-card', '.coach-card', '.cc-card', '.cs-card',
            '.sd-card', '.sd-box', '.ms-card', '.ch-box', '.cm-filter', '.cm-card',
            '.cm-box', '.pg-card', '.pd-info', '.pd-day', '.pe-card', '.cc-empty',
            '.cs-empty', '.ms-empty', '.pg-empty'
        ].join(',');
        var items = document.querySelectorAll(selector);
        for (var i = 0; i < items.length; i++) {
            items[i].classList.add('coach-motion-item');
            items[i].style.setProperty('--coach-motion-delay', Math.min(i, 8) * 35 + 'ms');
        }
        if (!reduceMotion) body.classList.add('coach-motion-ready');
    }

    function markCurrentNavigation() {
        var path = (window.location.pathname || '/').toLowerCase();
        var key = 'home';
        if (path.indexOf('/coach/classes') === 0 ||
            path.indexOf('/coach/classstudents') === 0 ||
            path.indexOf('/coach/studentdetails') === 0) {
            key = 'classes';
        } else if (path.indexOf('/coach/programs') === 0 ||
                   path.indexOf('/coach/programedit') === 0 ||
                   path.indexOf('/coach/programdetails') === 0) {
            key = 'programs';
        } else if (path.indexOf('/coach/messages') === 0 ||
                   path.indexOf('/coach/chat') === 0) {
            key = 'messages';
        }

        var bottomItems = document.querySelectorAll('#coachBottomNav .cbn-item');
        for (var i = 0; i < bottomItems.length; i++) {
            var active = bottomItems[i].getAttribute('data-key') === key;
            bottomItems[i].classList.toggle('cbn-active', active);
            if (active) bottomItems[i].setAttribute('aria-current', 'page');
            else bottomItems[i].removeAttribute('aria-current');
        }

        var topLinks = document.querySelectorAll('.coach-top-nav .nav-link');
        for (var j = 0; j < topLinks.length; j++) {
            if (topLinks[j].classList.contains('nav-active')) topLinks[j].setAttribute('aria-current', 'page');
            else topLinks[j].removeAttribute('aria-current');
        }
    }

    function enhanceKeyboardCards() {
        var students = document.getElementById('btnAllStudents');
        if (students) {
            students.setAttribute('role', 'button');
            students.setAttribute('tabindex', '0');
            students.setAttribute('aria-controls', 'stuOverlay');
            students.addEventListener('keydown', function (event) {
                if (event.key !== 'Enter' && event.key !== ' ') return;
                event.preventDefault();
                students.click();
            });
        }

        var messageCards = document.querySelectorAll('.ms-card[onclick]');
        for (var i = 0; i < messageCards.length; i++) {
            messageCards[i].setAttribute('role', 'button');
            messageCards[i].setAttribute('tabindex', '0');
            messageCards[i].setAttribute('aria-expanded', 'false');
            messageCards[i].addEventListener('keydown', function (event) {
                if (event.key !== 'Enter' && event.key !== ' ') return;
                event.preventDefault();
                this.click();
            });
        }

        var commissionHeads = document.querySelectorAll('.cmr-head');
        for (var j = 0; j < commissionHeads.length; j++) {
            commissionHeads[j].setAttribute('role', 'button');
            commissionHeads[j].setAttribute('tabindex', '0');
            commissionHeads[j].setAttribute('aria-expanded', 'false');
            commissionHeads[j].addEventListener('keydown', function (event) {
                if (event.key !== 'Enter' && event.key !== ' ') return;
                event.preventDefault();
                this.click();
            });
        }
    }

    addMotionHooks();
    markCurrentNavigation();
    enhanceKeyboardCards();

    window.addEventListener('resize', markCurrentNavigation, { passive: true });
})();

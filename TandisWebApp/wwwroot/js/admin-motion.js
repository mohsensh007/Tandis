(function () {
    'use strict';

    var documentRef = document;
    var body = documentRef.body;
    if (!body) return;

    var scope = documentRef.querySelector('[data-admin-shell]') || body;
    var reducedMotionQuery = window.matchMedia
        ? window.matchMedia('(prefers-reduced-motion: reduce)')
        : null;
    var reducedMotion = !!(reducedMotionQuery && reducedMotionQuery.matches);
    var cardSelector = [
        '[data-admin-motion="card"]',
        '.card',
        '.dash-card',
        '.report-card',
        '.rep-card',
        '.ch-main-card',
        '.ch-share-card',
        '.ch-side-card',
        '.shift-banner',
        '.admin-setting-card'
    ].join(',');
    var cardIndex = 0;
    var intersectionObserver = null;

    body.classList.add('admin-motion-ready');

    function isElement(value) {
        return value && value.nodeType === 1;
    }

    function matches(element, selector) {
        return isElement(element) && element.matches(selector);
    }

    function isMotionCard(element) {
        return isElement(element) && matches(element, cardSelector);
    }

    function addMotionMetadata(element) {
        if (!isMotionCard(element) || matches(element, '[data-admin-motion="page"]')) {
            return false;
        }

        if (!element.hasAttribute('data-admin-motion')) {
            element.setAttribute('data-admin-motion', 'card');
        }

        element.classList.add('admin-motion-pending');

        if (!element.style.getPropertyValue('--admin-motion-delay')) {
            element.style.setProperty('--admin-motion-delay', Math.min(cardIndex, 7) * 45 + 'ms');
            cardIndex++;
        }

        return true;
    }

    function collectCards() {
        var candidates = scope.querySelectorAll(cardSelector);
        var cards = [];

        for (var i = 0; i < candidates.length; i++) {
            if (addMotionMetadata(candidates[i])) {
                cards.push(candidates[i]);
            }
        }

        return cards;
    }

    function preparePage() {
        var page = matches(scope, '[data-admin-motion="page"]')
            ? scope
            : scope.querySelector('[data-admin-motion="page"]');

        if (page) {
            page.classList.add('admin-motion-pending');
        }

        return page;
    }

    function clearMotionState(element) {
        element.classList.remove('admin-motion-pending');
        element.style.removeProperty('--admin-motion-delay');
    }

    function reveal(element) {
        if (!isElement(element) || element.classList.contains('is-visible')) return;

        element.classList.add('is-visible');

        if (reducedMotion || !element.classList.contains('admin-motion-pending')) {
            clearMotionState(element);
            return;
        }

        var cleaned = false;
        function cleanup(event) {
            if (cleaned || (event && event.target !== element)) return;

            cleaned = true;
            element.removeEventListener('transitionend', cleanup);
            clearMotionState(element);
        }

        element.addEventListener('transitionend', cleanup);
        window.setTimeout(cleanup, 1000);
    }

    function revealPage(page) {
        if (!page) return;

        if (reducedMotion || !window.requestAnimationFrame) {
            reveal(page);
            return;
        }

        window.requestAnimationFrame(function () {
            reveal(page);
        });
    }

    function observeCards(cards) {
        if (!cards.length) return;

        if (reducedMotion || !intersectionObserver) {
            cards.forEach(reveal);
            return;
        }

        cards.forEach(function (card) {
            intersectionObserver.observe(card);
        });
    }

    var cards = collectCards();
    var page = preparePage();
    revealPage(page);

    if (!reducedMotion && 'IntersectionObserver' in window) {
        intersectionObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting && entry.intersectionRatio <= 0) return;

                reveal(entry.target);
                intersectionObserver.unobserve(entry.target);
            });
        }, {
            root: null,
            rootMargin: '0px 0px -8% 0px',
            threshold: 0.05
        });
    }

    observeCards(cards);

    if ('MutationObserver' in window) {
        var mutationObserver = new MutationObserver(function (records) {
            var addedCards = [];

            records.forEach(function (record) {
                for (var i = 0; i < record.addedNodes.length; i++) {
                    var node = record.addedNodes[i];
                    if (!isElement(node)) continue;

                    if (addMotionMetadata(node) && addedCards.indexOf(node) === -1) {
                        addedCards.push(node);
                    }

                    var nestedCards = node.querySelectorAll(cardSelector);
                    for (var j = 0; j < nestedCards.length; j++) {
                        if (addMotionMetadata(nestedCards[j]) && addedCards.indexOf(nestedCards[j]) === -1) {
                            addedCards.push(nestedCards[j]);
                        }
                    }
                }
            });

            observeCards(addedCards);
        });

        mutationObserver.observe(scope, {
            childList: true,
            subtree: true
        });
    }
})();

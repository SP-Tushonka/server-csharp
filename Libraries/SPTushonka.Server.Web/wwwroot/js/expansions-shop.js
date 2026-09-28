/**
 * Parallax drift of the store background. The layer is 150px wider than the viewport and inset 75px,
 * and the wrapper moves by up to that much. It is only viewport tall, so it only moves sideways
 */
(function () {
    var RANGE = 75;
    var layer = null;
    var targetX = 0;
    var currentX = 0;
    var running = false;

    /**
     * Find the parallax wrapper
     * @returns {boolean} True once found
     */
    function find() {
        layer = document.querySelector('.parallax-wrapper');
        return layer !== null;
    }

    /**
     * Move the layer one frame toward its target
     */
    function step() {
        // Ease toward the pointer rather than tracking it exactly, so the drift stays soft.
        currentX += (targetX - currentX) * 0.06;

        if (layer) {
            layer.style.transform = 'translate3d(' + currentX.toFixed(2) + 'px, 0, 0)';
        }

        if (Math.abs(targetX - currentX) > 0.1) {
            requestAnimationFrame(step);
        } else {
            running = false;
        }
    }

    document.addEventListener('mousemove', function (event) {
        if (layer === null && !find()) {
            return;
        }

        // -1 at the left edge and 1 at the right. The layer moves against the pointer.
        var x = (event.clientX / window.innerWidth) * 2 - 1;
        targetX = -x * RANGE;

        if (!running) {
            running = true;
            requestAnimationFrame(step);
        }
    }, { passive: true });
})();

/**
 * Close the shop. The client reads {type, payload} over the Vuplex bridge, with the camel case type
 * such as closeShop rather than the EBrowserEventType member name
 */
window.shopExit = function () {
    if (window.vuplex) {
        window.vuplex.postMessage({ type: 'closeShop', payload: {} });
    }
};

/**
 * Messages from the page to the game. UI sounds, error toasts, the purchase dialog's countdown and reveal,
 * and transaction signing. A sound is posted as {type: <EBrowserSoundEventType name>} with no payload
 */
(function () {
    /**
     * Post a message to the game over the Vuplex bridge
     * @param {object} message Message with its type at the top level
     */
    function post(message) {
        if (window.vuplex) {
            window.vuplex.postMessage(message);
        }
    }

    /**
     * Play a UI sound in the game
     * @param {string} name EBrowserSoundEventType name
     */
    window.shopSound = function (name) {
        post({ type: name });
    };

    /**
     * Show an error toast in the game
     * @param {string} summary Toast title
     * @param {string} detail Toast text
     */
    window.shopNotify = function (summary, detail) {
        post({ type: 'shopNotification', severity: 'error', summary: summary, detail: detail });
    };

    // Escape dismisses the purchase dialog through its mask, leaves an item page through its back button,
    // and closes the shop anywhere else. The clicks play their own sounds.
    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') {
            return;
        }

        var mask = document.querySelector('.s-dialog__mask');
        var back = document.querySelector('.catalog-item-nav__back-btn');
        if (mask) {
            mask.click();
        } else if (back) {
            back.click();
        } else {
            post({ type: 'closeShop', payload: {} });
        }
    });

    // Sounds by element as [selector, hover sound, click sound]. The first match wins, so specific ones go first.
    var rules = [
        ['.category-tree__root-button', 'MainMenuButtonHover', 'MainMenuButtonClick'],
        ['.category-tree-node__button', 'SubMenuButtonHover', 'SubMenuButtonClick'],
        ['.product-horizontal-carousel__arrow', 'OfferSlideHover', 'OfferSlideClick'],
        ['.product-horizontal-carousel__media', 'OfferCardHover', 'OfferCardClick'],
        ['.s-counter__button', 'ButtonHover', 'PlusButtonClick'],
        ['.button, .btn, .tab, .s-tab-view__tab, .user-balance', 'ButtonHover', 'ButtonClick'],
        ['.product-card', 'OfferCardHover', null],
        ['.product-card > a', null, 'OfferCardClick'],
    ];

    /**
     * Find the sound an element plays
     * @param {Element} target Element the event came from
     * @param {number} index 1 for hover, 2 for click
     * @returns {{element: Element, sound: string}|null} The matched element and its sound
     */
    function match(target, index) {
        for (var i = 0; i < rules.length; i++) {
            var element = target.closest && target.closest(rules[i][0]);
            if (element && rules[i][index]) {
                return { element: element, sound: rules[i][index] };
            }
        }

        return null;
    }

    // mouseover bubbles where mouseenter does not, so a move within the same element is skipped here.
    document.addEventListener('mouseover', function (event) {
        var hit = match(event.target, 1);
        if (hit && !(event.relatedTarget && hit.element.contains(event.relatedTarget)) && !hit.element.disabled) {
            window.shopSound(hit.sound);
        }
    }, { passive: true });

    document.addEventListener('click', function (event) {
        var hit = match(event.target, 2);
        if (!hit || hit.element.disabled) {
            return;
        }

        window.shopSound(hit.sound);
        if (hit.sound === 'OfferSlideClick') {
            window.shopSound('OfferSlideAnimationStart');
        }

        // A tab with a sound theme switches the game's theme too
        var theme = hit.element.dataset && hit.element.dataset.soundTheme;
        if (theme) {
            post({ type: 'shopChangeSoundThemeEvent', theme: theme });
        }
    }, true);

    /**
     * Purchase dialog told when the countdown and the reveal wipe finish
     */
    var dialog = null;

    /**
     * Register the open purchase dialog
     * @param {object} dotnet DotNetObjectReference to the dialog
     */
    window.shopPurchaseDialog = function (dotnet) {
        dialog = dotnet;
    };

    /**
     * Count down in MM:SS.mmm every frame from the element's data-countdown-ms, then tell the dialog
     * @param {HTMLElement} element Countdown text
     */
    function countdown(element) {
        var end = performance.now() + (parseInt(element.dataset.countdownMs, 10) || 0);

        function tick() {
            if (!element.isConnected) {
                return;
            }

            var left = Math.max(0, end - performance.now());
            var minutes = Math.floor(left / 60000);
            var seconds = Math.floor(left / 1000) % 60;
            var millis = Math.floor(left % 1000);
            element.textContent = String(minutes).padStart(2, '0') + ':' + String(seconds).padStart(2, '0') + '.' + String(millis).padStart(3, '0');

            if (left > 0) {
                requestAnimationFrame(tick);
            } else if (dialog) {
                dialog.invokeMethodAsync('CountdownComplete');
            }
        }

        requestAnimationFrame(tick);
    }

    // When the reveal wipe over the monochrome banner ends, hide the scanline and the noise and play the
    // accepted video straight away. The server catches up with its own render after.
    document.addEventListener('animationend', function (event) {
        var image = event.target;
        if (image.tagName !== 'IMG' || !image.classList.contains('purchase-banner__img--mono') || event.animationName.indexOf('reveal-wipe') === -1) {
            return;
        }

        var media = image.closest('.purchase-banner__media');
        if (media) {
            media.querySelectorAll('.purchase-banner__scanline, video.purchase-banner__img--mono').forEach(function (node) {
                node.style.visibility = 'hidden';
            });

            var accepted = media.querySelector('video.purchase-banner__video:not(.purchase-banner__img--mono)');
            if (accepted) {
                accepted.play().catch(function () {});
            }
        }

        if (dialog) {
            dialog.invokeMethodAsync('WipeEnded');
        }
    }, true);

    // Load the popup videos up front so the first purchase does not stall on them.
    ['Animation_NOISE_BG_EKRAN_animation_2menshecontrst.webm', 'Animation_STORE_2pt.webm', 'Animation_NOISE_BG_EKRAN.webm'].forEach(function (name) {
        var video = document.createElement('video');
        video.preload = 'auto';
        video.muted = true;
        video.src = '/files/shop/' + name;
        video.load();
    });

    /**
     * Ask the game to sign a purchase, which it does at /v2/client/shop/purchase/sign to complete it
     * @param {string} transactionId Transaction from the purchase
     */
    window.shopSignTransaction = function (transactionId) {
        post({ type: 'needTransactionSign', transactionId: transactionId });
    };

    // The dialog's open and close sounds follow it in the dom, and a countdown starts as soon as it appears.
    new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType !== 1) {
                    return;
                }
                if (node.matches('.s-dialog') || node.querySelector('.s-dialog')) {
                    window.shopSound('GenericPopupOpen');
                }
                (node.matches('[data-countdown-ms]') ? [node] : node.querySelectorAll('[data-countdown-ms]')).forEach(countdown);
            });
            mutation.removedNodes.forEach(function (node) {
                if (node.nodeType === 1 && (node.matches('.s-dialog') || node.querySelector('.s-dialog'))) {
                    window.shopSound('GenericPopupClose');
                }
            });
        });
    }).observe(document.documentElement, { childList: true, subtree: true });
})();

/**
 * Gridstack cell height, derived from the rendered width. Measured after every render because blazor
 * replaces the prerendered grid with its own node, and again on resize
 */
(function () {
    /**
     * Set a grid's cell height from its width
     * @param {HTMLElement} grid Mosaic grid
     */
    function size(grid) {
        // Setting the height can add or remove a scrollbar, which changes the width the cell was
        // measured from, so settle it rather than trusting the first pass.
        for (var pass = 0; pass < 3; pass++) {
            var cell = grid.clientWidth / 4;
            if (!cell) {
                return;
            }

            var height = cell * (parseInt(grid.dataset.rows, 10) || 0);
            if (grid.style.height === height + 'px') {
                return;
            }

            grid.style.setProperty('--gs-cell-height', cell + 'px');
            grid.style.height = height + 'px';
        }
    }

    var observer = new ResizeObserver(function (entries) {
        entries.forEach(function (entry) { size(entry.target); });
    });

    var watched = new WeakSet();

    /**
     * Size every mosaic grid and watch it for resizes
     */
    window.shopLayoutMosaic = function () {
        document.querySelectorAll('.grid-stack.gs-4').forEach(function (grid) {
            size(grid);

            if (!watched.has(grid)) {
                watched.add(grid);
                observer.observe(grid);
            }
        });
    };

    window.shopLayoutMosaic();
})();

/**
 * Bundle contents carousel. Translates the track and enables or disables the arrows at the ends
 */
(function () {
    /**
     * Get a carousel's parts from one of its arrows
     * @param {Element} arrow Arrow button
     * @returns {{carousel: Element, viewport: Element, track: HTMLElement}|null} The parts, or null outside a carousel
     */
    function parts(arrow) {
        var carousel = arrow.closest('.product-horizontal-carousel');
        if (!carousel) {
            return null;
        }

        var viewport = carousel.querySelector('.product-horizontal-carousel__viewport');
        var track = carousel.querySelector('.product-horizontal-carousel__track');

        return (viewport && track) ? { carousel: carousel, viewport: viewport, track: track } : null;
    }

    /**
     * Get the distance one slide takes, gap included
     * @param {HTMLElement} track Carousel track
     * @returns {number} Width in pixels
     */
    function step(track) {
        var slide = track.firstElementChild;
        if (!slide) {
            return 0;
        }

        var gap = parseFloat(getComputedStyle(track).columnGap) || 0;

        return slide.getBoundingClientRect().width + gap;
    }

    /**
     * Move the track to an offset clamped to the ends, and enable the arrows that can still move it
     * @param {{carousel: Element, viewport: Element, track: HTMLElement}} p Carousel parts
     * @param {number} offset Pixels scrolled
     */
    function apply(p, offset) {
        var limit = Math.max(0, p.track.scrollWidth - p.viewport.clientWidth);
        var next = Math.min(Math.max(0, offset), limit);

        p.track.dataset.offset = next;
        p.track.style.transform = 'translateX(' + -next + 'px)';

        var arrows = p.carousel.querySelectorAll('.product-horizontal-carousel__arrow');
        if (arrows.length === 2) {
            arrows[0].disabled = next <= 0;
            arrows[1].disabled = next >= limit - 0.5;
        }
    }

    document.addEventListener('click', function (event) {
        var arrow = event.target.closest && event.target.closest('.product-horizontal-carousel__arrow');
        if (!arrow || arrow.disabled) {
            return;
        }

        var p = parts(arrow);
        if (!p) {
            return;
        }

        // The first arrow scrolls back, the second forward.
        var back = arrow === p.carousel.querySelector('.product-horizontal-carousel__arrow');
        var offset = parseFloat(p.track.dataset.offset) || 0;

        apply(p, offset + (back ? -step(p.track) : step(p.track)));
    });

    /**
     * Enable each carousel's arrows for its current offset. The right arrow is only enabled when there is
     * something to scroll to
     */
    window.shopLayoutCarousels = function () {
        document.querySelectorAll('.product-horizontal-carousel').forEach(function (carousel) {
            var arrow = carousel.querySelector('.product-horizontal-carousel__arrow');
            var p = arrow && parts(arrow);
            if (p) {
                apply(p, parseFloat(p.track.dataset.offset) || 0);
            }
        });
    };
})();

/**
 * Hide the game's loading overlay. It stays up until the page posts shopHideLoader or its fallback timeout
 * passes. The page is prerendered, so it is ready once it has loaded
 */
(function () {
    var posted = false;

    /**
     * Post shopHideLoader once
     */
    function hideLoader() {
        if (posted || !window.vuplex) {
            return;
        }
        posted = true;
        window.vuplex.postMessage({ type: 'shopHideLoader', payload: {} });
    }
    if (document.readyState === 'complete') {
        hideLoader();
    } else {
        window.addEventListener('load', hideLoader);
    }
})();

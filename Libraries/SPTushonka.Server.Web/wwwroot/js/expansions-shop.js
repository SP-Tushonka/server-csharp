// Live sizes the parallax layer 150px wider than the viewport, inset 75px, and moves the fixed
// wrapper by that much. The layer is only viewport tall, so vertical movement exposes its edge.
(function () {
    var RANGE = 75;
    var layer = null;
    var targetX = 0;
    var currentX = 0;
    var running = false;

    function find() {
        layer = document.querySelector('.parallax-wrapper');
        return layer !== null;
    }

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

        // -1 at the left edge, 1 at the right; the layer moves against the pointer.
        var x = (event.clientX / window.innerWidth) * 2 - 1;
        targetX = -x * RANGE;

        if (!running) {
            running = true;
            requestAnimationFrame(step);
        }
    }, { passive: true });
})();

// Exit button. The client takes {type, payload} over the Vuplex bridge; the type is the camel case
// name live posts (closeShop, shopShowLoader, ...), not the EBrowserEventType member name.
window.shopExit = function () {
    if (window.vuplex) {
        window.vuplex.postMessage({ type: 'closeShop', payload: {} });
    }
};

// Gridstack derives the pixel cell height from the rendered width. Measure after every render,
// since blazor replaces the prerendered grid with its own node, and again on resize.
(function () {
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

// The edition contents carousel. Live ships a component that translates the track and enables or
// disables the arrows at the ends; the markup and styling are theirs, this is only the behaviour.
(function () {
    function parts(arrow) {
        var carousel = arrow.closest('.product-horizontal-carousel');
        if (!carousel) {
            return null;
        }

        var viewport = carousel.querySelector('.product-horizontal-carousel__viewport');
        var track = carousel.querySelector('.product-horizontal-carousel__track');

        return (viewport && track) ? { carousel: carousel, viewport: viewport, track: track } : null;
    }

    function step(track) {
        var slide = track.firstElementChild;
        if (!slide) {
            return 0;
        }

        var gap = parseFloat(getComputedStyle(track).columnGap) || 0;

        return slide.getBoundingClientRect().width + gap;
    }

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

        // The first arrow scrolls back, the second forward, the same order live renders them in.
        var back = arrow === p.carousel.querySelector('.product-horizontal-carousel__arrow');
        var offset = parseFloat(p.track.dataset.offset) || 0;

        apply(p, offset + (back ? -step(p.track) : step(p.track)));
    });

    // Their right arrow is live only when there is something to scroll to.
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

// The client shows its own loading overlay when the shop opens and keeps it up until the page posts
// shopHideLoader, or until its fallback timeout. Live posts it once its initial data has loaded;
// ours is prerendered, so the page is ready as soon as it has loaded.
(function () {
    var posted = false;
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

/**
 * blog-lazy.js — Blog Performance Optimizer
 * =============================================
 * 1. Lazy-load images via native loading="lazy" + IntersectionObserver fallback
 * 2. YouTube Facade: render thumbnail preview instead of iframe at page load.
 *    Only load real iframe when user clicks play → saves ~500 KB per video.
 * 3. Lightbox for article images.
 * 4. ReadTime display.
 * Uses youtube-nocookie.com to avoid cross-site cookie tracking.
 */

(function () {
    'use strict';

    /* ─── 1. Native lazy-load polyfill for older browsers ─────────────────── */
    function applyNativeLazy() {
        if ('loading' in HTMLImageElement.prototype) return; // native support
        // Fallback: IntersectionObserver for browsers without loading="lazy"
        const lazyImages = document.querySelectorAll('img[loading="lazy"]');
        if (!lazyImages.length) return;

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                const img = entry.target;
                if (img.dataset.src) {
                    img.src = img.dataset.src;
                    img.removeAttribute('data-src');
                }
                img.classList.add('lazy-loaded');
                observer.unobserve(img);
            });
        }, { rootMargin: '200px 0px' });

        lazyImages.forEach(function (img) { observer.observe(img); });
    }

    /* ─── 2. YouTube Facade (click-to-play) ──────────────────────────────── */
    /**
     * Replaces every <div data-yt-facade data-yt-id="VIDEO_ID"> with:
     *   - A thumbnail image (hqdefault from img.youtube.com — no cookies)
     *   - A Play button overlay
     * On click → replaces itself with the real iframe using youtube-nocookie.com
     *
     * Markup required in Razor views:
     *   <div class="yt-facade ratio ratio-16x9 rounded-4 overflow-hidden shadow-sm border"
     *        data-yt-facade
     *        data-yt-id="VIDEO_ID"
     *        data-yt-title="Video title for accessibility">
     *   </div>
     */
    function initYouTubeFacades() {
        const facades = document.querySelectorAll('[data-yt-facade]');
        if (!facades.length) return;

        facades.forEach(function (wrapper) {
            const ytId = (wrapper.dataset.ytId || '').trim();
            const title = (wrapper.dataset.ytTitle || 'Video nha khoa 3D').trim();
            if (!ytId) return;

            // Build thumbnail URL — maxresdefault → hqdefault fallback via onerror
            const thumbUrl = 'https://i.ytimg.com/vi/' + ytId + '/hqdefault.jpg';
            const thumbHigh = 'https://i.ytimg.com/vi/' + ytId + '/maxresdefault.jpg';

            wrapper.innerHTML =
                '<img class="yt-facade-thumb" ' +
                    'src="' + thumbHigh + '" ' +
                    'onerror="this.src=\'' + thumbUrl + '\'" ' +
                    'alt="' + _escAttr(title) + '" ' +
                    'loading="lazy" ' +
                    'decoding="async" ' +
                    'width="640" height="360" />' +
                '<button class="yt-facade-btn" aria-label="Phát video: ' + _escAttr(title) + '">' +
                    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 68 48" aria-hidden="true">' +
                        '<path class="yt-btn-bg" d="M66.52 7.74c-.78-2.93-2.49-5.41-5.42-6.19C55.79.13 34 0 34 0S12.21.13 6.9 1.55c-2.93.78-4.63 3.26-5.42 6.19C.06 13.05 0 24 0 24s.06 10.95 1.48 16.26c.78 2.93 2.49 5.41 5.42 6.19C12.21 47.87 34 48 34 48s21.79-.13 27.1-1.55c2.93-.78 4.64-3.26 5.42-6.19C67.94 34.95 68 24 68 24s-.06-10.95-1.48-16.26z"/>' +
                        '<path class="yt-btn-icon" d="M45 24L27 14v20"/>' +
                    '</svg>' +
                '</button>';

            wrapper.setAttribute('role', 'button');
            wrapper.setAttribute('tabindex', '0');
            wrapper.style.cursor = 'pointer';
            wrapper.style.position = 'relative';

            function loadIframe() {
                const iframe = document.createElement('iframe');
                iframe.src = 'https://www.youtube-nocookie.com/embed/' + ytId +
                    '?autoplay=1&rel=0&modestbranding=1&playsinline=1';
                iframe.title = title;
                iframe.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture';
                iframe.allowFullscreen = true;
                iframe.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;border:0;';
                wrapper.innerHTML = '';
                wrapper.appendChild(iframe);
                wrapper.removeAttribute('role');
                wrapper.removeAttribute('tabindex');
                wrapper.style.cursor = '';
            }

            wrapper.addEventListener('click', loadIframe);
            wrapper.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); loadIframe(); }
            });
        });
    }

    /* ─── 3. Lazy load iframes (non-YouTube embeds) ───────────────────────── */
    function lazyLoadIframes() {
        const iframes = document.querySelectorAll('iframe[data-src]');
        if (!iframes.length) return;

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                const iframe = entry.target;
                iframe.src = iframe.dataset.src;
                iframe.removeAttribute('data-src');
                observer.unobserve(iframe);
            });
        }, { rootMargin: '300px 0px' });

        iframes.forEach(function (iframe) { observer.observe(iframe); });
    }

    /* ─── 4. Auto apply loading="lazy" to article content images ─────────── */
    function applyLazyToArticleImages() {
        const articleImages = document.querySelectorAll(
            '.blog-article-content img:not([loading]), ' +
            '.blog-card-thumb:not([loading]), ' +
            '.related-posts-section img:not([loading])'
        );
        articleImages.forEach(function (img) {
            img.setAttribute('loading', 'lazy');
            img.setAttribute('decoding', 'async');
        });
    }

    /* ─── 5. Lightbox for article images ─────────────────────────────────── */
    function initLightbox() {
        const modalEl = document.getElementById('blogImageLightboxModal');
        const lightboxImg = document.getElementById('blogLightboxModalImg');
        if (!modalEl || !lightboxImg || !window.bootstrap) return;

        const lightboxModal = new bootstrap.Modal(modalEl);

        const articleImgs = document.querySelectorAll('.blog-article-content img, .blog-zoomable-img');
        articleImgs.forEach(function (img) {
            img.classList.add('blog-zoomable-img');
            img.style.cursor = 'zoom-in';
            img.addEventListener('click', function () {
                lightboxImg.src = this.src;
                lightboxImg.alt = this.alt || 'Ảnh bài viết phóng to';
                lightboxModal.show();
            });
        });
    }

    /* ─── 6. Smooth scroll for anchor links in article ───────────────────── */
    function initSmoothScroll() {
        document.querySelectorAll('.blog-article-content a[href^="#"]').forEach(function (anchor) {
            anchor.addEventListener('click', function (e) {
                const target = document.getElementById(this.getAttribute('href').slice(1));
                if (target) {
                    e.preventDefault();
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        });
    }

    /* ─── 7. Reading progress bar ─────────────────────────────────────────── */
    function initReadingProgress() {
        const bar = document.getElementById('blogReadingProgress');
        const article = document.querySelector('.blog-article-content');
        if (!bar || !article) return;

        function updateProgress() {
            const rect = article.getBoundingClientRect();
            const articleH = article.offsetHeight;
            const viewH = window.innerHeight;
            // Progress from 0 (top of article at viewport bottom) to 100 (article bottom at viewport top)
            const scrolled = Math.max(0, viewH - rect.top);
            const pct = Math.min(100, Math.round((scrolled / (articleH + viewH)) * 100));
            bar.style.width = pct + '%';
            bar.setAttribute('aria-valuenow', pct);
        }

        window.addEventListener('scroll', updateProgress, { passive: true });
        updateProgress();
    }

    /* ─── 8. Related post images: lazy via IntersectionObserver ──────────── */
    function lazyRelatedImages() {
        const imgs = document.querySelectorAll('.related-posts-section img');
        imgs.forEach(function (img) {
            if (!img.getAttribute('loading')) img.setAttribute('loading', 'lazy');
        });
    }

    /* ─── 9. Fade-in for lazy images using onload ─────────────────────────── */
    function applyFadeInOnLoad() {
        document.querySelectorAll('img[loading="lazy"]').forEach(function (img) {
            if (img.complete && img.naturalWidth > 0) {
                img.classList.add('loaded');
            } else {
                img.addEventListener('load', function () { this.classList.add('loaded'); }, { once: true });
                img.addEventListener('error', function () { this.style.opacity = '1'; }, { once: true });
            }
        });
    }

    /* ─── Helpers ─────────────────────────────────────────────────────────── */
    function _escAttr(str) {
        return str.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    /* ─── Init on DOMContentLoaded ────────────────────────────────────────── */
    function init() {
        applyNativeLazy();
        applyLazyToArticleImages();
        initYouTubeFacades();
        lazyLoadIframes();
        initLightbox();
        initSmoothScroll();
        initReadingProgress();
        lazyRelatedImages();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();

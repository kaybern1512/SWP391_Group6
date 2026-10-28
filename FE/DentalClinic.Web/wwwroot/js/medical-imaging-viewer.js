/**
 * MedicalImagingViewer - High-Precision Dental X-Ray Zoom & Inspection Viewer
 * Day 01: Zoom In / Zoom Out / Fit to Screen / Reset View
 * DentalCare Clinical EMR
 */
(function (global) {
    'use strict';

    const MIN_ZOOM = 0.25; // 25%
    const MAX_ZOOM = 4.0;  // 400%
    const ZOOM_STEP = 0.25; // 25%

    let isInitialized = false;
    let currentZoom = 1.0;
    let isFitToScreen = true;

    // Cached DOM elements
    let modalEl = null;
    let viewportEl = null;
    let imgEl = null;
    let btnZoomIn = null;
    let btnZoomOut = null;
    let btnFitScreen = null;
    let btnResetView = null;
    let badgeZoom = null;

    function clamp(val, min, max) {
        return Math.min(Math.max(val, min), max);
    }

    function updateUI(statusText) {
        if (!badgeZoom) return;

        const percent = Math.round(currentZoom * 100);
        if (statusText) {
            badgeZoom.textContent = statusText;
        } else if (isFitToScreen && currentZoom === 1.0) {
            badgeZoom.textContent = '100% (Vừa khung)';
        } else {
            badgeZoom.textContent = percent + '%';
        }

        if (btnZoomIn) {
            btnZoomIn.disabled = currentZoom >= MAX_ZOOM;
            btnZoomIn.setAttribute('aria-disabled', currentZoom >= MAX_ZOOM ? 'true' : 'false');
        }
        if (btnZoomOut) {
            btnZoomOut.disabled = currentZoom <= MIN_ZOOM;
            btnZoomOut.setAttribute('aria-disabled', currentZoom <= MIN_ZOOM ? 'true' : 'false');
        }
    }

    function applyTransform() {
        if (!imgEl) return;
        imgEl.style.transform = 'scale(' + currentZoom + ')';
    }

    function setZoom(newZoom, originX, originY, isFit) {
        currentZoom = clamp(Math.round(newZoom * 100) / 100, MIN_ZOOM, MAX_ZOOM);
        isFitToScreen = !!isFit;

        if (imgEl) {
            if (typeof originX === 'number' && typeof originY === 'number') {
                imgEl.style.transformOrigin = originX.toFixed(1) + '% ' + originY.toFixed(1) + '%';
            } else {
                imgEl.style.transformOrigin = 'center center';
            }
            applyTransform();
        }

        updateUI();
        return currentZoom;
    }

    function zoomIn(step) {
        isFitToScreen = false;
        const delta = typeof step === 'number' ? step : ZOOM_STEP;
        return setZoom(currentZoom + delta);
    }

    function zoomOut(step) {
        isFitToScreen = false;
        const delta = typeof step === 'number' ? step : ZOOM_STEP;
        return setZoom(currentZoom - delta);
    }

    function fitToScreen() {
        if (!imgEl) return currentZoom;
        imgEl.style.transformOrigin = 'center center';
        currentZoom = 1.0;
        isFitToScreen = true;
        applyTransform();
        updateUI('100% (Vừa khung)');
        return currentZoom;
    }

    function resetView() {
        if (!imgEl) return currentZoom;
        imgEl.style.transformOrigin = 'center center';
        currentZoom = 1.0;
        isFitToScreen = false;
        applyTransform();
        updateUI('100%');
        return currentZoom;
    }

    function onWheel(e) {
        if (!viewportEl || !imgEl || imgEl.classList.contains('d-none')) return;

        // Prevent page/modal scroll while interacting with X-ray viewer
        e.preventDefault();
        e.stopPropagation();

        const rect = imgEl.getBoundingClientRect();
        let originX = 50;
        let originY = 50;

        if (rect.width > 0 && rect.height > 0) {
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;
            originX = clamp((mouseX / rect.width) * 100, 0, 100);
            originY = clamp((mouseY / rect.height) * 100, 0, 100);
        }

        const delta = e.deltaY < 0 ? ZOOM_STEP : -ZOOM_STEP;
        isFitToScreen = false;
        setZoom(currentZoom + delta, originX, originY);
    }

    function onKeyDown(e) {
        if (!modalEl || !modalEl.classList.contains('show')) return;
        const activeTag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
        if (activeTag === 'input' || activeTag === 'textarea' || activeTag === 'select') return;

        if (e.key === '+' || e.key === '=') {
            e.preventDefault();
            zoomIn();
        } else if (e.key === '-' || e.key === '_') {
            e.preventDefault();
            zoomOut();
        } else if (e.key === '0' || e.key === 'r' || e.key === 'R') {
            e.preventDefault();
            resetView();
        } else if (e.key === 'f' || e.key === 'F') {
            e.preventDefault();
            fitToScreen();
        }
    }

    function init() {
        modalEl = document.getElementById('imagingLightboxModal');
        if (!modalEl) return;

        viewportEl = document.getElementById('imagingViewport');
        imgEl = document.getElementById('lightboxImage');
        btnZoomIn = document.getElementById('btnZoomIn');
        btnZoomOut = document.getElementById('btnZoomOut');
        btnFitScreen = document.getElementById('btnFitScreen');
        btnResetView = document.getElementById('btnResetView');
        badgeZoom = document.getElementById('zoomPercentageBadge');

        // Only register DOM event listeners once to prevent duplicate listeners
        if (!isInitialized) {
            if (btnZoomIn) {
                btnZoomIn.addEventListener('click', function (e) {
                    e.preventDefault();
                    zoomIn();
                });
            }

            if (btnZoomOut) {
                btnZoomOut.addEventListener('click', function (e) {
                    e.preventDefault();
                    zoomOut();
                });
            }

            if (btnFitScreen) {
                btnFitScreen.addEventListener('click', function (e) {
                    e.preventDefault();
                    fitToScreen();
                });
            }

            if (btnResetView) {
                btnResetView.addEventListener('click', function (e) {
                    e.preventDefault();
                    resetView();
                });
            }

            if (viewportEl) {
                viewportEl.addEventListener('wheel', onWheel, { passive: false });
            }

            document.addEventListener('keydown', onKeyDown);

            modalEl.addEventListener('hidden.bs.modal', function () {
                resetView();
            });

            isInitialized = true;
        }

        resetView();
    }

    // Auto-init on DOMContentLoaded if DOM is ready
    if (typeof document !== 'undefined') {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    // Public API
    const viewerApi = {
        init: init,
        zoomIn: zoomIn,
        zoomOut: zoomOut,
        setZoom: setZoom,
        fitToScreen: fitToScreen,
        resetView: resetView,
        getCurrentZoom: function () { return currentZoom; },
        getMinZoom: function () { return MIN_ZOOM; },
        getMaxZoom: function () { return MAX_ZOOM; },
        getZoomStep: function () { return ZOOM_STEP; },
        isInitialized: function () { return isInitialized; }
    };

    global.MedicalImagingViewer = viewerApi;

})(typeof window !== 'undefined' ? window : this);

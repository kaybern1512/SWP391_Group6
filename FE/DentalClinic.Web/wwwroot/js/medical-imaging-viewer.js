/**
 * MedicalImagingViewer - High-Precision Dental X-Ray Zoom & Pan Viewer
 * Day 01: Zoom In / Zoom Out / Fit to Screen / Reset View
 * Day 02: Pan / Drag / Pointer Events / Boundary Clamping / Cursor States
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

    // Pan state variables
    let panX = 0;
    let panY = 0;
    let isPanning = false;
    let activePointerId = null;
    let pointerStartX = 0;
    let pointerStartY = 0;
    let panStartX = 0;
    let panStartY = 0;

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

    function getImageDimensions() {
        if (!imgEl) return { width: 0, height: 0 };
        let w = imgEl.clientWidth;
        let h = imgEl.clientHeight;
        if (!w || !h) {
            w = imgEl.naturalWidth || 0;
            h = imgEl.naturalHeight || 0;
        }
        return { width: w, height: h };
    }

    function getViewportDimensions() {
        if (!viewportEl) return { width: 0, height: 0 };
        return {
            width: viewportEl.clientWidth,
            height: viewportEl.clientHeight
        };
    }

    function getMaxPan(zoom) {
        const imgDim = getImageDimensions();
        const vpDim = getViewportDimensions();
        const z = typeof zoom === 'number' ? zoom : currentZoom;

        const scaledW = imgDim.width * z;
        const scaledH = imgDim.height * z;

        const maxX = Math.max(0, (scaledW - vpDim.width) / 2);
        const maxY = Math.max(0, (scaledH - vpDim.height) / 2);

        return {
            maxX: Math.round(maxX * 100) / 100,
            maxY: Math.round(maxY * 100) / 100
        };
    }

    function canPan(zoom) {
        const bounds = getMaxPan(zoom);
        return bounds.maxX > 0 || bounds.maxY > 0;
    }

    function clampPan(x, y, zoom) {
        const bounds = getMaxPan(zoom);
        const clampedX = bounds.maxX > 0 ? clamp(x, -bounds.maxX, bounds.maxX) : 0;
        const clampedY = bounds.maxY > 0 ? clamp(y, -bounds.maxY, bounds.maxY) : 0;
        return {
            x: Math.round(clampedX * 100) / 100,
            y: Math.round(clampedY * 100) / 100
        };
    }

    function updateCursor() {
        if (!viewportEl || !imgEl) return;

        if (isPanning) {
            viewportEl.style.cursor = 'grabbing';
            imgEl.style.cursor = 'grabbing';
            viewportEl.classList.add('is-panning');
            imgEl.classList.add('is-panning');
            viewportEl.classList.remove('can-pan');
            imgEl.classList.remove('can-pan');
        } else if (canPan()) {
            viewportEl.style.cursor = 'grab';
            imgEl.style.cursor = 'grab';
            viewportEl.classList.remove('is-panning');
            imgEl.classList.remove('is-panning');
            viewportEl.classList.add('can-pan');
            imgEl.classList.add('can-pan');
        } else {
            viewportEl.style.cursor = 'default';
            imgEl.style.cursor = 'default';
            viewportEl.classList.remove('is-panning', 'can-pan');
            imgEl.classList.remove('is-panning', 'can-pan');
        }
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

    function applyTransform(withTransition) {
        if (!imgEl) return;
        if (withTransition) {
            imgEl.style.transition = 'transform 0.12s cubic-bezier(0.2, 0, 0, 1)';
        } else {
            imgEl.style.transition = 'none';
        }
        imgEl.style.transformOrigin = 'center center';
        imgEl.style.transform = 'translate(' + panX + 'px, ' + panY + 'px) scale(' + currentZoom + ')';
    }

    function setZoom(newZoom, focalX, focalY, isFit) {
        const oldZoom = currentZoom;
        currentZoom = clamp(Math.round(newZoom * 100) / 100, MIN_ZOOM, MAX_ZOOM);
        isFitToScreen = !!isFit;

        if (currentZoom !== oldZoom && oldZoom > 0) {
            if (typeof focalX === 'number' && typeof focalY === 'number') {
                const ratio = currentZoom / oldZoom;
                panX = focalX - (focalX - panX) * ratio;
                panY = focalY - (focalY - panY) * ratio;
            } else {
                const ratio = currentZoom / oldZoom;
                panX = panX * ratio;
                panY = panY * ratio;
            }
            const clamped = clampPan(panX, panY, currentZoom);
            panX = clamped.x;
            panY = clamped.y;
        }

        applyTransform(true);
        updateUI();
        updateCursor();
        return currentZoom;
    }

    function zoomIn(step) {
        isFitToScreen = false;
        const delta = typeof step === 'number' ? step : ZOOM_STEP;
        return setZoom(currentZoom + delta, null, null, false);
    }

    function zoomOut(step) {
        isFitToScreen = false;
        const delta = typeof step === 'number' ? step : ZOOM_STEP;
        return setZoom(currentZoom - delta, null, null, false);
    }

    function fitToScreen() {
        if (!imgEl) return currentZoom;
        if (isPanning) {
            endPan();
        }
        panX = 0;
        panY = 0;
        currentZoom = 1.0;
        isFitToScreen = true;
        applyTransform(true);
        updateUI('100% (Vừa khung)');
        updateCursor();
        return currentZoom;
    }

    function resetView() {
        if (!imgEl) return currentZoom;
        if (isPanning) {
            endPan();
        }
        panX = 0;
        panY = 0;
        currentZoom = 1.0;
        isFitToScreen = false;
        applyTransform(true);
        updateUI('100%');
        updateCursor();
        return currentZoom;
    }

    function resetPan() {
        if (isPanning) {
            endPan();
        }
        panX = 0;
        panY = 0;
        applyTransform(true);
        updateCursor();
        return { x: panX, y: panY };
    }

    function onWheel(e) {
        if (!viewportEl || !imgEl || imgEl.classList.contains('d-none')) return;

        // Prevent page/modal scroll while interacting with X-ray viewer
        e.preventDefault();
        e.stopPropagation();

        const delta = e.deltaY < 0 ? ZOOM_STEP : -ZOOM_STEP;
        const oldZoom = currentZoom;
        const newZoom = clamp(Math.round((oldZoom + delta) * 100) / 100, MIN_ZOOM, MAX_ZOOM);

        if (newZoom === oldZoom) return;

        isFitToScreen = false;

        // Focal zoom calculation relative to viewport center
        const vpRect = viewportEl.getBoundingClientRect();
        const vpCenterX = vpRect.left + vpRect.width / 2;
        const vpCenterY = vpRect.top + vpRect.height / 2;

        const focalX = e.clientX - vpCenterX;
        const focalY = e.clientY - vpCenterY;

        setZoom(newZoom, focalX, focalY, false);
    }

    function onPointerDown(e) {
        // Only respond to primary/left mouse button (button 0)
        if (e.button !== 0) return;
        if (!viewportEl || !imgEl || imgEl.classList.contains('d-none')) return;

        // Only start pan if image overflows viewport
        if (!canPan()) {
            return;
        }

        e.preventDefault();

        isPanning = true;
        activePointerId = e.pointerId;
        pointerStartX = e.clientX;
        pointerStartY = e.clientY;
        panStartX = panX;
        panStartY = panY;

        try {
            if (viewportEl.setPointerCapture) {
                viewportEl.setPointerCapture(e.pointerId);
            }
        } catch (err) {
            // Ignore capture failure in mock or non-supporting environments
        }

        updateCursor();
        applyTransform(false); // Disable transition for instant, lag-free dragging
    }

    function onPointerMove(e) {
        if (!isPanning) return;
        if (activePointerId !== null && e.pointerId !== activePointerId) return;

        e.preventDefault();

        const dx = e.clientX - pointerStartX;
        const dy = e.clientY - pointerStartY;

        const newX = panStartX + dx;
        const newY = panStartY + dy;

        const clamped = clampPan(newX, newY, currentZoom);
        panX = clamped.x;
        panY = clamped.y;

        applyTransform(false);
    }

    function onPointerUp(e) {
        if (!isPanning) return;
        if (activePointerId !== null && e.pointerId !== activePointerId) return;
        endPan();
    }

    function onPointerCancel(e) {
        if (!isPanning) return;
        endPan();
    }

    function onLostPointerCapture(e) {
        if (isPanning) {
            endPan();
        }
    }

    function endPan() {
        if (!isPanning) return;
        isPanning = false;

        if (activePointerId !== null && viewportEl && viewportEl.releasePointerCapture) {
            try {
                if (viewportEl.hasPointerCapture && viewportEl.hasPointerCapture(activePointerId)) {
                    viewportEl.releasePointerCapture(activePointerId);
                }
            } catch (err) {}
        }
        activePointerId = null;

        updateCursor();
        applyTransform(true); // Re-enable smooth transition
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
                viewportEl.addEventListener('pointerdown', onPointerDown);
                viewportEl.addEventListener('pointermove', onPointerMove);
                viewportEl.addEventListener('pointerup', onPointerUp);
                viewportEl.addEventListener('pointercancel', onPointerCancel);
                viewportEl.addEventListener('lostpointercapture', onLostPointerCapture);
                viewportEl.addEventListener('dragstart', function (e) { e.preventDefault(); });
            }

            if (imgEl) {
                imgEl.addEventListener('dragstart', function (e) { e.preventDefault(); });
                imgEl.addEventListener('load', function () {
                    updateCursor();
                });
            }

            document.addEventListener('keydown', onKeyDown);

            window.addEventListener('resize', function () {
                if (modalEl && modalEl.classList.contains('show')) {
                    const clamped = clampPan(panX, panY, currentZoom);
                    panX = clamped.x;
                    panY = clamped.y;
                    applyTransform(true);
                    updateCursor();
                }
            });

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
        resetPan: resetPan,
        panTo: function (x, y) {
            const clamped = clampPan(x, y, currentZoom);
            panX = clamped.x;
            panY = clamped.y;
            applyTransform(true);
            updateCursor();
            return { x: panX, y: panY };
        },
        getPan: function () {
            return { x: panX, y: panY };
        },
        getMaxPan: function (zoom) {
            return getMaxPan(zoom);
        },
        canPan: function (zoom) {
            return canPan(zoom);
        },
        isPanning: function () {
            return isPanning;
        },
        getCurrentZoom: function () {
            return currentZoom;
        },
        getMinZoom: function () {
            return MIN_ZOOM;
        },
        getMaxZoom: function () {
            return MAX_ZOOM;
        },
        getZoomStep: function () {
            return ZOOM_STEP;
        },
        isInitialized: function () {
            return isInitialized;
        }
    };

    global.MedicalImagingViewer = viewerApi;

})(typeof window !== 'undefined' ? window : this);

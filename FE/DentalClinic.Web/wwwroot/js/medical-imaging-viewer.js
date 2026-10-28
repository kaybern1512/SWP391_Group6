/**
 * MedicalImagingViewer - High-Precision Dental X-Ray Zoom, Pan & Rotate Viewer
 * Day 01: Zoom In / Zoom Out / Fit to Screen / Reset View
 * Day 02: Pan / Drag / Pointer Events / Boundary Clamping / Cursor States
 * Day 03: Rotate 90° Left/Right / Rotation State / Bounding Box Recalculation / Fit on Rotation
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

    // Rotation state variable (0, 90, 180, 270)
    let rotationDegrees = 0;

    // Cached DOM elements
    let modalEl = null;
    let viewportEl = null;
    let imgEl = null;
    let btnZoomIn = null;
    let btnZoomOut = null;
    let btnFitScreen = null;
    let btnResetView = null;
    let btnRotateLeft = null;
    let btnRotateRight = null;
    let btnResetRotation = null;
    let badgeZoom = null;
    let badgeRotation = null;

    function clamp(val, min, max) {
        return Math.min(Math.max(val, min), max);
    }

    function normalizeRotation(deg) {
        let r = deg % 360;
        if (r < 0) r += 360;
        return r;
    }

    function isRotated90(deg) {
        const r = typeof deg === 'number' ? normalizeRotation(deg) : rotationDegrees;
        return r === 90 || r === 270;
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

    function getVisualDimensions(deg) {
        const imgDim = getImageDimensions();
        if (isRotated90(deg)) {
            return { width: imgDim.height, height: imgDim.width };
        }
        return { width: imgDim.width, height: imgDim.height };
    }

    function getMaxPan(zoom, deg) {
        const visualDim = getVisualDimensions(deg);
        const vpDim = getViewportDimensions();
        const z = typeof zoom === 'number' ? zoom : currentZoom;

        const scaledW = visualDim.width * z;
        const scaledH = visualDim.height * z;

        const maxX = Math.max(0, (scaledW - vpDim.width) / 2);
        const maxY = Math.max(0, (scaledH - vpDim.height) / 2);

        return {
            maxX: Math.round(maxX * 100) / 100,
            maxY: Math.round(maxY * 100) / 100
        };
    }

    function canPan(zoom, deg) {
        const bounds = getMaxPan(zoom, deg);
        return bounds.maxX > 0 || bounds.maxY > 0;
    }

    function clampPan(x, y, zoom, deg) {
        const bounds = getMaxPan(zoom, deg);
        const clampedX = bounds.maxX > 0 ? clamp(x, -bounds.maxX, bounds.maxX) : 0;
        const clampedY = bounds.maxY > 0 ? clamp(y, -bounds.maxY, bounds.maxY) : 0;
        return {
            x: Math.round(clampedX * 100) / 100,
            y: Math.round(clampedY * 100) / 100
        };
    }

    function calculateFitZoom(deg) {
        const vpDim = getViewportDimensions();
        const visualDim = getVisualDimensions(deg);
        if (visualDim.width <= 0 || visualDim.height <= 0 || vpDim.width <= 0 || vpDim.height <= 0) {
            return 1.0;
        }
        const scaleX = vpDim.width / visualDim.width;
        const scaleY = vpDim.height / visualDim.height;
        const fitScale = Math.min(scaleX, scaleY, 1.0);
        return clamp(Math.round(fitScale * 100) / 100, MIN_ZOOM, MAX_ZOOM);
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
        if (badgeZoom) {
            const percent = Math.round(currentZoom * 100);
            if (statusText) {
                badgeZoom.textContent = statusText;
            } else if (isFitToScreen) {
                badgeZoom.textContent = percent + '% (Vừa khung)';
            } else {
                badgeZoom.textContent = percent + '%';
            }
        }

        if (badgeRotation) {
            badgeRotation.textContent = rotationDegrees + '°';
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
        imgEl.style.transform = 'translate(' + panX + 'px, ' + panY + 'px) rotate(' + rotationDegrees + 'deg) scale(' + currentZoom + ')';
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
            const clamped = clampPan(panX, panY, currentZoom, rotationDegrees);
            panX = clamped.x;
            panY = clamped.y;
        }

        applyTransform(true);
        updateUI();
        updateCursor();
        return currentZoom;
    }

    function setRotation(newDeg) {
        rotationDegrees = normalizeRotation(newDeg);

        // Re-clamp pan according to new rotated dimensions
        const clamped = clampPan(panX, panY, currentZoom, rotationDegrees);
        panX = clamped.x;
        panY = clamped.y;

        applyTransform(true);
        updateUI();
        updateCursor();
        return rotationDegrees;
    }

    function rotateRight() {
        return setRotation(rotationDegrees + 90);
    }

    function rotateLeft() {
        return setRotation(rotationDegrees - 90);
    }

    function resetRotation() {
        return setRotation(0);
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
        const fitZoom = calculateFitZoom(rotationDegrees);
        currentZoom = fitZoom;
        isFitToScreen = true;
        applyTransform(true);
        const percent = Math.round(currentZoom * 100);
        updateUI(percent + '% (Vừa khung)');
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
        rotationDegrees = 0;
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

        const clamped = clampPan(newX, newY, currentZoom, rotationDegrees);
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
        } else if (e.key === '0') {
            e.preventDefault();
            resetView();
        } else if (e.key === 'f' || e.key === 'F') {
            e.preventDefault();
            fitToScreen();
        } else if (e.key === '[') {
            e.preventDefault();
            rotateLeft();
        } else if (e.key === ']') {
            e.preventDefault();
            rotateRight();
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
        btnRotateLeft = document.getElementById('btnRotateLeft');
        btnRotateRight = document.getElementById('btnRotateRight');
        btnResetRotation = document.getElementById('btnResetRotation');
        badgeZoom = document.getElementById('zoomPercentageBadge');
        badgeRotation = document.getElementById('rotationBadge');

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

            if (btnRotateLeft) {
                btnRotateLeft.addEventListener('click', function (e) {
                    e.preventDefault();
                    rotateLeft();
                });
            }

            if (btnRotateRight) {
                btnRotateRight.addEventListener('click', function (e) {
                    e.preventDefault();
                    rotateRight();
                });
            }

            if (btnResetRotation) {
                btnResetRotation.addEventListener('click', function (e) {
                    e.preventDefault();
                    resetRotation();
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
                    const clamped = clampPan(panX, panY, currentZoom, rotationDegrees);
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
        rotateLeft: rotateLeft,
        rotateRight: rotateRight,
        setRotation: setRotation,
        resetRotation: resetRotation,
        getRotation: function () {
            return rotationDegrees;
        },
        calculateFitZoom: function (deg) {
            return calculateFitZoom(deg);
        },
        panTo: function (x, y) {
            const clamped = clampPan(x, y, currentZoom, rotationDegrees);
            panX = clamped.x;
            panY = clamped.y;
            applyTransform(true);
            updateCursor();
            return { x: panX, y: panY };
        },
        getPan: function () {
            return { x: panX, y: panY };
        },
        getMaxPan: function (zoom, deg) {
            return getMaxPan(zoom, deg);
        },
        canPan: function (zoom, deg) {
            return canPan(zoom, deg);
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

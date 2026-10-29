/**
 * MedicalImagingViewer - High-Precision Dental X-Ray Zoom, Pan, Rotate, Filter & Annotation Viewer
 * Day 01: Zoom In / Zoom Out / Fit to Screen / Reset View
 * Day 02: Pan / Drag / Pointer Events / Boundary Clamping / Cursor States
 * Day 03: Rotate 90° Left/Right / Rotation State / Bounding Box Recalculation / Fit on Rotation
 * Day 04: Brightness & Contrast Adjustment / CSS Image Filters / Real-time Sliders / Reset Filters
 * Day 05: SVG Overlay Annotations / Circle / Arrow / Text Note / FDI Tooth Label / Select / Undo / Clear All
 * DentalCare Clinical EMR
 */
(function (global) {
    'use strict';

    // Zoom constants
    const MIN_ZOOM = 0.25; // 25%
    const MAX_ZOOM = 4.0;  // 400%
    const ZOOM_STEP = 0.25; // 25%

    // Filter constants (Day 04)
    const MIN_BRIGHTNESS = 50;  // 50%
    const MAX_BRIGHTNESS = 150; // 150%
    const DEFAULT_BRIGHTNESS = 100;
    const BRIGHTNESS_STEP = 5;

    const MIN_CONTRAST = 50;    // 50%
    const MAX_CONTRAST = 200;   // 200%
    const DEFAULT_CONTRAST = 100;
    const CONTRAST_STEP = 5;

    // Annotation constants (Day 05)
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const MAX_TEXT_NOTE_LENGTH = 200;
    const MIN_CIRCLE_RADIUS = 2; // Discard r <= 2px (zero-radius protection)
    const MIN_ARROW_LENGTH = 3;  // Discard len <= 3px (zero-length protection)

    // FDI Two-Digit Tooth Codes:
    // Permanent: 11-18, 21-28, 31-38, 41-48 (32 teeth)
    // Primary / Deciduous: 51-55, 61-65, 71-75, 81-85 (20 teeth)
    const VALID_FDI_CODES = new Set([
        11, 12, 13, 14, 15, 16, 17, 18,
        21, 22, 23, 24, 25, 26, 27, 28,
        31, 32, 33, 34, 35, 36, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48,
        51, 52, 53, 54, 55,
        61, 62, 63, 64, 65,
        71, 72, 73, 74, 75,
        81, 82, 83, 84, 85
    ]);

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

    // Filter state variables (Day 04)
    let brightnessValue = DEFAULT_BRIGHTNESS;
    let contrastValue = DEFAULT_CONTRAST;

    // Annotation state variables (Day 05)
    let activeTool = 'pan'; // 'pan' | 'select' | 'circle' | 'arrow' | 'text' | 'fdi'
    let annotations = []; // In-memory temporary annotations
    let selectedAnnotationId = null;
    let undoStack = []; // History stack for Create, Delete, Clear
    let isDrawing = false;
    let drawingStart = null; // { x, y, clientX, clientY }
    let selectedFdiCode = 16; // Default to tooth 16 (Permanent Upper Right First Molar)

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

    // Filter DOM elements (Day 04)
    let sliderBrightness = null;
    let sliderContrast = null;
    let badgeBrightness = null;
    let badgeContrast = null;
    let btnResetFilters = null;
    let btnFilterDropdown = null;

    // Annotation DOM elements (Day 05)
    let svgOverlayEl = null;
    let drawingGroupEl = null;
    let previewGroupEl = null;
    let toolbarAnnotation = null;
    let btnToolPan = null;
    let btnToolSelect = null;
    let btnToolCircle = null;
    let btnToolArrow = null;
    let btnToolText = null;
    let btnToolFdi = null;
    let selectedFdiBadge = null;
    let btnUndoAnnotation = null;
    let btnDeleteAnnotation = null;
    let btnClearAllAnnotations = null;

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

    function generateAnnotationId() {
        return 'ann_' + Date.now().toString(36) + '_' + Math.random().toString(36).substr(2, 6);
    }

    function isValidFdiToothCode(code) {
        const num = parseInt(code, 10);
        return !isNaN(num) && VALID_FDI_CODES.has(num);
    }

    function getImageDimensions() {
        if (!imgEl) return { width: 0, height: 0, naturalWidth: 0, naturalHeight: 0 };
        let w = imgEl.clientWidth;
        let h = imgEl.clientHeight;
        let nw = imgEl.naturalWidth || 0;
        let nh = imgEl.naturalHeight || 0;
        if (!w || !h) {
            w = nw || 800;
            h = nh || 600;
        }
        if (!nw || !nh) {
            nw = w;
            nh = h;
        }
        return { width: w, height: h, naturalWidth: nw, naturalHeight: nh };
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

    // =========================================================================
    // Coordinate Transformations (Day 05 Architecture)
    // Synchronizes Screen / Mouse Pointer Coordinates with Natural Image Coordinates
    // Properly inverts translation, zoom, and rotation (0°, 90°, 180°, 270°)
    // =========================================================================

    function getViewportCenter() {
        if (!viewportEl) return { x: 0, y: 0 };
        const rect = viewportEl.getBoundingClientRect();
        return {
            x: rect.left + rect.width / 2,
            y: rect.top + rect.height / 2
        };
    }

    function clientToImageCoords(clientX, clientY) {
        const imgDim = getImageDimensions();
        const nw = imgDim.naturalWidth || imgDim.width || 800;
        const nh = imgDim.naturalHeight || imgDim.height || 600;

        // Visual rendered dimensions before CSS transform
        const rw = imgDim.width || nw;
        const rh = imgDim.height || nh;

        // Viewport center
        const vpCenter = getViewportCenter();
        const centerX = vpCenter.x + panX;
        const centerY = vpCenter.y + panY;

        // 1. Invert Pan translation
        const sx = clientX - centerX;
        const sy = clientY - centerY;

        // 2. Invert Zoom
        const z = currentZoom || 1.0;
        const rx = sx / z;
        const ry = sy / z;

        // 3. Invert Rotation (-rotationDegrees)
        const rad = (rotationDegrees * Math.PI) / 180;
        const cosA = Math.cos(rad);
        const sinA = Math.sin(rad);

        // Analytical inverse 2D rotation matrix: R(-theta)
        const ox = rx * cosA + ry * sinA;
        const oy = -rx * sinA + ry * cosA;

        // 4. Map from rendered dimensions origin (center) to natural image coordinates
        const normX = (ox + rw / 2) / rw;
        const normY = (oy + rh / 2) / rh;

        const naturalX = normX * nw;
        const naturalY = normY * nh;

        return {
            x: clamp(Math.round(naturalX * 10) / 10, 0, nw),
            y: clamp(Math.round(naturalY * 10) / 10, 0, nh)
        };
    }

    function imageToClientCoords(imgX, imgY) {
        const imgDim = getImageDimensions();
        const nw = imgDim.naturalWidth || imgDim.width || 800;
        const nh = imgDim.naturalHeight || imgDim.height || 600;
        const rw = imgDim.width || nw;
        const rh = imgDim.height || nh;

        // 1. Normalized position relative to center in rendered pixels
        const normX = imgX / nw;
        const normY = imgY / nh;
        const ox = (normX - 0.5) * rw;
        const oy = (normY - 0.5) * rh;

        // 2. Forward Rotation (rotationDegrees)
        const rad = (rotationDegrees * Math.PI) / 180;
        const cosA = Math.cos(rad);
        const sinA = Math.sin(rad);

        const rx = ox * cosA - oy * sinA;
        const ry = ox * sinA + oy * cosA;

        // 3. Forward Zoom
        const z = currentZoom || 1.0;
        const sx = rx * z;
        const sy = ry * z;

        // 4. Forward Pan translation
        const vpCenter = getViewportCenter();
        const centerX = vpCenter.x + panX;
        const centerY = vpCenter.y + panY;

        return {
            x: Math.round((centerX + sx) * 10) / 10,
            y: Math.round((centerY + sy) * 10) / 10
        };
    }

    function syncOverlayDimensions() {
        if (!imgEl || !svgOverlayEl) return;
        const imgDim = getImageDimensions();
        const nw = imgDim.naturalWidth || 800;
        const nh = imgDim.naturalHeight || 600;
        const rw = imgDim.width || nw;
        const rh = imgDim.height || nh;

        svgOverlayEl.style.width = rw + 'px';
        svgOverlayEl.style.height = rh + 'px';
        svgOverlayEl.setAttribute('viewBox', '0 0 ' + nw + ' ' + nh);
        svgOverlayEl.classList.remove('d-none');
    }

    function updateCursor() {
        if (!viewportEl || !imgEl) return;

        if (activeTool === 'pan') {
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
        } else if (activeTool === 'circle' || activeTool === 'arrow') {
            viewportEl.style.cursor = 'crosshair';
            imgEl.style.cursor = 'crosshair';
            viewportEl.classList.remove('is-panning', 'can-pan');
            imgEl.classList.remove('is-panning', 'can-pan');
        } else if (activeTool === 'text' || activeTool === 'fdi') {
            viewportEl.style.cursor = 'crosshair';
            imgEl.style.cursor = 'crosshair';
            viewportEl.classList.remove('is-panning', 'can-pan');
            imgEl.classList.remove('is-panning', 'can-pan');
        } else if (activeTool === 'select') {
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

        updateFilterUI();
        updateAnnotationUI();
    }

    // =========================================================================
    // Filter Management (Day 04)
    // Applies strictly to X-ray image (imgEl.style.filter), NOT to SVG Overlay!
    // =========================================================================

    function setBrightness(val) {
        const num = parseFloat(val);
        if (isNaN(num)) return brightnessValue;
        brightnessValue = clamp(Math.round(num), MIN_BRIGHTNESS, MAX_BRIGHTNESS);
        applyImageFilters();
        updateFilterUI();
        return brightnessValue;
    }

    function setContrast(val) {
        const num = parseFloat(val);
        if (isNaN(num)) return contrastValue;
        contrastValue = clamp(Math.round(num), MIN_CONTRAST, MAX_CONTRAST);
        applyImageFilters();
        updateFilterUI();
        return contrastValue;
    }

    function applyImageFilters() {
        if (!imgEl) return;
        if (brightnessValue === 100 && contrastValue === 100) {
            imgEl.style.filter = 'none';
        } else {
            const b = (brightnessValue / 100).toFixed(2);
            const c = (contrastValue / 100).toFixed(2);
            imgEl.style.filter = 'brightness(' + b + ') contrast(' + c + ')';
        }
    }

    function resetFilters() {
        brightnessValue = DEFAULT_BRIGHTNESS;
        contrastValue = DEFAULT_CONTRAST;
        applyImageFilters();
        updateFilterUI();
        return { brightness: brightnessValue, contrast: contrastValue };
    }

    function updateFilterUI() {
        if (badgeBrightness) {
            badgeBrightness.textContent = brightnessValue + '%';
        }
        if (sliderBrightness && parseInt(sliderBrightness.value, 10) !== brightnessValue) {
            sliderBrightness.value = brightnessValue;
        }
        if (badgeContrast) {
            badgeContrast.textContent = contrastValue + '%';
        }
        if (sliderContrast && parseInt(sliderContrast.value, 10) !== contrastValue) {
            sliderContrast.value = contrastValue;
        }
        if (btnFilterDropdown) {
            if (brightnessValue !== 100 || contrastValue !== 100) {
                btnFilterDropdown.classList.add('text-info');
            } else {
                btnFilterDropdown.classList.remove('text-info');
            }
        }
    }

    // =========================================================================
    // Geometric Transform (Day 01, 02, 03, 05)
    // Synchronizes Image and SVG Overlay geometrically
    // =========================================================================

    function applyTransform(withTransition) {
        if (!imgEl) return;
        const transformStr = 'translate(' + panX + 'px, ' + panY + 'px) rotate(' + rotationDegrees + 'deg) scale(' + currentZoom + ')';
        const transitionStr = withTransition ? 'transform 0.12s cubic-bezier(0.2, 0, 0, 1)' : 'none';

        imgEl.style.transition = transitionStr;
        imgEl.style.transformOrigin = 'center center';
        imgEl.style.transform = transformStr;

        if (svgOverlayEl) {
            svgOverlayEl.style.transition = transitionStr;
            svgOverlayEl.style.transformOrigin = 'center center';
            svgOverlayEl.style.transform = transformStr;
        }
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
        resetFilters(); // Reset brightness & contrast alongside zoom, pan, and rotation
        applyTransform(true);
        updateUI('100%');
        updateCursor();
        // Day 05 Critical Invariant: Reset View preserves temporary annotations on the same image!
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

    // =========================================================================
    // Day 05: Annotation Management & SVG Rendering Engine
    // Independent SVG layer - Zero modification to original image
    // Safe textContent - 100% immune to XSS
    // Temporary in-memory state - Never persisted to DB / LocalStorage
    // =========================================================================

    function setActiveTool(tool) {
        const validTools = ['pan', 'select', 'circle', 'arrow', 'text', 'fdi'];
        if (validTools.indexOf(tool) === -1) {
            return activeTool;
        }
        activeTool = tool;

        if (svgOverlayEl) {
            if (activeTool === 'pan') {
                svgOverlayEl.classList.add('tool-pan');
            } else {
                svgOverlayEl.classList.remove('tool-pan');
            }
        }

        if (activeTool !== 'select') {
            selectedAnnotationId = null;
            renderSvgAnnotations();
        }

        updateAnnotationUI();
        updateCursor();
        return activeTool;
    }

    function addAnnotation(ann, addToUndo = true) {
        if (!ann || !ann.type) return null;
        if (!ann.id) {
            ann.id = generateAnnotationId();
        }
        annotations.push(ann);

        if (addToUndo) {
            undoStack.push({ type: 'create', annotation: ann });
        }

        selectedAnnotationId = ann.id;
        renderSvgAnnotations();
        updateAnnotationUI();
        return ann;
    }

    function selectAnnotation(id) {
        if (!id) {
            selectedAnnotationId = null;
        } else {
            const found = annotations.find(function (a) { return a.id === id; });
            selectedAnnotationId = found ? found.id : null;
        }
        renderSvgAnnotations();
        updateAnnotationUI();
        return selectedAnnotationId;
    }

    function deleteSelectedAnnotation() {
        if (!selectedAnnotationId) return false;
        const idx = annotations.findIndex(function (a) { return a.id === selectedAnnotationId; });
        if (idx === -1) {
            selectedAnnotationId = null;
            updateAnnotationUI();
            return false;
        }
        const deleted = annotations.splice(idx, 1)[0];
        undoStack.push({ type: 'delete', annotation: deleted, index: idx });
        selectedAnnotationId = null;
        renderSvgAnnotations();
        updateAnnotationUI();
        return true;
    }

    function undo() {
        if (undoStack.length === 0) return null;
        const op = undoStack.pop();

        if (op.type === 'create') {
            const idx = annotations.findIndex(function (a) { return a.id === op.annotation.id; });
            if (idx !== -1) {
                annotations.splice(idx, 1);
            }
        } else if (op.type === 'delete') {
            if (typeof op.index === 'number' && op.index >= 0 && op.index <= annotations.length) {
                annotations.splice(op.index, 0, op.annotation);
            } else {
                annotations.push(op.annotation);
            }
        } else if (op.type === 'clear') {
            annotations = (op.annotations || []).slice();
        }

        selectedAnnotationId = null;
        renderSvgAnnotations();
        updateAnnotationUI();
        return op;
    }

    function clearAllAnnotations(skipConfirm) {
        if (annotations.length === 0) return false;
        if (!skipConfirm && typeof window !== 'undefined' && window.confirm) {
            const confirmed = window.confirm('Bạn có chắc chắn muốn xóa toàn bộ chú thích trên ảnh này?\n(Lưu ý: Chú thích là tạm thời và không ảnh hưởng đến ảnh gốc)');
            if (!confirmed) {
                return false;
            }
        }

        undoStack.push({ type: 'clear', annotations: annotations.slice() });
        annotations = [];
        selectedAnnotationId = null;
        renderSvgAnnotations();
        updateAnnotationUI();
        return true;
    }

    function clearTemporaryAnnotations() {
        annotations = [];
        undoStack = [];
        selectedAnnotationId = null;
        isDrawing = false;
        drawingStart = null;
        clearPreview();
        renderSvgAnnotations();
        updateAnnotationUI();
    }

    function setFdiToothCode(code) {
        const num = parseInt(code, 10);
        if (!isValidFdiToothCode(num)) {
            return false;
        }
        selectedFdiCode = num;
        if (selectedFdiBadge) {
            selectedFdiBadge.textContent = num.toString();
        }
        // Update active class on FDI dropdown buttons
        if (toolbarAnnotation) {
            const btns = toolbarAnnotation.querySelectorAll('.fdi-tooth-btn');
            btns.forEach(function (btn) {
                if (parseInt(btn.getAttribute('data-fdi'), 10) === num) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        }
        return true;
    }

    function renderSvgAnnotations() {
        if (!drawingGroupEl) return;
        while (drawingGroupEl.firstChild) {
            drawingGroupEl.removeChild(drawingGroupEl.firstChild);
        }

        annotations.forEach(function (ann) {
            const isSelected = ann.id === selectedAnnotationId;
            const itemG = document.createElementNS(SVG_NS, 'g');
            itemG.setAttribute('class', 'annotation-item' + (isSelected ? ' is-selected' : ''));
            itemG.setAttribute('data-id', ann.id);

            if (ann.type === 'circle') {
                const circle = document.createElementNS(SVG_NS, 'circle');
                circle.setAttribute('cx', ann.cx);
                circle.setAttribute('cy', ann.cy);
                circle.setAttribute('r', ann.r);
                circle.setAttribute('fill', 'rgba(239, 68, 68, 0.1)');
                circle.setAttribute('stroke', isSelected ? '#fbbf24' : (ann.stroke || '#ef4444'));
                circle.setAttribute('stroke-width', isSelected ? '3.5' : (ann.strokeWidth || '2.5'));
                circle.setAttribute('stroke-dasharray', isSelected ? '6,3' : 'none');
                itemG.appendChild(circle);
            } else if (ann.type === 'arrow') {
                const line = document.createElementNS(SVG_NS, 'line');
                line.setAttribute('x1', ann.x1);
                line.setAttribute('y1', ann.y1);
                line.setAttribute('x2', ann.x2);
                line.setAttribute('y2', ann.y2);
                line.setAttribute('stroke', isSelected ? '#fbbf24' : (ann.stroke || '#38bdf8'));
                line.setAttribute('stroke-width', isSelected ? '3.5' : (ann.strokeWidth || '2.5'));
                line.setAttribute('marker-end', isSelected ? 'url(#annotationArrowheadSelected)' : 'url(#annotationArrowhead)');
                line.setAttribute('stroke-linecap', 'round');
                itemG.appendChild(line);
            } else if (ann.type === 'text') {
                const bgRect = document.createElementNS(SVG_NS, 'rect');
                bgRect.setAttribute('rx', '4');
                bgRect.setAttribute('ry', '4');
                bgRect.setAttribute('fill', 'rgba(15, 23, 42, 0.88)');
                bgRect.setAttribute('stroke', isSelected ? '#fbbf24' : (ann.color || '#f59e0b'));
                bgRect.setAttribute('stroke-width', isSelected ? '2' : '1');

                const textNode = document.createElementNS(SVG_NS, 'text');
                textNode.setAttribute('x', ann.x + 8);
                textNode.setAttribute('y', ann.y + 17);
                textNode.setAttribute('fill', '#ffffff');
                textNode.setAttribute('font-size', '13');
                textNode.setAttribute('font-family', 'sans-serif');
                textNode.setAttribute('font-weight', '500');
                textNode.textContent = ann.text; // 100% XSS SAFE: Uses textContent

                const approxW = Math.max(60, (ann.text ? ann.text.length : 0) * 8 + 16);
                bgRect.setAttribute('x', ann.x);
                bgRect.setAttribute('y', ann.y);
                bgRect.setAttribute('width', approxW);
                bgRect.setAttribute('height', '24');

                itemG.appendChild(bgRect);
                itemG.appendChild(textNode);
            } else if (ann.type === 'fdi') {
                const circle = document.createElementNS(SVG_NS, 'circle');
                circle.setAttribute('cx', ann.x);
                circle.setAttribute('cy', ann.y);
                circle.setAttribute('r', '14');
                circle.setAttribute('fill', isSelected ? '#fbbf24' : (ann.color || '#0ea5e9'));
                circle.setAttribute('stroke', '#ffffff');
                circle.setAttribute('stroke-width', '2');

                const textNode = document.createElementNS(SVG_NS, 'text');
                textNode.setAttribute('x', ann.x);
                textNode.setAttribute('y', ann.y + 4);
                textNode.setAttribute('text-anchor', 'middle');
                textNode.setAttribute('fill', isSelected ? '#000000' : '#ffffff');
                textNode.setAttribute('font-size', '11');
                textNode.setAttribute('font-weight', 'bold');
                textNode.setAttribute('font-family', 'monospace');
                textNode.textContent = ann.toothCode.toString(); // Safe textContent

                itemG.appendChild(circle);
                itemG.appendChild(textNode);
            }

            drawingGroupEl.appendChild(itemG);
        });
    }

    function renderPreview(tool, start, current) {
        if (!previewGroupEl) return;
        clearPreview();

        if (tool === 'circle') {
            const r = Math.hypot(current.x - start.x, current.y - start.y);
            const circle = document.createElementNS(SVG_NS, 'circle');
            circle.setAttribute('cx', start.x);
            circle.setAttribute('cy', start.y);
            circle.setAttribute('r', r);
            circle.setAttribute('fill', 'rgba(239, 68, 68, 0.15)');
            circle.setAttribute('stroke', '#ef4444');
            circle.setAttribute('stroke-width', '2');
            circle.setAttribute('stroke-dasharray', '4,4');
            circle.setAttribute('class', 'annotation-preview');
            previewGroupEl.appendChild(circle);
        } else if (tool === 'arrow') {
            const line = document.createElementNS(SVG_NS, 'line');
            line.setAttribute('x1', start.x);
            line.setAttribute('y1', start.y);
            line.setAttribute('x2', current.x);
            line.setAttribute('y2', current.y);
            line.setAttribute('stroke', '#38bdf8');
            line.setAttribute('stroke-width', '2');
            line.setAttribute('stroke-dasharray', '4,4');
            line.setAttribute('marker-end', 'url(#annotationArrowhead)');
            line.setAttribute('class', 'annotation-preview');
            previewGroupEl.appendChild(line);
        }
    }

    function clearPreview() {
        if (!previewGroupEl) return;
        while (previewGroupEl.firstChild) {
            previewGroupEl.removeChild(previewGroupEl.firstChild);
        }
    }

    function updateAnnotationUI() {
        if (btnToolPan) btnToolPan.classList.toggle('active', activeTool === 'pan');
        if (btnToolSelect) btnToolSelect.classList.toggle('active', activeTool === 'select');
        if (btnToolCircle) btnToolCircle.classList.toggle('active', activeTool === 'circle');
        if (btnToolArrow) btnToolArrow.classList.toggle('active', activeTool === 'arrow');
        if (btnToolText) btnToolText.classList.toggle('active', activeTool === 'text');
        if (btnToolFdi) btnToolFdi.classList.toggle('active', activeTool === 'fdi');

        if (btnUndoAnnotation) {
            btnUndoAnnotation.disabled = undoStack.length === 0;
            btnUndoAnnotation.setAttribute('aria-disabled', undoStack.length === 0 ? 'true' : 'false');
        }

        if (btnDeleteAnnotation) {
            btnDeleteAnnotation.disabled = !selectedAnnotationId;
            btnDeleteAnnotation.setAttribute('aria-disabled', !selectedAnnotationId ? 'true' : 'false');
        }

        if (btnClearAllAnnotations) {
            btnClearAllAnnotations.disabled = annotations.length === 0;
            btnClearAllAnnotations.setAttribute('aria-disabled', annotations.length === 0 ? 'true' : 'false');
        }
    }

    function handleCreateTextNote(imgCoords) {
        const promptMsg = 'Nhập ghi chú nha khoa cho vị trí này (tối đa ' + MAX_TEXT_NOTE_LENGTH + ' ký tự):';
        let rawNote = null;
        if (typeof window !== 'undefined' && window.prompt) {
            rawNote = window.prompt(promptMsg, '');
        }
        if (rawNote === null) return;
        const trimmed = rawNote.trim();
        if (trimmed.length === 0) return;

        const safeNote = trimmed.substring(0, MAX_TEXT_NOTE_LENGTH);
        const ann = {
            id: generateAnnotationId(),
            type: 'text',
            x: imgCoords.x,
            y: imgCoords.y,
            text: safeNote,
            color: '#f59e0b'
        };
        addAnnotation(ann);
    }

    function handleCreateFdiLabel(imgCoords) {
        let code = selectedFdiCode;
        if (!isValidFdiToothCode(code)) {
            code = 16;
        }
        const ann = {
            id: generateAnnotationId(),
            type: 'fdi',
            x: imgCoords.x,
            y: imgCoords.y,
            toothCode: code,
            label: 'R' + code,
            color: '#0ea5e9'
        };
        addAnnotation(ann);
    }

    // =========================================================================
    // Event Handlers
    // Distinguishes Pan Mode vs Annotation Mode safely
    // =========================================================================

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
        if (e.button !== 0) return;
        if (!viewportEl || !imgEl || imgEl.classList.contains('d-none')) return;

        // View / Pan Mode
        if (activeTool === 'pan') {
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
            } catch (err) {}

            updateCursor();
            applyTransform(false); // Instant dragging
            return;
        }

        // Annotation Modes: 'select', 'circle', 'arrow', 'text', 'fdi'
        const imgCoords = clientToImageCoords(e.clientX, e.clientY);

        if (activeTool === 'select') {
            // Check if clicked an annotation element
            let target = e.target;
            let clickedId = null;
            while (target && target !== viewportEl && target !== svgOverlayEl) {
                if (target.getAttribute && target.getAttribute('data-id')) {
                    clickedId = target.getAttribute('data-id');
                    break;
                }
                target = target.parentNode;
            }
            selectAnnotation(clickedId);
            return;
        }

        if (activeTool === 'circle' || activeTool === 'arrow') {
            e.preventDefault();
            isDrawing = true;
            activePointerId = e.pointerId;
            drawingStart = {
                x: imgCoords.x,
                y: imgCoords.y,
                clientX: e.clientX,
                clientY: e.clientY
            };

            try {
                if (viewportEl.setPointerCapture) {
                    viewportEl.setPointerCapture(e.pointerId);
                }
            } catch (err) {}

            renderPreview(activeTool, drawingStart, imgCoords);
            return;
        }

        if (activeTool === 'text') {
            e.preventDefault();
            handleCreateTextNote(imgCoords);
            return;
        }

        if (activeTool === 'fdi') {
            e.preventDefault();
            handleCreateFdiLabel(imgCoords);
            return;
        }
    }

    function onPointerMove(e) {
        if (isPanning) {
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
            return;
        }

        if (isDrawing && drawingStart) {
            if (activePointerId !== null && e.pointerId !== activePointerId) return;
            e.preventDefault();
            const imgCoords = clientToImageCoords(e.clientX, e.clientY);
            renderPreview(activeTool, drawingStart, imgCoords);
            return;
        }
    }

    function onPointerUp(e) {
        if (isPanning) {
            if (activePointerId !== null && e.pointerId !== activePointerId) return;
            endPan();
            return;
        }

        if (isDrawing && drawingStart) {
            if (activePointerId !== null && e.pointerId !== activePointerId) return;
            e.preventDefault();
            const imgCoords = clientToImageCoords(e.clientX, e.clientY);

            if (activeTool === 'circle') {
                const r = Math.hypot(imgCoords.x - drawingStart.x, imgCoords.y - drawingStart.y);
                if (r >= MIN_CIRCLE_RADIUS) {
                    addAnnotation({
                        id: generateAnnotationId(),
                        type: 'circle',
                        cx: drawingStart.x,
                        cy: drawingStart.y,
                        r: Math.round(r * 10) / 10,
                        stroke: '#ef4444',
                        strokeWidth: 2.5
                    });
                }
            } else if (activeTool === 'arrow') {
                const len = Math.hypot(imgCoords.x - drawingStart.x, imgCoords.y - drawingStart.y);
                if (len >= MIN_ARROW_LENGTH) {
                    addAnnotation({
                        id: generateAnnotationId(),
                        type: 'arrow',
                        x1: drawingStart.x,
                        y1: drawingStart.y,
                        x2: imgCoords.x,
                        y2: imgCoords.y,
                        stroke: '#38bdf8',
                        strokeWidth: 2.5
                    });
                }
            }

            isDrawing = false;
            drawingStart = null;
            clearPreview();

            if (activePointerId !== null && viewportEl && viewportEl.releasePointerCapture) {
                try {
                    if (viewportEl.hasPointerCapture && viewportEl.hasPointerCapture(activePointerId)) {
                        viewportEl.releasePointerCapture(activePointerId);
                    }
                } catch (err) {}
            }
            activePointerId = null;
        }
    }

    function onPointerCancel(e) {
        if (isPanning) {
            endPan();
        }
        if (isDrawing) {
            isDrawing = false;
            drawingStart = null;
            clearPreview();
            if (activePointerId !== null && viewportEl && viewportEl.releasePointerCapture) {
                try {
                    if (viewportEl.hasPointerCapture && viewportEl.hasPointerCapture(activePointerId)) {
                        viewportEl.releasePointerCapture(activePointerId);
                    }
                } catch (err) {}
            }
            activePointerId = null;
        }
    }

    function onLostPointerCapture(e) {
        if (isPanning) {
            endPan();
        }
        if (isDrawing) {
            isDrawing = false;
            drawingStart = null;
            clearPreview();
            activePointerId = null;
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

        // Escape: cancel drawing or deselect
        if (e.key === 'Escape') {
            if (isDrawing) {
                e.preventDefault();
                isDrawing = false;
                drawingStart = null;
                clearPreview();
                return;
            }
            if (selectedAnnotationId) {
                e.preventDefault();
                selectAnnotation(null);
                return;
            }
        }

        // Delete / Backspace: delete selected annotation
        if ((e.key === 'Delete' || e.key === 'Backspace') && selectedAnnotationId) {
            e.preventDefault();
            deleteSelectedAnnotation();
            return;
        }

        // Ctrl+Z: Undo
        if ((e.ctrlKey || e.metaKey) && (e.key === 'z' || e.key === 'Z')) {
            e.preventDefault();
            undo();
            return;
        }

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

        // Filter elements (Day 04)
        sliderBrightness = document.getElementById('brightnessSlider');
        sliderContrast = document.getElementById('contrastSlider');
        badgeBrightness = document.getElementById('brightnessBadge');
        badgeContrast = document.getElementById('contrastBadge');
        btnResetFilters = document.getElementById('btnResetFilters');
        btnFilterDropdown = document.getElementById('btnFilterDropdown');

        // Annotation elements (Day 05)
        svgOverlayEl = document.getElementById('imagingAnnotationOverlay');
        drawingGroupEl = document.getElementById('annotationDrawingGroup');
        previewGroupEl = document.getElementById('annotationPreviewGroup');
        toolbarAnnotation = document.getElementById('imagingAnnotationToolbar');
        btnToolPan = document.getElementById('btnToolPan');
        btnToolSelect = document.getElementById('btnToolSelect');
        btnToolCircle = document.getElementById('btnToolCircle');
        btnToolArrow = document.getElementById('btnToolArrow');
        btnToolText = document.getElementById('btnToolText');
        btnToolFdi = document.getElementById('btnToolFdi');
        selectedFdiBadge = document.getElementById('selectedFdiBadge');
        btnUndoAnnotation = document.getElementById('btnUndoAnnotation');
        btnDeleteAnnotation = document.getElementById('btnDeleteAnnotation');
        btnClearAllAnnotations = document.getElementById('btnClearAllAnnotations');

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

            // Filter event listeners (Day 04)
            if (sliderBrightness) {
                sliderBrightness.addEventListener('input', function (e) {
                    setBrightness(e.target.value);
                });
            }

            if (sliderContrast) {
                sliderContrast.addEventListener('input', function (e) {
                    setContrast(e.target.value);
                });
            }

            if (btnResetFilters) {
                btnResetFilters.addEventListener('click', function (e) {
                    e.preventDefault();
                    resetFilters();
                });
            }

            // Annotation event listeners (Day 05)
            if (btnToolPan) {
                btnToolPan.addEventListener('click', function (e) {
                    e.preventDefault();
                    setActiveTool('pan');
                });
            }

            if (btnToolSelect) {
                btnToolSelect.addEventListener('click', function (e) {
                    e.preventDefault();
                    setActiveTool('select');
                });
            }

            if (btnToolCircle) {
                btnToolCircle.addEventListener('click', function (e) {
                    e.preventDefault();
                    setActiveTool('circle');
                });
            }

            if (btnToolArrow) {
                btnToolArrow.addEventListener('click', function (e) {
                    e.preventDefault();
                    setActiveTool('arrow');
                });
            }

            if (btnToolText) {
                btnToolText.addEventListener('click', function (e) {
                    e.preventDefault();
                    setActiveTool('text');
                });
            }

            if (btnToolFdi) {
                btnToolFdi.addEventListener('click', function (e) {
                    setActiveTool('fdi');
                });
            }

            // FDI tooth buttons inside dropdown menu
            if (toolbarAnnotation) {
                toolbarAnnotation.addEventListener('click', function (e) {
                    const toothBtn = e.target.closest('.fdi-tooth-btn');
                    if (toothBtn) {
                        e.preventDefault();
                        const code = toothBtn.getAttribute('data-fdi');
                        setFdiToothCode(code);
                        setActiveTool('fdi');
                    }
                });
            }

            if (btnUndoAnnotation) {
                btnUndoAnnotation.addEventListener('click', function (e) {
                    e.preventDefault();
                    undo();
                });
            }

            if (btnDeleteAnnotation) {
                btnDeleteAnnotation.addEventListener('click', function (e) {
                    e.preventDefault();
                    deleteSelectedAnnotation();
                });
            }

            if (btnClearAllAnnotations) {
                btnClearAllAnnotations.addEventListener('click', function (e) {
                    e.preventDefault();
                    clearAllAnnotations(false);
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
                    syncOverlayDimensions();
                    updateCursor();
                });
            }

            document.addEventListener('keydown', onKeyDown);

            window.addEventListener('resize', function () {
                if (modalEl && modalEl.classList.contains('show')) {
                    const clamped = clampPan(panX, panY, currentZoom, rotationDegrees);
                    panX = clamped.x;
                    panY = clamped.y;
                    syncOverlayDimensions();
                    applyTransform(true);
                    updateCursor();
                }
            });

            modalEl.addEventListener('hidden.bs.modal', function () {
                clearTemporaryAnnotations();
                resetView();
            });

            isInitialized = true;
        }

        resetView();
        syncOverlayDimensions();
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
        // Filter methods (Day 04)
        setBrightness: setBrightness,
        setContrast: setContrast,
        getBrightness: function () {
            return brightnessValue;
        },
        getContrast: function () {
            return contrastValue;
        },
        resetFilters: resetFilters,
        applyImageFilters: applyImageFilters,
        getMinBrightness: function () { return MIN_BRIGHTNESS; },
        getMaxBrightness: function () { return MAX_BRIGHTNESS; },
        getMinContrast: function () { return MIN_CONTRAST; },
        getMaxContrast: function () { return MAX_CONTRAST; },
        // Pan methods (Day 02)
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
        },
        // Annotation methods (Day 05)
        getActiveTool: function () {
            return activeTool;
        },
        setActiveTool: setActiveTool,
        getAnnotations: function () {
            return annotations.slice();
        },
        addAnnotation: addAnnotation,
        selectAnnotation: selectAnnotation,
        getSelectedAnnotation: function () {
            return annotations.find(function (a) { return a.id === selectedAnnotationId; }) || null;
        },
        getSelectedAnnotationId: function () {
            return selectedAnnotationId;
        },
        deleteAnnotation: function (id) {
            const idx = annotations.findIndex(function (a) { return a.id === id; });
            if (idx === -1) return false;
            const deleted = annotations.splice(idx, 1)[0];
            undoStack.push({ type: 'delete', annotation: deleted, index: idx });
            if (selectedAnnotationId === id) {
                selectedAnnotationId = null;
            }
            renderSvgAnnotations();
            updateAnnotationUI();
            return true;
        },
        deleteSelectedAnnotation: deleteSelectedAnnotation,
        undo: undo,
        canUndo: function () {
            return undoStack.length > 0;
        },
        clearAllAnnotations: clearAllAnnotations,
        clearTemporaryAnnotations: clearTemporaryAnnotations,
        isValidFdiToothCode: isValidFdiToothCode,
        getFdiToothCode: function () {
            return selectedFdiCode;
        },
        setFdiToothCode: setFdiToothCode,
        getValidFdiCodes: function () {
            return Array.from(VALID_FDI_CODES);
        },
        clientToImageCoords: clientToImageCoords,
        imageToClientCoords: imageToClientCoords,
        isDrawing: function () {
            return isDrawing;
        },
        syncOverlayDimensions: syncOverlayDimensions,
        getMaxTextNoteLength: function () {
            return MAX_TEXT_NOTE_LENGTH;
        }
    };

    global.MedicalImagingViewer = viewerApi;

})(typeof window !== 'undefined' ? window : this);

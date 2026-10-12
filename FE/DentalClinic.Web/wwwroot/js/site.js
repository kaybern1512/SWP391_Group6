// DentalCare Clinic Management System - UI Interactive Helpers Only
// Strictly NO business logic or API calls in JavaScript

// 1. Global Toast Notification System
window.showToast = function (type, message, title, delay = 4500) {
  let container = document.getElementById('appToastContainer');
  if (!container) {
    container = document.createElement('div');
    container.id = 'appToastContainer';
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '1095';
    document.body.appendChild(container);
  }

  const toastId = 'toast-' + Date.now() + '-' + Math.floor(Math.random() * 1000);
  let iconClass = 'bi-info-circle-fill text-info';
  let borderClass = 'border-start border-4 border-info';
  let defaultTitle = 'Thông Báo';

  switch (type ? type.toLowerCase() : 'info') {
    case 'success':
      iconClass = 'bi-check-circle-fill text-success';
      borderClass = 'border-start border-4 border-success';
      defaultTitle = 'Thành Công';
      break;
    case 'danger':
    case 'error':
      iconClass = 'bi-x-circle-fill text-danger';
      borderClass = 'border-start border-4 border-danger';
      defaultTitle = 'Thông Báo Lỗi';
      delay = Math.max(delay, 6000);
      break;
    case 'warning':
      iconClass = 'bi-exclamation-triangle-fill text-warning';
      borderClass = 'border-start border-4 border-warning';
      defaultTitle = 'Cảnh Báo';
      delay = Math.max(delay, 5000);
      break;
    default:
      iconClass = 'bi-info-circle-fill text-info';
      borderClass = 'border-start border-4 border-info';
      defaultTitle = 'Thông Báo';
      break;
  }

  const toastHtml = `
    <div id="${toastId}" class="toast align-items-center shadow-lg rounded-3 mb-2 bg-white ${borderClass} dc-toast" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="${delay}">
      <div class="toast-header bg-transparent border-0 pb-0 pt-2 px-3">
        <i class="bi ${iconClass} me-2 fs-5"></i>
        <strong class="me-auto text-dark small fw-bold">${title || defaultTitle}</strong>
        <small class="text-muted" style="font-size: 0.725rem;">Vừa xong</small>
        <button type="button" class="btn-close ms-2" data-bs-dismiss="toast" aria-label="Đóng"></button>
      </div>
      <div class="toast-body pt-1 pb-3 px-3 text-secondary" style="font-size: 0.875rem;">
        ${message}
      </div>
    </div>
  `;

  container.insertAdjacentHTML('beforeend', toastHtml);
  const toastEl = document.getElementById(toastId);
  if (toastEl && typeof bootstrap !== 'undefined' && bootstrap.Toast) {
    const bsToast = new bootstrap.Toast(toastEl, { delay: delay, autohide: true });
    toastEl.addEventListener('hidden.bs.toast', function () {
      toastEl.remove();
    });
    bsToast.show();
  }
};

window.toastSuccess = function (msg, title) { return window.showToast('success', msg, title); };
window.toastError = function (msg, title) { return window.showToast('error', msg, title); };
window.toastWarning = function (msg, title) { return window.showToast('warning', msg, title); };
window.toastInfo = function (msg, title) { return window.showToast('info', msg, title); };

// 2. Safety Override: Replace browser native alert() with sleek Toast
window.alert = function (message) {
  window.showToast('warning', message, 'Thông Báo');
};

// Safety Override: Deprecate native window.confirm() to prevent blocking popups
window.confirm = function (message) {
  console.warn('Native window.confirm is disabled. Use [data-confirm] attribute for interactive confirmation modal.', message);
  return false;
};

document.addEventListener('DOMContentLoaded', function () {
  // 3. Password Visibility Toggle
  const toggleButtons = document.querySelectorAll('.password-toggle-btn');
  toggleButtons.forEach(btn => {
    btn.addEventListener('click', function () {
      const targetId = this.getAttribute('data-target');
      const input = document.getElementById(targetId);
      if (input) {
        const isPassword = input.getAttribute('type') === 'password';
        input.setAttribute('type', isPassword ? 'text' : 'password');
        const icon = this.querySelector('i');
        if (icon) {
          icon.classList.toggle('bi-eye', !isPassword);
          icon.classList.toggle('bi-eye-slash', isPassword);
        }
      }
    });
  });

  // 4. Avatar Preview before form submit
  const avatarInput = document.getElementById('avatarFileInput');
  const avatarPreview = document.getElementById('avatarPreviewImg');
  if (avatarInput && avatarPreview) {
    avatarInput.addEventListener('change', function (e) {
      const file = e.target.files[0];
      if (file) {
        if (file.size > 5 * 1024 * 1024) {
          window.showToast('error', 'Kích thước tệp hình ảnh không được vượt quá 5 MB.', 'Cảnh Báo Kích Thước');
          this.value = '';
          return;
        }
        const reader = new FileReader();
        reader.onload = function (event) {
          avatarPreview.src = event.target.result;
        };
        reader.readAsDataURL(file);
      }
    });
  }

  // 5. Initialize Server-Rendered Bootstrap Toasts
  if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
    const toastElList = [].slice.call(document.querySelectorAll('.toast'));
    toastElList.forEach(function (toastEl) {
      const delay = parseInt(toastEl.getAttribute('data-bs-delay')) || 5000;
      const toast = new bootstrap.Toast(toastEl, { delay: delay, autohide: true });
      toastEl.addEventListener('hidden.bs.toast', function () {
        toastEl.remove();
      });
      toast.show();
    });
  }

  // 6. Universal Confirmation Modal System (Replaces browser "localhost says" popups)
  let pendingConfirmCallback = null;
  const confirmModalEl = document.getElementById('appConfirmModal');
  const confirmModal = (confirmModalEl && typeof bootstrap !== 'undefined' && bootstrap.Modal)
    ? new bootstrap.Modal(confirmModalEl)
    : null;
  const confirmTitleEl = document.getElementById('appConfirmTitle');
  const confirmMessageEl = document.getElementById('appConfirmMessage');
  const confirmProceedBtn = document.getElementById('appConfirmProceedBtn');
  const confirmIconWrapper = document.getElementById('appConfirmIconWrapper');
  const confirmIcon = document.getElementById('appConfirmIcon');

  function openConfirmModal(message, onProceed, options = {}) {
    if (!confirmModal) {
      if (typeof onProceed === 'function') onProceed();
      return;
    }

    pendingConfirmCallback = onProceed;

    if (confirmMessageEl) {
      confirmMessageEl.textContent = message || 'Bạn có chắc chắn muốn thực hiện thao tác này không?';
    }
    if (confirmTitleEl) {
      confirmTitleEl.textContent = options.title || 'Xác Nhận Thao Tác';
    }

    const isDanger = options.isDanger ?? (
      /xóa|hủy|từ chối|đăng xuất|ngưng|khóa/i.test(message || '')
    );

    if (confirmProceedBtn) {
      confirmProceedBtn.className = isDanger
        ? 'btn btn-danger px-4 py-2 rounded-3 fw-semibold small shadow-sm'
        : 'btn btn-primary px-4 py-2 rounded-3 fw-semibold small shadow-sm';
      confirmProceedBtn.textContent = options.confirmText || 'Xác Nhận';
    }

    if (confirmIconWrapper && confirmIcon) {
      if (isDanger) {
        confirmIconWrapper.style.backgroundColor = '#fef2f2';
        confirmIconWrapper.style.color = '#dc2626';
        confirmIcon.className = 'bi bi-exclamation-triangle fs-3';
      } else {
        confirmIconWrapper.style.backgroundColor = '#eff6ff';
        confirmIconWrapper.style.color = '#2563eb';
        confirmIcon.className = 'bi bi-question-circle fs-3';
      }
    }

    confirmModal.show();
  }

  if (confirmProceedBtn) {
    confirmProceedBtn.addEventListener('click', function () {
      if (confirmModal) confirmModal.hide();
      if (typeof pendingConfirmCallback === 'function') {
        const action = pendingConfirmCallback;
        pendingConfirmCallback = null;
        action();
      }
    });
  }

  // Intercept click on [data-confirm] buttons / links (Only actionable elements, NEVER generic containers or inputs)
  document.addEventListener('click', function (e) {
    const trigger = e.target.closest('button[data-confirm], a[data-confirm], input[type="submit"][data-confirm], input[type="button"][data-confirm]');
    if (!trigger || trigger.dataset.confirmBypassed === 'true') return;

    const message = trigger.getAttribute('data-confirm');
    const form = trigger.closest('form');

    e.preventDefault();
    e.stopPropagation();

    openConfirmModal(message, function () {
      trigger.dataset.confirmBypassed = 'true';
      if (trigger.tagName === 'A' && trigger.href) {
        window.location.href = trigger.href;
      } else if (form) {
        if (typeof form.requestSubmit === 'function') {
          form.requestSubmit(trigger);
        } else {
          form.submit();
        }
      }
    });
  }, true);

  // Intercept submit on forms with data-confirm
  document.addEventListener('submit', function (e) {
    const form = e.target.closest('form[data-confirm]');
    if (!form || form.dataset.confirmBypassed === 'true') return;

    const message = form.getAttribute('data-confirm');
    const submitter = e.submitter || null;
    e.preventDefault();
    e.stopPropagation();

    openConfirmModal(message, function () {
      form.dataset.confirmBypassed = 'true';
      if (typeof form.requestSubmit === 'function') {
        form.requestSubmit(submitter);
      } else {
        form.submit();
      }
    });
  }, true);

  // 7. Sidebar Toggle (Both Desktop Collapse & Mobile Offcanvas)
  const sidebarToggle = document.getElementById('sidebarToggleBtn');
  const sidebarCollapseHeader = document.getElementById('sidebarCollapseHeaderBtn');
  const sidebar = document.getElementById('appSidebar');
  const appWrapper = document.querySelector('.app-wrapper');
  const backdrop = document.getElementById('sidebarBackdrop');

  // Restore saved collapse state on desktop
  if (appWrapper && window.innerWidth > 992) {
    if (localStorage.getItem('dc_sidebar_collapsed') === 'true') {
      appWrapper.classList.add('sidebar-collapsed');
    }
  }

  function toggleSidebar() {
    if (window.innerWidth <= 992) {
      if (sidebar) sidebar.classList.toggle('show');
      if (backdrop) backdrop.classList.toggle('show');
    } else {
      if (appWrapper) {
        appWrapper.classList.toggle('sidebar-collapsed');
        const isCollapsed = appWrapper.classList.contains('sidebar-collapsed');
        localStorage.setItem('dc_sidebar_collapsed', isCollapsed ? 'true' : 'false');
      }
    }
  }

  if (sidebarToggle) {
    sidebarToggle.addEventListener('click', toggleSidebar);
  }
  if (sidebarCollapseHeader) {
    sidebarCollapseHeader.addEventListener('click', toggleSidebar);
  }
  if (backdrop) {
    backdrop.addEventListener('click', function () {
      if (sidebar) sidebar.classList.remove('show');
      if (backdrop) backdrop.classList.remove('show');
    });
  }

  // Keyboard shortcut: Ctrl + B or Alt + S
  document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey && e.key.toLowerCase() === 'b') || (e.altKey && e.key.toLowerCase() === 's')) {
      // Don't intercept when user is typing in form inputs
      const tag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
      if (tag !== 'input' && tag !== 'textarea' && tag !== 'select') {
        e.preventDefault();
        toggleSidebar();
      }
    }
  });
});

/**
 * DENTALCARE RECEPTIONIST PORTAL - REAL-TIME QUEUE & DISPATCH SCRIPT
 * Enforces SignalR live updates, dynamic waiting timers, service overrun alerts, and walk-in intake.
 */
(function () {
    'use strict';

    let queueHubConnection = null;
    let isConnected = false;

    document.addEventListener('DOMContentLoaded', function () {
        initSignalR();
        startWaitingTimers();
        initWalkInModal();
        initFilterInteractions();
    });

    // ==========================================
    // 1. SIGNALR REAL-TIME DISPATCH CONNECTION
    // ==========================================
    function initSignalR() {
        const statusBadge = document.getElementById('signalrStatusBadge');
        if (typeof signalR === 'undefined') {
            console.warn('[SignalR] Library not loaded. Real-time updates disabled.');
            if (statusBadge) {
                statusBadge.className = 'badge bg-secondary';
                statusBadge.innerHTML = '<i class="bi bi-wifi-off me-1"></i>SignalR: Ngoại tuyến';
            }
            return;
        }

        const hubUrl = '/hubs/clinic-queue';

        queueHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                skipNegotiation: false,
                transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        function setBadgeStatus(connected, text, bgClass) {
            if (!statusBadge) return;
            statusBadge.className = `badge ${bgClass} shadow-sm`;
            statusBadge.innerHTML = `<i class="bi ${connected ? 'bi-wifi' : 'bi-wifi-off'} me-1"></i>${text}`;
        }

        queueHubConnection.onreconnecting(function () {
            isConnected = false;
            setBadgeStatus(false, 'SignalR: Đang kết nối lại...', 'bg-warning text-dark');
        });

        queueHubConnection.onreconnected(function () {
            isConnected = true;
            setBadgeStatus(true, 'SignalR: Trực tuyến', 'bg-success text-white');
            showToast('SignalR đã kết nối lại thành công.', 'info');
        });

        queueHubConnection.onclose(function () {
            isConnected = false;
            setBadgeStatus(false, 'SignalR: Mất kết nối', 'bg-danger text-white');
        });

        // Event: Patient Checked-in
        queueHubConnection.on('ReceivePatientCheckedIn', function (data) {
            console.log('[SignalR] ReceivePatientCheckedIn:', data);
            showToast(`Bệnh nhân <strong>${escapeHtml(data.patientName || 'vãng lai')}</strong> (${escapeHtml(data.queueNumber || '')}) vừa Check-in!`, 'success');
            refreshTableWithDelay();
        });

        // Event: Patient In-Service
        queueHubConnection.on('ReceivePatientInService', function (data) {
            console.log('[SignalR] ReceivePatientInService:', data);
            showToast(`Bác sĩ <strong>${escapeHtml(data.assignedDentistName || '')}</strong> đã bắt đầu khám cho <strong>${escapeHtml(data.patientName || '')}</strong>.`, 'primary');
            refreshTableWithDelay();
        });

        // Event: Treatment Completed
        queueHubConnection.on('ReceiveTreatmentCompleted', function (data) {
            console.log('[SignalR] ReceiveTreatmentCompleted:', data);
            showToast(`Ca khám của <strong>${escapeHtml(data.patientName || '')}</strong> đã hoàn tất! Vui lòng xuất hóa đơn.`, 'warning');
            refreshTableWithDelay();
        });

        // Event: Appointment Created
        queueHubConnection.on('ReceiveAppointmentCreated', function (data) {
            console.log('[SignalR] ReceiveAppointmentCreated:', data);
            showToast(`Lịch hẹn mới <strong>${escapeHtml(data.appointmentCode || '')}</strong> vừa được tạo!`, 'info');
            refreshTableWithDelay();
        });

        // Event: Queue Updated
        queueHubConnection.on('ReceiveQueueUpdated', function () {
            refreshTableWithDelay();
        });

        // Start Connection
        setBadgeStatus(false, 'SignalR: Đang kết nối...', 'bg-secondary text-white');
        queueHubConnection.start()
            .then(function () {
                isConnected = true;
                setBadgeStatus(true, 'SignalR: Trực tuyến', 'bg-success text-white');
                queueHubConnection.invoke('JoinClinicQueue').catch(function (err) {
                    console.warn('[SignalR] JoinClinicQueue error:', err);
                });
            })
            .catch(function (err) {
                console.warn('[SignalR] Connection start failed:', err);
                setBadgeStatus(false, 'SignalR: Ngoại tuyến', 'bg-secondary text-white');
            });
    }

    let refreshTimeout = null;
    function refreshTableWithDelay() {
        if (refreshTimeout) clearTimeout(refreshTimeout);
        refreshTimeout = setTimeout(function () {
            // Soft reload if not interacting with modals
            const openModal = document.querySelector('.modal.show');
            if (!openModal) {
                window.location.reload();
            }
        }, 1500);
    }

    // ==========================================
    // 2. LIVE WAITING & OVERRUN TIMERS
    // ==========================================
    function startWaitingTimers() {
        updateAllTimers();
        setInterval(updateAllTimers, 10000); // Update every 10s
    }

    function updateAllTimers() {
        const now = new Date();

        // A. Waiting Timers (CheckedIn)
        const waitingNodes = document.querySelectorAll('[data-timer-type="waiting"]');
        waitingNodes.forEach(function (el) {
            const rawTime = el.getAttribute('data-checkin-time');
            if (!rawTime) return;

            const checkInDate = new Date(rawTime);
            if (isNaN(checkInDate.getTime())) return;

            const diffMs = now - checkInDate;
            const diffMin = Math.max(0, Math.floor(diffMs / 60000));

            const badgeContainer = el.closest('tr')?.querySelector('.waiting-alert-badge');

            if (diffMin >= 20) {
                el.innerHTML = `<span class="text-danger fw-bold"><i class="bi bi-clock-fill me-1"></i>Đã chờ: ${diffMin} phút</span>`;
                if (badgeContainer) {
                    badgeContainer.innerHTML = '<span class="badge bg-danger text-white shadow-sm animate__animated animate__pulse animate__infinite"><i class="bi bi-exclamation-triangle-fill me-1"></i>⚠️ Chờ > 20p</span>';
                }
            } else {
                el.innerHTML = `<span class="text-muted small"><i class="bi bi-clock me-1"></i>Đã chờ: ${diffMin} phút</span>`;
                if (badgeContainer) {
                    badgeContainer.innerHTML = '';
                }
            }
        });

        // B. In-Service Overrun Timers
        const inServiceNodes = document.querySelectorAll('[data-timer-type="inservice"]');
        inServiceNodes.forEach(function (el) {
            const rawStartTime = el.getAttribute('data-start-time');
            const duration = parseInt(el.getAttribute('data-duration') || '30', 10);
            if (!rawStartTime) return;

            const startDate = new Date(rawStartTime);
            if (isNaN(startDate.getTime())) return;

            const elapsedMin = Math.max(0, Math.floor((now - startDate) / 60000));
            const maxDuration = duration + 10; // Overrun threshold: duration + 10 mins

            const overrunContainer = el.closest('tr')?.querySelector('.overrun-alert-badge');
            if (elapsedMin > maxDuration && overrunContainer) {
                overrunContainer.innerHTML = `<span class="badge bg-warning text-dark border border-warning shadow-sm"><i class="bi bi-exclamation-circle-fill me-1"></i>🟡 Quá giờ khám (+${elapsedMin - duration}p)</span>`;
            }
        });
    }

    // ==========================================
    // 3. WALK-IN INTAKE MODAL LOGIC
    // ==========================================
    function initWalkInModal() {
        const modal = document.getElementById('walkInModal');
        if (!modal) return;

        const lookupInput = document.getElementById('walkInLookupInput');
        const lookupResults = document.getElementById('walkInLookupResults');
        const patientIdInput = document.getElementById('walkInPatientId');
        const fullNameInput = document.getElementById('walkInFullName');
        const phoneInput = document.getElementById('walkInPhone');
        const cccdInput = document.getElementById('walkInCccd');
        const dobInput = document.getElementById('walkInDob');
        const genderSelect = document.getElementById('walkInGender');
        const patientStatusBadge = document.getElementById('walkInPatientStatusBadge');
        const clearPatientBtn = document.getElementById('walkInClearPatientBtn');

        const deptSelect = document.getElementById('walkInDeptSelect');
        const serviceSelect = document.getElementById('walkInServiceSelect');
        const dentistSelect = document.getElementById('walkInDentistSelect');
        const roomSelect = document.getElementById('walkInRoomSelect');
        const chairSelect = document.getElementById('walkInChairSelect');

        // Lookup patient by phone or CCCD
        let searchTimer = null;
        if (lookupInput) {
            lookupInput.addEventListener('input', function () {
                const query = lookupInput.value.trim();
                clearTimeout(searchTimer);
                if (query.length < 2) {
                    if (lookupResults) lookupResults.style.display = 'none';
                    return;
                }
                searchTimer = setTimeout(function () {
                    fetch(`/Receptionist/Appointments/SearchPatient?query=${encodeURIComponent(query)}`)
                        .then(r => r.json())
                        .then(res => {
                            if (res.success && res.patients && res.patients.length > 0) {
                                renderSearchResults(res.patients);
                            } else {
                                if (lookupResults) {
                                    lookupResults.innerHTML = '<div class="p-2 text-muted small text-center">Không tìm thấy bệnh nhân. Hãy nhập thông tin mới bên dưới.</div>';
                                    lookupResults.style.display = 'block';
                                }
                            }
                        })
                        .catch(err => console.error('Patient search error:', err));
                }, 300);
            });
        }

        function renderSearchResults(patients) {
            if (!lookupResults) return;
            lookupResults.innerHTML = '';
            patients.forEach(p => {
                const item = document.createElement('a');
                item.href = '#';
                item.className = 'list-group-item list-group-item-action py-2';
                item.innerHTML = `
                    <div class="d-flex justify-content-between align-items-center">
                        <div class="fw-bold">${escapeHtml(p.fullName)}</div>
                        <span class="badge bg-primary">${escapeHtml(p.code || '')}</span>
                    </div>
                    <div class="small text-muted">SĐT: ${escapeHtml(p.phoneNumber || 'N/A')} &bull; CCCD: ${escapeHtml(p.nationalId || 'N/A')}</div>
                `;
                item.addEventListener('click', function (e) {
                    e.preventDefault();
                    selectPatient(p);
                });
                lookupResults.appendChild(item);
            });
            lookupResults.style.display = 'block';
        }

        function selectPatient(p) {
            if (patientIdInput) patientIdInput.value = p.userId;
            if (fullNameInput) { fullNameInput.value = p.fullName; fullNameInput.readOnly = true; }
            if (phoneInput) { phoneInput.value = p.phoneNumber || ''; phoneInput.readOnly = true; }
            if (cccdInput) { cccdInput.value = p.nationalId || ''; cccdInput.readOnly = true; }
            if (dobInput && p.dateOfBirth) { dobInput.value = p.dateOfBirth; dobInput.readOnly = true; }
            if (genderSelect && p.gender) { genderSelect.value = p.gender; }

            if (lookupResults) lookupResults.style.display = 'none';
            if (lookupInput) lookupInput.value = '';

            if (patientStatusBadge) {
                patientStatusBadge.className = 'badge bg-success';
                patientStatusBadge.innerHTML = `<i class="bi bi-person-check-fill me-1"></i>Hồ sơ cũ: ${escapeHtml(p.fullName)} (${escapeHtml(p.code || '')})`;
            }
            if (clearPatientBtn) clearPatientBtn.style.display = 'inline-block';
        }

        if (clearPatientBtn) {
            clearPatientBtn.addEventListener('click', function () {
                if (patientIdInput) patientIdInput.value = '';
                if (fullNameInput) { fullNameInput.value = ''; fullNameInput.readOnly = false; }
                if (phoneInput) { phoneInput.value = ''; phoneInput.readOnly = false; }
                if (cccdInput) { cccdInput.value = ''; cccdInput.readOnly = false; }
                if (dobInput) { dobInput.value = ''; dobInput.readOnly = false; }
                if (patientStatusBadge) {
                    patientStatusBadge.className = 'badge bg-info text-dark';
                    patientStatusBadge.innerHTML = '<i class="bi bi-person-plus-fill me-1"></i>Khách vãng lai mới';
                }
                clearPatientBtn.style.display = 'none';
            });
        }

        // Cascading Department -> Services, Dentists, Rooms & Chairs
        if (deptSelect) {
            deptSelect.addEventListener('change', function () {
                const deptId = deptSelect.value;
                if (!deptId) return;

                // Load Services
                fetch(`/Receptionist/Appointments/GetServices?departmentId=${deptId}`)
                    .then(r => r.json())
                    .then(data => {
                        if (serviceSelect) {
                            serviceSelect.innerHTML = '<option value="">-- Chọn dịch vụ tiếp đón --</option>';
                            data.forEach(s => {
                                const opt = document.createElement('option');
                                opt.value = s.departmentServiceId || s.serviceId;
                                opt.textContent = `${s.serviceName} (${formatVND(s.price || 0)})`;
                                serviceSelect.appendChild(opt);
                            });
                        }
                    });

                // Load Dentists
                fetch(`/Receptionist/Appointments/GetDentists?departmentId=${deptId}`)
                    .then(r => r.json())
                    .then(data => {
                        if (dentistSelect) {
                            dentistSelect.innerHTML = '<option value="">-- Chọn bác sĩ phụ trách --</option>';
                            data.forEach(d => {
                                const opt = document.createElement('option');
                                opt.value = d.userId || d.dentistUserId;
                                opt.textContent = `BS. ${d.fullName || d.dentistName}`;
                                dentistSelect.appendChild(opt);
                            });
                        }
                    });

                // Load Rooms & Chairs
                fetch(`/Receptionist/Appointments/GetResources?departmentId=${deptId}`)
                    .then(r => r.json())
                    .then(data => {
                        if (roomSelect) {
                            roomSelect.innerHTML = '<option value="">-- Chọn phòng khám --</option>';
                            (data.rooms || []).forEach(rm => {
                                const opt = document.createElement('option');
                                opt.value = rm.roomId;
                                opt.textContent = `${rm.roomName} (${rm.roomCode})`;
                                roomSelect.appendChild(opt);
                            });
                        }
                        if (chairSelect) {
                            chairSelect.innerHTML = '<option value="">-- Chọn ghế nha khoa --</option>';
                            (data.chairs || []).forEach(ch => {
                                const opt = document.createElement('option');
                                opt.value = ch.dentalChairId;
                                opt.textContent = `Ghế ${ch.chairCode} (${ch.chairName || 'Ghế điều trị'})`;
                                chairSelect.appendChild(opt);
                            });
                        }
                    });
            });
        }

        // Form validation
        const walkInForm = document.getElementById('walkInForm');
        if (walkInForm) {
            walkInForm.addEventListener('submit', function (e) {
                const phone = phoneInput ? phoneInput.value.trim() : '';
                const cccd = cccdInput ? cccdInput.value.trim() : '';

                if (phone && !/^\d{10}$/.test(phone)) {
                    e.preventDefault();
                    alert('Số điện thoại phải bao gồm đúng 10 chữ số hợp lệ.');
                    phoneInput.focus();
                    return false;
                }

                if (cccd && !/^\d{12}$/.test(cccd)) {
                    e.preventDefault();
                    alert('Số CCCD/Căn cước công dân phải bao gồm đúng 12 chữ số.');
                    cccdInput.focus();
                    return false;
                }
            });
        }
    }

    // ==========================================
    // 4. MULTI-TIER FILTER & KPI CARDS
    // ==========================================
    function initFilterInteractions() {
        // Cascading Department filter in toolbar
        const filterDeptSelect = document.getElementById('filterDepartmentSelect');
        const filterDentistSelect = document.getElementById('filterDentistSelect');

        if (filterDeptSelect && filterDentistSelect) {
            filterDeptSelect.addEventListener('change', function () {
                const deptId = filterDeptSelect.value;
                if (!deptId) {
                    filterDentistSelect.innerHTML = '<option value="">-- Tất cả bác sĩ --</option>';
                    return;
                }
                fetch(`/Receptionist/Appointments/GetDentists?departmentId=${deptId}`)
                    .then(r => r.json())
                    .then(dentists => {
                        filterDentistSelect.innerHTML = '<option value="">-- Tất cả bác sĩ --</option>';
                        dentists.forEach(d => {
                            const opt = document.createElement('option');
                            opt.value = d.userId || d.dentistUserId;
                            opt.textContent = `BS. ${d.fullName || d.dentistName}`;
                            filterDentistSelect.appendChild(opt);
                        });
                    });
            });
        }
    }

    // Toast notification helper
    function showToast(message, type) {
        let toastContainer = document.getElementById('receptionistToastContainer');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'receptionistToastContainer';
            toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
            toastContainer.style.zIndex = '9999';
            document.body.appendChild(toastContainer);
        }

        const toastEl = document.createElement('div');
        const bgClass = type === 'success' ? 'bg-success text-white' :
                        type === 'warning' ? 'bg-warning text-dark' :
                        type === 'primary' ? 'bg-primary text-white' : 'bg-info text-dark';
        toastEl.className = `toast align-items-center ${bgClass} border-0 shadow-lg show mb-2`;
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');
        toastEl.innerHTML = `
            <div class="d-flex">
                <div class="toast-body fs-6">
                    <i class="bi bi-bell-fill me-2"></i>${message}
                </div>
                <button type="button" class="btn-close me-2 m-auto ${type === 'success' || type === 'primary' ? 'btn-close-white' : ''}" data-bs-dismiss="toast"></button>
            </div>
        `;
        toastContainer.appendChild(toastEl);
        setTimeout(function () {
            toastEl.classList.remove('show');
            setTimeout(function () { toastEl.remove(); }, 500);
        }, 5000);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str.toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function formatVND(amt) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amt);
    }

    // Expose functions globally for inline onclick
    window.dcFilterTableByStatus = function (status) {
        const rows = document.querySelectorAll('#todayAppointmentsTable tbody tr.appointment-row');
        const pills = document.querySelectorAll('.filter-pill');

        pills.forEach(p => {
            if (p.getAttribute('data-filter') === status) {
                p.classList.add('active');
            } else {
                p.classList.remove('active');
            }
        });

        let visibleCount = 0;
        rows.forEach(r => {
            const rowStatus = r.getAttribute('data-status');
            if (status === 'all' || rowStatus === status || (status === 'CheckedIn' && (rowStatus === 'CheckedIn' || rowStatus === 'Waiting'))) {
                r.style.display = '';
                visibleCount++;
            } else {
                r.style.display = 'none';
            }
        });

        const noRow = document.getElementById('noFilterResultsRow');
        if (noRow) {
            noRow.style.display = visibleCount === 0 ? '' : 'none';
        }
    };

})();

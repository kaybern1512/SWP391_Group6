# TẬP HỢP BIỂU ĐỒ HỆ THỐNG TOÀN DIỆN (SYSTEM ARCHITECTURAL & WORKFLOW DIAGRAMS)
**Tài liệu:** `14_DIAGRAMS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Định dạng:** Chuẩn Markdown Mermaid Diagrams

---

## 1. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ HỆ THỐNG (SYSTEM ARCHITECTURE)

```mermaid
flowchart TD
    subgraph ClientLayer["LỚP CLIENT & NGƯỜI DÙNG"]
        U_PAT["Bệnh nhân (Web Browser)"]
        U_REC["Tiếp tân / Thu ngân"]
        U_DEN["Bác sĩ điều trị"]
        U_MGR["Trưởng khoa chuyên môn"]
        U_ADM["Quản trị viên hệ thống"]
    end

    subgraph FrontendApp["FRONTEND MVC APPLICATION (DentalClinic.Web - Port 7129)"]
        MVC_CTRL["MVC Controllers & Razor Views"]
        COOKIE_AUTH["Cookie Authentication (HttpOnly, SameSite)"]
        SESSION_MGR["Session & Token Store"]
        AUTH_HANDLER["ApiAuthorizationHandler (Bearer Token & Mutex Refresh)"]
        TYPED_CLIENTS["Typed HttpClients (IAppointmentApiService, etc.)"]
        
        MVC_CTRL --> COOKIE_AUTH
        MVC_CTRL --> TYPED_CLIENTS
        TYPED_CLIENTS --> AUTH_HANDLER
        AUTH_HANDLER --> SESSION_MGR
    end

    subgraph BackendApp["BACKEND RESTFUL WEB API (DentalClinic.Api - Port 7350)"]
        API_CTRL["API Controllers ([Authorize(Roles=...)])"]
        JWT_AUTH["JWT Bearer Authentication Handler"]
        EX_HANDLER["Global Exception Middleware"]
        SIGNALR["SignalR LiveChatHub (/hubs/livechat)"]
        
        subgraph AppLayer["DentalClinic.Application"]
            SVC_INTERFACES["Service Interfaces (IAppointmentService, etc.)"]
            FLUENT_VAL["FluentValidation Auto-Validators"]
            DTOS["Data Transfer Objects (DTOs)"]
        end

        subgraph DomainLayer["DentalClinic.Domain"]
            ENUMS["Enums (UserRole, AppointmentStatus)"]
            CORE_RULES["Business Rules & Constants"]
        end

        subgraph InfraLayer["DentalClinic.Infrastructure"]
            SVC_IMPL["Service Implementations (AppointmentService, etc.)"]
            EF_DBCONTEXT["DentalClinicDbContext (EF Core 8)"]
            EMAIL_SVC["EmailService (SMTP Mailer)"]
            IMG_STORAGE["ImageStorageService (Local / Cloudinary)"]
        end

        API_CTRL --> JWT_AUTH
        API_CTRL --> EX_HANDLER
        API_CTRL --> SVC_INTERFACES
        SVC_INTERFACES --> SVC_IMPL
        SVC_IMPL --> EF_DBCONTEXT
        SVC_IMPL --> EMAIL_SVC
        SVC_IMPL --> IMG_STORAGE
        EF_DBCONTEXT --> DOMAIN_ENTITIES["38 Domain Entities"]
    end

    subgraph DatabaseLayer["LỚP DỮ LIỆU & LƯU TRỮ VẬT LÝ"]
        SQL_SERVER[("Microsoft SQL Server (DentalClinicManagementDB)")]
        FILE_SYSTEM["Lưu trữ ảnh X-Quang / Avatars"]
    end

    subgraph ExternalServices["DỊCH VỤ NGOẠI VI"]
        VIETQR_GATEWAY["VietQR Gateway (img.vietqr.io - Napas 247)"]
        GOOGLE_OAUTH["Google OAuth 2.0 (OpenID Connect)"]
    end

    ClientLayer ==> FrontendApp
    AUTH_HANDLER ==>|HTTPS REST Calls with Bearer Token| API_CTRL
    SIGNALR <==>|WebSockets Live Chat| FrontendApp
    EF_DBCONTEXT ==> SQL_SERVER
    IMG_STORAGE ==> FILE_SYSTEM
    FrontendApp -.->|Sinh mã QR thanh toán| VIETQR_GATEWAY
    FrontendApp -.->|Xác thực tài khoản Google| GOOGLE_OAUTH
```

---

## 2. BIỂU ĐỒ CỖ MÁY TRẠNG THÁI LỊCH HẸN (APPOINTMENT STATE MACHINE)

```mermaid
stateDiagram-v2
    [*] --> PendingReceptionReview: Bệnh nhân tạo lịch hẹn mới (CreatePatientAppointment)
    
    PendingReceptionReview --> Rejected: Tiếp tân từ chối yêu cầu (ReceptionistReject)
    PendingReceptionReview --> Withdrawn: Bệnh nhân chủ động rút yêu cầu (PatientWithdraw)
    PendingReceptionReview --> PendingDepartmentReview: Tiếp tân duyệt chuyển tiếp (ReceptionistForward)
    
    PendingDepartmentReview --> Confirmed: Trưởng khoa phê duyệt & gán phòng/ghế (ManagerConfirm)
    PendingDepartmentReview --> AwaitingPatientResponse: Trưởng khoa đề xuất đổi lịch/bác sĩ (ManagerProposeChange)
    
    AwaitingPatientResponse --> PendingDepartmentReview: Bệnh nhân chấp nhận/từ chối đề xuất (PatientRespondProposal)
    AwaitingPatientResponse --> Cancelled: Quá hạn hoặc bệnh nhân hủy
    
    Confirmed --> Cancelled: Bệnh nhân hủy hẹn (Tính gậy nếu <12h) (PatientCancel)
    Confirmed --> NoShow: Bệnh nhân không đến khám (ReceptionistMarkNoShow)
    Confirmed --> CheckedIn: Bệnh nhân đến quầy làm thủ tục (ReceptionistCheckIn)
    
    CheckedIn --> InService: Bác sĩ bấm Vào Khám & chọn ghế (DentistStartAppointment)
    InService --> Completed: Bác sĩ hoàn thành thủ thuật y khoa (DentistCompleteAppointment)
    
    Completed --> [*]: Chuyển Thu ngân lập hóa đơn & thanh toán VietQR
    Rejected --> [*]
    Withdrawn --> [*]
    Cancelled --> [*]
    NoShow --> [*]
```

---

## 3. BIỂU ĐỒ TRÌNH TỰ BÀN GIAO KHÉP KÍN 5 PHÂN QUYỀN (END-TO-END SEQUENCE)

```mermaid
sequenceDiagram
    autonumber
    actor Pat as Bệnh nhân (Patient)
    actor Rec as Tiếp tân (Receptionist)
    actor Mgr as Trưởng khoa (Manager)
    actor Den as Bác sĩ (Dentist)
    participant FE as Web MVC (DentalClinic.Web)
    participant BE as Web API (DentalClinic.Api)
    participant DB as SQL Server Database

    Note over Pat, DB: 1. GIAI ĐOẠN ĐẶT HẸN & TIẾP NHẬN SƠ BỘ
    Pat->>FE: Đặt lịch khám trực tuyến (/Patient/Appointment/Book)
    FE->>BE: POST /api/patient/appointments
    BE->>DB: INSERT Appointments (Status='PendingReceptionReview')
    BE-->>FE: Return ApiResponse.Ok(APP-20261029-001)
    FE-->>Pat: Hiển thị màn hình thành công (/Success)

    Rec->>FE: Mở danh sách chờ duyệt (/Receptionist/Appointments/Requests)
    FE->>BE: GET /api/receptionist/appointment-requests
    BE-->>FE: Trả về danh sách ca mới
    Rec->>FE: Bấm [Chuyển tiếp]
    FE->>BE: POST /api/receptionist/appointments/{id}/forward
    BE->>DB: UPDATE Appointments (Status='PendingDepartmentReview')

    Note over Mgr, DB: 2. GIAI ĐOẠN DUYỆT CHUYÊN MÔN & XẾP PHÒNG/GHẾ
    Mgr->>FE: Mở màn hình duyệt ca (/Department/Appointments/Review/{id})
    FE->>BE: GET /api/department/appointments/{id}/available-dentists
    BE-->>FE: Hiển thị Bác sĩ & Ghế trống
    Mgr->>FE: Gán BS. Thảo, Phòng 101, Ghế 1 -> Bấm [Xác nhận]
    FE->>BE: POST /api/department/appointments/{id}/confirm
    BE->>DB: UPDATE Appointments (Status='Confirmed', QueueNumber='Q001')
    BE-->>Pat: Gửi Email xác nhận & Thông báo In-App

    Note over Pat, Den: 3. GIAI ĐOẠN CHECK-IN & KHÁM ĐIỀU TRỊ
    Pat->>Rec: Bệnh nhân đến quầy tiếp đón
    Rec->>FE: Tìm ca khám -> Bấm [Check-in]
    FE->>BE: POST /api/receptionist/appointments/{id}/check-in
    BE->>DB: UPDATE Appointments (Status='CheckedIn', CheckedInAt=UtcNow)

    Den->>FE: Mở hàng đợi ca hôm nay (/Dentist/Appointments/Today)
    FE->>BE: GET /api/dentist/today-appointments
    BE-->>FE: Hiển thị ca đã Check-in (Q001)
    Den->>FE: Bấm [Vào Khám]
    FE->>BE: POST /api/dentist/appointments/{id}/start
    BE->>DB: UPDATE Appointments (Status='InService')

    Den->>FE: Mở sơ đồ răng FDI 32 răng -> Ghi nhận sâu răng R46
    FE->>BE: POST /api/medical-records/entry
    BE->>DB: INSERT ClinicalEntries, OdontogramEntries
    Den->>FE: Bấm [Hoàn Thành Ca Khám]
    FE->>BE: POST /api/dentist/appointments/{id}/complete
    BE->>DB: UPDATE Appointments (Status='Completed') & Khóa ghế

    Note over Rec, Pat: 4. GIAI ĐOẠN THU NGÂN & THANH TOÁN VIETQR
    Rec->>FE: Mở danh sách hóa đơn (/Receptionist/Billing/Create)
    FE->>BE: GET /api/billing/appointments/billable
    Rec->>FE: Bấm [Phát hành hóa đơn] -> 400,000 VNĐ
    FE->>BE: POST /api/billing/invoices
    BE->>DB: INSERT Invoices (Status='Unpaid'), InvoiceItems
    FE->>Pat: Hiển thị mã VietQR động (img.vietqr.io)
    Pat->>Rec: Quét mã chuyển khoản qua App Ngân hàng
    Rec->>FE: Bấm [Xác nhận đã thu tiền]
    FE->>BE: POST /api/billing/payments
    BE->>DB: INSERT Payments, UPDATE Invoices (Status='Paid')
    Rec->>FE: Bấm [In hóa đơn] -> In biên lai A5 giao Bệnh nhân
```

---

## 4. QUY TRÌNH THANH TOÁN MÃ QR ĐỘNG VIETQR

```mermaid
flowchart LR
    A["Hóa đơn viện phí<br>(TotalAmount, InvoiceCode)"] --> B["Tạo Dynamic URL:<br>img.vietqr.io/image/MB-039123456789..."]
    B --> C["Hiển thị mã VietQR trên UI<br>(Receptionist & Patient View)"]
    C --> D["Bệnh nhân dùng Mobile Banking<br>quét mã QR"]
    D --> E["App Ngân hàng tự điền:<br>- STK: 039123456789<br>- Ngân hàng: MBBank<br>- Tiền: Số dư còn nợ<br>- Nội dung: THANH TOAN {Code}"]
    E --> F["Bệnh nhân bấm Chuyển tiền"]
    F --> G["Lễ tân kiểm tra biến động số dư<br>-> Bấm 'Xác nhận thanh toán'"]
    G --> H["Database cập nhật:<br>- Invoice.Status = 'Paid'<br>- Tạo bản ghi Payments"]
    H --> I["In biên lai viện phí A5"]
```

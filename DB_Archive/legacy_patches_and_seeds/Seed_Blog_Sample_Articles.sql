-- ========================================================
-- Seed Script: Professional Dental Health Articles (Task 6)
-- Target: SQL Server / Azure SQL / LocalDB
-- Description: Idempotent script inserting and updating 5 rich, clinical dental articles
--              with dental illustrations and 3D Periopixel models
-- ========================================================

IF DB_ID(N'DentalClinicManagementDB') IS NOT NULL
BEGIN
    USE DentalClinicManagementDB;
END;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Ensure Categories exist with clean Vietnamese names
IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'benh-ly-rang-mieng')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Bệnh lý răng miệng', 'benh-ly-rang-mieng', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Bệnh lý răng miệng' WHERE Slug = 'benh-ly-rang-mieng';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'cay-ghep-implant')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Cấy ghép Implant', 'cay-ghep-implant', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Cấy ghép Implant' WHERE Slug = 'cay-ghep-implant';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'nieng-rang-chinh-nha')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Niềng răng - Chỉnh nha', 'nieng-rang-chinh-nha', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Niềng răng - Chỉnh nha' WHERE Slug = 'nieng-rang-chinh-nha';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'cham-soc-rang-mieng')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Chăm sóc răng miệng', 'cham-soc-rang-mieng', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Chăm sóc răng miệng' WHERE Slug = 'cham-soc-rang-mieng';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'nha-khoa-tham-my')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Nha khoa thẩm mỹ', 'nha-khoa-tham-my', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Nha khoa thẩm mỹ' WHERE Slug = 'nha-khoa-tham-my';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories WHERE Slug = 'nha-khoa-tre-em')
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt) VALUES (N'Nha khoa trẻ em', 'nha-khoa-tre-em', SYSDATETIME());
ELSE
    UPDATE dbo.BlogCategories SET Name = N'Nha khoa trẻ em' WHERE Slug = 'nha-khoa-tre-em';
GO

-- 2. Ensure Doctor Profiles with PhD and specialist credentials
IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE UserId = 1)
BEGIN
    UPDATE dbo.StaffProfiles 
    SET FullName = N'TS. BS. Nguyễn Thị Phương Thảo' 
    WHERE UserId = 1;

    IF EXISTS (SELECT 1 FROM dbo.DentistProfiles WHERE UserId = 1)
        UPDATE dbo.DentistProfiles 
        SET Qualification = N'Tiến sĩ Y khoa (PhD) - Chuyên khoa Răng Hàm Mặt',
            YearsOfExperience = 15,
            Biography = N'Tiến sĩ Y khoa (PhD) chuyên ngành Răng Hàm Mặt - Đại học Y Dược TP.HCM. Từng tu nghiệp chuyên sâu về Nội nha vi phẫu và Phục hình răng sứ tại Đại học Bordeaux (Pháp). Hơn 15 năm kinh nghiệm điều trị và nghiên cứu lâm sàng.'
        WHERE UserId = 1;
    ELSE
        INSERT INTO dbo.DentistProfiles (UserId, Qualification, YearsOfExperience, Biography)
        VALUES (1, N'Tiến sĩ Y khoa (PhD) - Chuyên khoa Răng Hàm Mặt', 15, N'Tiến sĩ Y khoa (PhD) chuyên ngành Răng Hàm Mặt - Đại học Y Dược TP.HCM. Từng tu nghiệp chuyên sâu về Nội nha vi phẫu và Phục hình răng sứ tại Đại học Bordeaux (Pháp). Hơn 15 năm kinh nghiệm điều trị và nghiên cứu lâm sàng.');

    UPDATE dbo.UserAccounts 
    SET AvatarUrl = '/images/doctors/dr-phuong-thao.svg' 
    WHERE UserId = 1;
END;

IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE UserId = 4)
BEGIN
    UPDATE dbo.StaffProfiles 
    SET FullName = N'ThS. BS. Trần Văn Hùng' 
    WHERE UserId = 4;

    IF EXISTS (SELECT 1 FROM dbo.DentistProfiles WHERE UserId = 4)
        UPDATE dbo.DentistProfiles 
        SET Qualification = N'Thạc sĩ Y khoa (MSc) - Chuyên khoa Chỉnh nha & Thẩm mỹ',
            YearsOfExperience = 12,
            Biography = N'Thạc sĩ Răng Hàm Mặt - Đại học Y Dược TP.HCM. Chuyên gia nắn chỉnh răng và niềng răng trong suốt Invisalign Platinum Provider. 12 năm kinh nghiệm tái tạo nụ cười chuẩn thẩm mỹ.'
        WHERE UserId = 4;
    ELSE
        INSERT INTO dbo.DentistProfiles (UserId, Qualification, YearsOfExperience, Biography)
        VALUES (4, N'Thạc sĩ Y khoa (MSc) - Chuyên khoa Chỉnh nha & Thẩm mỹ', 12, N'Thạc sĩ Răng Hàm Mặt - Đại học Y Dược TP.HCM. Chuyên gia nắn chỉnh răng và niềng răng trong suốt Invisalign Platinum Provider. 12 năm kinh nghiệm tái tạo nụ cười chuẩn thẩm mỹ.');

    UPDATE dbo.UserAccounts 
    SET AvatarUrl = '/images/doctors/dr-tran-van-hung.svg' 
    WHERE UserId = 4;
END;

IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE UserId = 5)
BEGIN
    UPDATE dbo.StaffProfiles 
    SET FullName = N'BS. CKI Lê Hoàng Nam' 
    WHERE UserId = 5;

    IF EXISTS (SELECT 1 FROM dbo.DentistProfiles WHERE UserId = 5)
        UPDATE dbo.DentistProfiles 
        SET Qualification = N'Bác sĩ Chuyên khoa I (CKI) - Phẫu thuật Hàm mặt & Implant',
            YearsOfExperience = 10,
            Biography = N'Bác sĩ Chuyên khoa I Phẫu thuật Tạo hình Hàm mặt. Trưởng khoa Phẫu thuật & Implant DentalCare. Đã thực hiện hơn 2.500 ca cấy ghép Implant kỹ thuật số và nhổ răng khôn Piezotome không đau.'
        WHERE UserId = 5;
    ELSE
        INSERT INTO dbo.DentistProfiles (UserId, Qualification, YearsOfExperience, Biography)
        VALUES (5, N'Bác sĩ Chuyên khoa I (CKI) - Phẫu thuật Hàm mặt & Implant', 10, N'Bác sĩ Chuyên khoa I Phẫu thuật Tạo hình Hàm mặt. Trưởng khoa Phẫu thuật & Implant DentalCare. Đã thực hiện hơn 2.500 ca cấy ghép Implant kỹ thuật số và nhổ răng khôn Piezotome không đau.');

    UPDATE dbo.UserAccounts 
    SET AvatarUrl = '/images/doctors/dr-le-hoang-nam.svg' 
    WHERE UserId = 5;
END;
GO

DECLARE @AuthorUserId BIGINT;
SELECT TOP 1 @AuthorUserId = UserId FROM dbo.UserAccounts WHERE Role = 'Dentist' ORDER BY UserId ASC;
IF @AuthorUserId IS NULL SELECT TOP 1 @AuthorUserId = UserId FROM dbo.UserAccounts ORDER BY UserId ASC;
IF @AuthorUserId IS NULL SET @AuthorUserId = 1;

DECLARE @CatBenhLy INT, @CatImplant INT, @CatNiengRang INT, @CatChamSoc INT, @CatThamMy INT;
SELECT @CatBenhLy = CategoryId FROM dbo.BlogCategories WHERE Slug = 'benh-ly-rang-mieng';
SELECT @CatImplant = CategoryId FROM dbo.BlogCategories WHERE Slug = 'cay-ghep-implant';
SELECT @CatNiengRang = CategoryId FROM dbo.BlogCategories WHERE Slug = 'nieng-rang-chinh-nha';
SELECT @CatChamSoc = CategoryId FROM dbo.BlogCategories WHERE Slug = 'cham-soc-rang-mieng';
SELECT @CatThamMy = CategoryId FROM dbo.BlogCategories WHERE Slug = 'nha-khoa-tham-my';

IF @CatBenhLy IS NULL SELECT TOP 1 @CatBenhLy = CategoryId FROM dbo.BlogCategories;
IF @CatImplant IS NULL SET @CatImplant = @CatBenhLy;
IF @CatNiengRang IS NULL SET @CatNiengRang = @CatBenhLy;
IF @CatChamSoc IS NULL SET @CatChamSoc = @CatBenhLy;
IF @CatThamMy IS NULL SET @CatThamMy = @CatBenhLy;

-- ========================================================
-- BÀI VIẾT 1: ĐIỀU TRỊ VIÊM TỦY RĂNG NHƯ THẾ NÀO?
-- ========================================================
DECLARE @Content1 NVARCHAR(MAX) = N'<p class="lead"><strong>Tủy răng</strong> được ví như "trái tim" nuôi dưỡng sự sống của một chiếc răng, chứa hệ thống mạch máu và dây thần kinh cảm giác. Khi tủy răng bị tổn thương hoặc viêm nhiễm do vi khuẩn tấn công, người bệnh sẽ phải đối mặt với những cơn đau buốt buốt óc, ảnh hưởng nghiêm trọng đến ăn nhai và giấc ngủ. Cùng đội ngũ bác sĩ chuyên khoa tìm hiểu chi tiết về dấu hiệu nhận biết và phác đồ điều trị chuẩn y khoa qua bài viết dưới đây.</p>

<div class="my-4 text-center">
    <img src="/images/blog/viem-tuy-rang-banner.svg" alt="Sơ đồ cấu trúc giải phẫu răng và viêm tủy cấp" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 420px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 1: Mặt cắt giải phẫu răng cối - Vi khuẩn sâu răng ăn mòn men, ngà và gây viêm nhiễm buồng tủy cấp tính.</p>
</div>

<h2>1. Viêm tủy răng là gì?</h2>
<p>Viêm tủy răng là phản ứng viêm của các mô liên kết mạch máu và thần kinh nằm trong buồng tủy và ống tủy. Khác với các mô mềm khác trên cơ thể, tủy răng nằm trong một buồng kín có thành cứng (men và ngà răng). Khi bị viêm, mạch máu giãn nở gây tăng áp lực bên trong buồng tủy nhưng không thể giải phóng ra ngoài, dẫn đến chèn ép các đầu tận cùng thần kinh và gây ra những cơn đau nhức dữ dội.</p>

<div class="my-4 text-center">
    <img src="/images/blog/periopixel-dental-arch.png" alt="Mô phỏng 3D cung răng và nướu răng Periopixel" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 380px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 2: Mô phỏng 3D cung răng và mô nướu trong điều trị nội nha (Nguồn cảm hứng: 3D Dental Patient Education - Periopixel).</p>
</div>

<h2>2. Nguyên nhân hàng đầu dẫn đến viêm tủy răng</h2>
<p>Viêm tủy răng thường bắt nguồn từ những tổn thương kéo dài không được can thiệp đúng lúc:</p>
<ul>
    <li><strong>Sâu răng ăn sâu không chữa trị:</strong> Vi khuẩn sâu răng phá hủy ngà răng và trực tiếp xâm nhập vào buồng tủy (chiếm hơn 80% các ca viêm tủy).</li>
    <li><strong>Chấn thương cơ học:</strong> Răng bị sứt mẻ, nứt vỡ lớn làm lộ buồng tủy.</li>
    <li><strong>Viêm nha chu ngược dòng:</strong> Vi khuẩn từ túi nha chu quanh chân răng lây lan ngược qua chóp chân răng đi vào tủy.</li>
    <li><strong>Thói quen mòn cổ răng, nghiến răng:</strong> Men răng bị bào mòn quá mức khiến ngà răng mỏng dần, kích thích buồng tủy.</li>
</ul>

<h2>3. Quy trình điều trị viêm tủy răng chuẩn y khoa 5 bước</h2>
<p>Tại nha khoa DentalCare, quy trình điều trị tủy vi phẫu được thực hiện nghiêm ngặt theo tiêu chuẩn quốc tế:</p>
<ol>
    <li><strong>Thăm khám và chụp phim CT / X-quang:</strong> Khảo sát chiều dài ống tủy, vị trí nhiễm trùng và giải phẫu chân răng.</li>
    <li><strong>Gây tê không đau &amp; đặt đê cao su cách ly (Rubber Dam):</strong> Đảm bảo vô trùng tuyệt đối, ngăn nước bọt và bảo vệ đường thở.</li>
    <li><strong>Mở tủy và làm sạch bằng máy đo chóp định vị:</strong> Sử dụng hệ thống trâm xoay thông minh NiTi dẻo dai làm sạch triệt để mô tủy viêm.</li>
    <li><strong>Bơm rửa khử trùng chuyên sâu:</strong> Sử dụng dung dịch sát khuẩn y khoa loại trừ 99.9% vi khuẩn yếm khí.</li>
    <li><strong>Trám bít ống tủy 3D &amp; phục hình mão sứ:</strong> Trám kín hệ thống ống tủy bằng Gutta-percha sinh học và bọc sứ bảo vệ thân răng.</li>
</ol>

<blockquote class="p-3 bg-light rounded-3 border-start border-4 border-primary">
    <strong>Lời khuyên từ TS. BS. Nguyễn Thị Phương Thảo:</strong> Răng sau điều trị tủy sẽ mất nguồn nuôi dưỡng nên dần trở nên giòn. Việc bọc mão sứ bảo vệ là vô cùng cần thiết để khôi phục khả năng ăn nhai vững chắc và giữ răng thật bền lâu trọn đời.
</blockquote>';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'dieu-tri-viem-tuy-rang-nhu-the-nao')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatBenhLy, @AuthorUserId,
        N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
        'dieu-tri-viem-tuy-rang-nhu-the-nao',
        N'Tủy răng được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám.',
        @Content1,
        '/images/blog/viem-tuy-rang-banner.svg',
        'https://www.youtube.com/embed/VR8IQ9QFdWw',
        'Published', 1, 350, DATEADD(DAY, -2, SYSDATETIME()), DATEADD(DAY, -2, SYSDATETIME())
    );
END
ELSE
BEGIN
    UPDATE dbo.BlogPosts 
    SET Title = N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
        Content = @Content1,
        ThumbnailUrl = '/images/blog/viem-tuy-rang-banner.svg',
        VideoUrl = 'https://www.youtube.com/embed/VR8IQ9QFdWw',
        Status = 'Published',
        IsFeatured = 1,
        AuthorUserId = @AuthorUserId
    WHERE Slug = 'dieu-tri-viem-tuy-rang-nhu-the-nao';
END;

-- ========================================================
-- BÀI VIẾT 2: NHỔ RĂNG KHÔN MỌC LỆCH BẰNG PIEZOTOME
-- ========================================================
DECLARE @Content2 NVARCHAR(MAX) = N'<p class="lead">Răng khôn (răng số 8) mọc ngầm, mọc lệch là nỗi ám ảnh của nhiều người bởi những cơn đau nhức ê ẩm, sưng má và viêm lợi trùm. Nhờ ứng dụng công nghệ sóng siêu âm Piezotome tiên tiến, việc nhổ răng khôn hiện nay diễn ra nhẹ nhàng, êm ái, hạn chế tối đa xâm lấn và nhanh hồi phục.</p>

<div class="my-4 text-center">
    <img src="/images/blog/nho-rang-khon-banner.svg" alt="Răng khôn mọc lệch 90 độ và công nghệ sóng siêu âm Piezotome" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 420px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 1: Mô phỏng răng khôn số 8 nằm ngang đâm vào chân răng số 7 - Bóc tách êm ái bằng sóng siêu âm Piezotome.</p>
</div>

<h2>1. Biến chứng nguy hiểm nếu không nhổ răng khôn mọc lệch</h2>
<ul>
    <li><strong>Hỏng răng ăn nhai số 7:</strong> Răng khôn đẩy vào thân hoặc chân răng bên cạnh gây tiêu chân răng và sâu kẽ răng số 7.</li>
    <li><strong>Viêm lợi trùm và sốt cao:</strong> Nắp lợi che phủ răng khôn bị ứ đọng thức ăn gây sưng mủ, cứng hàm, khó há miệng.</li>
    <li><strong>Xô lệch toàn bộ hàm răng:</strong> Áp lực mọc răng số 8 có thể đẩy chen chúc các răng cửa phía trước.</li>
    <li><strong>U nang xương hàm:</strong> Tiêu hủy xương hàm âm thầm xung quanh thân răng ngầm.</li>
</ul>

<div class="my-4 text-center">
    <img src="/images/blog/periopixel-dental-arch.png" alt="Mô hình giải phẫu cung răng trước phẫu thuật" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 380px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 2: Định vị góc nghiêng và giải phẫu chân răng trên mô hình 3D (Periopixel 3D Dental Education).</p>
</div>

<h2>2. Vì sao nên chọn nhổ răng khôn bằng sóng siêu âm Piezotome?</h2>
<p>Công nghệ Piezotome sử dụng các bước sóng siêu âm cao tần chọn lọc chỉ tác động lên mô cứng của răng và làm đứt nhẹ nhàng hệ thống dây chằng nha chu xung quanh, hoàn toàn không làm tổn thương mô mềm, mạch máu hay nẹp xương ổ răng.</p>
<ul>
    <li>Thời gian nhổ nhanh chóng chỉ từ 10 - 15 phút.</li>
    <li>Giảm đến 85% tình trạng sưng bầm và chảy máu sau tiểu phẫu.</li>
    <li>Khả năng lành thương nhanh gấp 2 lần so với kỹ thuật đục gõ kìm bẩy truyền thống.</li>
</ul>';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'nho-rang-khon-moc-lech-khi-nao-quy-trinh-nho-rang-khong-dau')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatBenhLy, @AuthorUserId,
        N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
        'nho-rang-khon-moc-lech-khi-nao-quy-trinh-nho-rang-khong-dau',
        N'Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục.',
        @Content2,
        '/images/blog/nho-rang-khon-banner.svg',
        'https://www.youtube.com/embed/9KtA83_tUFQ',
        'Published', 1, 260, DATEADD(DAY, -4, SYSDATETIME()), DATEADD(DAY, -4, SYSDATETIME())
    );
END
ELSE
BEGIN
    UPDATE dbo.BlogPosts 
    SET Title = N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
        Content = @Content2,
        ThumbnailUrl = '/images/blog/nho-rang-khon-banner.svg',
        VideoUrl = 'https://www.youtube.com/embed/9KtA83_tUFQ',
        Status = 'Published',
        IsFeatured = 1,
        AuthorUserId = @AuthorUserId
    WHERE Slug = 'nho-rang-khon-moc-lech-khi-nao-quy-trinh-nho-rang-khong-dau';
END;

-- ========================================================
-- BÀI VIẾT 3: CẤY GHÉP IMPLANT NHA KHOA
-- ========================================================
DECLARE @Content3 NVARCHAR(MAX) = N'<p class="lead">Mất răng là nguyên nhân trực tiếp dẫn đến tiêu xương hàm, hóp má và suy giảm khả năng ăn nhai. Cấy ghép Implant là bước đột phá vĩ đại trong nha khoa hiện đại, giúp tái tạo một chiếc răng mới hoàn hảo từ chân răng đến thân răng sứ với độ bền trọn đời.</p>

<div class="my-4 text-center">
    <img src="/images/blog/implant-banner.svg" alt="Sơ đồ 3 phần cấu tạo của trụ Implant nha khoa" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 420px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 1: Cấu trúc 3 thành phần của răng Implant sinh học: Trụ Titanium tích hợp xương, khớp nối Abutment và Mão toàn sứ cao cấp.</p>
</div>

<h2>1. Cấu tạo ưu việt của răng Implant sinh học</h2>
<ul>
    <li><strong>Trụ Titanium nguyên chất:</strong> Vật liệu sinh học tương thích tuyệt đối với cơ thể người, kích thích tế bào xương hàm bám chặt và tích hợp vững chắc.</li>
    <li><strong>Khớp nối Abutment:</strong> Chốt liên kết chịu lực đóng vai trò nâng đỡ mão răng.</li>
    <li><strong>Mão răng toàn sứ CAD/CAM:</strong> Được chế tác bằng công nghệ kỹ thuật số sắc nét từng vân răng, mang lại vẻ đẹp tự nhiên và lực ăn nhai 100% như răng thật.</li>
</ul>

<div class="my-4 text-center">
    <img src="/images/blog/periopixel-dental-arch.png" alt="Vị trí phục hồi răng đã mất trên cung hàm 3D" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 380px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 2: Mô phỏng vị trí cấy ghép Implant trên cung hàm 3D (3D Dental Patient Education - Periopixel).</p>
</div>

<h2>2. Ưu thế vượt trội so với cầu răng sứ truyền thống</h2>
<table class="table table-bordered table-striped mt-3">
    <thead class="table-primary">
        <tr>
            <th>Tiêu chí</th>
            <th>Cấy ghép Implant</th>
            <th>Cầu răng sứ truyền thống</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td><strong>Bảo tồn răng thật</strong></td>
            <td>Không mài bất kỳ răng thật nào</td>
            <td>Phải mài nhỏ 2 răng thật bên cạnh</td>
        </tr>
        <tr>
            <td><strong>Ngăn tiêu xương hàm</strong></td>
            <td>Kích thích xương hàm, ngăn hóp má triệt để</td>
            <td>Xương bên dưới cầu răng vẫn tiêu dần</td>
        </tr>
        <tr>
            <td><strong>Tuổi thọ sử dụng</strong></td>
            <td>Trên 25 năm hoặc trọn đời nếu chăm sóc tốt</td>
            <td>Trung bình từ 7 - 10 năm</td>
        </tr>
    </tbody>
</table>';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatImplant, @AuthorUserId,
        N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
        'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat',
        N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật.',
        @Content3,
        '/images/blog/implant-banner.svg',
        'https://www.youtube.com/embed/B-sNEYkqoCk',
        'Published', 1, 520, DATEADD(DAY, -6, SYSDATETIME()), DATEADD(DAY, -6, SYSDATETIME())
    );
END
ELSE
BEGIN
    UPDATE dbo.BlogPosts 
    SET Title = N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
        Content = @Content3,
        ThumbnailUrl = '/images/blog/implant-banner.svg',
        VideoUrl = 'https://www.youtube.com/embed/B-sNEYkqoCk',
        Status = 'Published',
        IsFeatured = 1,
        AuthorUserId = @AuthorUserId
    WHERE Slug = 'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat';
END;

-- ========================================================
-- BÀI VIẾT 4: NIỀNG RĂNG MẮC CÀI HAY INVISALIGN
-- ========================================================
DECLARE @Content4 NVARCHAR(MAX) = N'<p class="lead">Bạn đang băn khoăn giữa niềng răng mắc cài cổ điển và máng niềng trong suốt Invisalign? Sự phát triển của ngành chỉnh nha hiện đại mang đến cho bạn nhiều lựa chọn tối ưu, phù hợp với từng nhu cầu thẩm mỹ và ngân sách.</p>

<div class="my-4 text-center">
    <img src="/images/blog/nieng-rang-banner.svg" alt="Khay niềng trong suốt Invisalign ôm sát hàm răng 3D" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 420px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 1: Khay chỉnh nha trong suốt Invisalign - Thẩm mỹ vô hình và dịch chuyển răng chuẩn xác theo phác đồ 3D ClinCheck.</p>
</div>

<h2>1. So sánh chi tiết hai phương pháp chỉnh nha</h2>
<ul>
    <li><strong>Niềng răng mắc cài:</strong> Lực kéo mạnh mẽ, hiệu quả cao cho mọi tình trạng răng hô, móm, khấp khểnh nặng; mức chi phí đa dạng, phù hợp với nhiều đối tượng.</li>
    <li><strong>Niềng răng máng trong suốt Invisalign:</strong> Độ trong suốt đạt 99%, tháo lắp linh hoạt khi ăn uống và vệ sinh, không cọ sát gây nhiệt miệng, biết trước kết quả dịch chuyển qua video mô phỏng 3D ClinCheck.</li>
</ul>

<blockquote class="p-3 bg-light rounded-3 border-start border-4 border-primary">
    <strong>Lời khuyên:</strong> Hãy đến gặp bác sĩ chuyên khoa chỉnh nha để được quét mẫu hàm 3D iTero và tư vấn phác đồ phù hợp nhất với cấu trúc xương hàm của bạn.
</blockquote>';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'nieng-rang-mac-cai-hay-invisalign-nen-chon-phuong-phap-nao')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatNiengRang, @AuthorUserId,
        N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
        'nieng-rang-mac-cai-hay-invisalign-nen-chon-phuong-phap-nao',
        N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất.',
        @Content4,
        '/images/blog/nieng-rang-banner.svg',
        'https://www.youtube.com/embed/so1FkkahF4U',
        'Published', 0, 195, DATEADD(DAY, -8, SYSDATETIME()), DATEADD(DAY, -8, SYSDATETIME())
    );
END
ELSE
BEGIN
    UPDATE dbo.BlogPosts 
    SET Title = N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
        Content = @Content4,
        ThumbnailUrl = '/images/blog/nieng-rang-banner.svg',
        VideoUrl = 'https://www.youtube.com/embed/so1FkkahF4U',
        Status = 'Published',
        AuthorUserId = @AuthorUserId
    WHERE Slug = 'nieng-rang-mac-cai-hay-invisalign-nen-chon-phuong-phap-nao';
END;

-- ========================================================
-- BÀI VIẾT 5: 6 THÓI QUEN CHĂM SÓC RĂNG MIỆNG ĐÚNG CÁCH
-- ========================================================
DECLARE @Content5 NVARCHAR(MAX) = N'<p class="lead">Nụ cười khỏe đẹp bắt đầu từ những thói quen vệ sinh răng miệng đúng chuẩn mỗi ngày. Chải răng 2 lần/ngày đã đủ chưa? Khám phá 6 thói quen vàng chuẩn nha khoa quốc tế để ngăn ngừa 100% sâu răng và viêm nướu.</p>

<div class="my-4 text-center">
    <img src="/images/blog/cham-soc-rang-mieng-3d.png" alt="Mô phỏng 3D kỹ thuật chải răng đúng cách Periopixel" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 400px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 1: Mô phỏng 3D kỹ thuật chải răng góc nghiêng 45 độ theo phương pháp Bass (Nguồn: 3D Dental Patient Education - Periopixel).</p>
</div>

<h2>1. Chải răng đúng kỹ thuật Bass cải tiến</h2>
<p>Nghiêng đầu bàn chải 45 độ so với trục răng và viền nướu. Rung nhẹ theo chiều dọc hoặc xoay tròn để lông bàn chải len vào rãnh nướu làm sạch mảng bám vi khuẩn mà không làm mòn cổ men răng.</p>

<div class="my-4 text-center">
    <img src="/images/blog/cham-soc-rang-banner.svg" alt="Bộ dụng cụ chăm sóc răng miệng khoa học" class="img-fluid rounded-4 shadow-sm border blog-zoomable-img" style="max-height: 420px; width: 100%; object-fit: cover;" />
    <p class="text-muted small mt-2 fst-italic"><i class="bi bi-camera me-1"></i>Hình 2: Thói quen chăm sóc răng miệng toàn diện với kem Fluoride, chỉ nha khoa và máy tăm nước.</p>
</div>

<h2>2. Sử dụng chỉ nha khoa và máy tăm nước mỗi ngày</h2>
<p>Bàn chải chỉ làm sạch 60% mặt răng. 40% mảng bám còn lại nằm ở các kẽ hở giữa hai răng – nơi chỉ có chỉ nha khoa hoặc tia nước áp lực từ máy tăm nước mới tiếp cận và rửa trôi được.</p>

<h2>3. Vệ sinh mặt lưỡi và súc miệng nước muối sinh lý</h2>
<p>Lưỡi chứa hàng triệu vi khuẩn gây mùi hôi. Sử dụng cây cạo lưỡi chuyên dụng mỗi sáng kết hợp súc miệng giúp khoang miệng luôn thơm tho và sạch khuẩn.</p>

<h2>4. Thăm khám và cạo vôi răng định kỳ 6 tháng/lần</h2>
<p>Vôi răng (cao răng) đã bị khoáng hóa cứng không thể chải sạch bằng bàn chải thông thường. Cạo vôi răng bằng sóng siêu âm định kỳ giúp nướu răng luôn săn chắc, hồng hào và ngăn chặn viêm nha chu.</p>';

IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'huong-dan-cham-soc-rang-mieng-dung-cach-6-thoi-quen')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatChamSoc, @AuthorUserId,
        N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
        'huong-dan-cham-soc-rang-mieng-dung-cach-6-thoi-quen',
        N'Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước.',
        @Content5,
        '/images/blog/cham-soc-rang-banner.svg',
        'https://www.youtube.com/embed/VdjmGxq-X7M',
        'Published', 0, 150, DATEADD(DAY, -10, SYSDATETIME()), DATEADD(DAY, -10, SYSDATETIME())
    );
END
ELSE
BEGIN
    UPDATE dbo.BlogPosts 
    SET Title = N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
        Content = @Content5,
        ThumbnailUrl = '/images/blog/cham-soc-rang-banner.svg',
        VideoUrl = 'https://www.youtube.com/embed/VdjmGxq-X7M',
        Status = 'Published',
        AuthorUserId = @AuthorUserId
    WHERE Slug = 'huong-dan-cham-soc-rang-mieng-dung-cach-6-thoi-quen';
END;
GO

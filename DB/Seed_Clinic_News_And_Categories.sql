USE DentalClinicManagementDB;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

DECLARE @Now DATETIME2 = SYSDATETIME();

IF NOT EXISTS (SELECT 1 FROM BlogCategories WHERE Slug = 'tin-tuc-su-kien')
BEGIN
    INSERT INTO BlogCategories (Name, Slug, CreatedAt) 
    VALUES (N'Tin tức & Sự kiện', 'tin-tuc-su-kien', @Now);
END

IF NOT EXISTS (SELECT 1 FROM BlogCategories WHERE Slug = 'thong-bao-phong-kham')
BEGIN
    INSERT INTO BlogCategories (Name, Slug, CreatedAt) 
    VALUES (N'Thông báo phòng khám', 'thong-bao-phong-kham', @Now);
END

DECLARE @AdminId BIGINT;
SELECT @AdminId = UserId FROM UserAccounts WHERE Email = 'admin@dentalcare.com';
IF @AdminId IS NULL SET @AdminId = 1;

DECLARE @NewsCatId INT;
SELECT @NewsCatId = CategoryId FROM BlogCategories WHERE Slug = 'tin-tuc-su-kien';

DECLARE @NoticeCatId INT;
SELECT @NoticeCatId = CategoryId FROM BlogCategories WHERE Slug = 'thong-bao-phong-kham';

IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'khai-truong-trung-tam-chuan-doan-hinh-anh-conebeam-3d')
BEGIN
    INSERT INTO BlogPosts (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt, UpdatedAt
    )
    VALUES (
        @NewsCatId, @AdminId,
        N'Nha Khoa DentalCare chính thức khai trương Trung Tâm Chuẩn Đoán Hình Ảnh & Laser 3D',
        'khai-truong-trung-tam-chuan-doan-hinh-anh-conebeam-3d',
        N'Hệ thống máy chụp CT ConeBeam 3D thế hệ mới nhất cùng công nghệ Laser hiện đại đã chính thức đi vào hoạt động nhằm nâng cao chất lượng chẩn đoán và điều trị.',
        N'<p>Nhằm không ngừng nâng cao chất lượng khám chữa bệnh, <strong>Hệ thống Nha khoa DentalCare</strong> vừa chính thức đưa vào vận hành <strong>Trung Tâm Chuẩn Đoán Hình Ảnh & Laser</strong> với trang thiết bị nhập khẩu chính hãng từ Đức và Thụy Sĩ.</p><p>Hệ thống CT ConeBeam 3D cho phép chụp cắt lớp vi tính cấu trúc xương hàm với độ chính xác micromet, liều tia X cực thấp, an toàn tối đa cho bệnh nhân ở mọi lứa tuổi.</p>',
        '/images/blog/laser-whitening.svg',
        'Published', 1, 150, @Now, @Now, @Now
    )
END

IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'thong-bao-lich-kham-va-cham-soc-khach-hang-2026')
BEGIN
    INSERT INTO BlogPosts (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt, UpdatedAt
    )
    VALUES (
        @NoticeCatId, @AdminId,
        N'Thông báo lịch làm việc chuyên khoa và chương trình chăm sóc định kỳ quý 4/2026',
        'thong-bao-lich-kham-va-cham-soc-khach-hang-2026',
        N'DentalCare thông báo lịch làm việc liên tục các ngày trong tuần từ 08:00 đến 20:00 và chương trình thăm khám răng định kỳ miễn phí cho cư dân.',
        N'<p>Nhằm phục vụ tốt nhất nhu cầu khám chữa bệnh răng miệng, <strong>Nha Khoa DentalCare</strong> thông báo lịch làm việc mở rộng tất cả các ngày trong tuần (kể cả Thứ 7 và Chủ Nhật).</p><p>Quý khách vui lòng đặt lịch hẹn trực tuyến qua website hoặc ứng dụng để được tiếp đón chu đáo và không phải chờ đợi lâu.</p>',
        '/images/blog/cham-soc-rang-banner.svg',
        'Published', 0, 85, @Now, @Now, @Now
    )
END

PRINT N'>>> ĐÃ BỔ SUNG DANH MỤC VÀ BÀI VIẾT TIN TỨC PHÒNG KHÁM THÀNH CÔNG!';

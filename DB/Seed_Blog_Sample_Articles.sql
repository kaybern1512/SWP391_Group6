-- ========================================================
-- Seed Script: Professional Dental Health Articles (Task 6)
-- Target: SQL Server / Azure SQL / LocalDB
-- Description: Idempotent script inserting 5 rich, clinical sample dental articles
--              including the requested "Điều trị viêm tủy răng như thế nào?"
-- ========================================================

IF DB_ID(N'DentalClinicManagementDB') IS NOT NULL
BEGIN
    USE DentalClinicManagementDB;
END;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @AuthorUserId BIGINT;
-- Find a dentist account, or fallback to first active user
SELECT TOP 1 @AuthorUserId = u.UserId
FROM dbo.UserAccounts u
LEFT JOIN dbo.Roles r ON u.RoleId = r.RoleId
WHERE r.RoleName = 'Dentist'
ORDER BY u.UserId ASC;

IF @AuthorUserId IS NULL
BEGIN
    SELECT TOP 1 @AuthorUserId = UserId FROM dbo.UserAccounts ORDER BY UserId ASC;
END;

-- If still null (e.g. fresh DB with no users yet), create or use ID 1
IF @AuthorUserId IS NULL SET @AuthorUserId = 1;

DECLARE @CatBenhLy INT, @CatImplant INT, @CatNiengRang INT, @CatChamSoc INT, @CatThamMy INT;
SELECT @CatBenhLy = CategoryId FROM dbo.BlogCategories WHERE Slug = 'benh-ly-rang-mieng';
SELECT @CatImplant = CategoryId FROM dbo.BlogCategories WHERE Slug = 'cay-ghep-implant';
SELECT @CatNiengRang = CategoryId FROM dbo.BlogCategories WHERE Slug = 'nieng-rang-chinh-nha';
SELECT @CatChamSoc = CategoryId FROM dbo.BlogCategories WHERE Slug = 'cham-soc-rang-mieng';
SELECT @CatThamMy = CategoryId FROM dbo.BlogCategories WHERE Slug = 'nha-khoa-tham-my';

-- Fallback category IDs if categories were empty
IF @CatBenhLy IS NULL SELECT TOP 1 @CatBenhLy = CategoryId FROM dbo.BlogCategories;
IF @CatImplant IS NULL SET @CatImplant = @CatBenhLy;
IF @CatNiengRang IS NULL SET @CatNiengRang = @CatBenhLy;
IF @CatChamSoc IS NULL SET @CatChamSoc = @CatBenhLy;
IF @CatThamMy IS NULL SET @CatThamMy = @CatBenhLy;

-- ========================================================
-- BÀI VIẾT 1: ĐIỀU TRỊ VIÊM TỦY RĂNG NHƯ THẾ NÀO? (TRỌNG TÂM)
-- ========================================================
IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'dieu-tri-viem-tuy-rang-nhu-the-nao')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatBenhLy,
        @AuthorUserId,
        N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
        'dieu-tri-viem-tuy-rang-nhu-the-nao',
        N'Tủy răng được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám.',
        N'<p class="lead"><strong>Tủy răng</strong> được ví như "trái tim" nuôi dưỡng sự sống của một chiếc răng, chứa hệ thống mạch máu và dây thần kinh cảm giác. Khi tủy răng bị tổn thương hoặc viêm nhiễm do vi khuẩn tấn công, người bệnh sẽ phải đối mặt với những cơn đau buốt buốt óc, ảnh hưởng nghiêm trọng đến ăn nhai và giấc ngủ. Vậy <em>điều trị viêm tủy răng như thế nào</em> và quy trình chuẩn y khoa diễn ra ra sao? Hãy cùng đội ngũ bác sĩ chuyên khoa tìm hiểu chi tiết qua bài viết dưới đây.</p>

<h2>1. Viêm tủy răng là gì?</h2>
<p>Viêm tủy răng là tình trạng phản ứng viêm của các mô liên kết mạch máu và thần kinh nằm trong buồng tủy và ống tủy. Khác với các mô mềm khác trên cơ thể, tủy răng nằm trong một buồng kín có thành cứng (men và ngà răng). Khi bị viêm, mạch máu giãn nở gây tăng áp lực bên trong buồng tủy nhưng không thể giải phóng ra ngoài, dẫn đến chèn ép các đầu tận cùng thần kinh và gây ra những cơn đau nhức dữ dội.</p>

<h2>2. Nguyên nhân hàng đầu dẫn đến viêm tủy răng</h2>
<p>Viêm tủy răng không tự nhiên xuất hiện mà thường là hậu quả của các vấn đề răng miệng không được can thiệp kịp thời:</p>
<ul>
    <li><strong>Sâu răng ăn sâu không chữa trị:</strong> Vi khuẩn sâu răng ăn mòn dần lớp men răng, phá hủy ngà răng và trực tiếp xâm nhập vào buồng tủy. Đây là nguyên nhân chiếm hơn 80% các ca viêm tủy.</li>
    <li><strong>Chấn thương cơ học:</strong> Răng bị mẻ, nứt vỡ lớn hoặc chấn thương do tai nạn làm lộ buồng tủy, tạo đường thông cho vi khuẩn khoang miệng xâm nhập.</li>
    <li><strong>Viêm nha chu ngược dòng:</strong> Vi khuẩn từ túi nha chu quanh chân răng lây lan ngược qua cuống răng (chóp chân răng) đi vào tủy.</li>
    <li><strong>Thói quen mòn cổ răng, nghiến răng:</strong> Lớp men răng bị bào mòn quá mức khiến lớp ngà mỏng dần, tạo điều kiện cho các kích thích nóng, lạnh xâm lấn tủy.</li>
</ul>

<h2>3. Các giai đoạn phát triển của bệnh viêm tủy răng</h2>
<p>Bệnh lý viêm tủy răng thường tiến triển qua 3 giai đoạn rõ rệt với mức độ nguy hiểm tăng dần:</p>
<ul>
    <li><strong>Giai đoạn 1: Viêm tủy có hồi phục:</strong> Răng bắt đầu ê buốt thoáng qua khi ăn đồ quá nóng, lạnh, chua hoặc ngọt. Cơn đau dứt ngay khi ngưng kích thích. Nếu phát hiện ở giai đoạn này, bác sĩ có thể làm sạch lỗ sâu và trám bảo tồn tủy răng.</li>
    <li><strong>Giai đoạn 2: Viêm tủy không hồi phục:</strong> Cơn đau xuất hiện tự nhiên, đau nhói từng cơn kéo dài hàng chục phút, lan lên nửa đầu và vùng thái dương. Đau dữ dội hơn vào ban đêm khi nằm xuống do áp lực máu dồn lên đầu. Lúc này, bắt buộc phải tiến hành lấy tủy răng.</li>
    <li><strong>Giai đoạn 3: Hoại tử tủy (Chết tủy):</strong> Tủy răng bị chết hoàn toàn, cảm giác đau nhức đột ngột biến mất khiến nhiều người lầm tưởng bệnh đã khỏi. Tuy nhiên, vi khuẩn bắt đầu phân hủy tủy tạo mủ, gây áp-xe chóp chân răng, sưng nướu, có mùi hôi và nguy cơ tiêu xương ổ răng, rụng răng.</li>
</ul>

<h2>4. Quy trình điều trị viêm tủy răng chuẩn y khoa 5 bước</h2>
<p>Tại nha khoa, quy trình điều trị tủy (nội nha) hiện đại được thực hiện nghiêm ngặt theo tiêu chuẩn của Bộ Y tế, bảo đảm vô trùng tuyệt đối và hạn chế tối đa cảm giác khó chịu cho bệnh nhân:</p>

<h3>Bước 1: Thăm khám và chụp phim X-quang kỹ thuật số</h3>
<p>Bác sĩ kiểm tra mức độ tổn thương lâm sàng và chỉ định chụp phim X-quang răng để quan sát hình thái buồng tủy, số lượng ống tủy, chiều dài chân răng và mức độ viêm nhiễm quanh cuống răng.</p>

<h3>Bước 2: Vệ sinh khoang miệng và gây tê cục bộ</h3>
<p>Trước khi thao tác, bác sĩ tiến hành sát khuẩn khoang miệng và tiêm thuốc gây tê cục bộ công nghệ mới. Nhờ đó, người bệnh hoàn toàn không có cảm giác đau buốt trong suốt quá trình lấy tủy.</p>

<h3>Bước 3: Đặt đê cao su cách ly tuyệt đối (Rubber Dam)</h3>
<p>Đê cao su là tiêu chuẩn vàng trong điều trị nội nha hiện đại. Tấm đê giúp cách ly răng cần chữa tủy với môi trường khoang miệng, ngăn không cho nước bọt chứa vi khuẩn xâm nhập vào ống tủy, đồng thời ngăn các dung dịch bơm rửa nha khoa rơi vào đường thở hoặc họng của bệnh nhân.</p>

<h3>Bước 4: Mở tủy, làm sạch và tạo hình hệ thống ống tủy</h3>
<p>Bác sĩ sử dụng mũi khoan chuyên dụng tạo đường vào buồng tủy. Sau đó, dùng hệ thống trâm xoay máy NiTi dẻo dai kết hợp máy định vị chóp điện tử để đo chính xác chiều dài làm việc, lấy sạch toàn bộ tủy viêm nhiễm và bơm rửa sát trùng bằng dung dịch chuyên dụng (NaOCl, EDTA).</p>

<h3>Bước 5: Trám bít ống tủy 3D và phục hình thân răng</h3>
<p>Sau khi ống tủy đã sạch và khô ráo, bác sĩ tiến hành trám bít kín khít toàn bộ hệ thống ống tủy bằng vật liệu nhựa sinh học Gutta-percha cùng xi măng nha khoa chuyên dụng để ngăn ngừa tái nhiễm khuẩn. Cuối cùng, răng được tái tạo thân bằng trám Composite thẩm mỹ hoặc bọc mão răng sứ.</p>

<blockquote>
    <strong>Bác sĩ khuyên:</strong> Răng sau khi lấy tủy không còn nguồn nuôi dưỡng nên sẽ trở nên giòn, dễ nứt vỡ khi nhai thức ăn cứng. Do đó, bọc mão răng sứ là giải pháp tối ưu giúp bảo tồn răng thật tồn tại chắc khỏe hàng chục năm.
</blockquote>

<h2>5. Điều trị tủy răng có đau không?</h2>
<p>Với sự phát triển của công nghệ nha khoa hiện đại, thuốc tê thế hệ mới cùng máy định vị chóp và trâm xoay thông minh, quá trình điều trị tủy răng diễn ra hết sức nhẹ nhàng và êm ái. Hầu hết bệnh nhân chỉ cảm thấy hơi căng tức nhẹ sau khi hết thuốc tê và cảm giác này sẽ biến mất sau 1 - 2 ngày.</p>

<h2>6. Lời khuyên phòng ngừa viêm tủy răng</h2>
<ul>
    <li>Chải răng đúng cách ít nhất 2 lần/ngày bằng kem đánh răng chứa Fluoride.</li>
    <li>Sử dụng chỉ nha khoa hoặc máy tăm nước sau mỗi bữa ăn để làm sạch thức ăn thừa ở kẽ răng.</li>
    <li>Điều trị sâu răng sớm ngay khi mới chớm hình thành đốm đen hoặc lỗ nhỏ.</li>
    <li>Thăm khám nha khoa và cạo vôi răng định kỳ 6 tháng một lần để phát hiện sớm các bệnh lý tiềm ẩn.</li>
</ul>',
        'https://res.cloudinary.com/demo/image/upload/v1/dental/viem-tuy-rang-banner.jpg',
        NULL,
        'Published',
        1,
        342,
        DATEADD(DAY, -2, SYSDATETIME()),
        DATEADD(DAY, -2, SYSDATETIME())
    );
END;
GO

-- ========================================================
-- BÀI VIẾT 2: NHỔ RĂNG KHÔN MỌC LỆCH BẰNG PIEZOTOME
-- ========================================================
IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'nho-rang-khon-moc-lech-khi-nao-quy-trinh-nho-rang-khong-dau')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatBenhLy,
        @AuthorUserId,
        N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
        'nho-rang-khon-moc-lech-khi-nao-quy-trinh-nho-rang-khong-dau',
        N'Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục.',
        N'<p class="lead">Răng khôn hay còn gọi là răng số 8 là những chiếc răng mọc cuối cùng trên cung hàm ở độ tuổi trưởng thành (từ 17 - 25 tuổi). Do xương hàm lúc này đã phát triển hoàn thiện và không còn đủ chỗ trống, răng khôn rất dễ mọc lệch, mọc ngầm, đâm ngang gây ra nhiều biến chứng nguy hiểm.</p>

<h2>1. Những biến chứng thường gặp do răng khôn mọc lệch</h2>
<ul>
    <li><strong>Sâu và hỏng răng số 7 bên cạnh:</strong> Răng khôn mọc nghiêng tựa vào thân hoặc chân răng số 7 tạo thành khe giắt thức ăn không thể làm sạch, dẫn đến sâu răng âm thầm và làm lung lay, rụng răng ăn nhai chính.</li>
    <li><strong>Viêm lợi trùm tái phát:</strong> Phần nướu trùm lên thân răng khôn bị ứ đọng thức ăn và vi khuẩn, gây viêm sưng đỏ, chảy mủ, sốt cao và há miệng khó khăn.</li>
    <li><strong>Xô lệch toàn bộ cung hàm:</strong> Lực đẩy liên tục của răng khôn trong quá trình mọc có thể làm xô lệch các răng phía trước, phá vỡ khớp cắn cân đối.</li>
    <li><strong>Nang thân răng, tiêu xương hàm:</strong> Một số trường hợp răng khôn mọc ngầm tạo thành u nang trong xương hàm, làm tiêu hủy xương và suy yếu xương hàm dưới.</li>
</ul>

<h2>2. Khi nào nên nhổ răng khôn?</h2>
<p>Bác sĩ chuyên khoa khuyến cáo nên nhổ bỏ răng khôn trong các trường hợp sau:</p>
<ol>
    <li>Răng khôn mọc lệch, mọc ngang, đâm vào răng số 7.</li>
    <li>Răng khôn bị sâu nặng hoặc viêm tủy.</li>
    <li>Gây viêm lợi trùm, nhiễm trùng tái phát nhiều lần.</li>
    <li>Khe giữa răng khôn và răng bên cạnh thường xuyên nhồi nhét thức ăn.</li>
    <li>Chỉ định nhổ để hỗ trợ chỉnh nha - niềng răng.</li>
</ol>

<h2>3. Công nghệ nhổ răng sóng siêu âm Piezotome – Êm ái, không đau</h2>
<p>Khác với phương pháp nhổ răng truyền thống dùng kìm bẩy và búa gây sang chấn lớn, công nghệ Piezotome sử dụng sóng siêu âm cao tần chọn lọc tác động nhẹ nhàng làm đứt các dây chằng nha chu quanh chân răng. Nhờ đó, răng được lấy ra nhanh chóng mà không làm tổn thương màng xương và nẹp lợi xung quanh.</p>

<blockquote>
    <strong>Ưu điểm vượt trội của Piezotome:</strong> Giảm cảm giác sợ hãi, vết cắt chuẩn xác, giảm 80% sưng phù và chảy máu, thời gian lành thương nhanh hơn gấp 2 lần so với phương pháp thông thường.
</blockquote>',
        'https://res.cloudinary.com/demo/image/upload/v1/dental/nho-rang-khon-banner.jpg',
        NULL,
        'Published',
        1,
        258,
        DATEADD(DAY, -4, SYSDATETIME()),
        DATEADD(DAY, -4, SYSDATETIME())
    );
END;
GO

-- ========================================================
-- BÀI VIẾT 3: CẤY GHÉP IMPLANT NHA KHOA
-- ========================================================
IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatImplant,
        @AuthorUserId,
        N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
        'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat',
        N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật.',
        N'<p class="lead">Mất răng là biến cố nha khoa đáng tiếc xảy ra do sâu răng nặng, chấn thương hoặc bệnh viêm nha chu tuổi trung niên. Trước đây, cầu răng sứ hay hàm tháo lắp là những giải pháp thường dùng nhưng đều có nhược điểm mài nhỏ răng thật hoặc gây tiêu xương hàm theo thời gian. <strong>Cấy ghép Implant nha khoa</strong> ra đời đã trở thành cuộc cách mạng phục hình răng đỉnh cao.</p>

<h2>1. Cấu tạo của một chiếc răng Implant hoàn chỉnh</h2>
<p>Một chiếc răng Implant bao gồm 3 bộ phận chính được gắn kết vững chắc:</p>
<ul>
    <li><strong>Trụ Implant:</strong> Được chế tạo từ Titanium tinh khiết có tính tương thích sinh học cao với cơ thể con người, đóng vai trò như một chân răng nhân tạo cắm trực tiếp vào xương hàm.</li>
    <li><strong>Khớp nối Abutment:</strong> Đóng vai trò làm chốt kết nối giữa trụ Implant bên dưới và thân răng sứ bên trên.</li>
    <li><strong>Mão răng toàn sứ:</strong> Được thiết kế tinh xảo theo hình dáng, kích thước và màu sắc tự nhiên của răng thật, đáp ứng tính thẩm mỹ cao nhất.</li>
</ul>

<h2>2. So sánh Cấy ghép Implant với Cầu răng sứ và Hàm tháo lắp</h2>
<table class="table table-bordered">
    <thead>
        <tr>
            <th>Tiêu chí</th>
            <th>Cấy ghép Implant</th>
            <th>Cầu răng sứ</th>
            <th>Hàm giả tháo lắp</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td><strong>Mài răng thật</strong></td>
            <td>Không mài, bảo tồn 100% răng bên cạnh</td>
            <td>Bắt buộc mài 2 răng thật bên cạnh</td>
            <td>Không mài hoặc dùng móc kim loại</td>
        </tr>
        <tr>
            <td><strong>Ngăn ngừa tiêu xương</strong></td>
            <td>Ngăn chặn tiêu xương hàm triệt để</td>
            <td>Xương dưới cầu răng vẫn tiêu dần</td>
            <td>Không ngăn được tiêu xương hàm</td>
        </tr>
        <tr>
            <td><strong>Lực ăn nhai</strong></td>
            <td>Gần như 100% như răng thật</td>
            <td>Khoảng 60% - 70%</td>
            <td>Chỉ đạt 30% - 40%</td>
        </tr>
        <tr>
            <td><strong>Tuổi thọ</strong></td>
            <td>Trên 25 năm hoặc trọn đời</td>
            <td>7 - 10 năm</td>
            <td>3 - 5 năm</td>
        </tr>
    </tbody>
</table>

<h2>3. Quy trình cấy ghép Implant kỹ thuật số an toàn</h2>
<ol>
    <li>Chụp phim CT Cone Beam 3D khảo sát mật độ xương và thần kinh hàm.</li>
    <li>Lập phác đồ điều trị 3D bằng phần mềm cấy ghép ảo và chế tạo máng định vị.</li>
    <li>Tiến hành tiểu phẫu cấy trụ Implant trong phòng phẫu thuật vô trùng tuyệt đối (15 - 20 phút/trụ).</li>
    <li>Giai đoạn lành thương và tích hợp xương sinh học (2 - 3 tháng).</li>
    <li>Lắp khớp nối Abutment và gắn mão răng sứ vĩnh viễn.</li>
</ol>',
        'https://res.cloudinary.com/demo/image/upload/v1/dental/implant-banner.jpg',
        NULL,
        'Published',
        1,
        512,
        DATEADD(DAY, -6, SYSDATETIME()),
        DATEADD(DAY, -6, SYSDATETIME())
    );
END;
GO

-- ========================================================
-- BÀI VIẾT 4: NIỀNG RĂNG MẮC CÀI HAY INVISALIGN
-- ========================================================
IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'nieng-rang-mac-cai-hay-invisalign-nen-chon-phuong-phap-nao')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatNiengRang,
        @AuthorUserId,
        N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
        'nieng-rang-mac-cai-hay-invisalign-nen-chon-phuong-phap-nao',
        N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất.',
        N'<p class="lead">Sở hữu một hàm răng đều đặn cùng nụ cười tỏa sáng là mong ước của nhiều người. Ngày nay, công nghệ chỉnh nha đã có những bước tiến vượt bậc với hai xu hướng lớn: <strong>Niềng răng mắc cài</strong> (truyền thống & hiện đại) và <strong>Máng niềng trong suốt Invisalign</strong>. Mỗi phương pháp đều sở hữu những ưu thế riêng biệt.</p>

<h2>1. Niềng răng mắc cài (Kim loại / Sứ)</h2>
<p>Niềng răng mắc cài sử dụng hệ thống dây cung và mắc cài gắn chặt lên mặt ngoài hoặc mặt trong của thân răng để tạo lực dịch chuyển răng liên tục và ổn định.</p>
<ul>
    <li><strong>Ưu điểm:</strong> Lực kéo mạnh mẽ, giải quyết hiệu quả cả những ca răng mọc lệch, khấp khểnh, hô móm rất phức tạp; chi phí đa dạng, hợp lý với học sinh, sinh viên.</li>
    <li><strong>Nhược điểm:</strong> Tính thẩm mỹ chưa cao (đối với mắc cài kim loại), cảm giác vướng víu trong miệng trong những tuần đầu, cần chăm sóc vệ sinh kẽ răng rất cẩn thận.</li>
</ul>

<h2>2. Niềng răng trong suốt Invisalign</h2>
<p>Invisalign sử dụng một chuỗi các khay niềng nhựa sinh học thông minh SmartTrack được thiết kế cá nhân hóa theo dấu răng của từng khách hàng qua công nghệ quét mẫu dấu 3D iTero.</p>
<ul>
    <li><strong>Ưu điểm:</strong> Khay niềng gần như vô hình, tính thẩm mỹ tuyệt đối khi giao tiếp; có thể tháo lắp tự do khi ăn uống và đánh răng; thấy trước kết quả nắn chỉnh qua video 3D ClinCheck.</li>
    <li><strong>Nhược điểm:</strong> Chi phí đầu tư cao hơn; đòi hỏi sự tự giác cao (cần đeo tối thiểu 20 - 22 tiếng mỗi ngày).</li>
</ul>

<blockquote>
    <strong>Lời khuyên từ bác sĩ chỉnh nha:</strong> Không có phương pháp nào là tốt nhất cho mọi người, chỉ có phương pháp phù hợp nhất với tình trạng răng, tính chất công việc và khả năng tài chính của bạn. Hãy đến thăm khám trực tiếp để nhận được kế hoạch điều trị tối ưu.
</blockquote>',
        'https://res.cloudinary.com/demo/image/upload/v1/dental/nieng-rang-banner.jpg',
        NULL,
        'Published',
        0,
        189,
        DATEADD(DAY, -8, SYSDATETIME()),
        DATEADD(DAY, -8, SYSDATETIME())
    );
END;
GO

-- ========================================================
-- BÀI VIẾT 5: 6 THÓI QUEN CHĂM SÓC RĂNG MIỆNG ĐÚNG CÁCH
-- ========================================================
IF NOT EXISTS (SELECT 1 FROM dbo.BlogPosts WHERE Slug = 'huong-dan-cham-soc-rang-mieng-dung-cach-6-thoi-quen')
BEGIN
    INSERT INTO dbo.BlogPosts 
    (
        CategoryId, AuthorUserId, Title, Slug, Summary, Content, 
        ThumbnailUrl, VideoUrl, Status, IsFeatured, ViewCount, PublishedAt, CreatedAt
    )
    VALUES
    (
        @CatChamSoc,
        @AuthorUserId,
        N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
        'huong-dan-cham-soc-rang-mieng-dung-cach-6-thoi-quen',
        N'Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước.',
        N'<p class="lead">Một nụ cười khỏe đẹp không chỉ giúp tăng sự tự tin trong giao tiếp hàng ngày mà còn là tấm gương phản chiếu sức khỏe tổng thể của cơ thể. Tuy nhiên, nhiều người vẫn đang mắc phải những sai lầm trong việc vệ sinh răng miệng. Dưới đây là 6 thói quen vàng được các chuyên gia nha khoa khuyên áp dụng hàng ngày.</p>

<h2>1. Chải răng đúng kỹ thuật Bass cải tiến</h2>
<p>Nhiều người có thói quen chải răng theo chiều ngang với lực mạnh, điều này dễ làm mòn men răng và tụt nướu. Kỹ thuật đúng là đặt lông bàn chải nghiêng góc 45 độ so với viền nướu, rung nhẹ tại chỗ rồi vuốt dọc từ viền nướu xuống thân răng. Thời gian chải răng tối thiểu nên kéo dài từ 2 đến 3 phút.</p>

<h2>2. Bổ sung chỉ nha khoa hoặc máy tăm nước</h2>
<p>Lông bàn chải thông thường chỉ làm sạch được khoảng 60% bề mặt răng, 40% còn lại nằm ở các kẽ răng tiếp giáp. Chỉ nha khoa hoặc tia nước áp lực từ máy tăm nước là giải pháp duy nhất loại bỏ triệt để thức ăn thừa giắt ở kẽ răng mà không làm thưa răng hay tổn thương nhú nướu.</p>

<h2>3. Vệ sinh lưỡi mỗi ngày</h2>
<p>Mặt lưng của lưỡi là nơi tích tụ hàng triệu vi khuẩn và cặn thức ăn phân hủy – nguyên nhân hàng đầu gây hôi miệng dù bạn đã đánh răng sạch. Hãy sử dụng dụng cụ cạo lưỡi chuyên dụng để làm sạch bề mặt lưỡi nhẹ nhàng từ trong ra ngoài mỗi sáng.</p>

<h2>4. Sử dụng kem đánh răng có nồng độ Fluoride thích hợp</h2>
<p>Fluoride giúp tái khoáng hóa men răng và ngăn ngừa sự phát triển của vi khuẩn sâu răng. Người lớn nên lựa chọn các loại kem đánh răng có nồng độ Fluoride từ 1.000 đến 1.450 ppm.</p>

<h2>5. Xây dựng chế độ dinh dưỡng bảo vệ men răng</h2>
<ul>
    <li>Hạn chế tối đa các loại đồ uống có ga, nước ngọt chứa nhiều acid photphoric làm mòn men răng.</li>
    <li>Không dùng răng để cắn xé các vật cứng như nắp chai, hạt cứng, đá lạnh.</li>
    <li>Tăng cường thực phẩm giàu canxi, vitamin D (sữa, phô mai, rau xanh đậm) và uống đủ 2 lít nước lọc mỗi ngày.</li>
</ul>

<h2>6. Khám định kỳ và lấy vôi răng 6 tháng/lần</h2>
<p>Vôi răng (cao răng) là mảng bám vôi hóa cứng đầu mà việc chải răng tại nhà không thể loại bỏ được. Lấy vôi răng định kỳ tại nha khoa giúp ngăn chặn nguy cơ viêm nướu, tụt lợi và viêm nha chu dẫn đến lung lay răng.</p>',
        'https://res.cloudinary.com/demo/image/upload/v1/dental/cham-soc-rang-banner.jpg',
        NULL,
        'Published',
        0,
        145,
        DATEADD(DAY, -10, SYSDATETIME()),
        DATEADD(DAY, -10, SYSDATETIME())
    );
END;
GO

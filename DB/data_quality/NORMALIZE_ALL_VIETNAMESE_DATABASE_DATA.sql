-- =====================================================================================
-- DENTALCARE: TOÀN DIỆN CHUẨN HÓA TIẾNG VIỆT & XÓA BỎ LỖI FONT / MOJIBAKE
-- Target Databases: DentalCare_Integration_Dev, DentalCare_FullRelease_QA
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

PRINT '>>> BẮT ĐẦU CHUẨN HÓA DỮ LIỆU TIẾNG VIỆT CHO DATABASE: ' + DB_NAME();

-- -------------------------------------------------------------------------------------
-- 1. CHUẨN HÓA GÓI DỊCH VỤ COMBO (ServiceCombos)
-- -------------------------------------------------------------------------------------
PRINT '1. Chuẩn hóa ServiceCombos...';

UPDATE ServiceCombos
SET Title = N'Chăm Sóc & Vệ Sinh Nụ Cười Sáng Khỏe',
    Tagline = N'Làm sạch mảng bám, ngừa sâu răng và bảo vệ nướu định kỳ',
    Badge = N'Bán chạy nhất',
    Description = N'Gói chăm sóc răng miệng toàn diện giúp loại bỏ hoàn toàn cao răng tích tụ, đánh bóng men răng sáng bóng và bổ sung lớp bảo vệ Fluor chống sâu răng hiệu quả.',
    IncludedServices = N'["Khám tổng quát & Đánh giá sức khỏe răng nướu","Lấy cao răng siêu âm nhẹ nhàng không đau","Đánh bóng loại bỏ mảng bám ố vàng","Bôi Fluoride tái khoáng men răng ngừa sâu răng"]',
    Benefits = N'["Tiết kiệm 22% so với đặt từng dịch vụ lẻ","Bảo vệ men răng và nướu chắc khỏe suốt 6 tháng","Bác sĩ chuyên khoa trực tiếp thực hiện nhẹ nhàng"]'
WHERE ComboCode = 'COMBO_CARE';

UPDATE ServiceCombos
SET Title = N'Nụ Cười Trắng Sáng Laser Whitening',
    Tagline = N'Bật 2 - 3 tone màu răng ngay sau 60 phút với công nghệ Mỹ',
    Badge = N'Thẩm mỹ cao cấp',
    Description = N'Gói thẩm mỹ cao cấp kết hợp vệ sinh làm sạch sâu bề mặt và kích hoạt hoạt chất tẩy trắng an toàn bằng ánh sáng Laser lạnh chuẩn nha khoa quốc tế.',
    IncludedServices = N'["Khám đo độ sáng màu răng hiện tại theo bảng Vita","Lấy cao răng & Vệ sinh chuẩn bị bề mặt men răng","Cách ly bảo vệ nướu và môi chuyên dụng","Chiếu đèn Laser Whitening kích hoạt trắng sáng","Thoa gel chống ê buốt và hướng dẫn duy trì nụ cười đẹp"]',
    Benefits = N'["Hiệu quả tức thì, không làm mòn men răng","Tiết kiệm 300.000đ khi kết hợp lấy cao răng","Cam kết độ sáng bền lâu từ 1.5 - 2 năm"]'
WHERE ComboCode = 'COMBO_WHITENING';

UPDATE ServiceCombos
SET Title = N'Thăm Khám & Tư Vấn Chỉnh Nha Toàn Diện',
    Tagline = N'Khảo sát khớp cắn, lấy dấu 3D và lập phác đồ niềng răng chi tiết',
    Badge = N'Chuyên gia chỉnh nha',
    Description = N'Gói dành riêng cho khách hàng có nhu cầu niềng răng (mắc cài kim loại, sứ, hoặc khay trong suốt Invisalign). Bác sĩ chuyên khoa II chỉnh nha sẽ trực tiếp thăm khám và lên phác đồ cá nhân hóa.',
    IncludedServices = N'["Khám trực tiếp cùng Chuyên gia Chỉnh nha","Khảo sát tương quan xương hàm & đường cười thẩm mỹ","Lấy dấu răng mẫu hàm hoặc quét dấu kỹ thuật số 3D","Lập kế hoạch phác đồ điều trị và dự toán thời gian/chi phí"]',
    Benefits = N'["Tư vấn 1:1 chuyên sâu cùng Bác sĩ Chỉnh nha","Xem trước mô phỏng kết quả sau khi niềng răng","Miễn phí 100% chi phí khám nếu đăng ký gói niềng răng"]'
WHERE ComboCode = 'COMBO_ORTHO';

UPDATE ServiceCombos
SET Title = N'Thăm Khám & Khảo Sát Chuyên Sâu Cấy Ghép Implant',
    Tagline = N'Đánh giá chất lượng xương hàm và kế hoạch phục hình răng mất',
    Badge = N'Chuyên khoa Implant',
    Description = N'Gói chuyên sâu dành cho người mất 1 hoặc nhiều răng. Bác sĩ phẫu thuật sẽ đo độ dày và mật độ xương cung hàm, tư vấn chọn dòng trụ Implant tương thích sinh học cao nhất.',
    IncludedServices = N'["Thăm khám lâm sàng và tiền sử bệnh nền toàn thân","Đọc phim khảo sát xoang hàm & dây thần kinh răng dưới","Đo mật độ xương và chỉ định kỹ thuật ghép xương (nếu cần)","Lên phương án phục hình răng sứ trên trụ Implant"]',
    Benefits = N'["Bác sĩ chuyên gia trên 10 năm kinh nghiệm phẫu thuật Implant","Tư vấn dòng trụ chính hãng Châu Âu / Hàn Quốc bảo hành trọn đời","Khấu trừ toàn bộ phí khám khi thực hiện cấy trụ"]'
WHERE ComboCode = 'COMBO_IMPLANT';

-- -------------------------------------------------------------------------------------
-- 2. CHUẨN HÓA PHÒNG KHÁM VÀ THIẾT BỊ X-QUANG (Rooms, ImagingEquipments)
-- -------------------------------------------------------------------------------------
PRINT '2. Chuẩn hóa Rooms và ImagingEquipments...';

UPDATE Rooms SET RoomName = N'Phòng Khám Răng Tổng Quát 101' WHERE RoomCode = 'ROOM-101';
UPDATE Rooms SET RoomName = N'Phòng Phẫu Thuật & Implant 201' WHERE RoomCode = 'ROOM-201';
UPDATE Rooms SET RoomName = N'Phòng Chỉnh Nha & Thẩm Mỹ 301' WHERE RoomCode = 'ROOM-301';
UPDATE Rooms SET RoomName = N'Phòng Điều Trị Răng Tổng Quát 102' WHERE RoomCode = 'ROOM-102';
UPDATE Rooms SET RoomName = N'Phòng Cấy Ghép Implant Kỹ Thuật Cao 202' WHERE RoomCode = 'ROOM-202';
UPDATE Rooms SET RoomName = N'Phòng Thẩm Mỹ Nụ Cười & Laser 302' WHERE RoomCode = 'ROOM-302';
UPDATE Rooms SET RoomName = N'Phòng Khám Răng Trẻ Em 401' WHERE RoomCode = 'ROOM-401';
UPDATE Rooms SET RoomName = N'Phòng Điều Trị Nha Nhi 402' WHERE RoomCode = 'ROOM-402';
UPDATE Rooms SET RoomName = N'Phòng Chụp X-quang 01' WHERE RoomCode = 'XRAY-01' OR RoomType = 'DiagnosticImaging';

UPDATE ImagingEquipments
SET EquipmentName = N'Máy Chụp X-Quang Kỹ Thuật Số Đa Năng Carestream CS 8200 3D'
WHERE EquipmentCode = 'XRAY-MACH-01';

-- -------------------------------------------------------------------------------------
-- 3. CHUẨN HÓA DỊCH VỤ NHA KHOA (DepartmentServices)
-- -------------------------------------------------------------------------------------
PRINT '3. Chuẩn hóa DepartmentServices...';

UPDATE DepartmentServices
SET ServiceName = N'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu',
    Description = N'Chăm sóc răng miệng định kỳ và ngừa sâu răng cho trẻ.'
WHERE DepartmentServiceId = 10;

UPDATE DepartmentServices
SET ServiceName = N'Trám răng thẩm mỹ Inlay / Onlay sứ'
WHERE DepartmentServiceId = 13;

UPDATE DepartmentServices
SET ServiceName = N'Nhổ răng sữa lung lay nhẹ nhàng, không đau'
WHERE DepartmentServiceId = 27;

UPDATE DepartmentServices
SET ServiceName = N'Chụp X-quang Cận Chóp (Periapical / Intraoral)',
    Description = N'Chụp phim X-quang kỹ thuật số cận chóp răng kiểm tra sâu răng và cuống răng.'
WHERE DepartmentServiceId = 95;

UPDATE DepartmentServices
SET ServiceName = N'Chụp X-quang Toàn Cảnh Panorama (OPG)',
    Description = N'Chụp phim X-quang toàn cảnh hàm răng khảo sát tổng thể cung răng và xương hàm.'
WHERE DepartmentServiceId = 96;

UPDATE DepartmentServices
SET ServiceName = N'Chụp Phim Cắt Lớp Vi Tính 3D Cone Beam CT (CBCT)',
    Description = N'Chụp phim cắt lớp vi tính 3 chiều Cone Beam CT khảo sát cấu trúc giải phẫu 3D.'
WHERE DepartmentServiceId = 97;

-- BookingInstructions
UPDATE DepartmentServices
SET BookingInstructions = N'Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.'
WHERE DepartmentServiceId IN (1, 5, 10);

UPDATE DepartmentServices
SET BookingInstructions = N'Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.'
WHERE DepartmentServiceId IN (8, 9, 21, 22, 23, 24, 25);

UPDATE DepartmentServices
SET BookingInstructions = N'Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.'
WHERE DepartmentServiceId IN (26, 27, 28, 29, 30, 31);

UPDATE DepartmentServices
SET BookingInstructions = N'Vui lòng tháo trang sức kim loại vùng đầu cổ trước khi vào phòng chụp X-quang.'
WHERE DepartmentServiceId IN (95, 96, 97);

-- -------------------------------------------------------------------------------------
-- 4. CHUẨN HÓA DANH MỤC THUỐC & ĐƠN THUỐC (Medicines, Prescriptions, PrescriptionItems)
-- -------------------------------------------------------------------------------------
PRINT '4. Chuẩn hóa Medicines, Prescriptions và PrescriptionItems...';

UPDATE Medicines SET Unit = N'Viên' WHERE Unit LIKE '%Vi%n%';
UPDATE Medicines SET Unit = N'Chai' WHERE Unit LIKE '%Chai%';
UPDATE Medicines SET Unit = N'Ống' WHERE Unit LIKE '%ng%';
UPDATE Medicines SET Unit = N'Gói' WHERE Unit LIKE '%G%i%';

UPDATE Medicines SET DosageForm = N'Viên nén' WHERE DosageForm LIKE '%Vi%n n%n%' AND DosageForm NOT LIKE '%bao phim%';
UPDATE Medicines SET DosageForm = N'Viên nén bao phim' WHERE DosageForm LIKE '%bao phim%';
UPDATE Medicines SET DosageForm = N'Viên nang' WHERE DosageForm LIKE '%Vi%n nang%';
UPDATE Medicines SET DosageForm = N'Viên sủi' WHERE DosageForm LIKE '%Vi%n s%i%';
UPDATE Medicines SET DosageForm = N'Viên ngậm' WHERE DosageForm LIKE '%Vi%n ng%m%';
UPDATE Medicines SET DosageForm = N'Dung dịch súc miệng' WHERE DosageForm LIKE '%s%c mi%ng%';

UPDATE Medicines
SET MedicineName = N'Nước súc miệng Chlorhexidine 0.12%',
    DosageForm = N'Dung dịch súc miệng',
    Unit = N'Chai'
WHERE MedicineId = 8;

UPDATE Medicines SET UsageInstructions = N'Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.' WHERE MedicineId = 1;
UPDATE Medicines SET UsageInstructions = N'Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.' WHERE MedicineId = 2;
UPDATE Medicines SET UsageInstructions = N'Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.' WHERE MedicineId = 3;
UPDATE Medicines SET UsageInstructions = N'Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.' WHERE MedicineId = 4;
UPDATE Medicines SET UsageInstructions = N'Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.' WHERE MedicineId = 5;
UPDATE Medicines SET UsageInstructions = N'Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.' WHERE MedicineId = 6;
UPDATE Medicines SET UsageInstructions = N'Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.' WHERE MedicineId = 7;
UPDATE Medicines SET UsageInstructions = N'Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.' WHERE MedicineId = 8;

UPDATE PrescriptionItems SET Duration = N'5 ngày' WHERE Duration LIKE '%5%ng%';
UPDATE PrescriptionItems SET Duration = N'7 ngày' WHERE Duration LIKE '%7%ng%';
UPDATE PrescriptionItems SET Duration = N'3 ngày' WHERE Duration LIKE '%3%ng%';
UPDATE PrescriptionItems SET Duration = N'10 ngày' WHERE Duration LIKE '%10%ng%';

UPDATE PrescriptionItems SET Frequency = N'2 lần/ngày' WHERE Frequency LIKE '%2%l%n%';
UPDATE PrescriptionItems SET Frequency = N'3 lần/ngày' WHERE Frequency LIKE '%3%l%n%';
UPDATE PrescriptionItems SET Frequency = N'1 lần/ngày' WHERE Frequency LIKE '%1%l%n%';
UPDATE PrescriptionItems SET Frequency = N'Khi đau nhức' WHERE Frequency LIKE '%nh%c%' OR Frequency LIKE '%au%';

UPDATE PrescriptionItems SET Instructions = N'Kê đơn: 10 viên, uống sau ăn no' WHERE Instructions LIKE '%10%';
UPDATE PrescriptionItems SET Instructions = N'Kê đơn: 14 viên, uống sau ăn no' WHERE Instructions LIKE '%14%';
UPDATE PrescriptionItems SET Instructions = N'Kê đơn: 20 viên, uống sau ăn no' WHERE Instructions LIKE '%20%';
UPDATE PrescriptionItems SET Instructions = N'Kê đơn: 1 chai, súc miệng 30 giây' WHERE Instructions LIKE '%chai%' OR Instructions LIKE '%súc%' OR Instructions LIKE '%sÃºc%';

UPDATE Prescriptions SET GeneralInstructions = N'Uống thuốc đúng giờ theo đơn bác sĩ, uống sau khi ăn no và tái khám theo lịch hẹn.' WHERE GeneralInstructions IS NOT NULL;

-- -------------------------------------------------------------------------------------
-- 5. CHUẨN HÓA BÀI VIẾT BLOG (BlogPosts)
-- -------------------------------------------------------------------------------------
PRINT '5. Chuẩn hóa BlogPosts...';

UPDATE BlogPosts
SET Title = N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
    Summary = N'Tủy răng được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám.',
    Content = N'<p class="lead"><strong>Tủy răng</strong> được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm.</p><p>Tại DentalCare, quy trình điều trị tủy được thực hiện bằng hệ thống máy định vị chóp hiện đại và trâm xoay NiTi linh hoạt, giúp làm sạch ống tủy nhanh chóng, triệt để và hoàn toàn êm ái.</p>'
WHERE BlogPostId = 1;

UPDATE BlogPosts
SET Title = N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
    Summary = N'Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục.',
    Content = N'<p class="lead"><strong>Răng khôn (răng số 8)</strong> mọc lệch, mọc ngầm là nỗi ám ảnh của nhiều người bởi những cơn đau nhức kéo dài, sưng má và khó há miệng.</p><p>Công nghệ nhổ răng sóng siêu âm Piezotome sử dụng bước sóng siêu âm chọn lọc để bóc tách mô nướu nhẹ nhàng mà không làm tổn thương xương ổ răng và dây thần kinh, giúp lành thương nhanh chóng.</p>'
WHERE BlogPostId = 2;

UPDATE BlogPosts
SET Title = N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
    Summary = N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật.',
    Content = N'<p class="lead"><strong>Cấy ghép Implant</strong> là giải pháp phục hình răng đã mất tiên tiến nhất hiện nay, mang lại độ bền chắc trọn đời và khôi phục 100% lực ăn nhai như răng thật.</p><p>DentalCare sử dụng các dòng trụ Implant hàng đầu thế giới từ Thụy Sĩ, Mỹ và Hàn Quốc với tỷ lệ tích hợp xương thành công lên tới 99.8%.</p>'
WHERE BlogPostId = 3;

UPDATE BlogPosts
SET Title = N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
    Summary = N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất.',
    Content = N'<p class="lead">Sở hữu một hàm răng đều đặn cùng nụ cười rạng rỡ là mong muốn của tất cả mọi người. Tuy nhiên, lựa chọn giữa niềng răng mắc cài hay khay trong suốt Invisalign luôn là thắc mắc lớn.</p><p>Hãy cùng các chuyên gia chỉnh nha tại DentalCare so sánh chi tiết ưu nhược điểm của từng phương pháp để tìm ra giải pháp tối ưu nhất cho bạn.</p>'
WHERE BlogPostId = 4;

UPDATE BlogPosts
SET Title = N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
    Summary = N'Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước.',
    Content = N'<p class="lead">Một nụ cười khỏe đẹp bắt nguồn từ những thói quen vệ sinh răng miệng đúng cách mỗi ngày.</p><p>Đừng bỏ qua 6 bước chăm sóc chuẩn y khoa được khuyến cáo bởi Hiệp hội Nha khoa Quốc tế để duy trì hàm răng sáng khỏe suốt đời.</p>'
WHERE BlogPostId = 5;

UPDATE BlogPosts
SET Title = N'Nha Khoa DentalCare chính thức khai trương Trung Tâm Chẩn Đoán Hình Ảnh & Laser 3D',
    Summary = N'Hệ thống máy chụp CT ConeBeam 3D thế hệ mới nhất cùng công nghệ Laser hiện đại đã chính thức đi vào hoạt động nhằm nâng cao chất lượng chẩn đoán và điều trị.',
    Content = N'<p class="lead">Nhằm không ngừng nâng cao chất lượng khám chữa bệnh, DentalCare tự hào ra mắt <strong>Trung Tâm Chẩn Đoán Hình Ảnh Kỹ Thuật Số & Laser 3D</strong> chuyên dụng.</p><p>Hệ thống cho phép khảo sát cấu trúc giải phẫu 3D chi tiết với liều lượng tia xạ tối thiểu, đảm bảo an toàn tuyệt đối cho khách hàng.</p>'
WHERE BlogPostId = 6;

UPDATE BlogPosts
SET Title = N'Cẩm nang chăm sóc răng miệng sau khi nhổ răng khôn',
    Slug = 'cam-nang-cham-soc-rang-mieng-sau-khi-nho-rang-khon',
    Summary = N'Những lưu ý quan trọng cần biết trong 48 giờ đầu sau phẫu thuật nhổ răng khôn.',
    Content = N'<p>Sau khi nhổ răng khôn, bệnh nhân cần cắn chặt bông gòn trong 45 phút đầu, chườm lạnh ngoài má trong ngày đầu tiên và không súc miệng bằng nước muối đậm đặc...</p>'
WHERE BlogPostId = 12;

-- -------------------------------------------------------------------------------------
-- 6. CHUẨN HÓA BẰNG CẤP VÀ TIỂU SỬ BÁC SĨ (DentistProfiles)
-- -------------------------------------------------------------------------------------
PRINT '6. Chuẩn hóa DentistProfiles...';

UPDATE DentistProfiles
SET Qualification = N'Thạc sĩ Răng Hàm Mặt, ĐHYD TP.HCM',
    Biography = N'Bác sĩ chuyên khoa Phục hình và Cấy ghép Implant chuyên sâu với hơn 8 năm kinh nghiệm lâm sàng.'
WHERE UserId = 3;

UPDATE DentistProfiles
SET Qualification = N'Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM',
    Biography = N'Chuyên gia Cấy ghép Implant và Phẫu thuật hàm mặt kỹ thuật cao với hơn 15 năm kinh nghiệm lâm sàng.'
WHERE UserId = 4;

UPDATE DentistProfiles
SET Qualification = N'Bác sĩ CKII Răng Hàm Mặt',
    Biography = N'Chuyên gia Điều trị tủy và Phục hình nha khoa tổng quát chuyên sâu với hơn 18 năm kinh nghiệm.'
WHERE UserId = 10;

UPDATE DentistProfiles
SET Qualification = N'Thạc sĩ Chỉnh nha Thẩm mỹ',
    Biography = N'Chuyên gia Niềng răng khay trong suốt Invisalign và Thẩm mỹ nụ cười toàn diện.'
WHERE UserId = 11;

UPDATE DentistProfiles
SET Qualification = N'Thạc sĩ Nha khoa Trẻ em',
    Biography = N'Bác sĩ chuyên khoa Nha khoa Trẻ em và Tiền chỉnh nha can thiệp sớm cho thanh thiếu niên.'
WHERE UserId = 12;

UPDATE DentistProfiles
SET Qualification = N'Chuyên gia Phục hình Thẩm mỹ DSD',
    Biography = N'Thiết kế nụ cười chuẩn tỷ lệ vàng và dán sứ Veneer thẩm mỹ cao cấp.'
WHERE UserId = 46;

UPDATE DentistProfiles
SET Qualification = N'Bác sĩ Nha khoa Bảo tồn',
    Biography = N'Tận tụy, chuyên sâu lấy cao răng siêu âm và điều trị bảo tồn răng thật.'
WHERE UserId = 47;

-- -------------------------------------------------------------------------------------
-- 7. CHUẨN HÓA HÓA ĐƠN & LỊCH LÀM VIỆC (InvoiceItems, DentistWorkSchedules)
-- -------------------------------------------------------------------------------------
PRINT '7. Chuẩn hóa InvoiceItems và DentistWorkSchedules...';

UPDATE InvoiceItems
SET Description = N'Khám tổng quát & tư vấn chuyên khoa răng miệng'
WHERE Description LIKE '%Kh%m%' AND Description LIKE '%t%ng qu%t%';

UPDATE InvoiceItems
SET Description = N'Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron'
WHERE Description LIKE '%L%y cao%' OR Description LIKE '%cao r%ng%';

UPDATE DentistWorkSchedules
SET Note = N'Ca trực chính hôm nay'
WHERE Note LIKE '%Ca tr%c ch%nh%' OR Note LIKE '%Ca tr%c%';

-- -------------------------------------------------------------------------------------
-- 8. CHUẨN HÓA CÁC CA LỊCH HẸN & BỆNH ÁN CŨ (Appointments, ClinicalEntries, Feedback)
-- -------------------------------------------------------------------------------------
PRINT '8. Chuẩn hóa Appointments, ClinicalEntries, VisitFeedback, AppointmentStatusHistories...';

UPDATE Appointments
SET ReasonForVisit = N'Khám răng định kỳ & cạo vôi răng (Demo Bác sĩ bấm Vào Khám)'
WHERE ReasonForVisit LIKE '%Kh%m r%ng %& c%o v%i%';

UPDATE Appointments
SET ReasonForVisit = N'Đau nhức ê buốt vùng răng hàm dưới R46 khi nhai (Demo Đang Khám & Sơ Đồ Răng)'
WHERE ReasonForVisit LIKE '%au nh%c %bu%t%';

UPDATE Appointments
SET ReasonForVisit = N'Tái khám kiểm tra kênh tủy răng 36 & hoàn tất trám kết thúc'
WHERE ReasonForVisit LIKE '%T%i kh%m ki%m tra%';

UPDATE Appointments
SET ReasonForVisit = N'Tư vấn trị liệu bác sĩ chuyên khoa'
WHERE ReasonForVisit LIKE '%T%v%n tr% li%u%';

UPDATE AppointmentStatusHistories
SET Note = N'Tự động chuyển tiếp sang hàng đợi Lễ tân tiếp nhận theo quy trình'
WHERE Note LIKE '%chuy%n ti%p sang h%ng %i%';

UPDATE AppointmentStatusHistories
SET Note = N'Lễ tân xác nhận và tiếp nhận lịch hẹn thành công'
WHERE Note LIKE '%x%c nh%n v% ti%p nh%n%';

UPDATE ClinicalEntries
SET ClinicalFindings = N'Lỗ sâu mặt nhai răng 36 kích thước 2x2mm',
    ClinicalNotes = N'Làm sạch lỗ sâu, trám composite quang trùng hợp',
    Diagnosis = N'Sâu ngà sâu răng 36, tủy bình thường',
    Symptoms = N'Ê buốt nhẹ khi ăn đồ ngọt'
WHERE ClinicalFindings LIKE '%s%u m%t nhai%';

UPDATE TreatmentSessions
SET ClinicalNote = N'Khám và kê đơn thuốc ban đầu',
    TreatmentResult = N'Thực hiện thủ thuật thành công'
WHERE ClinicalNote LIKE '%Kh%m v% k% %n thu%c%';

UPDATE VisitFeedback
SET Comment = N'Bác sĩ Hùng và Lễ tân Mai rất nhẹ nhàng, chu đáo, giải thích cặn kẽ quy trình.'
WHERE Comment LIKE '%B%c s% H%ng v% L% t%n%';

UPDATE VisitFeedback
SET Comment = N'Dịch vụ tuyệt vời, phòng khám sạch sẽ hiện đại, không phải chờ lâu.'
WHERE Comment LIKE '%D%ch v% tuy%t v%i%';

PRINT '=====================================================================================';
PRINT '>>> HOÀN TẤT CHUẨN HÓA TIẾNG VIỆT TOÀN DIỆN CHO DATABASE: ' + DB_NAME();
PRINT '=====================================================================================';
GO

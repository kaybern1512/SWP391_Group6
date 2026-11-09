const fs = require('fs');
const path = require('path');

const dumpDir = path.join(__dirname, 'data_dump');

function readJson(fileName) {
    const raw = fs.readFileSync(path.join(dumpDir, fileName), 'utf8');
    return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

// Mojibake regex detecting UTF-8 bytes misinterpreted as Windows-1252
const mojibakeRegex = /[\u00C0-\u00FF][\u0080-\u00BF]|Ã[¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿]|Ä[‘]|Æ[°¡]|á[º»¼½¾¿]|â[€“”•™]|Â[°\s]|Ã\s|\uFFFD/;

// Exact map of known mojibake words to clean Vietnamese
const mojibakeMap = {
    "ViÃªn": "Viên",
    "ViÃªn nang": "Viên nang",
    "ViÃªn nÃ©n": "Viên nén",
    "ViÃªn nÃ©n bao phim": "Viên nén bao phim",
    "ViÃªn sá»§i": "Viên sủi",
    "ViÃªn ngáº\u00adï¿½m": "Viên ngậm",
    "ViÃªn ngáº\u00adm": "Viên ngậm",
    "ViÃªn ngáº­m": "Viên ngậm",
    "Dung dá»‹ch sÃºc miá»‡ng": "Dung dịch súc miệng",
    "NÆ°á»›c sÃºc miá»‡ng Chlorhexidine 0.12%": "Nước súc miệng Chlorhexidine 0.12%",
    "Uá»‘ng sau Äƒn. NgÆ°á»\u009dï¿½i lá»›n 1 viÃªn x 3 láº§n/ngÃ y, cÃ¡ch 8 giá»\u009dï¿½.": "Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.",
    "Uá»‘ng ngay Ä‘áº§u bá»¯a Äƒn Ä‘á»ƒ giáº£m khÃ³ chá»‹u dáº¡ dÃ y. 1 viÃªn x 2 láº§n/ngÃ y.": "Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.",
    "Uá»‘ng khi Ä‘au nhá»©c hoáº·c sá»‘t > 38.5Â°C. Má»—i láº§n 1-2 viÃªn, cÃ¡ch nhau tá»‘i thiá»ƒu 4-6 giá»\u009dï¿½.": "Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.",
    "HÃ²a tan 1 viÃªn vÃ o 200ml nÆ°á»›c áº¥m khi Ä‘au dá»¯ dá»™i sau tiá»ƒu pháº«u. Tá»‘i Ä‘a 4 viÃªn/ngÃ y.": "Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.",
    "KhÃ¡ng viÃªm giáº£m Ä‘au sau nhá»• rÄƒng hoáº·c Ä‘iá»\u009dï¿½u trá»‹ tá»§y. Uá»‘ng sau Äƒn no.": "Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.",
    "Ä\u0090áº·c trá»‹ nhiá»…m trÃ¹ng rÄƒng miá»‡ng, viÃªm nha chu, Ã¡p xe nÆ°á»›u. Uá»‘ng trong bá»¯a Äƒn.": "Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.",
    "Ngáº\u00adï¿½m dÆ°á»›i lÆ°á»¡i 2 viÃªn x 2-3 láº§n/ngÃ y Ä‘á»ƒ chá»‘ng phÃ¹ ná»\u009dï¿½ sau pháº«u thuáº\u00adï¿½t hÃ m máº·t.": "Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.",
    "SÃºc miá»‡ng 15ml trong 30 giÃ¢y sau khi cháº£i rÄƒng, 2 láº§n/ngÃ y. KhÃ´ng Ä‘Æ°á»£c nuá»‘t.": "Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.",
    "Vui lÃ²ng Ä‘áº¿n trÆ°á»›c 10 phÃºt Ä‘á»ƒ hoÃ n táº¥t thá»§ tá»¥c tiáº¿p Ä‘Ã³n vÃ  chá»¥p phim cÆ¡ báº£n náº¿u cáº§n.": "Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.",
    "Thá»§ thuáº\u00adï¿½t chuyÃªn sÃ¢u cáº§n khÃ¡m Ä‘Ã¡nh giÃ¡ trÆ°á»›c. Vui lÃ²ng Äƒn nháº¹ trÆ°á»›c háº¹n 1 tiáº¿ng, khÃ´ng uá»‘ng cÃ  phÃª hoáº·c cháº¥t kÃ­ch thÃ­ch.": "Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.",
    "Thá»§ thuáº­t chuyÃªn sÃ¢u cáº§n khÃ¡m Ä‘Ã¡nh giÃ¡ trÆ°á»›c. Vui lÃ²ng Äƒn nháº¹ trÆ°á»›c háº¹n 1 tiáº¿ng, khÃ´ng uá»‘ng cÃ  phÃª hoáº·c cháº¥t kÃ­ch thÃ­ch.": "Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.",
    "Dá»‹ch vá»¥ nha khoa tráº» em nháº¹ nhÃ ng. Ba máº¹ vui lÃ²ng Ä‘á»™ng viÃªn tÃ¢m lÃ½ bÃ© trÆ°á»›c khi tá»›i phÃ²ng khÃ¡m.": "Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám."
};

const issues = [];

function addIssue(table, pkValue, column, original, proposed, errorType, severity, confidence, action, explanation) {
    issues.push({
        table,
        primaryKey: pkValue,
        column,
        originalValue: original,
        proposedValue: proposed,
        errorType,
        severity,
        confidence,
        action,
        explanation
    });
}

// 1. Audit DepartmentServices
console.log('Auditing DepartmentServices...');
const services = readJson('DepartmentServices.json');
services.forEach(s => {
    const id = s.DepartmentServiceId;
    
    // Check BookingInstructions mojibake
    if (s.BookingInstructions && s.BookingInstructions !== "null") {
        if (mojibakeRegex.test(s.BookingInstructions) || mojibakeMap[s.BookingInstructions]) {
            let fixed = mojibakeMap[s.BookingInstructions];
            if (!fixed) {
                if (s.BookingInstructions.includes('Vui lÃ²ng')) {
                    fixed = "Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.";
                } else if (s.BookingInstructions.includes('chuyÃªn sÃ¢u')) {
                    fixed = "Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.";
                } else if (s.BookingInstructions.includes('nha khoa tráº» em')) {
                    fixed = "Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.";
                }
            }
            if (fixed) {
                addIssue('DepartmentServices', id, 'BookingInstructions', s.BookingInstructions, fixed,
                    'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Lỗi hiển thị ký tự mã hóa UTF-8 bị lưu dưới dạng ANSI/CP1252.');
            }
        }
    }

    // Check ServiceName: Flour typo
    if (s.ServiceName && s.ServiceName.includes('Flour')) {
        const fixed = s.ServiceName.replace(/Flour/g, 'Fluor');
        addIssue('DepartmentServices', id, 'ServiceName', s.ServiceName, fixed,
            'Thuật ngữ / Chính tả', 'High', 'High', 'AUTO_SAFE', 'Từ "Flour" (bột mì) là lỗi chính tả tiếng Anh/tiếng Việt của khoáng chất nha khoa Fluor / Fluoride.');
    }

    // Check ServiceName: Hàn răng vs Trám răng
    if (s.ServiceName && s.ServiceName.includes('Hàn răng')) {
        const fixed = s.ServiceName.replace(/Hàn răng/g, 'Trám răng');
        addIssue('DepartmentServices', id, 'ServiceName', s.ServiceName, fixed,
            'Thuật ngữ nha khoa', 'Medium', 'Medium', 'NEEDS_REVIEW', 'Hệ thống dùng lẫn lộn giữa "Trám răng" và "Hàn răng". Đề xuất thống nhất chuẩn hóa sang "Trám răng" theo danh mục Bộ Y Tế.');
    }

    // Check Service 27: vô cảm nhẹ nhàng
    if (s.ServiceName && s.ServiceName.includes('vô cảm nhẹ nhàng')) {
        const fixed = s.ServiceName.replace('vô cảm nhẹ nhàng', 'bôi tê nhẹ nhàng, không đau');
        addIssue('DepartmentServices', id, 'ServiceName', s.ServiceName, fixed,
            'Văn phong / Thuật ngữ AI', 'Medium', 'Medium', 'NEEDS_REVIEW', 'Cụm từ "vô cảm nhẹ nhàng" dịch từ "anesthesia/painless" gây hiểu nhầm về tâm lý bệnh nhân.');
    }

    // Check Description: không flour
    if (s.Description && s.Description.includes('không flour')) {
        const fixed = s.Description.replace(/không flour/g, 'không fluor');
        addIssue('DepartmentServices', id, 'Description', s.Description, fixed,
            'Thuật ngữ / Chính tả', 'Medium', 'High', 'AUTO_SAFE', 'Sai chính tả tên khoáng chất Fluor.');
    }
});

// 2. Audit Medicines
console.log('Auditing Medicines...');
const medicines = readJson('Medicines.json');
medicines.forEach(m => {
    const id = m.MedicineId;

    // Unit
    if (m.Unit === 'ViÃªn') {
        addIssue('Medicines', id, 'Unit', m.Unit, 'Viên',
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Lỗi ký tự đơn vị tính bị biến dạng sang CP1252.');
    }

    // DosageForm
    if (m.DosageForm && mojibakeRegex.test(m.DosageForm)) {
        let fixed = m.DosageForm;
        if (fixed === 'ViÃªn nang') fixed = 'Viên nang';
        else if (fixed === 'ViÃªn nÃ©n bao phim') fixed = 'Viên nén bao phim';
        else if (fixed === 'ViÃªn nÃ©n') fixed = 'Viên nén';
        else if (fixed === 'ViÃªn sá»§i') fixed = 'Viên sủi';
        else if (fixed.includes('ViÃªn ngáº')) fixed = 'Viên ngậm';
        else if (fixed.includes('Dung dá»‹ch')) fixed = 'Dung dịch súc miệng';

        addIssue('Medicines', id, 'DosageForm', m.DosageForm, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Dạng bào chế bị lỗi hiển thị mã hóa.');
    }

    // MedicineName
    if (m.MedicineName && mojibakeRegex.test(m.MedicineName)) {
        let fixed = m.MedicineName;
        if (fixed.includes('NÆ°á»›c sÃºc miá»‡ng')) fixed = fixed.replace('NÆ°á»›c sÃºc miá»‡ng', 'Nước súc miệng');
        addIssue('Medicines', id, 'MedicineName', m.MedicineName, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Tên thuốc bị lỗi mã hóa tiếng Việt.');
    }

    // UsageInstructions
    if (m.UsageInstructions && mojibakeRegex.test(m.UsageInstructions)) {
        let fixed = m.UsageInstructions;
        if (id === 1) fixed = "Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.";
        else if (id === 2) fixed = "Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.";
        else if (id === 3) fixed = "Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.";
        else if (id === 4) fixed = "Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.";
        else if (id === 5) fixed = "Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.";
        else if (id === 6) fixed = "Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.";
        else if (id === 7) fixed = "Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.";
        else if (id === 8) fixed = "Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.";

        addIssue('Medicines', id, 'UsageInstructions', m.UsageInstructions, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Hướng dẫn dùng thuốc chuẩn y khoa bị lỗi mã hóa.');
    }
});

// 3. Audit BlogPosts
console.log('Auditing BlogPosts...');
const blogPosts = readJson('BlogPosts.json');
const blogCleanData = {
    6: {
        title: "Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa",
        summary: "Tủy răng được ví như \"trái tim\" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám."
    },
    7: {
        title: "Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome",
        summary: "Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục."
    },
    8: {
        title: "Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay",
        summary: "Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật."
    },
    9: {
        title: "Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?",
        summary: "Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất."
    },
    10: {
        title: "Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp",
        summary: "Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước."
    },
    11: {
        title: "Nha Khoa DentalCare chính thức khai trương Trung Tâm Chẩn Đoán Hình Ảnh & Laser 3D",
        summary: "Hệ thống máy chụp CT ConeBeam 3D thế hệ mới nhất cùng công nghệ Laser hiện đại đã chính thức đi vào hoạt động nhằm nâng cao chất lượng chẩn đoán và điều trị."
    },
    12: {
        title: "Thông báo lịch làm việc chuyên khoa và chương trình chăm sóc định kỳ quý 4/2026",
        summary: "DentalCare thông báo lịch làm việc liên tục các ngày trong tuần từ 08:00 đến 20:00 và chương trình thăm khám răng định kỳ miễn phí cho cư dân."
    }
};

blogPosts.forEach(p => {
    const id = p.BlogPostId;
    if (mojibakeRegex.test(p.Title) || (blogCleanData[id] && blogCleanData[id].title)) {
        const cleanTitle = (blogCleanData[id] && blogCleanData[id].title) || p.Title;
        addIssue('BlogPosts', id, 'Title', p.Title, cleanTitle,
            'Unicode / Chính tả', 'High', 'High', 'AUTO_SAFE', 'Tiêu đề bài viết Blog bị lỗi mã hóa hoặc chính tả (Chuẩn đoán -> Chẩn đoán).');
    }
    if (mojibakeRegex.test(p.Summary) || (blogCleanData[id] && blogCleanData[id].summary)) {
        const cleanSummary = (blogCleanData[id] && blogCleanData[id].summary) || p.Summary;
        addIssue('BlogPosts', id, 'Summary', p.Summary, cleanSummary,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Tóm tắt bài viết Blog bị lỗi mã hóa font tiếng Việt.');
    }
    if (p.Content && mojibakeRegex.test(p.Content)) {
        addIssue('BlogPosts', id, 'Content', p.Content.substring(0, 100) + '...', 'Khôi phục nội dung HTML chuẩn UTF-8 từ DentalClinic_Full_Database_And_Data.sql',
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Toàn bộ nội dung bài viết chuyên môn bị lỗi hiển thị tiếng Việt.');
    }
});

// 4. Audit DentistProfiles
console.log('Auditing DentistProfiles...');
const dentists = readJson('DentistProfiles.json');
dentists.forEach(d => {
    const id = d.UserId;
    if (d.Qualification && mojibakeRegex.test(d.Qualification)) {
        let fixed = d.Qualification;
        if (id === 4) fixed = "Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM";
        else if (id === 10) fixed = "Bác sĩ CKII Răng Hàm Mặt";
        else if (id === 11) fixed = "Thạc sĩ Chỉnh nha Thẩm mỹ";
        else if (id === 12) fixed = "Thạc sĩ Nha khoa Trẻ em";

        addIssue('DentistProfiles', id, 'Qualification', d.Qualification, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Bằng cấp bác sĩ hiển thị lỗi ký tự tiếng Việt.');
    }
    if (d.Biography && mojibakeRegex.test(d.Biography)) {
        let fixed = d.Biography;
        if (id === 4) fixed = "Chuyên gia Cấy ghép Implant và Phẫu thuật hàm mặt kỹ thuật cao với hơn 15 năm kinh nghiệm lâm sàng.";
        else if (id === 10) fixed = "Chuyên gia Điều trị tủy và Phục hình nha khoa tổng quát chuyên sâu với hơn 18 năm kinh nghiệm.";
        else if (id === 11) fixed = "Chuyên gia Niềng răng khay trong suốt Invisalign và Thẩm mỹ nụ cười toàn diện.";
        else if (id === 12) fixed = "Bác sĩ chuyên khoa Nha khoa Trẻ em và Tiền chỉnh nha can thiệp sớm cho thanh thiếu niên.";

        addIssue('DentistProfiles', id, 'Biography', d.Biography, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Tiểu sử bác sĩ chuyên khoa bị lỗi font.');
    }
});

// 5. Audit Rooms
console.log('Auditing Rooms...');
const rooms = readJson('Rooms.json');
rooms.forEach(r => {
    const id = r.RoomId;
    if (r.RoomName && r.RoomName.includes('Vui Nhộn')) {
        addIssue('Rooms', id, 'RoomName', r.RoomName, 'Phòng Khám Răng Trẻ Em 401',
            'Văn phong / Đặt tên', 'Low', 'Medium', 'NEEDS_REVIEW', 'Tên phòng chứa từ cảm tính "Vui Nhộn" do seed tự đặt, đề xuất chuẩn hóa y tế.');
    }
    if (r.RoomName && r.RoomName.includes('Hoàng Gia')) {
        addIssue('Rooms', id, 'RoomName', r.RoomName, 'Phòng Điều Trị Nha Nhi 402',
            'Văn phong / Đặt tên', 'Low', 'Medium', 'NEEDS_REVIEW', 'Tên phòng chứa từ khoa trương "Hoàng Gia" do seed tự đặt.');
    }
});

// 6. Audit InvoiceItems
console.log('Auditing InvoiceItems...');
const invoiceItems = readJson('InvoiceItems.json');
invoiceItems.forEach(i => {
    const id = i.InvoiceItemId;
    if (i.Description && mojibakeRegex.test(i.Description)) {
        let fixed = i.Description;
        if (fixed.includes('KhÃ¡m tá»•ng quÃ¡t')) fixed = "Khám tổng quát & tư vấn chuyên khoa răng miệng";
        else if (fixed.includes('Láº¥y cao rÄƒng')) fixed = "Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron";

        addIssue('InvoiceItems', id, 'Description', i.Description, fixed,
            'Unicode/Mojibake', 'High', 'High', 'AUTO_SAFE', 'Mô tả dịch vụ trong hóa đơn viện phí bị lỗi font mã hóa.');
    }
});

// 7. Audit Group 4 (Clinical, Feedback, Appointments, Referrals, Follow-ups) - AUDIT ONLY
console.log('Auditing Group 4 (Audit only)...');
const appointments = readJson('Appointments.json');
appointments.forEach(a => {
    const id = a.AppointmentId;
    if (a.ReasonForVisit) {
        if (a.ReasonForVisit.includes('flour') || a.ReasonForVisit.includes('Flour')) {
            const fixed = a.ReasonForVisit.replace(/flour/gi, 'fluor');
            addIssue('Appointments', id, 'ReasonForVisit', a.ReasonForVisit, fixed,
                'Chính tả', 'Low', 'High', 'DO_NOT_CHANGE', 'Dữ liệu ca khám thực tế của bệnh nhân chứa từ "flour" sai chính tả. Theo quy tắc Phase 4: Không tự sửa dữ liệu người dùng/lịch sử.');
        }
        if (mojibakeRegex.test(a.ReasonForVisit)) {
            addIssue('Appointments', id, 'ReasonForVisit', a.ReasonForVisit, 'Khôi phục tiếng Việt UTF-8',
                'Unicode/Mojibake', 'Medium', 'High', 'DO_NOT_CHANGE', 'Dữ liệu lịch hẹn cũ chứa chuỗi mojibake từ đợt seed trước. Ghi nhận audit, không sửa trực tiếp dữ liệu người dùng.');
        }
    }
});

const clinicalEntries = readJson('ClinicalEntries.json');
clinicalEntries.forEach(c => {
    const id = c.ClinicalEntryId;
    ['Symptoms', 'ClinicalFindings', 'Diagnosis', 'DentalConditions', 'ClinicalNotes'].forEach(col => {
        const val = c[col];
        if (val && mojibakeRegex.test(val)) {
            addIssue('ClinicalEntries', id, col, val.substring(0, 80) + '...', 'Khôi phục tiếng Việt UTF-8',
                'Unicode/Mojibake', 'High', 'High', 'DO_NOT_CHANGE', 'Bệnh án y khoa chứa chuỗi mã hóa lỗi. Giữ nguyên theo quy tắc bảo toàn hồ sơ bệnh án y tế.');
        }
    });
});

const feedback = readJson('VisitFeedback.json');
feedback.forEach(f => {
    const id = f.FeedbackId;
    if (f.Comment && mojibakeRegex.test(f.Comment)) {
        addIssue('VisitFeedback', id, 'Comment', f.Comment.substring(0, 80) + '...', 'Khôi phục tiếng Việt UTF-8',
            'Unicode/Mojibake', 'Medium', 'High', 'DO_NOT_CHANGE', 'Đánh giá bệnh nhân chứa chuỗi mã hóa lỗi. Giữ nguyên không tự sửa nội dung phản hồi.');
    }
});

console.log(`\nTOTAL ISSUES DETECTED: ${issues.length}`);

// Group by Action
const byAction = {};
issues.forEach(i => {
    byAction[i.action] = (byAction[i.action] || 0) + 1;
});
console.log('Issues by Action:', byAction);

// Write CSV
const csvHeader = 'Table,PrimaryKey,Column,OriginalValue,ProposedValue,ErrorType,Severity,Confidence,Action,Explanation\n';
const csvRows = issues.map(i => {
    const cleanOrig = '"' + (i.originalValue || '').replace(/"/g, '""').replace(/\r?\n/g, ' ') + '"';
    const cleanProp = '"' + (i.proposedValue || '').replace(/"/g, '""').replace(/\r?\n/g, ' ') + '"';
    const cleanExpl = '"' + (i.explanation || '').replace(/"/g, '""') + '"';
    return `${i.table},${i.primaryKey},${i.column},${cleanOrig},${cleanProp},${i.errorType},${i.severity},${i.confidence},${i.action},${cleanExpl}`;
}).join('\n');

const csvPath = path.join(__dirname, 'VIETNAMESE_DATA_ISSUES.csv');
fs.writeFileSync(csvPath, csvHeader + csvRows, 'utf8');
console.log('Saved CSV to:', csvPath);

// Save JSON summary
fs.writeFileSync(path.join(__dirname, 'audit_summary.json'), JSON.stringify(issues, null, 2), 'utf8');

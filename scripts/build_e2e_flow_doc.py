# -*- coding: utf-8 -*-
"""
Generate comprehensive Word Document for Dentist Workspace & X-ray V3 16-Stage Clinical Journey.
DentalCare V3 System.
"""
import os
import shutil
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

DOC_PATH = r"D:\SWP_2\SWP391_Group6\LUONG_CHAY_DENTIST_WORKSPACE_VA_XRAY_V3.docx"
ARTIFACT_PATH = r"C:\Users\LENOVO\.gemini\antigravity\brain\65bd4ac4-419f-49be-8642-5bac7220a50a\LUONG_CHAY_DENTIST_WORKSPACE_VA_XRAY_V3.docx"
IMG_DIR = r"D:\SWP_2\SWP391_Group6\docs\dentist_workspace_v3_evidence"

doc = Document()

# Page Margins (0.8 inch)
for s in doc.sections:
    s.top_margin = Inches(0.8)
    s.bottom_margin = Inches(0.8)
    s.left_margin = Inches(0.8)
    s.right_margin = Inches(0.8)

PRIMARY_COLOR = RGBColor(13, 110, 253)   # Bootstrap Blue #0d6efd
DARK_COLOR = RGBColor(33, 37, 41)        # Slate Dark #212529
MUTED_COLOR = RGBColor(108, 117, 125)    # Gray #6c757d
SUCCESS_COLOR = RGBColor(25, 135, 84)    # Green #198754

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    tcPr.append(parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>'))

def set_cell_margins(cell, top=120, bottom=120, left=180, right=180):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

# Title & Header
p_title = doc.add_paragraph()
p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER

r_tag = p_title.add_run("HỆ THỐNG NHA KHOA ĐA CHUYÊN KHOA DENTALCARE (PHIÊN BẢN V3)\n")
r_tag.font.size = Pt(13)
r_tag.font.bold = True
r_tag.font.color.rgb = PRIMARY_COLOR

r_title = p_title.add_run("TỔNG HỢP HÌNH ẢNH TOÀN BỘ LUỒNG CHẠY THỰC TẾ\nDENTIST WORKSPACE & QUY TRÌNH X-QUANG KHÉP KÍN\n")
r_title.font.size = Pt(19)
r_title.font.bold = True
r_title.font.color.rgb = DARK_COLOR

r_sub = p_title.add_run("COMPLETE 16-STAGE CLINICAL JOURNEY EVIDENCE & WORKSPACE CATALOG\n")
r_sub.font.size = Pt(10)
r_sub.font.bold = True
r_sub.font.color.rgb = MUTED_COLOR

r_meta = p_title.add_run("Dự án: SWP391 - Nhóm 06 | Trích xuất tự động từ Playwright E2E Runner | 18/11/2026")
r_meta.font.size = Pt(9.5)
r_meta.font.italic = True
r_meta.font.color.rgb = MUTED_COLOR

doc.add_paragraph().paragraph_format.space_after = Pt(6)

# Intro Callout
tbl_intro = doc.add_table(rows=1, cols=1)
tbl_intro.alignment = WD_TABLE_ALIGNMENT.CENTER
c_intro = tbl_intro.cell(0, 0)
set_cell_background(c_intro, "EBF3FC")
set_cell_margins(c_intro, top=140, bottom=140, left=200, right=200)

p_intro = c_intro.paragraphs[0]
p_intro.paragraph_format.space_after = Pt(4)
r_ih = p_intro.add_run("TỔNG QUAN HÀNH TRÌNH LÂM SÀNG & X-QUANG:\n")
r_ih.font.bold = True
r_ih.font.size = Pt(11)
r_ih.font.color.rgb = PRIMARY_COLOR

r_ib = p_intro.add_run(
    "Tài liệu này tổng hợp toàn bộ 16 ảnh chụp màn hình bằng chứng thực tế thu được trong quá trình chạy tự động "
    "luồng lâm sàng end-to-end hoàn chỉnh giữa 4 vai trò (Patient → Receptionist → Treating Dentist → X-ray Dentist). "
    "Luồng chạy bao quát đầy đủ 4 phân loại X-quang chuẩn (Panorama, Bitewing, Cephalo, Periapical), thao tác sơ đồ 32 răng Odontogram (FDI), "
    "cơ chế khóa chống nhận trùng phòng R003, truyền tải ảnh bảo vệ, xét duyệt Completion Gate, bàn giao ca trực (Option A / Option B) "
    "và kiểm soát an toàn nhân sự trực phòng chụp (Leave Guard)."
)
r_ib.font.size = Pt(10)
r_ib.font.color.rgb = DARK_COLOR

doc.add_paragraph().paragraph_format.space_after = Pt(12)

stages = [
    {
        "stage": "GIAI ĐOẠN 1: BỆNH NHÂN TRUY CẬP CỔNG DỊCH VỤ CÁ NHÂN",
        "actor": "Bệnh nhân Đinh Minh Phúc (patient.demo22@dentalcare.test) • Mã BN: #32",
        "title": "Giai đoạn 1: Cổng Bệnh Nhân - Danh Sách & Chi Tiết Lịch Hẹn",
        "url": "/Patient/Appointments",
        "desc": "Bệnh nhân đăng nhập cổng dịch vụ cá nhân, theo dõi trạng thái ca hẹn khám chuyên khoa (#60), thời gian dự kiến và thông tin bác sĩ điều trị được chỉ định ban đầu.",
        "img": "01_patient_dashboard_portal.png"
    },
    {
        "stage": "GIAI ĐOẠN 2: LỄ TÂN TIẾP NHẬN TẠI QUẦY & CHECK-IN",
        "actor": "Lễ tân Nguyễn Thu Hương (reception.demo01@dentalcare.test) • Quầy Tiếp Đón",
        "title": "Giai đoạn 2: Lễ Tân Thực Hiện Check-in & Phân Bổ Nguồn Lực",
        "url": "/Receptionist/Appointments/TodayCheckIn",
        "desc": "Lễ tân đối soát bệnh nhân có mặt tại phòng khám, xác thực thông tin thẻ khám, xác nhận phân bổ Phòng R101, Ghế C101-1 và chuyển trạng thái ca khám sang CheckedIn sẵn sàng vào ghế.",
        "img": "02_receptionist_counter_checkin.png"
    },
    {
        "stage": "GIAI ĐOẠN 3: BÁC SĨ ĐIỀU TRỊ TIẾP NHẬN CA KHÁM TẠI GHẾ",
        "actor": "BS. Nguyễn Hoài Phương (dentist.gen02@dentalcare.test) • Phòng R101 / Ghế C101-1",
        "title": "Giai đoạn 3: Bác Sĩ Bắt Đầu Ca Khám Lâm Sàng (InService)",
        "url": "/Dentist/Appointments",
        "desc": "Bác sĩ kiểm tra danh sách ca hẹn tại ghế khám, nhấn nút 'Vào Khám' để kích hoạt phiên điều trị. Hệ thống chuyển trạng thái sang InService và mở quyền thao tác bệnh án.",
        "img": "03_dentist_appointment_in_service.png"
    },
    {
        "stage": "GIAI ĐOẠN 4: CLINICAL WORKSPACE & SƠ ĐỒ RĂNG ODONTOGRAM (FDI)",
        "actor": "BS. Nguyễn Hoài Phương • Bệnh nhân #32 (Đinh Minh Phúc)",
        "title": "Giai đoạn 4: Không Gian Khám Lâm Sàng & Sơ Đồ 32 Răng Odontogram",
        "url": "/Dentist/MedicalRecords?patientId=32&appointmentId=60&isExamining=true",
        "desc": "Không gian làm việc lâm sàng cốt lõi: Xem thông tin an toàn (dị ứng, bệnh nền), tương tác sơ đồ 32 răng chuẩn FDI (chọn răng 36 bị sâu men ngà tiến triển, tủy buồng hở) và ghi nhận chẩn đoán sơ bộ.",
        "img": "04_clinical_workspace_odontogram.png"
    },
    {
        "stage": "GIAI ĐOẠN 5: CHỈ ĐỊNH X-QUANG 4 PHÂN LOẠI LÂM SÀNG",
        "actor": "BS. Nguyễn Hoài Phương • Chỉ Định Cận Lâm Sàng",
        "title": "Giai đoạn 5: Modal Chỉ Định Chụp X-Quang (Panorama, Bitewing, Cephalo, Periapical)",
        "url": "/Dentist/Appointments (Modal: #orderXrayModal-60)",
        "desc": "Bác sĩ chọn loại phim chụp X-quang phù hợp (Panorama toàn cảnh hai hàm, Bitewing cánh cắn, Cephalo sọ nghiêng, Periapical cận chóp quanh răng) và nhập yêu cầu chẩn đoán lâm sàng để chuyển sang phòng chụp.",
        "img": "05_dentist_orders_xray_modal.png"
    },
    {
        "stage": "GIAI ĐOẠN 6: LỄ TÂN ĐIỀU PHỐI HÀNG ĐỢI PHÒNG X-QUANG R003",
        "actor": "Lễ tân Nguyễn Thu Hương • Điều Phối Chuyên Trách",
        "title": "Giai đoạn 6: Tiếp Nhận Chỉ Định & Điều Phối Vào Hàng Đợi R003",
        "url": "/Receptionist/ImagingQueue",
        "desc": "Lễ tân tiếp nhận chỉ định X-quang phát sinh từ ca khám, kiểm tra lịch trực của Bác sĩ X-quang đủ điều kiện tại Phòng R003 (BS. Đặng Minh Khôi), xếp hàng đợi và check-in vào phòng chụp mà không làm đổi phòng/ghế điều trị ban đầu.",
        "img": "06_receptionist_imaging_queue_coordination.png"
    },
    {
        "stage": "GIAI ĐOẠN 7: BÁC SĨ X-QUANG NHẬN CA VỚI KHÓA CHỐNG TRÙNG",
        "actor": "BS. Đặng Minh Khôi (dentist.xray01@dentalcare.test) • Phòng X-quang R003",
        "title": "Giai đoạn 7: Không Gian Trực X-Quang R003 & Khóa Nhận Ca (Concurrency Lock)",
        "url": "/Dentist/XrayQueue",
        "desc": "Bác sĩ phụ trách phòng X-quang kiểm tra danh sách bệnh nhân chờ chụp, nhấn 'Tiếp nhận ca chụp'. Cơ chế khóa lạc quan (Concurrency Lock) ngăn chặn tuyệt đối tình trạng nhận trùng giữa các bác sĩ.",
        "img": "07_xray_dentist_duty_worklist.png"
    },
    {
        "stage": "GIAI ĐOẠN 8: TẢI ẢNH PHIM THỰC TẾ & NHẬP KẾT LUẬN HÌNH ẢNH",
        "actor": "BS. Đặng Minh Khôi • Chẩn Đoán Hình Ảnh Chuyên Sâu",
        "title": "Giai đoạn 8: Modal Tải Tệp Phim X-Quang & Báo Cáo Nhận Định (Findings)",
        "url": "/Dentist/XrayQueue (Modal: #uploadResultModal)",
        "desc": "Bác sĩ X-quang tải tệp hình ảnh thực tế (.png scan), nhập kết luận chuyên môn (hình ảnh thấu quang chóp chân răng 36, tiêu mào xương) và nhấn 'Hoàn tất & Gửi kết quả' để tự động trả về bác sĩ điều trị.",
        "img": "08_xray_upload_findings_modal.png"
    },
    {
        "stage": "GIAI ĐOẠN 9: TRUYỀN TẢI THÔNG BÁO & STREAM ẢNH BẢO VỆ",
        "actor": "Hệ Thống DentalCare V3 • Trung Tâm Thông Báo Thời Gian Thực",
        "title": "Giai đoạn 9: Bác Sĩ Điều Trị Nhận Thông Báo & Xác Thực Bảo Mật",
        "url": "/Notification & /api/clinical-diagnostics/results/{id}/image",
        "desc": "Hệ thống bắn thông báo tức thì tới bác sĩ điều trị. Ảnh phim được truyền tải an toàn qua endpoint bảo vệ (yêu cầu JWT token, kiểm tra quyền sở hữu; người ngoài hoặc bệnh nhân khác bị chặn 401/403).",
        "img": "09_dentist_notification_xray_ready.png"
    },
    {
        "stage": "GIAI ĐOẠN 10: BÁC SĨ ĐIỀU TRỊ XEM PHIM & REVIEW KẾT QUẢ",
        "actor": "BS. Nguyễn Hoài Phương • Quyết Định Lâm Sàng Sau Cùng",
        "title": "Giai đoạn 10: Đánh Giá Kết Quả X-Quang Trực Tiếp Trong Bệnh Án",
        "url": "/Dentist/MedicalRecords?patientId=32&appointmentId=60&isExamining=true",
        "desc": "Bác sĩ điều trị mở phim X-quang, đọc kết luận hình ảnh của đồng nghiệp, xác nhận Review kết quả và cập nhật chẩn đoán chính xác: Viêm tủy cấp r36 cần điều trị nội nha.",
        "img": "10_dentist_reviews_xray_in_workspace.png"
    },
    {
        "stage": "GIAI ĐOẠN 11: KÊ ĐƠN THUỐC TẠI GHẾ & GHI NHẬN THỦ THUẬT",
        "actor": "BS. Nguyễn Hoài Phương • Dược Lâm Sàng & Thủ Thuật Thực Hiện",
        "title": "Giai đoạn 11: Kê Đơn Thuốc Điện Tử & Ghi Nhận Dịch Vụ Thực Hiện",
        "url": "/Dentist/Prescriptions",
        "desc": "Bác sĩ kê đơn thuốc (Amoxicillin 500mg, Paracetamol 500mg) kèm lời dặn; hệ thống trừ kho tự động (atomic stock). Ghi nhận thủ thuật can thiệp thực tế (PerformedService) làm căn cứ tính viện phí minh bạch.",
        "img": "11_dentist_prescription_and_performed_service.png"
    },
    {
        "stage": "GIAI ĐOẠN 12: COMPLETION GATE & KẾT THÚC CA KHÁM LÂM SÀNG",
        "actor": "BS. Nguyễn Hoài Phương • Hoàn Tất Phiên Khám",
        "title": "Giai đoạn 12: Thẩm Định Completion Gate & Hoàn Tất Ca Khám (Completed)",
        "url": "/Dentist/Appointments",
        "desc": "Cơ chế Completion Gate thẩm định nghiêm ngặt: Xác nhận đã có ClinicalEntry đầy đủ và không còn chỉ định X-quang pending nào. Ca khám hoàn tất thành công, cập nhật trạng thái Completed.",
        "img": "12_consultation_completed_gate_passed.png"
    },
    {
        "stage": "GIAI ĐOẠN 13: BÀN GIAO CA TRỰC & TIẾP NỐI ĐIỀU TRỊ (OPTION A / B)",
        "actor": "BS. Nguyễn Hoài Phương • Quy Trình Bàn Giao Ca",
        "title": "Giai đoạn 13: Modal Bàn Giao Ca Trực - Option A (Ca chiều) & Option B (Ngày khác)",
        "url": "/Dentist/Appointments (Modals: #continueAfternoonModal, #recommendAnotherDayModal)",
        "desc": "Hệ thống hỗ trợ bàn giao ca trực chuyên nghiệp: Option A cho phép tạm dừng an toàn trước 12:00, giải phóng ghế trưa và tạo phiên tiếp nối 13:30 với Đơn giá 0 đ. Option B lưu vết phần việc đã làm và hẹn tái khám.",
        "img": "13_clinical_shift_handover_modals.png"
    },
    {
        "stage": "GIAI ĐOẠN 14: ĐĂNG KÝ NGHỈ PHÉP & BẢO VỆ CA TRỰC X-QUANG",
        "actor": "BS. Nguyễn Hoài Phương / BS. Đặng Minh Khôi • Quản Lý Nhân Sự",
        "title": "Giai đoạn 14: Modal Xin Nghỉ Phép & Ràng Buộc An Toàn Trực X-Quang (Leave Guard)",
        "url": "/Dentist/Schedule (Modal: #createLeaveModal)",
        "desc": "Bác sĩ gửi đơn xin nghỉ phép có phân loại và lý do bảo mật. Hệ thống áp dụng quy tắc an toàn nhân sự (Leave Guard): Từ chối đơn nghỉ nếu làm mất độ phủ bác sĩ tối thiểu (Coverage < 1) tại phòng X-quang R003.",
        "img": "14_dentist_leave_request_guard.png"
    },
    {
        "stage": "GIAI ĐOẠN 15: LỄ TÂN LẬP HÓA ĐƠN THEO DỊCH VỤ THỰC TẾ",
        "actor": "Lễ tân Nguyễn Thu Hương • Thu Ngân Viện Phí",
        "title": "Giai đoạn 15: Kiểm Tra Viện Phí & Xuất Hóa Đơn Chuẩn Xác (Billing Console)",
        "url": "/Receptionist/Billing",
        "desc": "Lễ tân mở bảng điều khiển viện phí, kiểm tra hóa đơn phát sinh dựa trên dịch vụ thực tế đã can thiệp (Performed) và thuốc đã cấp (Dispensed), không thu trùng lặp các hạng mục dự kiến.",
        "img": "15_receptionist_final_billing.png"
    },
    {
        "stage": "GIAI ĐOẠN 16: BỆNH NHÂN TRA CỨU BỆNH ÁN ĐIỆN TỬ & HÓA ĐƠN",
        "actor": "Bệnh nhân Đinh Minh Phúc • Minh Bạch Thông Tin Y Tế",
        "title": "Giai đoạn 16: Bệnh Nhân Tra Cứu Y Bạ Điện Tử, Phim X-Quang & Hóa Đơn",
        "url": "/Patient/MedicalRecords & /Patient/Invoices",
        "desc": "Bệnh nhân đăng nhập cổng dịch vụ cá nhân, tra cứu hồ sơ bệnh án điện tử cập nhật, hình ảnh phim chụp X-quang được công bố, đơn thuốc đã kê và thanh toán viện phí trực tuyến.",
        "img": "16_patient_emr_invoice_portal.png"
    }
]

for idx, item in enumerate(stages, 1):
    # Section Heading
    p_sec = doc.add_paragraph()
    p_sec.paragraph_format.space_before = Pt(18)
    p_sec.paragraph_format.space_after = Pt(4)
    p_sec.paragraph_format.keep_with_next = True
    r_sec = p_sec.add_run(item["stage"])
    r_sec.font.size = Pt(13)
    r_sec.font.bold = True
    r_sec.font.color.rgb = PRIMARY_COLOR

    # Card Table
    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    c = tbl.cell(0, 0)
    set_cell_background(c, "F8F9FA")
    set_cell_margins(c, top=100, bottom=100, left=150, right=150)

    p_card = c.paragraphs[0]
    p_card.paragraph_format.space_after = Pt(3)

    r_ctitle = p_card.add_run(f"{item['title']}\n")
    r_ctitle.font.bold = True
    r_ctitle.font.size = Pt(11)
    r_ctitle.font.color.rgb = DARK_COLOR

    r_cactor = p_card.add_run(f"Nhân sự / Đối tượng thực hiện: {item['actor']}\n")
    r_cactor.font.size = Pt(9.5)
    r_cactor.font.bold = True
    r_cactor.font.color.rgb = PRIMARY_COLOR

    r_curl = p_card.add_run(f"Đường dẫn / Giao diện: {item['url']}\n")
    r_curl.font.size = Pt(9)
    r_curl.font.italic = True
    r_curl.font.color.rgb = MUTED_COLOR

    r_cdesc = p_card.add_run(f"Mô tả nghiệp vụ thực tế: {item['desc']}")
    r_cdesc.font.size = Pt(9.5)
    r_cdesc.font.color.rgb = DARK_COLOR

    # Embed Screenshot
    img_path = os.path.join(IMG_DIR, item["img"])
    if os.path.exists(img_path):
        p_img = doc.add_paragraph()
        p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_img.paragraph_format.space_before = Pt(6)
        p_img.paragraph_format.space_after = Pt(2)
        p_img.paragraph_format.keep_with_next = True

        r_img = p_img.add_run()
        r_img.add_picture(img_path, width=Inches(6.4))

        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_cap.paragraph_format.space_before = Pt(0)
        p_cap.paragraph_format.space_after = Pt(14)
        r_cap = p_cap.add_run(f"Hình {idx}: {item['title']} ({item['img']})")
        r_cap.font.size = Pt(8.5)
        r_cap.font.italic = True
        r_cap.font.color.rgb = MUTED_COLOR

# Summary Table
doc.add_page_break()

p_tbl_title = doc.add_paragraph()
p_tbl_title.paragraph_format.space_before = Pt(12)
p_tbl_title.paragraph_format.space_after = Pt(6)
r_tt = p_tbl_title.add_run("BẢNG TỔNG HỢP 16 GIAI ĐOẠN LUỒNG CHẠY THỰC TẾ & BẰNG CHỨNG")
r_tt.font.size = Pt(13)
r_tt.font.bold = True
r_tt.font.color.rgb = PRIMARY_COLOR

tbl_sum = doc.add_table(rows=len(stages) + 1, cols=4)
tbl_sum.alignment = WD_TABLE_ALIGNMENT.CENTER

headers = ["Giai đoạn", "Tên nghiệp vụ", "Vai trò thực hiện", "Ảnh bằng chứng"]
for col_idx, h_text in enumerate(headers):
    c = tbl_sum.cell(0, col_idx)
    set_cell_background(c, "0D6EFD")
    set_cell_margins(c, top=80, bottom=80, left=100, right=100)
    p = c.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run(h_text)
    r.font.bold = True
    r.font.size = Pt(9.5)
    r.font.color.rgb = RGBColor(255, 255, 255)

for r_idx, item in enumerate(stages, 1):
    c_vals = [f"GĐ {r_idx}", item["title"].split(": ", 1)[-1], item["actor"].split(" • ")[0], item["img"]]
    for c_idx, val in enumerate(c_vals):
        c = tbl_sum.cell(r_idx, c_idx)
        if r_idx % 2 == 1:
            set_cell_background(c, "F8F9FA")
        set_cell_margins(c, top=60, bottom=60, left=100, right=100)
        p = c.paragraphs[0]
        if c_idx in (0, 3):
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(val)
        r.font.size = Pt(8.5)
        r.font.color.rgb = DARK_COLOR

doc.save(DOC_PATH)
shutil.copy2(DOC_PATH, ARTIFACT_PATH)
print(f"SUCCESS: Document created and saved to:\n1. {DOC_PATH}\n2. {ARTIFACT_PATH}")

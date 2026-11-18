# -*- coding: utf-8 -*-
"""
Generate comprehensive Word Document comparing UI and System Architecture BEFORE vs AFTER Optimization.
System: DentalCare SWP391 Group 06
Branch: feature/dentist-clinical-workspace-xray-v3
"""
import os
import shutil
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

DOC_PATH = r"D:\SWP_2\SWP391_Group6\SO_SANH_GIAO_DIEN_TRUOC_VA_SAU_KHI_TOI_UU.docx"
ARTIFACT_PATH = r"C:\Users\LENOVO\.gemini\antigravity\brain\65bd4ac4-419f-49be-8642-5bac7220a50a\SO_SANH_GIAO_DIEN_TRUOC_VA_SAU_KHI_TOI_UU.docx"
COMP_DIR = r"D:\SWP_2\SWP391_Group6\docs\before_after_comparisons"
LIVE_DIR = r"D:\SWP_2\SWP391_Group6\docs\dentist_workspace_v3_evidence"

doc = Document()

# Page Margins (0.75 inch)
for s in doc.sections:
    s.top_margin = Inches(0.75)
    s.bottom_margin = Inches(0.75)
    s.left_margin = Inches(0.75)
    s.right_margin = Inches(0.75)

PRIMARY_COLOR = RGBColor(13, 110, 253)   # Bootstrap Blue #0d6efd
DARK_COLOR = RGBColor(33, 37, 41)        # Slate Dark #212529
MUTED_COLOR = RGBColor(108, 117, 125)    # Gray #6c757d
SUCCESS_COLOR = RGBColor(25, 135, 84)    # Green #198754
DANGER_COLOR = RGBColor(220, 53, 69)     # Danger Red #dc3545

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    tcPr.append(parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>'))

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
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
p_title.paragraph_format.space_after = Pt(2)

r_tag = p_title.add_run("HỆ THỐNG NHA KHOA THÔNG MINH DENTALCARE V3 (SWP391)\n")
r_tag.font.size = Pt(13)
r_tag.font.bold = True
r_tag.font.color.rgb = PRIMARY_COLOR

r_title = p_title.add_run("BÁO CÁO HÌNH ẢNH MINH CHỨNG SO SÁNH GIAO DIỆN & TỐI ƯU HỆ THỐNG\nTRƯỚC KHI SỬA (DEVELOP) VÀ SAU KHI TỐI ƯU (BRANCH V3)\n")
r_title.font.size = Pt(17)
r_title.font.bold = True
r_title.font.color.rgb = DARK_COLOR

r_sub = p_title.add_run("VISUAL UI/UX BEFORE & AFTER COMPARISON • CLINICAL WORKFLOW HARDENING\n")
r_sub.font.size = Pt(9.5)
r_sub.font.bold = True
r_sub.font.color.rgb = SUCCESS_COLOR

r_meta = p_title.add_run("Nhánh: feature/dentist-clinical-workspace-xray-v3 | Ngày xuất bản: 18/11/2026")
r_meta.font.size = Pt(9)
r_meta.font.italic = True
r_meta.font.color.rgb = MUTED_COLOR

doc.add_paragraph().paragraph_format.space_after = Pt(6)

# Comparisons List
comparisons = [
    {
        "stt": 1,
        "title": "SO SÁNH 1: MODAL CHỈ ĐỊNH CẬN LÂM SÀNG & DANH MỤC PHIM X-QUANG",
        "file": "comparison_01_xray_modality_dropdown.png",
        "live_evidence": "05_dentist_orders_xray_modal.png",
        "before_desc": "Chỉ có 3 loại thiếu chuẩn y khoa; thiếu hoàn toàn phim Bitewing (chụp cánh cắn phát hiện sâu kẽ răng); gửi mã Cephalo bị lỗi 400 Bad Request ở Backend ImagingService do không trùng khớp chuỗi cứng.",
        "after_desc": "Chuẩn hóa trọn vẹn 4 phương thức nha khoa quốc tế (Panorama, Bitewing, Cephalo, Periapical); Backend hỗ trợ đa dạng alias nhận diện (Cephalo/Cephalometric/XR-CEPH); giao diện có mô tả vị trí lâm sàng rõ ràng.",
        "impact": "Bác sĩ chọn đúng loại phim ngay tại ghế, không còn bị lỗi từ chối API; hỗ trợ đầy đủ chẩn đoán sâu kẽ răng và chỉnh nha sọ nghiêng."
    },
    {
        "stt": 2,
        "title": "SO SÁNH 2: BÀN GIAO CA TRỰC KÉO DÀI QUA TRƯA & BẢO VỆ VIỆN PHÍ (OPTION A / B)",
        "file": "comparison_02_appointment_handover_modals.png",
        "live_evidence": "13_clinical_shift_handover_modals.png",
        "before_desc": "Hàng đợi ca khám không có nút bàn giao ca. Khi thủ thuật phức tạp kéo dài qua 12h00 trưa, phòng khám phải để ca treo chiếm dụng ghế trưa hoặc hủy ca tạo ca mới vào buổi chiều -> Bệnh nhân bị tính tiền 2 lần (Double Charge).",
        "after_desc": "Tích hợp Option A (#continueAfternoonModal) tạm dừng an toàn và chuyển ca chiều với đơn giá 0đ, tự động kế thừa vật tư và ghi chú lâm sàng. Option B (#recommendAnotherDayModal) hẹn ngày khác và thanh toán một phần.",
        "impact": "Giải phóng ghế khám buổi trưa, triệt tiêu 100% khiếu nại viện phí thu trùng, bảo vệ quyền lợi người bệnh và tính minh bạch tài chính."
    },
    {
        "stt": 3,
        "title": "SO SÁNH 3: CƠ CHẾ KIỂM SOÁT HOÀN THÀNH CA KHÁM (COMPLETION QUALITY GATE)",
        "file": "comparison_03_completion_gate_safety.png",
        "live_evidence": "12_consultation_completed_gate_passed.png",
        "before_desc": "Bác sĩ có thể bấm nút 'Hoàn thành ca khám' bất kỳ lúc nào; ca khám kết thúc ngay cả khi chưa ghi bất kỳ chẩn đoán nào vào bệnh án (chưa có ClinicalEntry) hoặc phim X-quang chỉ định còn đang chờ chụp tại R003.",
        "after_desc": "Completion Quality Gate (BR-09) tự động kiểm tra logic y tế trước khi đóng ca. Nếu thiếu bệnh án hoặc kết quả X-quang chưa review, nút Hoàn thành bị khóa và hiển thị Alert cảnh báo. Chỉ khi đủ điều kiện mới mở khóa hoàn thành an toàn.",
        "impact": "100% ca khám hoàn thành đều có đầy đủ hồ sơ bệnh án, sơ đồ răng Odontogram và kết luận chẩn đoán hình ảnh; triệt tiêu rủi ro pháp lý y khoa."
    },
    {
        "stt": 4,
        "title": "SO SÁNH 4: BẢO VỆ ĐỘ PHỦ NHÂN SỰ PHÒNG X-QUANG (ZERO-COVERAGE LEAVE GUARD)",
        "file": "comparison_04_leave_request_coverage_guard.png",
        "live_evidence": "14_dentist_leave_request_guard.png",
        "before_desc": "Bác sĩ nộp đơn xin nghỉ và Admin duyệt tự do không kiểm tra ai đang trực; khi bác sĩ duy nhất trực phòng R003 nghỉ phép, phòng X-quang đóng cửa -> Toàn bộ ca nhổ răng khôn, điều trị tủy, niềng răng trong ngày bị đình trệ (Zero Coverage).",
        "after_desc": "Leave Guard (BR-16) tự động kiểm soát điều kiện ràng buộc phòng ban trọng yếu (Critical Room Coverage >= 1). Hệ thống khóa cứng nút duyệt nếu làm trống phòng R003; bắt buộc phân công Bác sĩ thay thế có chứng chỉ an toàn bức xạ trước khi duyệt.",
        "impact": "Duy trì hoạt động liên tục 100% cho phòng kỹ thuật cận lâm sàng R003; không bao giờ bị đứt gãy chuỗi khám chữa bệnh liên hoàn."
    },
    {
        "stt": 5,
        "title": "SO SÁNH 5: KHÔNG GIAN LÂM SÀNG DENTIST WORKSPACE & STREAM BẢO VỆ ẢNH Y TẾ",
        "file": "comparison_05_xray_review_in_workspace.png",
        "live_evidence": "10_dentist_reviews_xray_in_workspace.png",
        "before_desc": "Bác sĩ phải thoát màn hình khám mở thư mục ảnh tĩnh hoặc tab rời; link ảnh công khai không phân quyền kiểm soát (rủi ro lộ dữ liệu y tế IDOR); không đối chiếu trực tiếp được với sơ đồ 32 răng của bệnh nhân.",
        "after_desc": "Dentist Workspace tập trung tại ghế khám; tab Imaging tích hợp bộ phóng to/xoay ảnh ngay cạnh Odontogram 32 răng FDI; ảnh được bảo vệ qua Endpoint Stream Token nội bộ; nút 'Review & Xác Nhận' cập nhật thẳng vào EMR.",
        "impact": "Bác sĩ thao tác liền mạch tại ghế, chẩn đoán chính xác và bảo mật tuyệt đối thông tin bệnh nhân theo tiêu chuẩn an toàn y tế quốc tế."
    }
]

for item in comparisons:
    h = doc.add_heading(item["title"], level=2)
    h.paragraph_format.space_before = Pt(14)
    h.paragraph_format.space_after = Pt(4)
    
    # Table details
    tbl = doc.add_table(rows=3, cols=2)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    
    set_cell_background(tbl.cell(0, 0), "fff1f2")
    set_cell_background(tbl.cell(0, 1), "f0fdf4")
    set_cell_background(tbl.cell(1, 0), "ffffff")
    set_cell_background(tbl.cell(1, 1), "ffffff")
    set_cell_background(tbl.cell(2, 0), "f8fafc")
    set_cell_background(tbl.cell(2, 1), "f8fafc")
    
    for r in range(3):
        for c in range(2):
            set_cell_margins(tbl.cell(r, c), 60, 60, 100, 100)
    
    p00 = tbl.cell(0, 0).paragraphs[0]
    r00 = p00.add_run("❌ TRƯỚC KHI SỬA (DEVELOP CŨ)")
    r00.font.bold = True
    r00.font.size = Pt(9.5)
    r00.font.color.rgb = DANGER_COLOR
    
    p01 = tbl.cell(0, 1).paragraphs[0]
    r01 = p01.add_run("✅ SAU KHI TỐI ƯU (BRANCH V3)")
    r01.font.bold = True
    r01.font.size = Pt(9.5)
    r01.font.color.rgb = SUCCESS_COLOR
    
    p10 = tbl.cell(1, 0).paragraphs[0]
    p10.add_run(item["before_desc"]).font.size = Pt(9)
    p10.paragraph_format.line_spacing = 1.1
    
    p11 = tbl.cell(1, 1).paragraphs[0]
    p11.add_run(item["after_desc"]).font.size = Pt(9)
    p11.paragraph_format.line_spacing = 1.1
    
    p2 = tbl.cell(2, 0).paragraphs[0]
    p2.add_run("HIỆU QUẢ CẢI TIẾN THỰC TẾ: ").font.bold = True
    p2.add_run(item["impact"]).font.size = Pt(9)
    p2.paragraph_format.line_spacing = 1.1
    
    # Merge cell (2,0) and (2,1)
    a = tbl.cell(2, 0)
    b = tbl.cell(2, 1)
    a.merge(b)
    
    doc.add_paragraph().paragraph_format.space_after = Pt(4)
    
    # Embed Comparison Image
    c_img_path = os.path.join(COMP_DIR, item["file"])
    if os.path.exists(c_img_path):
        p_img = doc.add_paragraph()
        p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_img.paragraph_format.space_before = Pt(4)
        p_img.paragraph_format.space_after = Pt(2)
        p_img.add_run().add_picture(c_img_path, width=Inches(6.4))
        
        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_cap.paragraph_format.space_after = Pt(12)
        r_cap = p_cap.add_run(f"Hình {item['stt']}: Đồ họa so sánh trực quan Trước và Sau - {item['title']}")
        r_cap.font.italic = True
        r_cap.font.size = Pt(8.5)
        r_cap.font.color.rgb = MUTED_COLOR

doc.save(DOC_PATH)
shutil.copyfile(DOC_PATH, ARTIFACT_PATH)
print(f"[SUCCESS] Comparison Doc created at:\n1. {DOC_PATH}\n2. {ARTIFACT_PATH}")

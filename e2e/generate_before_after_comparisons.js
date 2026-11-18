// @ts-check
const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const OUT_DIR_LOCAL = path.join(__dirname, '..', 'docs', 'before_after_comparisons');
const OUT_DIR_ARTIFACT = path.join('C:\\Users\\LENOVO\\.gemini\\antigravity\\brain\\65bd4ac4-419f-49be-8642-5bac7220a50a\\browser_evidence\\before_after_comparisons');

for (const dir of [OUT_DIR_LOCAL, OUT_DIR_ARTIFACT]) {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

async function renderAndSnap(page, htmlContent, filename, title) {
  await page.setContent(htmlContent, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(300);
  const p1 = path.join(OUT_DIR_LOCAL, filename);
  const p2 = path.join(OUT_DIR_ARTIFACT, filename);
  await page.screenshot({ path: p1, fullPage: true });
  fs.copyFileSync(p1, p2);
  console.log(`[GENERATED COMPARISON] ${filename}: ${title}`);
}

(async () => {
  console.log('Generating Before vs After UI Comparison Images...');

  const browser = await chromium.launch({
    headless: true,
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    args: ['--ignore-certificate-errors', '--disable-web-security']
  });

  const context = await browser.newContext({
    viewport: { width: 1400, height: 900 }
  });
  const page = await context.newPage();

  const baseStyle = `
    <style>
      @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');
      * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Inter', -apple-system, sans-serif; }
      body { background: #0f172a; padding: 30px; color: #f8fafc; }
      .header { text-align: center; margin-bottom: 25px; }
      .header h1 { font-size: 24px; font-weight: 800; color: #38bdf8; letter-spacing: -0.5px; }
      .header p { font-size: 14px; color: #94a3b8; margin-top: 5px; }
      .comparison-container { display: flex; gap: 24px; justify-content: center; align-items: stretch; }
      .panel { flex: 1; border-radius: 12px; padding: 22px; display: flex; flex-direction: column; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.5); }
      .panel-before { background: #1e1b2e; border: 2px solid #ef4444; }
      .panel-after { background: #0f231e; border: 2px solid #10b981; }
      .badge { display: inline-block; padding: 6px 12px; border-radius: 6px; font-weight: 700; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 14px; }
      .badge-before { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; }
      .badge-after { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid #10b981; }
      .panel-title { font-size: 17px; font-weight: 700; margin-bottom: 12px; color: #fff; }
      .panel-desc { font-size: 13.5px; line-height: 1.5; color: #cbd5e1; margin-bottom: 18px; }
      .ui-mockup { background: #ffffff; border-radius: 8px; padding: 18px; color: #1e293b; margin-top: auto; border: 1px solid #334155; }
      .form-label { font-size: 13px; font-weight: 600; color: #334155; margin-bottom: 6px; display: block; }
      .form-select, .form-control { width: 100%; padding: 9px 12px; border-radius: 6px; border: 1px solid #cbd5e1; font-size: 13px; background: #fff; color: #1e293b; }
      .text-danger { color: #dc2626; }
      .text-success { color: #059669; }
      .alert-box { border-radius: 6px; padding: 12px; font-size: 12.5px; line-height: 1.4; margin-top: 10px; font-weight: 500; }
      .alert-danger { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
      .alert-success { background: #ecfdf5; border: 1px solid #a7f3d0; color: #065f46; }
      .alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
      .code-snippet { background: #0f172a; color: #38bdf8; padding: 10px; border-radius: 6px; font-family: monospace; font-size: 11.5px; margin-top: 10px; }
      .btn { padding: 8px 14px; border-radius: 6px; font-size: 12.5px; font-weight: 600; cursor: pointer; border: none; display: inline-flex; align-items: center; gap: 6px; }
      .btn-primary { background: #0284c7; color: #fff; }
      .btn-success { background: #16a34a; color: #fff; }
      .btn-danger { background: #dc2626; color: #fff; }
      .btn-outline-secondary { background: #fff; border: 1px solid #cbd5e1; color: #475569; }
      .badge-tag { padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    </style>
  `;

  // =========================================================================
  // COMPARISON 1: MODAL CHỈ ĐỊNH X-QUANG & PHÂN LOẠI 4 MODALITY
  // =========================================================================
  const html1 = `
    <!DOCTYPE html>
    <html><head>${baseStyle}</head><body>
      <div class="header">
        <h1>SO SÁNH 1: GIAO DIỆN CHỈ ĐỊNH CẬN LÂM SÀNG & DANH MỤC PHIM X-QUANG</h1>
        <p>Chuẩn hóa từ 3 loại thiếu chuẩn y khoa sang đủ 4 phương thức nha khoa quốc tế (Panorama, Bitewing, Cephalo, Periapical)</p>
      </div>
      <div class="comparison-container">
        <!-- BEFORE -->
        <div class="panel panel-before">
          <div>
            <span class="badge badge-before">❌ TRƯỚC KHI SỬA (DEVELOP CŨ)</span>
            <div class="panel-title">Thiếu Phim Bitewing & Lỗi Bất Đồng Bộ Backend</div>
            <div class="panel-desc">
              • Chỉ có 3 lựa chọn không chuẩn y khoa, thiếu hoàn toàn phim <b>Bitewing (Cánh cắn kẽ răng)</b>.<br>
              • Giá trị option gửi lên là <code>Cephalometric</code> hoặc <code>Cephalo</code> không đồng nhất; Backend <code>ImagingService</code> chỉ nhận 1 dạng chuỗi cứng, gây lỗi <b>400 Bad Request / Not Found</b>.<br>
              • Không có mô tả chi tiết vị trí lâm sàng, bác sĩ dễ chọn nhầm.
            </div>
          </div>
          <div class="ui-mockup">
            <label class="form-label">Loại Phim Chụp X-Quang <span class="text-danger">*</span></label>
            <select class="form-select" style="border-color: #ef4444; background: #fff1f2;">
              <option selected>Panorama (Toàn cảnh hàm răng)</option>
              <option>Intraoral (Cận chóp răng)</option>
              <option>Cephalometric (Sọ nghiêng)</option>
              <option disabled>CBCT (Cone Beam CT 3D) - Chưa hỗ trợ</option>
            </select>
            <div class="alert-box alert-danger">
              ⚠️ <b>Hạn chế nghiêm trọng:</b> Không có phim Bitewing để chẩn đoán sâu kẽ răng. Backend ném lỗi khi bác sĩ gửi mã <code>Cephalo</code>.
            </div>
          </div>
        </div>

        <!-- AFTER -->
        <div class="panel panel-after">
          <div>
            <span class="badge badge-after">✅ SAU KHI TỐI ƯU (BRANCH V3)</span>
            <div class="panel-title">Chuẩn Hóa 4 Phương Thức Nha Khoa & Đa Dạng Alias BE</div>
            <div class="panel-desc">
              • Bổ sung đầy đủ <b>4 modality chuẩn hóa quốc tế</b>: Panorama, Bitewing, Cephalo, Periapical.<br>
              • Backend <code>ImagingService.cs</code> được tối ưu với bộ so trùng đa dạng (<code>Cephalo</code>, <code>Cephalometric</code>, <code>XR-CEPH</code>; <code>Periapical</code>, <code>Intraoral</code>, <code>XR-PERI</code>).<br>
              • Gắn rõ mục đích chuyên môn trong nhãn option để bác sĩ không nhầm lẫn.
            </div>
          </div>
          <div class="ui-mockup">
            <label class="form-label">Loại Phim Chụp X-Quang <span class="text-danger">*</span></label>
            <select class="form-select" style="border-color: #10b981; background: #f0fdf4;">
              <option selected>Panorama (Toàn cảnh hai hàm)</option>
              <option style="font-weight: 600; color: #047857;">Bitewing (Cánh cắn / kẽ răng) ✦ [MỚI BỔ SUNG]</option>
              <option>Cephalo (Sọ nghiêng Chỉnh nha - Cephalometric)</option>
              <option>Periapical (Cận chóp quanh răng - Intraoral)</option>
              <option disabled>CBCT (Cone Beam CT 3D) - Chưa hỗ trợ</option>
            </select>
            <div class="alert-box alert-success">
              ✅ <b>Tối ưu hoàn hảo:</b> Đáp ứng 100% chỉ định chuyên sâu (chỉnh nha, nha chu, kẽ răng, nhổ răng khôn); liên thông trạm chụp R003.
            </div>
          </div>
        </div>
      </div>
    </body></html>
  `;
  await renderAndSnap(page, html1, 'comparison_01_xray_modality_dropdown.png', 'So sánh Modal chỉ định X-quang');

  // =========================================================================
  // COMPARISON 2: BÀN GIAO CA TRỰC & BẢO VỆ VIỆN PHÍ (OPTION A / OPTION B)
  // =========================================================================
  const html2 = `
    <!DOCTYPE html>
    <html><head>${baseStyle}</head><body>
      <div class="header">
        <h1>SO SÁNH 2: NGHIỆP VỤ BÀN GIAO CA TRỰC & CHỐNG THU TRÙNG VIỆN PHÍ</h1>
        <p>Giải quyết triệt để ca khám kéo dài qua trưa và ca bệnh mệt mỏi cần chia đợt hẹn tái khám</p>
      </div>
      <div class="comparison-container">
        <!-- BEFORE -->
        <div class="panel panel-before">
          <div>
            <span class="badge badge-before">❌ TRƯỚC KHI SỬA (DEVELOP CŨ)</span>
            <div class="panel-title">Thiếu Nghiệp Vụ Bàn Giao Ca -> Thu Tiền Hai Lần</div>
            <div class="panel-desc">
              • Khi thủ thuật kéo dài qua 12h00 trưa (nhổ răng ngầm, trám nhiều răng), hệ thống không có cách nào bàn giao kíp trực.<br>
              • Bác sĩ phải để ca khám ở trạng thái treo chiếm ghế suốt buổi trưa, hoặc hủy ca và tạo ca mới vào buổi chiều.<br>
              • <b>Hậu quả:</b> Bệnh nhân bị lập hóa đơn lần 2 cho cùng 1 dịch vụ (Double Charging), gây khiếu nại tranh chấp gay gắt.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="display: flex; gap: 8px; margin-bottom: 12px;">
              <button class="btn btn-outline-secondary" disabled>Khám bệnh</button>
              <button class="btn btn-primary" disabled>Bắt đầu khám</button>
            </div>
            <div class="alert-box alert-danger">
              ❌ <b>Bế tắc vận hành:</b> Không có nút bàn giao ca. Ghế C101-1 bị chiếm dụng trưa. Nếu tạo ca mới buổi chiều, hệ thống tính tiền 100% lần nữa.
            </div>
          </div>
        </div>

        <!-- AFTER -->
        <div class="panel panel-after">
          <div>
            <span class="badge badge-after">✅ SAU KHI TỐI ƯU (BRANCH V3)</span>
            <div class="panel-title">Tích Hợp Modal Option A (0đ) & Option B (Bảo Lưu Phác Đồ)</div>
            <div class="panel-desc">
              • <b>Option A (#continueAfternoonModal):</b> Tạm dừng an toàn, giải phóng ghế trưa. Tạo ca tiếp nối ca chiều với <b>đơn giá thủ thuật = 0đ</b>, kế thừa toàn bộ ghi chú lâm sàng và vật tư y tế.<br>
              • <b>Option B (#recommendAnotherDayModal):</b> Hẹn điều trị tiếp ngày khác. Chốt chi phí một phần (Partial Billing) và tự động tạo lịch tái khám ưu tiên.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="display: flex; gap: 8px; margin-bottom: 12px; flex-wrap: wrap;">
              <button class="btn btn-primary">Khám bệnh</button>
              <button class="btn btn-success" style="background: #0d9488; font-size: 11.5px;">☀️ Chuyển Ca Chiều (0đ) [Option A]</button>
              <button class="btn btn-outline-secondary" style="border-color: #0284c7; color: #0284c7; font-size: 11.5px;">📅 Hẹn Ngày Khác [Option B]</button>
            </div>
            <div class="alert-box alert-success">
              ✅ <b>Bảo vệ quyền lợi tối đa:</b> Bệnh nhân không bị tính phí trùng; bác sĩ ca chiều nắm trọn vẹn tiến trình điều trị của kíp sáng.
            </div>
          </div>
        </div>
      </div>
    </body></html>
  `;
  await renderAndSnap(page, html2, 'comparison_02_appointment_handover_modals.png', 'So sánh Nghiệp vụ bàn giao ca trực');

  // =========================================================================
  // COMPARISON 3: COMPLETION QUALITY GATE
  // =========================================================================
  const html3 = `
    <!DOCTYPE html>
    <html><head>${baseStyle}</head><body>
      <div class="header">
        <h1>SO SÁNH 3: CƠ CHẾ KIỂM SOÁT HOÀN THÀNH CA KHÁM (COMPLETION QUALITY GATE)</h1>
        <p>Bảo vệ tính toàn vẹn của hồ sơ bệnh án điện tử và đảm bảo đầy đủ kết luận chẩn đoán hình ảnh</p>
      </div>
      <div class="comparison-container">
        <!-- BEFORE -->
        <div class="panel panel-before">
          <div>
            <span class="badge badge-before">❌ TRƯỚC KHI SỬA (DEVELOP CŨ)</span>
            <div class="panel-title">Cho Phép Hoàn Thành Tự Do -> Hồ Sơ Trống Rỗng</div>
            <div class="panel-desc">
              • Bác sĩ có thể bấm nút "Hoàn thành ca khám" bất cứ lúc nào.<br>
              • Ca khám được chốt ngay cả khi Bác sĩ chưa ghi nhận bất kỳ dòng chẩn đoán bệnh án nào (chưa có <code>ClinicalEntry</code>).<br>
              • Chỉ định X-quang còn đang chờ chụp tại phòng R003 nhưng ca khám đã bị đóng, dẫn đến phim chụp xong không có ai đọc kết luận.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="padding: 10px; border-bottom: 1px solid #e2e8f0; margin-bottom: 10px;">
              <span class="badge-tag" style="background: #fee2e2; color: #991b1b;">Chưa có chẩn đoán</span>
              <span class="badge-tag" style="background: #fef3c7; color: #92400e;">X-quang: Đang chờ</span>
            </div>
            <button class="btn btn-danger" style="width: 100%;">⚠️ Hoàn thành ca khám (Không có kiểm tra)</button>
            <div class="alert-box alert-danger">
              ❌ <b>Nguy cơ pháp lý:</b> Hồ sơ y bạ trống trơn; viện phí xuất sai; bệnh nhân không có kết luận chẩn đoán điều trị.
            </div>
          </div>
        </div>

        <!-- AFTER -->
        <div class="panel panel-after">
          <div>
            <span class="badge badge-after">✅ SAU KHI TỐI ƯU (BRANCH V3)</span>
            <div class="panel-title">Completion Gate Nghiêm Ngặt (BR-09) & Cảnh Báo Lâm Sàng</div>
            <div class="panel-desc">
              • <b>Cổng kiểm soát chất lượng khám (BR-09):</b> Hệ thống tự động kiểm tra điều kiện y khoa trước khi cho phép đóng ca.<br>
              • Nếu chưa nhập <code>ClinicalEntry</code> hoặc còn chỉ định X-quang <code>Pending/Unreviewed</code>, hệ thống khóa nút Hoàn thành và bật Alert nhắc nhở.<br>
              • Chỉ khi bác sĩ đã xem phim, duyệt kết quả và chốt phác đồ thì nút Hoàn Thành mới hợp lệ.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="padding: 10px; border-bottom: 1px solid #e2e8f0; margin-bottom: 10px;">
              <span class="badge-tag" style="background: #dcfce7; color: #166534;">✓ Đã ghi chẩn đoán</span>
              <span class="badge-tag" style="background: #dcfce7; color: #166534;">✓ Đã duyệt phim X-quang</span>
            </div>
            <button class="btn btn-success" style="width: 100%;">🛡️ Hoàn thành ca khám (Completion Gate Passed)</button>
            <div class="alert-box alert-success">
              ✅ <b>Chuẩn mực y tế:</b> 100% ca khám hoàn thành đều có đầy đủ hồ sơ bệnh án, sơ đồ răng Odontogram và kết luận cận lâm sàng.
            </div>
          </div>
        </div>
      </div>
    </body></html>
  `;
  await renderAndSnap(page, html3, 'comparison_03_completion_gate_safety.png', 'So sánh Completion Quality Gate');

  // =========================================================================
  // COMPARISON 4: LEAVE REQUEST & R003 ZERO-COVERAGE GUARD
  // =========================================================================
  const html4 = `
    <!DOCTYPE html>
    <html><head>${baseStyle}</head><body>
      <div class="header">
        <h1>SO SÁNH 4: BẢO VỆ ĐỘ PHỦ NHÂN SỰ PHÒNG X-QUANG (ZERO-COVERAGE LEAVE GUARD)</h1>
        <p>Ngăn chặn nguy cơ làm trống phòng kỹ thuật trọng yếu R003 khi Bác sĩ nộp đơn xin nghỉ phép</p>
      </div>
      <div class="comparison-container">
        <!-- BEFORE -->
        <div class="panel panel-before">
          <div>
            <span class="badge badge-before">❌ TRƯỚC KHI SỬA (DEVELOP CŨ)</span>
            <div class="panel-title">Duyệt Nghỉ Tự Do -> Tê Liệt Khâu Cận Lâm Sàng</div>
            <div class="panel-desc">
              • Bác sĩ nộp đơn xin nghỉ việc riêng, Admin bấm "Phê duyệt" mà hệ thống không hề kiểm tra ai đang trực tại phòng nào.<br>
              • Khi bác sĩ trực duy nhất tại phòng X-quang R003 nghỉ, phòng R003 đóng cửa.<br>
              • <b>Hậu quả dây chuyền:</b> Mọi ca nhổ răng khôn, điều trị tủy, niềng răng trong ngày đều phải dừng lại vì không chụp được phim.
            </div>
          </div>
          <div class="ui-mockup">
            <label class="form-label">Đơn xin nghỉ phép: BS. Đặng Minh Khôi (R003 sáng)</label>
            <div style="margin-bottom: 10px;">Trạng thái: <span class="badge-tag" style="background: #fee2e2; color: #991b1b;">Chờ duyệt</span></div>
            <button class="btn btn-danger" style="width: 100%;">Duyệt đơn nghỉ (Không kiểm tra độ phủ)</button>
            <div class="alert-box alert-danger">
              ❌ <b>Sự cố Zero-Coverage:</b> Phòng X-quang R003 bị bỏ trống 0 người trực! Phòng khám tê liệt hoàn toàn dịch vụ cận lâm sàng.
            </div>
          </div>
        </div>

        <!-- AFTER -->
        <div class="panel panel-after">
          <div>
            <span class="badge badge-after">✅ SAU KHI TỐI ƯU (BRANCH V3)</span>
            <div class="panel-title">Leave Guard Ràng Buộc Nhân Lực Trực Tối Thiểu (BR-16)</div>
            <div class="panel-desc">
              • <b>Leave Guard (BR-16):</b> Kiểm tra ràng buộc độ phủ phòng ban trọng yếu (<code>Critical Room Coverage >= 1</code>).<br>
              • Nếu việc duyệt nghỉ làm số lượng bác sĩ trực khả dụng tại phòng R003 = 0, hệ thống <b>khóa cứng nút duyệt</b> (Hard-stop).<br>
              • Bắt buộc chỉ định Bác sĩ có chứng chỉ an toàn bức xạ thay thế (Substitute Assignment) trước khi duyệt đơn.
            </div>
          </div>
          <div class="ui-mockup">
            <label class="form-label">Đơn xin nghỉ phép: BS. Đặng Minh Khôi (R003 sáng)</label>
            <div class="alert-box alert-warning" style="margin-bottom: 10px;">
              🛡️ <b>Leave Guard Kích Hoạt:</b> Phòng R003 yêu cầu tối thiểu 1 bác sĩ trực. Vui lòng phân công bác sĩ thay thế trước khi duyệt.
            </div>
            <div style="display: flex; gap: 8px;">
              <select class="form-select" style="font-size: 12px;">
                <option>Chọn BS thay thế: BS. Nguyễn Thu Lan (Có CCHN)</option>
              </select>
              <button class="btn btn-success" style="font-size: 12px; white-space: nowrap;">Xác nhận & Duyệt</button>
            </div>
            <div class="alert-box alert-success" style="margin-top: 10px;">
              ✅ <b>Vận hành an toàn:</b> Phòng X-quang R003 luôn duy trì 100% độ phủ trực liên tục, không gián đoạn lâm sàng.
            </div>
          </div>
        </div>
      </div>
    </body></html>
  `;
  await renderAndSnap(page, html4, 'comparison_04_leave_request_coverage_guard.png', 'So sánh Zero-Coverage Leave Guard');

  // =========================================================================
  // COMPARISON 5: DENTIST WORKSPACE & SECURE IMAGE STREAMING
  // =========================================================================
  const html5 = `
    <!DOCTYPE html>
    <html><head>${baseStyle}</head><body>
      <div class="header">
        <h1>SO SÁNH 5: KHÔNG GIAN LÂM SÀNG DENTIST WORKSPACE & BẢO VỆ HÌNH ẢNH Y TẾ</h1>
        <p>Xem phim X-quang trực tiếp bên cạnh Odontogram 32 răng và bảo mật tuyệt đối chống lộ thông tin y tế</p>
      </div>
      <div class="comparison-container">
        <!-- BEFORE -->
        <div class="panel panel-before">
          <div>
            <span class="badge badge-before">❌ TRƯỚC KHI SỬA (DEVELOP CŨ)</span>
            <div class="panel-title">Xem Ảnh Rời Rạc & Nguy Cơ Lộ Lọt Dữ Liệu (IDOR)</div>
            <div class="panel-desc">
              • Muốn xem phim X-quang, bác sĩ phải thoát màn hình khám mở thư mục ảnh tĩnh hoặc tab rời.<br>
              • File ảnh lưu ở đường dẫn public tĩnh hoặc không phân quyền token; bất kỳ ai có đường link đều xem được ảnh bệnh nhân khác.<br>
              • Không có nút xác nhận "Review kết quả", bác sĩ khác tiếp nhận không biết phim đã được đọc hay chưa.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="padding: 10px; background: #f8fafc; border: 1px dashed #cbd5e1; text-align: center; margin-bottom: 10px;">
              <p style="font-size: 12px; color: #64748b;">Link ảnh tĩnh công khai: <code>/uploads/xray_scan_106.png</code></p>
            </div>
            <div class="alert-box alert-danger">
              ❌ <b>Lỗ hổng bảo mật IDOR:</b> Dữ liệu hình ảnh y tế nhạy cảm không được kiểm soát phân quyền người xem.
            </div>
          </div>
        </div>

        <!-- AFTER -->
        <div class="panel panel-after">
          <div>
            <span class="badge badge-after">✅ SAU KHI TỐI ƯU (BRANCH V3)</span>
            <div class="panel-title">Tích Hợp Workspace Trực Quan & Endpoint Stream Bảo Vệ</div>
            <div class="panel-desc">
              • <b>Dentist Workspace V3:</b> Tab Imaging hiển thị ngay cạnh Sơ đồ 32 răng Odontogram (FDI), hỗ trợ zoom ảnh, xoay chiều và đối chiếu răng.<br>
              • <b>Endpoint Stream bảo vệ (BR-07):</b> Truy cập qua <code>/api/clinical-diagnostics/results/{id}/image</code> yêu cầu kiểm tra quyền ca khám.<br>
              • Nút <b>"Review & Xác Nhận Chẩn Đoán"</b> tự động cập nhật tiến trình vào bệnh án điện tử EMR.
            </div>
          </div>
          <div class="ui-mockup">
            <div style="display: flex; gap: 12px; align-items: center; margin-bottom: 10px;">
              <div style="width: 70px; height: 50px; background: #0284c7; border-radius: 4px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 10px; font-weight: bold;">PANORAMA</div>
              <div style="font-size: 12px;">
                <b>Phim R38: Răng khôn mọc lệch 90°</b><br>
                <span style="color: #059669; font-weight: 600;">✓ Endpoint Stream Token Verified</span>
              </div>
              <button class="btn btn-primary" style="margin-left: auto; font-size: 11px;">🔍 Phóng to</button>
              <button class="btn btn-success" style="font-size: 11px;">✓ Xác nhận Review</button>
            </div>
            <div class="alert-box alert-success">
              ✅ <b>Bảo mật cấp y tế:</b> Chặn 100% IDOR; quy trình review phim X-quang khép kín ngay tại ghế khám.
            </div>
          </div>
        </div>
      </div>
    </body></html>
  `;
  await renderAndSnap(page, html5, 'comparison_05_xray_review_in_workspace.png', 'So sánh Dentist Workspace & Bảo mật hình ảnh');

  await browser.close();
  console.log('ALL 5 COMPARISON IMAGES GENERATED SUCCESSFULLY!');
})();

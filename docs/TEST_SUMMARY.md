# DentalCare SWP391 — Automated Test Suite & Regression Verification Summary

**Date:** November 18, 2026
**Repository Branch:** `develop`
**Git HEAD:** `9697849ff390b90ef4d0add411bbd4edadb1659c`
**Testing Environment:** Windows 11, .NET 8.0 SDK (`9.0.301`), SQL Server Express (`.\SQLEXPRESS`)
**Verified Database:** `DentalCare_SWP391_DEMO_V2_TEST`
**Regression Pass Rate:** **100% (803 / 803 Passed)**

---

## 1. AUTOMATED TEST SUITE SUMMARY

| Test Project | Framework | Total Tests | Passed | Failed | Skipped | Pass Rate | Key Areas Covered |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **`BE/DentalClinic.Tests`** | xUnit / .NET 8 | **452** | 452 | 0 | 0 | **100%** | Auth, 4-Role RBAC, Booking Engine, Diagnostics, CBCT Mapping, Clinical Entries, Billing XOR, Medicine, Treatment Plans, Follow-up, Workforce Scheduling |
| **`FE/DentalClinic.Web.Tests`** | xUnit / .NET 8 | **351** | 351 | 0 | 0 | **100%** | Admin Controllers, Dentist UI Workflows, Receptionist Queue & Scheduling, Patient Appointment Flow, Imaging Web UI |
| **TOTAL SOLUTION** | | **803** | **803** | **0** | **0** | **100%** | **Full System Regression Passed** |

---

## 2. BREAKDOWN OF VERIFIED TEST DOMAINS

### 2.1. Clinical Diagnostics & X-Ray Type Mapping (`DiagnosticTypeMappingTests.cs`)
- **Panorama:** Input `"Panorama"` or `"XR-PANO"` strictly resolves to service `XR-PANO`, canonical type `"Panorama"`, and unit price 180,000 VND / 80,000 VND.
- **Cephalometric:** Input `"Cephalometric"` or `"XR-CEPH"` strictly resolves to service `XR-CEPH`, canonical type `"Cephalometric"`, and unit price 220,000 VND / 180,000 VND.
- **Periapical / Intraoral:** Input `"Periapical"`, `"Intraoral"`, or `"XR-PERI"` strictly resolves to `XR-PERI` and unit price 90,000 VND / 100,000 VND.
- **Bitewing:** Input `"Bitewing"` or `"XR-BITE"` strictly resolves to `XR-BITE` and unit price 100,000 VND / 120,000 VND.
- **CBCT Strict Rejection:** Requesting `"CBCT"` or `"XR-CBCT"` when no active CBCT service is configured strictly throws `InvalidOperationException` with explicit Vietnamese message.
- **Clinical Treatment Fallback Guard:** Never falls back to non-imaging clinical treatment services (e.g. `GEN-EXAM`, `GEN-FILLING`).

### 2.2. Operations & Operational Safety (`RealSqlServerOperationalSafetyIntegrationTests.cs`)
- Real SQL Server E2E pipeline for dedicated X-ray dentist workflow: order creation $\to$ queue assignment $\to$ imaging upload $\to$ result retrieval.
- Dedicated X-ray dentist duty enforcement: only on-duty certified dentists assigned to Room R003 can start sessions.
- Room exclusivity and chair allocation safety constraints.

### 2.3. Booking & Vietnam Timezone Integrity (`BookingCoreIntegrityTests.cs`, `VietnamTimezoneStandardizationTests.cs`)
- Slot expansion and duration calculation for single-service, multi-service, and combos.
- Double-booking prevention via SQL app locks.
- Wall-clock time preservation across UTC day transitions (UTC+7 SE Asia Standard Time).

### 2.4. Billing & Financial Integrity (`InvoiceAndPaymentIntegrityTests.cs`, `ClinicalBillingAcceptanceTests.cs`)
- Enforcement of `CK_InvoiceItems_SourceLink` XOR constraint (performed service vs. prescription item).
- Multi-service invoice total calculation (Subtotal - Discount + Tax = FinalAmount).
- Strict payment allocation: 37 Paid invoices (116,630,000 VND) + 7 PartiallyPaid invoices (3,730,000 VND) = 120,360,000 VND total payments.

---

## 3. VERIFIED API & BROWSER ENDPOINTS

1. **Authentication API:** `POST /api/auth/login` verified for all 4 roles (`SystemAdministrator`, `Receptionist`, `Dentist`, `Patient`).
2. **Booking Services API:** `GET /api/booking/services` returns 12 active catalog services with correct pricing and duration.
3. **Web Portal Routes:**
   - Home / Landing: `https://localhost:7129/` (HTTP 200)
   - Account Login: `https://localhost:7129/Account/Login` (HTTP 200)
   - Dentist Portal: `https://localhost:7129/Dentist/Appointments` (HTTP 302 unauthenticated / 200 authenticated)
   - Receptionist Portal: `https://localhost:7129/Receptionist/Appointments` (HTTP 302 unauthenticated / 200 authenticated)
   - Admin Portal: `https://localhost:7129/Admin/Dashboard` (HTTP 302 unauthenticated / 200 authenticated)

---

## 4. KNOWN LIMITATIONS & SCOPE BOUNDARIES

1. **Cone Beam CT (CBCT 3D):** Hardware scanner is not present at the physical clinic. The software correctly rejects CBCT orders with descriptive clinical feedback and disables the option on the Web UI.
2. **Historical Fixture Rows:** Historical rows from 01/11/2026 to 17/11/2026 represent normalized fixture datasets used for reporting and baseline verification; live interactive tests should use demo day appointments (#55–#62) on 19/11/2026.

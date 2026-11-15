# DENTALCARE SWP391 — FINAL OFFICIAL SUBMISSION PACKAGE
## ONE-PASS SOURCE EXTRACTION, REQUIREMENTS, DESIGN, TRACKING, ISSUE HISTORY & FINAL RELEASE DOCUMENTATION

**Project Title:** DentalCare Clinic Management System  
**Course / Subject:** SWP391 — Software Development Project  
**Institution:** FPT University  
**Group:** Group 6  
**Git Remote:** `https://github.com/kaybern1512/SWP391_Group6`  
**Git Branch:** `develop`  
**Verified Git Commit:** `074f0bd9f9114a3ef4bf3bc9fa30be0f2e64bc22`  
**Verification Date:** November 15, 2026  

---

## 1. Official FPT University Submission Deliverables

All six deliverables have been populated in Microsoft Office formats (.xlsx / .docx) using the official FPT University templates, containing 100% verified project evidence from the DentalCare codebase:

| # | File Name | Format | Size | Scope & Key Contents |
|---|---|---|---|---|
| **01** | [`01_DentalCare_AI_Usage_Report_FINAL.xlsx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/01_DentalCare_AI_Usage_Report_FINAL.xlsx) | Excel (.xlsx) | ~66 KB | Preserved `0.Overview`, weekly AI usage log sheets (`1. Week 1`, `2. Week 2`), and `Instruction `. Populated with 15 repository-evidenced AI activities across SDLC phases (citing `CLAUDE.md`, `AGENTS.md`, `.gitnexus`, `dentalcare-testing-sync`, and commits). Unverified student codes and ratings marked `USER INPUT REQUIRED`. |
| **02** | [`02_DentalCare_Project_Tracking_FINAL.xlsx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/02_DentalCare_Project_Tracking_FINAL.xlsx) | Excel (.xlsx) | ~30 KB | Preserved `Project`, `Iter1`, `Iter2`, `Iter3`, and `Iter4` sheets. Tracks 60 project functions across all 4 roles with dynamic formulas (`ROW()-3`, `ROW()-5`), honest committer identities, exact SRS (`II.x / UC-...`) and SDS (`II.x`) cross-references, and verified statuses. |
| **03** | [`03_DentalCare_RDS_FINAL.docx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/03_DentalCare_RDS_FINAL.docx) | Word (.docx) | ~2.67 MB | Complete Requirement & Design Specification: FPT cover logo, Record of Changes, Actors (4 roles), Use Cases (5 embedded diagrams `uc_00`..`uc_04` + 39 catalog rows), Screen Navigation Flows (4 diagrams `flow_01`..`flow_04`), Screen Descriptions (40 screens), Screen Authorization Matrix, 8 Non-UI functions, High-Level Database Design (embedded `erd_00_overview` + 38 table summary), Code Packages (embedded `pkg_01_architecture` + 7 projects), detailed Use Case specifications, UI design specifications with embedded real screenshots, and Appendix with 28 business rules (`BR-001`..`BR-028`). |
| **04** | [`04_DentalCare_SDS_FINAL.docx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/04_DentalCare_SDS_FINAL.docx) | Word (.docx) | ~6.62 MB | Complete Software Design Specification: FPT cover logo, Record of Changes, Code Packages, Database Design (6 embedded ERD diagrams `erd_00`..`erd_05` + full 38-table Data Dictionary with columns, data types, nullability, PKs, FKs, and check constraints), and complete Code Designs for all 12 core subsystems (each with embedded Class Diagram `cls_01`..`cls_12`, Class Specifications, embedded Sequence Diagram `seq_01`..`seq_12`, and parameterized SQL / EF Core queries). |
| **05** | [`05_DentalCare_Issues_Report_FINAL.xlsx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/05_DentalCare_Issues_Report_FINAL.xlsx) | Excel (.xlsx) | ~13 KB | Preserved `Issues Report` sheet structure with zero sample GitLab rows. Contains 22 reconstructed defects and documented scope items (`REC-001`..`REC-022`) backed by real GitHub commit URLs (`https://github.com/kaybern1512/SWP391_Group6/commit/...`), committer identities, milestones (`Iter1`..`Iter4`), and affected screens. |
| **06** | [`06_DentalCare_Final_Release_Document_FINAL.docx`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/final/06_DentalCare_Final_Release_Document_FINAL.docx) | Word (.docx) | ~6.84 MB | Complete Final Release Document: FPT cover logo, Record of Changes, 16-row Deliverable Package table, Installation Guides (Windows .NET 8 SDK, SQL Server Express, isolated test database safety rules, `run_demo.ps1` & `stop_demo.ps1`, ports 7350 / 7129, health checks, troubleshooting, zero plaintext passwords), and User Manual featuring 38 step-by-step user workflows across all 4 roles with embedded real UI screenshots. |

---

## 2. Supporting Evidence & Artifact Inventory

The supporting evidence generated directly from source code and SQL Server is located in [`docs/submission/evidence/`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/):

- [`FEATURE_SOURCE_INVENTORY.csv`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/FEATURE_SOURCE_INVENTORY.csv): 40+ features mapped to exact MVC Controllers, Actions, UI Routes, API Controllers, Endpoints, Application Services, Entities, Business Rules (`BR-001`..`BR-028`), and Test references.
- [`CANONICAL_DATABASE_INVENTORY.md`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/CANONICAL_DATABASE_INVENTORY.md): Comprehensive inventory of all 38 physical tables, columns, primary keys, 125 foreign key columns, unique indexes, and 80 check constraints extracted from `DentalCare_SWP391_UAT_Fixture_TEST`.
- [`IMPLEMENTATION_HISTORY.md`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/IMPLEMENTATION_HISTORY.md): 434-commit implementation timeline across Iterations 1 through 4, committer distribution on `develop`, and milestone summaries.
- [`TEST_EVIDENCE_INDEX.md`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/TEST_EVIDENCE_INDEX.md): Index of 648 automated .NET tests, 53 discrete Playwright browser tests across 7 spec files, and the verified 18-step clinical X-ray journey.
- [`DOCUMENT_TRACEABILITY_MATRIX.md`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/DOCUMENT_TRACEABILITY_MATRIX.md): Bidirectional traceability between Requirements (RDS), Software Design (SDS), Database Entities, Source Code, Automated Tests, and User Manual workflows.
- [`screenshots/`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/screenshots/): 36 verified UI screenshots captured from live application execution on port 7129 (including all 18 demo journey steps) + [`SCREENSHOT_MANIFEST.md`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/screenshots/SCREENSHOT_MANIFEST.md).
- [`diagrams/`](file:///D:/SWP391/DentalCare-DB-Simplification/docs/submission/evidence/diagrams/): 40 Mermaid `.mmd` source files and 40 rendered `.png` diagrams embedded into the Word documents.

---

## 3. System Quality Gates & Verification Status

1. **Automated .NET Test Suite:**
   - **Backend (`DentalClinic.Tests`):** 309/309 PASS (100%)
   - **Frontend (`DentalClinic.Web.Tests`):** 339/339 PASS (100%)
   - **Total .NET Automated Tests:** **648/648 PASS** (0 failures, 0 skipped)
2. **Browser End-to-End Suite (Playwright):**
   - **Total Discrete Browser Tests:** **53/53 PASS** across 7 spec files
   - **Clinical Demonstration Journey:** **18/18 sequential steps verified and captured**
3. **Canonical Architecture:**
   - **Database:** Microsoft SQL Server, Database-First, exactly 38 physical tables
   - **User Roles:** Exactly 4 roles (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`) enforced by `CK_UserAccounts_Role`. Legacy `DepartmentManager` permanently removed; X-Ray Dentist is a `Dentist` with `CanPerformXray = 1`.
4. **Environment & Security Protections:**
   - Protected databases `DentalCare_SWP391` and `DentalCare_SWP391_Refactor_UAT` were **NEVER modified**.
   - Zero plaintext credentials in any generated document or repository file.
   - Demo launcher (`run_demo.ps1`) targets `DentalCare_SWP391_UAT_Fixture_TEST` exclusively.

---

## 4. Academic Integrity Checklist (User Input Required)

To preserve academic integrity and university grading compliance, the following institutional and personal fields are marked with `USER INPUT REQUIRED` across the deliverables. Group members should verify and update these fields before submission:

- [ ] **Class Code** (e.g., `SE18xx`): In `01_DentalCare_AI_Usage_Report_FINAL.xlsx` (cell B4), `03_DentalCare_RDS_FINAL.docx`, `04_DentalCare_SDS_FINAL.docx`, and `06_DentalCare_Final_Release_Document_FINAL.docx`.
- [ ] **Semester** (e.g., `Fall 2026`): In `01_DentalCare_AI_Usage_Report_FINAL.xlsx` (cell B5) and Word document cover pages.
- [ ] **Lecturer Name**: In `01_DentalCare_AI_Usage_Report_FINAL.xlsx` (cell B6) and Word document cover pages.
- [ ] **Student Information Table**:
  - In `01_DentalCare_AI_Usage_Report_FINAL.xlsx` (`0.Overview`, rows 12–16) and cover pages of `03`, `04`, and `06`:
    - Student 1: Fill in Student Code & Full Legal Name (Committer in Git: `kaybern1512` / `vovandong1512@gmail.com`).
    - Student 2: Fill in Student Code & Full Legal Name (Committer in Git: `buiminhduc1403-ctrl` / `buiminhduc1403@gmail.com`).
    - Student 3: Fill in Student Code & Full Legal Name (Committer in Git: `M` / `manhduhnee@gmail.com`).
    - Student 4: Fill in Student Code & Full Legal Name (Committer in Git: `Thanh` / `thanh@dentalcare.com`).
    - Student 5: Fill in Student Code & Full Legal Name.
- [ ] **AI Usage Report — Value Added & Student Validation**:
  - In `01_DentalCare_AI_Usage_Report_FINAL.xlsx` (`1. Week 1` and `2. Week 2`, Columns F & I): Assign each member's subjective assessment of AI usefulness (`Value Added (1-5)`) and personal validation notes.

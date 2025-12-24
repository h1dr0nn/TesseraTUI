# 🚀 Tessera – CSV Editor — Project Roadmap

Modern cross-platform **CSV Editor** built with **Avalonia (.NET)**

---

## 1. Goals

- Giao diện hiện đại
- Hỗ trợ đầy đủ 3 chế độ xem & chỉnh sửa:
  - **Table View** — chỉnh cell trực tiếp
  - **Schema View** — nhận diện type
  - **JSON View** — chỉnh JSON và đồng bộ hai chiều.
- Unified Data Core thống nhất
- Kiến trúc mở rộng: công thức
- Build đa nền tảng

---

## 2. Architecture Overview

```Avalonia Desktop App
├── UI Layer (MVVM)
│   ├── Table View (Grid)
│   ├── Schema View (Column Rules)
│   └── JSON View (Text Editor)
│
├── Unified Data Core (.NET)
│   ├── TableModel
│   ├── SchemaModel
│   ├── JSONModel
│   └── ValidationEngine
│
└── FileIO Services
    ├── CSV Parser / Writer
    ├── JSON Parser / Writer
    └── Schema Storage
```

---

## 3. Milestones

---

### 🧩 **Phase 1 — Foundation Setup**

**Status: Completed**

- [x] Init repo: `/app`
- [x] Setup Avalonia project (MVVM).
- [x] Tạo Unified Data Core (TableModel
- [x] Viết CSV loader cơ bản (dòng → cell).
- [x] Thiết kế cơ chế đồng bộ giữa các model (Table ↔ Schema ↔ JSON).
- [x] ValidationEngine sơ bộ.

---

### 🎨 **Phase 2 — Modern UI/UX Design**

**Status: Completed**

- [x] Sidebar chuyển tab view.
- [x] Header bar: file name
- [x] Light/Dark mode.
- [x] Rounded corners + shadow + subtle transitions.
- [x] Smooth resize layout.
- [x] Error toast + validation feedback.

---

### 🏗 **Phase 3 — Table View (Editable Grid)**

**Status: Completed**

- [x] Virtualized DataGrid cho dataset lớn.
- [x] Edit cell inline.
- [x] Sync thay đổi sang TableModel và ngược lại.
- [x] Highlight lỗi type/constraint.
- [x] Undo/redo đơn giản.
- [x] Copy/paste & keyboard navigation.

---

### 📐 **Phase 4 — Schema View (Types & Rules)**

**Status: Completed**

- [x] Schema inference:
  - string / int / float / bool / date
- [x] UI chỉnh:
  - tên cột
  - type
  - nullable
  - min/max (numeric)
  - sample value + distinct count
- [x] Validate toàn bảng khi chỉnh schema.
- [x] Rollback khi lỗi.

---

### 🧾 **Phase 5 — JSON View (Realtime Sync)**

**Status: Completed**

- [x] JSON editor with syntax highlight.
- [x] Validate JSON structure.
- [x] Apply → sync TableModel nếu hợp lệ.
- [x] Diff mini trước khi commit.
- [x] Highlight key mismatch.

---

### 🧪 **Phase 6 — Testing & Stability**

**Status: Completed**

- [x] Unit test cho Unified Data Core.
- [x] Test đồng bộ 3 view.
- [x] Stress test CSV lớn.
- [x] Kiểm tra UI trên 3 OS.
- [x] Bộ test validate schema.

---

### 🚀 **Phase 7 — First Release (v0.1.0)**

**Status: Completed**

- [x] Build self-contained cho Windows/macOS/Linux.
- [x] Final UX polish:
  - Animation
  - Error display
  - Basic preferences
- [x] Tạo icon + branding cho Tessera.
- [x] Tối ưu start-up time.
- [x] Publish GitHub Release + changelog.

---

### 🧮 **Phase 8 — Formula Engine (Basic)**

**Status: Completed** ✅

- [x] Công thức cơ bản:
  - [x] SUM
  - [x] AVG
  - [x] MIN/MAX
  - [x] COUNT
  - etc
- [x] Support reference kiểu column-based.
- [x] Recalc khi cell thay đổi.
- [x] Circular detection đơn giản.
- [x] View preview kết quả ngay trong Table.
- [x] Tách phần tính toán nặng sang module Rust tăng tốc:
  - [x] Create `Tessera.Native` (Rust)
  - [x] Export basic formula functions (FFI)
  - [x] C# wrapper trong Unified Data Core
  - [ ] Benchmark hiệu năng so với C# thuần (sẽ làm trong Phase 10)
- [x] Fallback gracefully nếu module native không chạy.

---

### 🔧 **Phase 9 — Unity 6 Integration**

**Status: Completed** ✅

- [x] Convert Unified Data Core thành package dùng được trong Unity (.NET Standard 2.1).
- [x] Build riêng module Rust (`Tessera.Native`) cho Unity:
  - [x] Windows `.dll`
- [x] Tạo `/UnityTessera/package.json`.
- [x] Unity EditorWindow với Table View cơ bản:
  - [x] Load CSV file
  - [x] Display table grid
  - [x] Inline cell editing
  - [x] Save changes
  - [x] Schema View
  - [x] JSON View
- [x] Core integration với ValidationAgent, SchemaAgent, CsvAgent.

---

### 🛡️ **Phase 10 — Stability & Unity Integration Completion**

**Status: In Progress** ✅

**Bug Fixes & Improvements:**
- [x] Fix: File JSON khi edit và save bị lưu thành CSV thay vì giữ nguyên format JSON.
- [x] Fix: App đọc được file không hợp lệ (không phải CSV/JSON) → cần filter file type.
- [ ] Fix: Edit state không tắt khi click ra ngoài UI. (Unity)
- [x] Fix: Border bo góc bị mất ở Table và Schema views. (Avalonia)
- [x] Improvement: Natural file sort (1, 2, 10 instead of 1, 10, 2).
- [x] Improvement: Add "New File/Folder" to header menu.
- [x] Improvement: Add "Rename" and "Open in Explorer" to context menu.
- [x] Fix: App Start file association handling.
- [x] Improvement: File type filtering setting (CSV/JSON only).
- [x] Improvement: File search bar in Explorer.
- [x] Fix: Settings sidebar không hiện section mới. (User feedback)
- [x] Improvement: Inline rename thay vì popup. (User feedback)
- [x] Fix: Edit mode (DataGrid) không tắt khi click ra ngoài. (Avalonia - User feedback)
- [x] Performance: Search bar bị lag → cần debounce. (User feedback)

**Remaining Unity Integration:**
- [ ] Build module Rust cho các platform khác:
  - [ ] macOS `.dylib`
  - [ ] Linux `.so`
- [ ] Import CSV → ScriptableObject theo schema.
- [ ] Validate CSV khi build.
- [ ] Đồng bộ loại dữ liệu & schema từ editor sang runtime.
- [ ] Benchmark hiệu năng Rust vs C# thuần (từ Phase 8).

---

### 🌐 **Phase 11 — Advanced Ecosystem & Pro Features**

**Status: Pending**

- [ ] Plugin architecture.
- [ ] Macro / automation.
- [ ] Advanced formulas & built-in functions (Rust-accelerated).
- [ ] Mở rộng native module:
  - [ ] diff engine
  - [ ] search/indexing engine
  - [ ] vectorized numeric ops
- [ ] Export thêm: SQL
- [ ] Cloud sync schema (optional).
- [ ] CLI hỗ trợ convert CSV ↔ JSON ↔ schema (reuse Unified Data Core + Rust module).
- [ ] Template system cho game data pipelines.
- [ ] Workspace: lưu nhiều bảng + schema + liên kết (multi-table projects).

---

## 4. Success Criteria

- UI hiện đại
- 3 view đồng bộ 100% chính xác.
- Validation an toàn: sửa ở view nào cũng không gây lỗi dữ liệu.
- Build ổn định đa nền tảng.
- Unity Editor tích hợp trơn tru.
- Hệ thống mở rộng lâu dài (formula, plugin, tooling).

---

## 5. Long-term Vision

Tessera không chỉ là CSV Editor
mà là **nền tảng quản lý dữ liệu hiện đại dành cho game developer và technical artists**:

- Schema rõ ràng → giảm lỗi runtime.
- UI trực quan → thao tác nhanh hơn Excel/Sheets.
- Tích hợp Unity sâu → tối ưu workflow sản xuất game.
- Kiến trúc modular → mở rộng thành bộ công cụ quản lý dữ liệu mạnh mẽ.

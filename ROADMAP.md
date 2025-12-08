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

**Status: Pending**

- [ ] Công thức cơ bản:
  - SUM
- [ ] Support reference kiểu column-based.
- [ ] Recalc khi cell thay đổi.
- [ ] Circular detection đơn giản.
- [ ] View preview kết quả ngay trong Table.
- [ ] Tách phần tính toán nặng sang module Rust tăng tốc:
  - [ ] Create `Tessera.Native` (Rust)
  - [ ] Export basic formula functions (FFI)
  - [ ] C# wrapper trong Unified Data Core
  - [ ] Benchmark hiệu năng so với C# thuần
- [ ] Fallback gracefully nếu module native không chạy.

---

### 🔧 **Phase 9 — Unity 6 Integration**

**Status: Pending**

- [ ] Convert Unified Data Core thành package dùng được trong Unity.
- [ ] Build riêng module Rust (`Tessera.Native`) cho Unity:
  - [ ] Windows `.dll`
  - [ ] macOS `.dylib`
  - [ ] Linux `.so`
- [ ] Tạo `/UnityTessera/package.json`.
- [ ] Unity EditorWindow có:
  - Table View
  - Schema View
  - JSON View
- [ ] Import CSV → ScriptableObject theo schema.
- [ ] Validate CSV khi build.
- [ ] Đồng bộ loại dữ liệu & schema từ editor sang runtime.
- [ ] Cho phép custom validator của Unity hook vào ValidationAgent.

---

### 🌐 **Phase 10 — Advanced Ecosystem & Pro Features**

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
- Hệ thống mở rộng lâu dài (formula

---

## 5. Long-term Vision

Tessera không chỉ là CSV Editor
mà là **nền tảng quản lý dữ liệu hiện đại dành cho game developer và technical artists**:

- Schema rõ ràng → giảm lỗi runtime.
- UI trực quan → thao tác nhanh hơn Excel/Sheets.
- Tích hợp Unity sâu → tối ưu workflow sản xuất game.
- Kiến trúc modular → mở rộng thành bộ công cụ quản lý dữ liệu mạnh mẽ.

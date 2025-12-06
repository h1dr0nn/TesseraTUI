# 🚀 Tessera – CSV Editor — Project Roadmap

Modern cross-platform **CSV Editor** built with **Avalonia (.NET)**, featuring a clean modern UI and powerful multi-view editing: **Table View**, **Schema View**, and **JSON View** — all synchronized in real time with strict validation.

---

## 1. Goals

- Giao diện hiện đại, tối giản, cảm giác native trên Windows/macOS/Linux.
- Hỗ trợ đầy đủ 3 chế độ xem & chỉnh sửa:
  - **Table View** — chỉnh cell trực tiếp, virtualization mượt.
  - **Schema View** — nhận diện type, chỉnh rule, validate toàn bảng.
  - **JSON View** — chỉnh JSON và đồng bộ hai chiều.
- Unified Data Core thống nhất, dùng lại trong Unity Editor (phase 8+).
- Kiến trúc mở rộng: công thức, plugin, tooling nâng cao.
- Build đa nền tảng, dễ phát hành bản stable.

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

**Status: Pending**

- [ ] Init repo: `/app`, `/core`, `/tests`.
- [ ] Setup Avalonia project (MVVM).
- [ ] Tạo Unified Data Core (TableModel, SchemaModel, JSONModel).
- [ ] Viết CSV loader cơ bản (dòng → cell).
- [ ] Thiết kế cơ chế đồng bộ giữa các model (Table ↔ Schema ↔ JSON).
- [ ] ValidationEngine sơ bộ.

---

### 🎨 **Phase 2 — Modern UI/UX Design**

**Status: Pending**

- [ ] Sidebar chuyển tab view.
- [ ] Header bar: file name, save, reload, status.
- [ ] Light/Dark mode.
- [ ] Rounded corners + shadow + subtle transitions.
- [ ] Smooth resize layout.
- [ ] Error toast + validation feedback.

---

### 🏗 **Phase 3 — Table View (Editable Grid)**

**Status: Pending**

- [ ] Virtualized DataGrid cho dataset lớn.
- [ ] Edit cell inline.
- [ ] Sync thay đổi sang TableModel và ngược lại.
- [ ] Highlight lỗi type/constraint.
- [ ] Undo/redo đơn giản.
- [ ] Copy/paste & keyboard navigation.

---

### 📐 **Phase 4 — Schema View (Types & Rules)**

**Status: Pending**

- [ ] Schema inference:
  - string / int / float / bool / date
- [ ] UI chỉnh:
  - tên cột
  - type
  - nullable
  - min/max (numeric)
  - sample value + distinct count
- [ ] Validate toàn bảng khi chỉnh schema.
- [ ] Rollback khi lỗi.

---

### 🧾 **Phase 5 — JSON View (Realtime Sync)**

**Status: Pending**

- [ ] JSON editor with syntax highlight.
- [ ] Validate JSON structure.
- [ ] Apply → sync TableModel nếu hợp lệ.
- [ ] Diff mini trước khi commit.
- [ ] Highlight key mismatch.

---

### 🧪 **Phase 6 — Testing & Stability**

**Status: Pending**

- [ ] Unit test cho Unified Data Core.
- [ ] Test đồng bộ 3 view.
- [ ] Stress test CSV lớn.
- [ ] Kiểm tra UI trên 3 OS.
- [ ] Bộ test validate schema.

---

### 🚀 **Phase 7 — First Release (v0.1.0)**

**Status: Pending**

- [ ] Build self-contained cho Windows/macOS/Linux.
- [ ] Final UX polish:
  - Animation
  - Error display
  - Basic preferences
- [ ] Tạo icon + branding cho Tessera.
- [ ] Tối ưu start-up time.
- [ ] Publish GitHub Release + changelog.

*(Đây là bản đầu tiên đủ tính năng để dùng thật.)*

---

### 🧮 **Phase 8 — Formula Engine (Basic)**

**Status: Pending**

- [ ] Công thức cơ bản:
  - SUM, AVG, COUNT, MIN, MAX
- [ ] Support reference kiểu column-based.
- [ ] Recalc khi cell thay đổi.
- [ ] Circular detection đơn giản.
- [ ] View preview kết quả ngay trong Table.

---

### 🔧 **Phase 9 — Unity 6 Integration**

**Status: Pending**

- [ ] Convert Unified Data Core thành package dùng được trong Unity.
- [ ] Tạo `/UnityTessera/package.json`.
- [ ] Unity EditorWindow có:
  - Table View
  - Schema View
  - JSON View
- [ ] Import CSV → ScriptableObject theo schema.
- [ ] Validate CSV khi build.

---

### 🌐 **Phase 10 — Advanced Ecosystem & Pro Features**

**Status: Pending**

- [ ] Plugin architecture.
- [ ] Macro / automation.
- [ ] Advanced formulas & functions.
- [ ] Export thêm: SQL, Parquet, Markdown.
- [ ] Cloud sync schema (optional).
- [ ] CLI hỗ trợ convert CSV ↔ JSON ↔ schema.
- [ ] Template system cho game data pipelines.

---

## 4. Success Criteria

- UI hiện đại, mượt, dễ sử dụng.
- 3 view đồng bộ 100% chính xác.
- Validation an toàn: sửa ở view nào cũng không gây lỗi dữ liệu.
- Build ổn định đa nền tảng.
- Unity Editor tích hợp trơn tru.
- Hệ thống mở rộng lâu dài (formula, plugin, tooling).

---

## 5. Long-term Vision

Tessera không chỉ là CSV Editor,  
mà là **nền tảng quản lý dữ liệu hiện đại dành cho game developer và technical artists**:

- Schema rõ ràng → giảm lỗi runtime.  
- UI trực quan → thao tác nhanh hơn Excel/Sheets.  
- Tích hợp Unity sâu → tối ưu workflow sản xuất game.  
- Kiến trúc modular → mở rộng thành bộ công cụ quản lý dữ liệu mạnh mẽ.

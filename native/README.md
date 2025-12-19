# Tessera Native - Rust Formula Engine

Rust-accelerated formula calculation engine for Tessera CSV Editor.

## 🚀 Build cho Unity (Windows)

### Yêu cầu:
- ✅ [Rust](https://www.rust-lang.org/tools/install) (1.70+)
- ✅ Visual Studio 2022 với **Desktop development with C++** workload

### Cách build:

**Từ thư mục gốc dự án:**
```powershell
cd native
PowerShell -ExecutionPolicy Bypass -File .\build_unity_native.ps1
```

**Hoặc từ Developer PowerShell for VS 2022:**
```powershell
cd D:\Game Projects\Repositories\TesseraTUI\native
.\build_unity_native.ps1
```

Script sẽ tự động:
- Tìm Visual Studio installation
- Setup VS environment
- Build Rust native module
- Copy DLL vào `UnityTessera/Runtime/Plugins/x86_64/tessera_native.dll`

### Nếu gặp lỗi:

**Lỗi "cannot open input file 'kernel32.lib'":**
- Mở **Developer PowerShell for VS 2022** từ Start Menu
- Chạy lại script từ đó

**Lỗi "linker link.exe not found":**
- Cài Visual Studio Installer
- Thêm workload: **Desktop development with C++**
- Chọn: MSVC v143 và Windows SDK

---

## 📦 Build cho Avalonia App (Tất cả Platforms)

### Windows:
```powershell
cd native
cargo build --release --target x86_64-pc-windows-msvc
# Output: target/x86_64-pc-windows-msvc/release/tessera_native.dll
```

### macOS:
```bash
cd native
cargo build --release
# Output: target/release/libtessera_native.dylib
```

### Linux:
```bash
cd native
cargo build --release
# Output: target/release/libtessera_native.so
```

Copy DLL vào thư mục output của ứng dụng (cùng thư mục với `app.dll` hoặc executable).

---

## 🔧 FFI API

- `tessera_sum` - Tính tổng cột
- `tessera_avg` - Tính trung bình cột
- `tessera_min` / `tessera_max` - Min/Max cột
- `tessera_count` - Đếm giá trị
- `tessera_parse_formula` - Parse công thức (e.g., "=SUM(ColumnA)")
- `tessera_free_string` - Giải phóng memory từ native functions

---

## 💡 Fallback

Nếu native library không load được, C# code sẽ tự động fallback về pure C# implementation. Ứng dụng vẫn hoạt động bình thường.

---

## ⚡ Performance

Tối ưu cho:
- Datasets lớn (10,000+ rows)
- Vectorized numeric operations
- Minimal memory allocation


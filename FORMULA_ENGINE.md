# Formula Engine - Phase 8 Implementation

## Overview

Phase 8 Formula Engine đã được implement với Rust native module cho hiệu năng cao. Hệ thống hỗ trợ công thức cơ bản với SUM function và có fallback C# nếu Rust module không available.

## Features Implemented

✅ **Basic Formula Support**
- SUM function: `=SUM(ColumnName)`
- Column-based references
- Formula parsing và validation

✅ **Rust Native Module**
- High-performance SUM calculation
- FFI-safe API
- Cross-platform (Windows/macOS/Linux)

✅ **C# Integration**
- FormulaAgent với dependency tracking
- Circular dependency detection
- Automatic recalculation khi cell thay đổi

✅ **UI Integration**
- Display computed results trong TableView
- Formula được lưu trong cell, nhưng hiển thị kết quả
- Error messages cho invalid formulas

✅ **Fallback Support**
- Tự động fallback sang C# implementation nếu Rust không load được
- Seamless experience cho user

## Architecture

```
┌─────────────────┐
│  TableViewModel │
└────────┬────────┘
         │
         v
┌─────────────────┐
│ TableViewAgent  │
└────────┬────────┘
         │
         v
┌─────────────────┐
│ DataSyncAgent   │◄───┐
└────────┬────────┘    │
         │             │
         v             │
┌─────────────────┐    │
│ FormulaAgent    │    │
└────────┬────────┘    │
         │             │
         v             │
┌─────────────────┐    │
│ FormulaNative   │────┘ (P/Invoke)
└────────┬────────┘
         │
         v
┌─────────────────┐
│ tessera_native  │ (Rust DLL)
│  (lib.rs)       │
└─────────────────┘
```

## Usage

### 1. Building Rust Module

**Windows:**
```powershell
.\build-native.ps1
```

**Linux/macOS:**
```bash
chmod +x build-native.sh
./build-native.sh
```

Hoặc manually:
```bash
cd native
cargo build --release
```

### 2. Using Formulas in UI

1. Click vào cell muốn đặt formula
2. Nhập công thức bắt đầu bằng `=`, ví dụ: `=SUM(Price)`
3. Press Enter để apply
4. Cell sẽ hiển thị kết quả tính toán
5. Để edit formula, click vào cell và sửa text

### 3. Formula Syntax

Hiện tại chỉ hỗ trợ:
- `=SUM(ColumnName)` - Tính tổng tất cả giá trị số trong cột

**Example:**
```
Price Column: 10, 20, 30, 40
Total cell (with =SUM(Price)): 100
```

## Implementation Details

### FormulaAgent

- Tracks tất cả formulas trong table
- Maintains dependency graph (hiện tại empty vì column-based formulas không có cell dependencies)
- Provides circular dependency detection
- Triggers recalculation khi cells thay đổi

### FormulaNative (C# Wrapper)

- Safe P/Invoke wrapper cho Rust functions
- Automatic memory management (Marshal strings)
- Graceful fallback nếu DLL không load

### Rust Module (tessera_native)

- FFI exports: `tessera_sum`, `tessera_parse_formula`
- Zero-copy string handling
- Efficient numeric parsing

## Future Enhancements (Phase 10)

- [ ] More functions: AVG, MIN, MAX, COUNT
- [ ] Cell references: `ColumnA[0]` hoặc `A5` style
- [ ] Range references: `SUM(ColumnA, 0, 10)`
- [ ] More complex formulas với operators (`+`, `-`, `*`, `/`)
- [ ] Performance benchmarks

## Testing

Để test formula engine:

1. Mở Tessera và load một CSV file có numeric columns
2. Tạo một cell mới và nhập `=SUM(YourColumnName)`
3. Verify kết quả hiển thị đúng
4. Thay đổi giá trị trong column và verify formula tự động recalculate
5. Test circular dependency bằng cách tạo formula reference chính nó (sẽ fail với error)

## Troubleshooting

**Formula không tính được:**
- Check column name có đúng không (case-insensitive)
- Check column có chứa numeric values không
- Xem error message trong cell

**Rust module không load:**
- Check DLL/dylib/so có trong output directory không
- Application sẽ tự động fallback sang C# implementation
- Check console logs để xem warnings

**Performance issues:**
- Nếu có Rust module, performance sẽ tốt hơn cho large datasets
- C# fallback vẫn đủ nhanh cho dataset vừa phải (<10k rows)


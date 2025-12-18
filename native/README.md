# Tessera Native - Rust Formula Engine

Rust-accelerated formula calculation engine for Tessera CSV Editor.

## Building

### Automated Build (CI/CD)

The Rust native library is automatically built by GitHub Actions for all platforms (Windows, macOS, Linux) on every push. The built libraries are available as artifacts.

### Local Development

#### Prerequisites
- Install [Rust](https://www.rust-lang.org/tools/install)
- Rust 1.70+ recommended

#### Build Commands

**All Platforms:**
```bash
cd native
cargo build --release
```

**Outputs:**
- Windows: `target/release/tessera_native.dll`
- macOS: `target/release/libtessera_native.dylib`
- Linux: `target/release/libtessera_native.so`

## Integration

The C# application will automatically look for the native library:
- Windows: `tessera_native.dll`
- macOS: `libtessera_native.dylib`
- Linux: `libtessera_native.so`

Copy the built library to the application's output directory (alongside `app.dll` or executable).

## FFI API

### `tessera_sum`
Calculate SUM for a column of values.

### `tessera_parse_formula`
Parse formula string (e.g., "=SUM(ColumnA)") into function and arguments.

### `tessera_free_string`
Free memory allocated by native functions.

## Fallback

If the native library cannot be loaded, the C# code will automatically fall back to a pure C# implementation. This ensures the application works even without the Rust module.

## Performance

The Rust implementation is optimized for:
- Large datasets (10,000+ rows)
- Vectorized numeric operations
- Minimal memory allocation

Benchmark results (to be added in Phase 8 testing).


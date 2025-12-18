# Quick build script for native module (run from native directory)
cargo build --release
Write-Host "Build complete. Library location: target\release\tessera_native.dll" -ForegroundColor Green


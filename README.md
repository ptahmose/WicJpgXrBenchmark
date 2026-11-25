# WIC JPEG-XR benchmark

Windows command-line benchmark for JPEG-XR decoding and encoding using WIC
through WPF. It can generate a test image or use an existing JPEG-XR file.

## Usage

Build and run with the .NET 10 SDK on Windows:

```powershell
dotnet run -c Release
dotnet run -c Release -- --encode --lossless
dotnet run -c Release -- --encode-only --input C:\path\to\tile.jxr --lossless
```

By default, the tool benchmarks decoding only. `--encode` adds a separate
encoding benchmark; `--encode-only` skips decode timings. Both use `--warmup`
and `--iterations` for each selected benchmark. Encoding is lossy at quality 0.9
by default; use `--lossless` for lossless compression.

Encoding starts with the generated pixels, or with pixels decoded once from
`--input`. Pixel preparation, hashing and file I/O are outside the timed section.
Each timed iteration creates a WPF `WmpBitmapEncoder` and encodes to memory.
The SHA-256 hash identifies the uncompressed pixel bytes; compare it, along with
the dimensions, format and compression settings, across machines.

`--save` saves the last measured encoding when encoding is enabled; otherwise it
saves the generated JPEG-XR image. Use `--help` for the other options.

## Documentation

- [Performance results](documentation/performance-results.md)
- [WPR trace analysis](documentation/wpr-trace-analysis.md)
- [Packaging a standalone executable](documentation/packaging.md)
- [Publishing a release](documentation/releasing.md)

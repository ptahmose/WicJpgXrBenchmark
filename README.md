# WIC JPEG-XR benchmark

Windows command-line benchmark for JPEG-XR decoding and encoding using WIC
through WPF. It can generate a test image or use an existing JPEG-XR file.

[Performance results](documentation/performance-results.md)

Build and run with the .NET 10 SDK on Windows:

```powershell
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

## Package a standalone executable

From the project directory on Windows, use the .NET 10 SDK to publish and
package the executable:

```powershell
dotnet publish .\WicJpgXrBenchmark.csproj -c Release -r win-x64 --self-contained true -o .\publish
Compress-Archive -Path .\publish\WicJpgXrBenchmark.exe -DestinationPath .\WicJpgXrBenchmark-win-x64.zip -Force
```

The ZIP is suitable for uploading as a GitHub Release asset once a release
version is chosen. It is not committed to the repository. Extract it on a
Windows x64 machine and run, for example:

```powershell
.\WicJpgXrBenchmark.exe --help
.\WicJpgXrBenchmark.exe --encode --lossless
```

The executable includes the .NET runtime and does not require .NET to be
installed, but still uses Windows' WIC codecs. Native components are extracted
at runtime.

# Packaging a standalone executable

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

# JPEG-XR decoding performance observations

Four benchmark runs showed two distinct decoding performance levels. The first
two runs had similar medians of about 51 ms. The third and fourth had medians
of about 117 ms, roughly 2.28 times as long. The results indicate an
update-related regression in this setup.

## Context and method

The first three runs used Windows 11 Enterprise LTSC 2024 in a VM on the same
host, with the same virtual hardware and settings: respectively without
updates, with some updates, and with all updates. The fourth ran on the
physical host machine with Windows 11 Pro, Version 26H2 (OS Build 26300.10075).

Each run used the WIC JPEG-XR benchmark with a generated 2048 x 2048 Gray16
image, lossy quality 0.9 (`lossless=False`). The encoded image was 3,832,040
bytes and decoded to 8,388,608 bytes of pixel data. The benchmark performed
5 warmup and 50 measured decode iterations, using .NET 10.0.15 x64. Each
measured decode creates a decoder and copies all pixels to force the lazy
decode to complete. These are synthetic generated-image decode tests, not
encoding throughput tests.

The image is generated and encoded locally using WIC in each run. The inputs
are considered identical across runs, as confirmed by the submitter.

## Results

| Run and reported environment | Min (ms) | Median (ms) | Mean +/- standard deviation (ms) | P95 (ms) | Max (ms) | Median throughput (MiB/s) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1: LTSC VM, without updates | 49.718 | 51.931 | 52.117 +/- 1.992 | 54.907 | 62.807 | 154.1 |
| 2: LTSC VM, some updates | 48.168 | 51.105 | 51.027 +/- 1.330 | 53.356 | 54.458 | 156.5 |
| 3: LTSC VM, all updates (bad case) | 114.238 | 116.999 | 117.266 +/- 2.069 | 121.829 | 121.829 | 68.4 |
| 4: Windows 11 Pro 26H2, build 26300.10075, host (bad case) | 113.261 | 116.456 | 116.597 +/- 1.397 | 118.570 | 119.316 | 68.7 |

Using run 2 as the baseline:

| Run | Median relative to run 2 | Change in median time | Change in reported median throughput |
| --- | ---: | ---: | ---: |
| 1 | 1.016x | +1.6% | -1.5% |
| 2 | 1.000x | baseline | baseline |
| 3 | 2.289x | +128.9% | -56.3% |
| 4 | 2.279x | +127.9% | -56.1% |

Runs 1 and 2 were close in performance; runs 3 and 4 took over twice as long
and had about 56% lower median throughput than run 2. Percentages use the
displayed median times and reported, rounded throughput values.

## Captured environment

All DLL versions below include the suffix `WinBuild.160101.0800`.

| Run | Reported OS version and UBR | wmpPhoto.dll | WindowsCodecs.dll | Image preparation (ms) |
| --- | --- | --- | --- | ---: |
| 1 | Microsoft Windows NT 10.0.26100.0, UBR 9457 | 10.0.26100.1 | 10.0.26100.9457 | 229.4 |
| 2 | Microsoft Windows NT 10.0.26100.0, UBR 1742 | 10.0.26100.1 | 10.0.26100.1591 | 218.3 |
| 3 | Microsoft Windows NT 10.0.26100.0, UBR 9974 | 10.0.26100.1 | 10.0.26100.9974 | 341.3 |
| 4 | Microsoft Windows NT 10.0.26300.0, UBR 10075 | 10.0.26100.8875 | 10.0.26100.10068 | 212.4 |

The preparation timings vary, but are outside the measured decode iterations;
they do not establish an encoding performance difference. Run 4 reported OS
build 26300 while both codec DLL versions were 26100-based.

The reported update labels do not follow the captured version order: run 1
("without updates") has UBR 9457 and WindowsCodecs.dll 26100.9457, while run 2
("some updates") has UBR 1742 and WindowsCodecs.dll 26100.1591.

## Captured console output

The following text transcribes the benchmark commands and output shown in the
screenshots. Decimal separators are preserved as captured.

### Run 1: LTSC VM, without updates

```text
c:\test>WicJpgXrBenchmark.exe
=== WIC JPEG-XR benchmark ===
OS                : Microsoft Windows NT 10.0.26100.0 (UBR 9457)
Runtime           : .NET 10.0.15, x64
wmpPhoto.dll      : 10.0.26100.1 (WinBuild.160101.0800)
WindowsCodecs.dll : 10.0.26100.9457 (WinBuild.160101.0800)
Generated image   : 2048x2048 Gray16, quality=0.9, lossless=False, preparation 229.4 ms, 3,832,040 encoded bytes
Warmup/Iter.      : 5/50

=== Decoding ===
Decoded           : 2048x2048 Gray16, 8,388,608 bytes of pixel data
Min               : 49.718 ms
Median            : 51.931 ms
Mean ± StdDev     : 52.117 ± 1.992 ms
P95               : 54.907 ms
Max               : 62.807 ms
Throughput        : 154.1 MiB/s (median)
```

### Run 2: LTSC VM, some updates

```text
c:\test>WicJpgXrBenchmark.exe
=== WIC JPEG-XR benchmark ===
OS                : Microsoft Windows NT 10.0.26100.0 (UBR 1742)
Runtime           : .NET 10.0.15, x64
wmpPhoto.dll      : 10.0.26100.1 (WinBuild.160101.0800)
WindowsCodecs.dll : 10.0.26100.1591 (WinBuild.160101.0800)
Generated image   : 2048x2048 Gray16, quality=0.9, lossless=False, preparation 218.3 ms, 3,832,040 encoded bytes
Warmup/Iter.      : 5/50

=== Decoding ===
Decoded           : 2048x2048 Gray16, 8,388,608 bytes of pixel data
Min               : 48,168 ms
Median            : 51,105 ms
Mean ± StdDev     : 51,027 ± 1,330 ms
P95               : 53,356 ms
Max               : 54,458 ms
Throughput        : 156,5 MiB/s (median)
```

### Run 3: LTSC VM, all updates

```text
c:\test>WicJpgXrBenchmark.exe
=== WIC JPEG-XR benchmark ===
OS                : Microsoft Windows NT 10.0.26100.0 (UBR 9974)
Runtime           : .NET 10.0.15, x64
wmpPhoto.dll      : 10.0.26100.1 (WinBuild.160101.0800)
WindowsCodecs.dll : 10.0.26100.9974 (WinBuild.160101.0800)
Generated image   : 2048x2048 Gray16, quality=0.9, lossless=False, preparation 341,3 ms, 3,832,040 encoded bytes
Warmup/Iter.      : 5/50

=== Decoding ===
Decoded           : 2048x2048 Gray16, 8,388,608 bytes of pixel data
Min               : 114,238 ms
Median            : 116,999 ms
Mean ± StdDev     : 117,266 ± 2,069 ms
P95               : 121,829 ms
Max               : 121,829 ms
Throughput        : 68,4 MiB/s (median)
```

### Run 4: Windows 11 Pro 26H2, build 26300.10075, host

```text
PS D:\dev\Zeiss\RMS_Users\Users\JBl\Tests\WicJpgXrBenchmark\publish> .\WicJpgXrBenchmark.exe
=== WIC JPEG-XR benchmark ===
OS                : Microsoft Windows NT 10.0.26300.0 (UBR 10075)
Runtime           : .NET 10.0.15, x64
wmpPhoto.dll      : 10.0.26100.8875 (WinBuild.160101.0800)
WindowsCodecs.dll : 10.0.26100.10068 (WinBuild.160101.0800)
Generated image   : 2048x2048 Gray16, quality=0.9, lossless=False, preparation 212.4 ms, 3,832,040 encoded bytes
Warmup/Iter.      : 5/50

=== Decoding ===
Decoded           : 2048x2048 Gray16, 8,388,608 bytes of pixel data
Min               : 113.261 ms
Median            : 116.456 ms
Mean ± StdDev     : 116.597 ± 1.397 ms
P95               : 118.570 ms
Max               : 119.316 ms
Throughput        : 68.7 MiB/s (median)
```

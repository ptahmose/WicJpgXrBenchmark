# WPR trace comparison

Two Windows Performance Recorder traces, labeled fast and slow, show a longer
benchmark process lifetime in the slow capture. In both, sampled benchmark CPU
activity is concentrated in `WMPhoto.dll`. The slow capture contains many
sampled feature-check and usage-reporting calls in the JPEG-XR decoder path;
none were observed for those functions in the selected fast window.

## Captures and method

Each trace was captured with the Windows Performance Recorder CPU profile in
file mode. In each environment, recording was started, the benchmark was run
once without arguments, and recording was stopped and saved. For example, from
the benchmark executable's directory:

```powershell
wpr -start CPU -filemode
.\WicJpgXrBenchmark.exe
wpr -stop .\jxr_fast.etl
```

The same procedure was used in the slow environment, saving the result as
`jxr_slow.etl`.

The analysis uses `jxr_fast.etl` and `jxr_slow.etl`, processed locally with
Windows Performance Toolkit `xperf`. Both record 12 logical processors and a
1 ms CPU sampling period; neither reports lost events or buffers. The full
captures are 19.424 s and 46.893 s respectively, including time outside the
benchmark. The raw traces are not included here.

Each trace contains one `WicJpgXrBenchmark.exe` process invoked without
arguments. Its observed lifetime was 3.385 s in the fast trace and 7.102 s in
the slow trace, about 2.10 times longer. Process lifetime includes startup and
shutdown, not just measured decode iterations, so it must not be treated as a
per-image decode time.

To compare active benchmark activity without mixing in the rest of each
capture, the following uses equal two-second windows: 4.000-6.000 s from the
start of the fast trace and 6.000-8.000 s from the start of the slow trace.
Both lie inside the respective benchmark process lifetimes and contain
sustained `WMPhoto.dll` samples. They are representative active windows, not
verified boundaries of identical decode iterations.

## Observations

| Metric | Fast | Slow |
| --- | ---: | ---: |
| Benchmark process lifetime | 3.385 s | 7.102 s |
| Benchmark process scheduled CPU in selected window | 2.114 s | 2.039 s |
| Busiest benchmark thread scheduled CPU in selected window | 1.994 s | 1.988 s |
| Benchmark `WMPhoto.dll` sampled CPU weight in selected window | 1.661 s | 1.830 s |

Scheduled process CPU sums all its threads, so it can exceed the two-second
wall-clock window. In each window one benchmark thread was scheduled for almost
the entire interval. The sampled CPU weights are estimates based on samples
whose instruction pointer fell in the module, not elapsed decode times or
counts of completed images. The two windows do not establish the per-image
cost of a particular function.

The benchmark was the largest non-idle process by scheduled CPU in both
selected windows. The next largest process, Windows System, accounted for
approximately 46 ms in the fast window and 14 ms in the slow window across
all processors. These observations do not rule out interference or other
causes, but the measured benchmark activity is not a sum of background
process samples.

## Feature checks and reporting: fast versus slow

The matching public Microsoft PDBs and codec images were loaded into a local
symbol cache. The table compares sampled stack hits for three `WMPhoto.dll`
functions in the selected two-second windows. **Exclusive** hits are samples
executing in the function itself; **inclusive** hits include samples executing
in its callees. The stack report was checked down to one hit.

| Function | Fast exclusive | Fast inclusive | Slow exclusive | Slow inclusive |
| --- | ---: | ---: | ---: | ---: |
| `wil::details::FeatureImpl<__WilFeatureTraits_Feature_1079403834>::__private_IsEnabled` | 0 | 0 | 690 | 1,097 |
| `wil_details_FeatureReporting_RecordUsageInCache` | 0 | 0 | 281 | 281 |
| `wil::details::ReportUsageToService` | 0 | 0 | 78 | 384 |

These are sampled hits, not call counts. Zero means no matching resolved stack
hits were observed in the fast window; it does not prove that the functions
never executed or were absent from the binary. In the slow window,
`__private_IsEnabled` accounts for 33.87% of sampled benchmark-process stack
hits exclusively, and `RecordUsageInCache` accounts for another 13.79%.

## Hot functions and call stacks

The table shows the largest resolved `WMPhoto.dll` functions by exclusive
sampled hits in the selected windows. Percentages are of all sampled
benchmark-process stack hits in the respective window; the process includes
other modules and threads.

| Trace | Function | Exclusive hits | Share |
| --- | --- | ---: | ---: |
| Fast | `DecodeMacroblockHighpass` | 1,200 | 56.87% |
| Fast | `outputNChannel` | 217 | 10.28% |
| Fast | `DecodeMacroblockLowpass` | 89 | 4.22% |
| Slow | `wil::details::FeatureImpl<__WilFeatureTraits_Feature_1079403834>::__private_IsEnabled` | 690 | 33.87% |
| Slow | `DecodeMacroblockHighpass` | 457 | 22.43% |
| Slow | `wil_details_FeatureReporting_RecordUsageInCache` | 281 | 13.79% |
| Slow | `outputNChannel` | 92 | 4.52% |
| Slow | `wil::details::ReportUsageToService` | 78 | 3.83% |

The sampled stacks contain the following simplified decoder path in both
windows. Values are **inclusive** hits: the function appeared anywhere in the
sample's stack, including while a callee was executing.

```text
WMPhoto.dll!CWmpDecoderFrame::CopyPixels_Core    fast 1,816   slow 1,904
  ... WMPhoto.dll!ImageStrDecDecode              fast 1,792   slow 1,895
    WMPhoto.dll!processMacroblockDec             fast 1,557   slow 1,792
      WMPhoto.dll!DecodeMacroblockHighpass        fast 1,295   slow 1,471
```

In the slow window, 945 of the 1,471 highpass inclusive hits also include its
feature-check callee
`wil::details::FeatureImpl<__WilFeatureTraits_Feature_1079403834>::__private_IsEnabled`.
That check also appears under lowpass decoding. Its sampled callees include
`wil::details::ReportUsageToService` and
`wil_details_FeatureReporting_RecordUsageInCache`. Their presence in the slow
decode path is a notable difference, not proof that a specific feature or
update caused the slowdown.

The same feature-check and usage-reporting functions were also prominent in a
second slow window, 8.000-10.000 s: their exclusive sampled weights were
approximately 694 ms for `__private_IsEnabled` and 304 ms for
`wil_details_FeatureReporting_RecordUsageInCache`. Sampling weights and
stack-hit counts are estimates, and equal-duration windows do not necessarily
contain equal numbers of completed images.

Trace image metadata reports the following versions:

| Module | Fast trace | Slow trace |
| --- | --- | --- |
| `WMPhoto.dll` | 10.0.26100.1 | 10.0.26100.9974 |
| `WindowsCodecs.dll` | 10.0.26100.1591 | 10.0.26100.9974 |

These are the versions reported by the trace metadata. The slow trace's
`WMPhoto.dll` version differs from the slow-run console transcript in
[performance results](performance-results.md); these are separate captures
and should not be assumed to have identical binaries.

## Interpretation and limits

The slower process spent substantially longer running, and its decode stacks
show substantial time in feature-check and usage-reporting code. The trace
windows were not matched to individual decode iterations; these traces alone
do not establish why this activity occurs or identify a specific KB. No
conclusion about CPU frequency or waits is made from this comparison.

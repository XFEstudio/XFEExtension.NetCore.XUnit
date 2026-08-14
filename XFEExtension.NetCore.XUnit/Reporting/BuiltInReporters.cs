using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Reporting;

internal static class BuiltInReporters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task WriteTestsAsync(TestRunSummary summary, ReportSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settings.ArtifactsPath);
        if (settings.Json)
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactsPath, "test-results.json"), JsonSerializer.Serialize(summary, JsonOptions), cancellationToken).ConfigureAwait(false);
        if (settings.JUnit)
        {
            var suite = new XElement("testsuite",
                new XAttribute("name", "XFEExtension.NetCore.XUnit"),
                new XAttribute("tests", summary.Total),
                new XAttribute("failures", summary.Failed),
                new XAttribute("skipped", summary.Skipped),
                new XAttribute("time", summary.Duration.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture)));
            foreach (var result in summary.Results)
            {
                var testCase = new XElement("testcase",
                    new XAttribute("name", result.DisplayName),
                    new XAttribute("time", result.TotalDuration.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture)));
                if (result.Outcome == TestOutcome.Skipped)
                    testCase.Add(new XElement("skipped", new XAttribute("message", result.Message ?? "Skipped")));
                else if (result.Outcome != TestOutcome.Passed)
                    testCase.Add(new XElement("failure", new XAttribute("message", result.Message ?? result.Outcome.ToString()), result.StackTrace));
                if (!string.IsNullOrEmpty(result.Output))
                    testCase.Add(new XElement("system-out", result.Output));
                suite.Add(testCase);
            }
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactsPath, "test-results.xml"), new XDocument(suite).ToString(), cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task WriteBenchmarksAsync(BenchmarkRunSummary summary, ReportSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settings.ArtifactsPath);
        if (settings.Json)
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactsPath, "benchmark-results.json"), JsonSerializer.Serialize(summary, JsonOptions), cancellationToken).ConfigureAwait(false);
        if (settings.Markdown)
        {
            var markdown = new StringBuilder("| Benchmark | Mean | Error | Median | P95 | Allocated/op | Gen0/1k | Ratio |\n|---|---:|---:|---:|---:|---:|---:|---:|\n");
            foreach (var benchmark in summary.Benchmarks)
                markdown.Append('|').Append(benchmark.DisplayName).Append('|')
                    .Append(FormatNanoseconds(benchmark.Statistics.MeanNanoseconds)).Append('|')
                    .Append(FormatNanoseconds(benchmark.Statistics.ErrorNanoseconds)).Append('|')
                    .Append(FormatNanoseconds(benchmark.Statistics.MedianNanoseconds)).Append('|')
                    .Append(FormatNanoseconds(benchmark.Statistics.P95Nanoseconds)).Append('|')
                    .Append(benchmark.Gc.AllocatedBytesPerOperation.ToString("F2", CultureInfo.InvariantCulture)).Append(" B|")
                    .Append(benchmark.Gc.Gen0CollectionsPerThousandOperations.ToString("F3", CultureInfo.InvariantCulture)).Append('|')
                    .Append(benchmark.BaselineRatio?.ToString("F3", CultureInfo.InvariantCulture) ?? "-").AppendLine("|");
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactsPath, "benchmark-results.md"), markdown.ToString(), cancellationToken).ConfigureAwait(false);
        }
        if (settings.Csv)
        {
            var csv = new StringBuilder("Id,DisplayName,MeanNs,ErrorNs,MedianNs,P95Ns,AllocatedBytes,Gen0Per1000,Ratio,Converged\n");
            foreach (var benchmark in summary.Benchmarks)
                csv.Append(EscapeCsv(benchmark.Id)).Append(',').Append(EscapeCsv(benchmark.DisplayName)).Append(',')
                    .Append(benchmark.Statistics.MeanNanoseconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.Statistics.ErrorNanoseconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.Statistics.MedianNanoseconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.Statistics.P95Nanoseconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.Gc.AllocatedBytesPerOperation.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.Gc.Gen0CollectionsPerThousandOperations.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(benchmark.BaselineRatio?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                    .AppendLine(benchmark.Statistics.Converged.ToString());
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactsPath, "benchmark-results.csv"), csv.ToString(), cancellationToken).ConfigureAwait(false);
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';

    internal static string FormatNanoseconds(double nanoseconds) => nanoseconds switch
    {
        >= 1_000_000_000 => $"{nanoseconds / 1_000_000_000:F3} s",
        >= 1_000_000 => $"{nanoseconds / 1_000_000:F3} ms",
        >= 1_000 => $"{nanoseconds / 1_000:F3} us",
        _ => $"{nanoseconds:F3} ns"
    };
}

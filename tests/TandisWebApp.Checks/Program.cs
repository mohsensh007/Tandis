using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using TandisWebApp.DTOs;
using TandisWebApp.Models;
using TandisWebApp.Services;

// No database, HTTP calls, private configuration, or test framework downloads.
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root != null && !File.Exists(Path.Combine(root.FullName, "TandisWebApp.sln"))) root = root.Parent;
if (root == null) throw new Exception("Run from a checkout containing TandisWebApp.sln.");
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
var service = new BodyAnalysisService(null!, null!, NullLogger<BodyAnalysisService>.Instance);
var records = new List<Ath_BodyRecord>
{
    new() { RecordID = 3, RecordDate = "1405/07/15", RecordDateTime = new(2026, 10, 14), WeightKg = 90, WaistCm = 90 },
    new() { RecordID = 1, RecordDate = "1405/07/21", RecordDateTime = null, WeightKg = 100, WaistCm = 100 },
    new() { RecordID = 2, RecordDate = "1405/07/21", RecordDateTime = new(2026, 10, 15), WeightKg = 80, WaistCm = 80 }
};
BodyAnalysisPageDto Page(IEnumerable<Ath_BodyRecord> input, decimal? height = 180, int? age = 30, bool? gender = true)
{
    var ordered = input.OrderBy(r => r.RecordID).ToList();
    BodyRecordDto Map(Ath_BodyRecord r) => new()
    {
        RecordID = r.RecordID, RecordDate = r.RecordDate, RecordDateTime = r.RecordDateTime,
        WeightKg = r.WeightKg, WaistCm = r.WaistCm
    };
    var page = new BodyAnalysisPageDto
    {
        FullName = "عضو آزمایشی", MemberID = 7, Age = age, Gender = gender,
        Profile = new() { HeightCm = height, ActivityLevel = 2, Goal = 3 },
        Records = ordered.Select(Map).ToList(), Latest = ordered.Count == 0 ? null : Map(ordered[^1])
    };
    page.Metrics = service.Compute(page, input.ToList());
    return page;
}
var initial = Page(records);
Check(initial.Metrics.Bmi == 27.8m && initial.Metrics.Bmr == 1880m, "latest insertion wins over measurement date, timestamp and input-list order");
Check(initial.Metrics.WeightDelta == -10m && initial.Metrics.RecordCount == 3, "same-day and null-timestamp records retain a correct insertion-order trend");
Check(initial.Metrics.ProteinG == 144m && initial.Metrics.WaistToHeight == .50m, "all weight/waist calculations use the same latest record");
var savedRecords = records.Append(new Ath_BodyRecord { RecordID = 4, RecordDate = "1405/07/21", WeightKg = 85 }).ToList();
var saved = Page(savedRecords);
Check(saved.Metrics.Bmi == 26.2m && saved.Metrics.WaistToHeight == null, "a new partial record never inherits the previous waist measurement");
var deleted = Page(savedRecords.Where(r => r.RecordID != 4));
Check(deleted.Metrics.Bmi == initial.Metrics.Bmi, "deleting latest uses next surviving insertion");
var empty = Page([]);
Check(empty.Latest == null && empty.Metrics.Bmi == null && empty.Metrics.Bmr == null, "deleting all records clears results");
Check(Page(records, age: null, gender: null).Metrics.Bmi == 27.8m && Page(records, age: null).Metrics.Bmr == null, "missing demographics preserve available calculations only");
var incomplete = Page(records, height: null);
Check(incomplete.Latest?.WeightKg == 90 && incomplete.Metrics.Bmi == null && incomplete.Metrics.WeightDelta == -10, "missing height preserves latest measurement and trend");
Check((await service.SaveRecordAsync(7, new BodyRecordSaveRequest())).Length > 0, "empty weights rejected before database access");
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var json = JsonSerializer.Serialize(initial, options);
using (var doc = JsonDocument.Parse(json))
{
    Check(doc.RootElement.GetProperty("metrics").GetProperty("bmi").GetDecimal() == 27.8m, "API metrics are camelCase");
    Check(doc.RootElement.GetProperty("latest").GetProperty("recordId").GetInt32() == 3, "API recordId agrees with frontend deletion contract");
}

var output = Path.Combine(root.FullName, ".test-output");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "body-fixtures.json"), JsonSerializer.Serialize(new { initial, saved, deleted, empty, incomplete }, options));
var view = File.ReadAllText(Path.Combine(root.FullName, "TandisWebApp/Views/BodyAnalysis/Index.cshtml"));
Check(view.Contains("JsonSerializerDefaults.Web"), "initial Razor payload uses the same JSON policy as API updates");
Check(Regex.Matches(view, "data-help-key=\"").Count == 12, "all twelve calculation cards have individual help keys");

// Cross-platform UI behavior runner: Node on Linux, built-in JScript on Windows.
var jsRunner = OperatingSystem.IsWindows() ? "cscript.exe" : "node";
var start = new ProcessStartInfo(jsRunner) { UseShellExecute = false, WorkingDirectory = root.FullName };
if (OperatingSystem.IsWindows()) start.ArgumentList.Add("//Nologo");
start.ArgumentList.Add(Path.Combine(root.FullName, "tests/ui-checks.js"));
start.ArgumentList.Add(root.FullName);
using var process = Process.Start(start) ?? throw new Exception("JavaScript runner unavailable (install the documented runtime).");
if (!process.WaitForExit(60_000)) { process.Kill(true); throw new Exception("UI checks timed out."); }
Check(process.ExitCode == 0, "UI behavior checks");
Console.WriteLine($"Completed {checks} .NET/contract checks (offline, synthetic data only).");

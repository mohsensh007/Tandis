using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace TandisWebApp.Services
{
    /// <summary>تنظیمات پنل: کارت‌های قابل نمایش در میز خدمات و بخش‌هایی که فیش چاپ می‌کنند</summary>
    public class PanelSettings
    {
        public List<string> DeskCards { get; set; } = new();
        public List<string> DeskPrint { get; set; } = new();
    }

    /// <summary>یک آیتم قابل تنظیم (کارت میز خدمات / نوع فیش)</summary>
    public class PanelItemInfo
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Icon { get; set; } = "";
        public PanelItemInfo() { }
        public PanelItemInfo(string key, string title, string icon) { Key = key; Title = title; Icon = icon; }
    }

    /// <summary>
    /// تنظیمات پنل در فایل App_Data/panel-settings.json نگه‌داری می‌شود؛
    /// پیش‌فرض (نبود فایل) یعنی همهٔ کارت‌ها نمایش داده شوند و همهٔ بخش‌ها فیش چاپ کنند.
    /// </summary>
    public class PanelSettingsService
    {
        public static readonly PanelItemInfo[] Items =
        {
            new("charge",   "افزایش اعتبار", "wallet2"),
            new("ticket",   "خرید بلیت",     "ticket-perforated"),
            new("single",   "تک جلسه",       "lightning-charge"),
            new("register", "ثبت‌نام",        "person-plus"),
            new("renew",    "تمدید دوره",    "arrow-repeat"),
            new("shop",     "فروشگاه",       "bag-heart"),
            new("buffet",   "بوفه",          "cup-hot"),
            new("services", "خدمات",         "tools")
        };

        private static readonly string[] Keys = Items.Select(x => x.Key).ToArray();

        private readonly string _path;
        private readonly ILogger<PanelSettingsService> _logger;
        private readonly object _lock = new();

        public PanelSettingsService(IWebHostEnvironment env, ILogger<PanelSettingsService> logger)
        {
            _logger = logger;
            _path = Path.Combine(env.ContentRootPath, "App_Data", "panel-settings.json");
        }

        public PanelSettings Get()
        {
            lock (_lock)
            {
                var result = new PanelSettings { DeskCards = Keys.ToList(), DeskPrint = Keys.ToList() };
                try
                {
                    if (!File.Exists(_path)) return result;

                    var json = File.ReadAllText(_path);
                    var saved = JsonSerializer.Deserialize<PanelSettings>(json, JsonOptions);
                    if (saved == null) return result;

                    result.DeskCards = Clean(saved.DeskCards);
                    result.DeskPrint = Clean(saved.DeskPrint);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خواندن تنظیمات پنل ناموفق بود؛ پیش‌فرض اعمال شد");
                    return result;
                }
            }
        }

        public void Save(PanelSettings input)
        {
            lock (_lock)
            {
                var clean = new PanelSettings
                {
                    DeskCards = Clean(input?.DeskCards),
                    DeskPrint = Clean(input?.DeskPrint)
                };

                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(clean, JsonOptions));
            }
        }

        /// <summary>فقط کلیدهای شناخته‌شده و بدون تکرار</summary>
        private static List<string> Clean(IEnumerable<string>? values)
        {
            var list = (values ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .Where(v => Keys.Contains(v))
                .Distinct()
                .ToList();

            // اگر هیچ کلید معتبری نبود، یعنی کاربر همه را خاموش کرده (لیست خالی معتبر است).
            // اما اگر ورودی null باشد، پیش‌فرض «همه فعال» است.
            if (values == null) return Keys.ToList();
            return list;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}

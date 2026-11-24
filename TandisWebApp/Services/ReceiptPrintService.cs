using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TandisWebApp.Services
{
    /// <summary>یک ردیف کالا/خدمات در فیش</summary>
    public class ReceiptRowDto
    {
        public string Name { get; set; } = "";
        public int Qty { get; set; } = 1;
        public long Amount { get; set; }
    }

    /// <summary>محتوای فیشی که باید چاپ شود (از سمت میز خدمات ارسال می‌شود)</summary>
    public class ReceiptDoc
    {
        public string ClubName { get; set; } = "";
        public string Title { get; set; } = "رسید";
        public string? Party { get; set; }
        public string? PartySub { get; set; }
        public List<ReceiptRowDto> Rows { get; set; } = new();
        public long Total { get; set; }
        public string? TotalLabel { get; set; }

        // کد سند بعضی مسیرها (مثل تمدید دوره) عددی فرستاده می‌شود؛ تبدیل خودکار عدد به متن
        [JsonConverter(typeof(LenientStringConverter))]
        public string? RefNo { get; set; }

        public string? PaymentMethod { get; set; }
        public string? Note { get; set; }
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
    }

    /// <summary>عدد/بول/تهی را هم به‌صورت متن می‌پذیرد تا بایند شدن JSON fail نشود</summary>
    public sealed class LenientStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out long l)
                        ? l.ToString(CultureInfo.InvariantCulture)
                        : reader.GetDouble().ToString("0.##", CultureInfo.InvariantCulture);
                case JsonTokenType.True: return "1";
                case JsonTokenType.False: return "0";
                case JsonTokenType.Null: return null;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    reader.Skip();
                    return null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    /// <summary>
    /// چاپ فیش روی پرینتر حرارتی POS.
    /// اگر روی سیستم سرور یکی از درایورهای POS-80 / POS-80C / POS-58 / POS-58C نصب باشد،
    /// فیش مستقیماً (بدون دیالوگ چاپ) روی همان پرینتر چاپ می‌شود.
    /// چیدمان بر اساس قالب‌های Telerik موجود در پروژهٔ کیوسک (Report/*.trdp) بازسازی شده است.
    /// </summary>
    public class ReceiptPrintService
    {
        private readonly ILogger<ReceiptPrintService> _logger;

        // ترتیب اولویت: مدل‌های ۸۰ میلی‌متری جلوتر از ۵۸
        private static readonly string[] PrinterKeywords = { "POS-80C", "POS-80", "POS-58C", "POS-58" };

        private static readonly object _printLock = new();

        public ReceiptPrintService(ILogger<ReceiptPrintService> logger) => _logger = logger;

        // ============================================================
        //  تشخیص پرینتر
        // ============================================================
        public string? DetectPrinter(out bool narrow)
        {
            narrow = false;
            try
            {
                string? best = null;
                int bestRank = int.MaxValue;

                foreach (string raw in PrinterSettings.InstalledPrinters)
                {
                    var name = (raw ?? "").Trim();
                    if (name.Length == 0) continue;

                    var compact = Compact(name);
                    for (int i = 0; i < PrinterKeywords.Length; i++)
                    {
                        if (compact.Contains(Compact(PrinterKeywords[i])) && i < bestRank)
                        {
                            best = name;
                            bestRank = i;
                        }
                    }
                }

                if (best != null)
                {
                    var c = Compact(best);
                    narrow = c.Contains("58");
                }
                return best;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خواندن لیست پرینترهای سیستم ناموفق بود");
                return null;
            }
        }

        private static string Compact(string s) =>
            (s ?? "").ToUpperInvariant().Replace(" ", "").Replace("_", "").Replace("/", "");

        /// <summary>نام پرینتر POS نصب‌شده (برای نمایش در UI) یا null</summary>
        public string? AvailablePrinterName()
        {
            var p = DetectPrinter(out _);
            return p;
        }

        // ============================================================
        //  چاپ
        // ============================================================
        public (bool ok, string message) Print(ReceiptDoc doc)
        {
            var printer = DetectPrinter(out bool narrow);
            if (printer == null)
                return (false, "پرینتر حرارتی POS (POS-80/80C/58/58C) روی سیستم یافت نشد");

            try
            {
                const float targetDpi = 203f;   // رزولوشن استاندارد پرینترهای حرارتی POS
                int widthPx = narrow ? 384 : 576;
                using var bmp = Render(doc, widthPx);

                lock (_printLock)
                {
                    using var pd = new PrintDocument();
                    pd.PrintController = new StandardPrintController();   // بدون دیالوگ
                    pd.PrinterSettings.PrinterName = printer;
                    if (!pd.PrinterSettings.IsValid)
                        return (false, $"پرینتر «{printer}» در دسترس نیست");

                    // ✅ عرض کاغذ = دقیقاً همان عرض بیت‌مپ (۸۰mm → 576 دات = ۷۲mm، ۵۸mm → 384 دات = ۴۸mm)
                    //    قبلاً عرض کاغذ ۸۰mm گذاشته می‌شد و تصویر در آن «وسط‌چین» می‌شد؛
                    //    چون سر چاپ فقط ۵۷۶ دات دارد، ۳۰ دات سمت راست بیرون می‌افتاد و متن بریده می‌شد.
                    float paperWidthHi = bmp.Width * 100f / targetDpi;
                    float heightHi = bmp.Height * 100f / targetDpi + 30f;

                    pd.DefaultPageSettings.PaperSize = new PaperSize("POS-Receipt", (int)Math.Ceiling(paperWidthHi), (int)Math.Ceiling(heightHi));
                    pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                    pd.DefaultPageSettings.Landscape = false;
                    pd.OriginAtMargins = false;

                    pd.PrintPage += (s, e) =>
                    {
                        var g = e.Graphics!;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.PageUnit = GraphicsUnit.Pixel;

                        // ✅ فیش از گوشهٔ چپ کاغذ و ۱:۱ رسم می‌شود (بدون وسط‌چین شدن که باعث بریدن لبهٔ راست بود)
                        float dpi = g.DpiX > 0 ? g.DpiX : targetDpi;
                        float pageDots = e.PageBounds.Width / 100f * dpi;
                        float destW = pageDots > 0 ? Math.Min(bmp.Width, pageDots) : bmp.Width;
                        float scale = destW / bmp.Width;
                        float destH = bmp.Height * scale;

                        g.DrawImage(bmp, new RectangleF(0, 0, destW, destH),
                                         new RectangleF(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
                        e.HasMorePages = false;
                    };

                    pd.Print();
                }

                return (true, $"فیش روی «{printer}» چاپ شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در چاپ فیش روی پرینتر POS");
                return (false, "خطا در چاپ فیش: " + ex.Message);
            }
        }

        // ============================================================
        //  ساخت تصویر فیش
        // ============================================================
        private sealed record El(string Text, float Size, bool Bold, int Align, float GapAfter);

        private Bitmap Render(ReceiptDoc doc, int width)
        {
            var els = new List<El>();

            void Center(string t, float size, bool bold, float gap = 4) => els.Add(new(t, size, bold, 0, gap));
            void Row(string t, float size, bool bold, float gap = 4) => els.Add(new(t, size, bold, 1, gap));
            void Small(string t) => els.Add(new(t, 15, false, 0, 2));

            // --- هدر
            Center(doc.ClubName.Length > 0 ? doc.ClubName : "باشگاه", 24, true, 2);
            Center(doc.Title.Length > 0 ? doc.Title : "رسید", 26, true, 6);
            Center("--------------------------------", 16, false, 6);

            // --- تاریخ / ساعت
            var dateLine = $"تاریخ: {doc.Date}    ساعت: {doc.Time}";
            Row(dateLine, 19, false, 4);

            if (!string.IsNullOrWhiteSpace(doc.Party))
                Row("نام: " + doc.Party, 19, false, 2);
            if (!string.IsNullOrWhiteSpace(doc.PartySub))
                Row(doc.PartySub!, 17, false, 4);

            if (doc.Rows.Count > 0)
            {
                Center("--------------------------------", 16, false, 6);
                foreach (var r in doc.Rows)
                {
                    var name = (r.Name ?? "").Trim();
                    if (name.Length == 0) continue;

                    if (r.Qty > 1)
                        Row($"{name} × {r.Qty}", 19, false, 1);
                    else
                        Row(name, 19, false, 1);

                    Row(FormatMoney(r.Amount * Math.Max(r.Qty, 1)) + " ریال", 18, false, 4);
                }
            }

            Center("--------------------------------", 16, false, 6);

            // --- مبلغ کل
            var totalLabel = string.IsNullOrWhiteSpace(doc.TotalLabel) ? "مبلغ کل" : doc.TotalLabel!;
            Center($"{totalLabel}: {FormatMoney(doc.Total)} ریال", 23, true, 6);

            if (!string.IsNullOrWhiteSpace(doc.PaymentMethod))
                Row("روش پرداخت: " + doc.PaymentMethod, 18, false, 3);
            if (!string.IsNullOrWhiteSpace(doc.RefNo))
                Row("کد سند: " + doc.RefNo, 18, false, 3);
            if (!string.IsNullOrWhiteSpace(doc.Note))
                Center(doc.Note!, 16, false, 4);

            Center("--------------------------------", 16, false, 6);

            // --- پاورقی (مطابق قالب‌های موجود)
            Small("ایمن تردد تندیس مرکز تخصصی");
            Small("هوشمندسازی اماکن ورزشی");
            Small("www.ittandis.com");
            Small("021-37549000");
            els.Add(new El("با تشکر از شما", 17, true, 0, 6));

            // --- اندازه‌گیری / شکستن خطوط
            // ✅ حاشیهٔ امن: ۳٪ عرض (حدود ۲ میلی‌متر) تا هیچ متنی به لبهٔ سر چاپ نچسبد
            int pad = Math.Max(14, (int)Math.Round(width * 0.03));
            float avail = width - pad * 2f;

            using var probe = new Bitmap(1, 1);
            using var pg = Graphics.FromImage(probe);
            pg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var fmt = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.DirectionRightToLeft
            };

            var fonts = new List<Font>();
            var lines = new List<(string Text, Font Font, int Align, float Gap)>();

            foreach (var el in els)
            {
                var baseFont = new Font("Tahoma", el.Size, el.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                fonts.Add(baseFont);

                var parts = WrapText(pg, el.Text ?? "", baseFont, avail, fmt);
                float gap = el.GapAfter / Math.Max(parts.Count, 1);

                foreach (var part in parts)
                {
                    var font = baseFont;
                    if (Measure(pg, part, font, fmt) > avail)
                    {
                        font = Shrink(pg, part, baseFont, avail, fmt);
                        fonts.Add(font);
                    }
                    lines.Add((part, font, el.Align, gap));
                }
            }

            var rows = new List<(string Text, Font Font, int Align, float Gap, float Height)>();
            float totalH = pad * 2;
            foreach (var ln in lines)
            {
                float lh = LineHeight(pg, ln.Font, fmt);
                rows.Add((ln.Text, ln.Font, ln.Align, ln.Gap, lh));
                totalH += lh + ln.Gap;
            }

            var bmp = new Bitmap(width, Math.Max(120, (int)Math.Ceiling(totalH)));
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = SmoothingMode.HighQuality;

                float y = pad;
                using var brush = new SolidBrush(Color.Black);

                foreach (var ln in rows)
                {
                    if (!string.IsNullOrEmpty(ln.Text))
                    {
                        float w = Measure(g, ln.Text, ln.Font, fmt);
                        // در حالت RTL، مختصهٔ افقیِ DrawString لبهٔ راست متن است
                        float rightEdge = ln.Align == 0 ? (width + w) / 2f : width - pad;
                        if (rightEdge - w < pad) rightEdge = pad + w;     // متن از کادر بیرون نزند
                        if (rightEdge > width - pad) rightEdge = width - pad;
                        g.DrawString(ln.Text, ln.Font, brush, rightEdge, y, fmt);
                    }
                    y += ln.Height + ln.Gap;
                }
            }

            foreach (var f in fonts) f.Dispose();
            return bmp;
        }

        private static string FormatMoney(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

        private static float Measure(Graphics g, string text, Font font, StringFormat fmt)
            => string.IsNullOrEmpty(text) ? 0f : g.MeasureString(text, font, new PointF(0, 0), fmt).Width;

        // ✅ ارتفاع خط با فاصلهٔ استاندارد فونت (MeasureString تایپوگرافیک فقط خودِ گلیف را می‌داد
        //    و خطوط پاورقی روی هم می‌افتادند)
        private static float LineHeight(Graphics g, Font font, StringFormat fmt)
            => font.GetHeight(g) + 1f;

        /// <summary>کوچک‌ترین فونت (حداقل ۹px) که متن در عرض فیش جا شود</summary>
        private static Font Shrink(Graphics g, string text, Font baseFont, float avail, StringFormat fmt)
        {
            for (float size = baseFont.Size - 0.5f; size >= 9f; size -= 0.5f)
            {
                var f = new Font(baseFont.FontFamily, size, baseFont.Style, GraphicsUnit.Pixel);
                if (Measure(g, text, f, fmt) <= avail) return f;
                f.Dispose();
            }
            return new Font(baseFont.FontFamily, 9f, baseFont.Style, GraphicsUnit.Pixel);
        }

        /// <summary>شکستن متن به خطوط متناسب با عرض فیش (کلمه‌ای، با شکستن کلمات بسیار بلند)</summary>
        private static List<string> WrapText(Graphics g, string text, Font font, float avail, StringFormat fmt)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }

            var line = new StringBuilder();
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (Measure(g, candidate, font, fmt) <= avail)
                {
                    line.Clear().Append(candidate);
                    continue;
                }

                if (line.Length > 0)
                {
                    result.Add(line.ToString());
                    line.Clear();
                }

                // کلمهٔ بسیار بلند (بدون فاصله) → شکستن کاراکتری تا در عرض فیش جا شود
                var rest = word;
                while (Measure(g, rest, font, fmt) > avail && rest.Length > 1)
                {
                    int take = rest.Length - 1;
                    while (take > 1 && Measure(g, rest.Substring(0, take), font, fmt) > avail) take--;
                    result.Add(rest.Substring(0, take));
                    rest = rest.Substring(take);
                }
                line.Append(rest);
            }

            if (line.Length > 0) result.Add(line.ToString());
            if (result.Count == 0) result.Add(text);
            return result;
        }
    }
}

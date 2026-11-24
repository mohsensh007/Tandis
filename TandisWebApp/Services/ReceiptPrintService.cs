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
    /// چیدمان تصویر، دقیقاً همان فیشی است که در پنل میز خدمت روی صفحه نمایش داده می‌شود
    /// (جدول کالا/فی/تعداد/مبلغ، کادر مبلغ کل، پاورقی).
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
        //  ساخت تصویر فیش (مطابق فیش روی صفحه)
        // ============================================================
        private Bitmap Render(ReceiptDoc doc, int width)
        {
            // همهٔ اندازه‌ها نسبت به عرض مرجع ۵۷۶ دات مقیاس می‌شوند تا روی ۵۸ میلی‌متری هم یکسان بماند
            float s = width / 576f;
            int pad = Math.Max(10, (int)Math.Round(width * 0.035f));

            var fonts = new List<Font>();
            Font MakeFont(float px, bool bold)
            {
                var f = new Font("Tahoma", Math.Max(8f, px * s), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                fonts.Add(f);
                return f;
            }

            var fClub = MakeFont(20, true);
            var fTitle = MakeFont(27, true);
            var fMeta = MakeFont(21, true);
            var fParty = MakeFont(20, true);
            var fCell = MakeFont(19, false);
            var fCellName = MakeFont(19, true);
            var fHead = MakeFont(19, true);
            var fSum = MakeFont(22, true);
            var fNote = MakeFont(19, false);
            var fFoot = MakeFont(17, false);

            using var probe = new Bitmap(1, 1);
            using var pg = Graphics.FromImage(probe);
            pg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var rtl = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.DirectionRightToLeft };
            var ltr = new StringFormat(StringFormat.GenericTypographic);

            float LH(Font f) => f.GetHeight(pg) + 1f;
            float MW(string t, Font f) => string.IsNullOrEmpty(t) ? 0f : pg.MeasureString(t, f, new PointF(0, 0), rtl).Width;

            float thick = Math.Max(1.4f, 2f * s);
            float x0 = pad, x1 = width - pad, tw = x1 - x0;

            using var pen = new Pen(Color.Black, thick);
            using var dotPen = new Pen(Color.Black, Math.Max(1f, thick * 0.8f)) { DashStyle = DashStyle.Dot };
            using var dashPen = new Pen(Color.Black, Math.Max(1f, thick * 0.8f)) { DashStyle = DashStyle.Dash };
            using var brush = new SolidBrush(Color.Black);

            var blocks = new List<(float H, Action<Graphics, float> Draw)>();
            void Add(float h, Action<Graphics, float> draw) => blocks.Add((h, draw));

            Font Fit(string text, Font baseFont, float avail)
            {
                if (MW(text, baseFont) <= avail) return baseFont;
                for (float size = baseFont.Size - 0.5f; size >= 8f; size -= 0.5f)
                {
                    var f = new Font(baseFont.FontFamily, size, baseFont.Style, GraphicsUnit.Pixel);
                    fonts.Add(f);
                    if (MW(text, f) <= avail) return f;
                }
                return baseFont;
            }

            // در حالت RTL نقطهٔ شروعِ DrawString لبهٔ راست متن است
            void DrawRtl(Graphics g, string text, Font f, float rightX, float y)
                => g.DrawString(text, f, brush, rightX, y, rtl);

            void DrawC(Graphics g, string text, Font f, float y)
            {
                float w = g.MeasureString(text, f, new PointF(0, 0), rtl).Width;
                g.DrawString(text, f, brush, (width + w) / 2f, y, rtl);
            }

            void DrawInCell(Graphics g, string text, Font f, float left, float cellW, float y)
            {
                float w = g.MeasureString(text, f, new PointF(0, 0), rtl).Width;
                g.DrawString(text, f, brush, left + (cellW + w) / 2f, y, rtl);
            }

            void DrawLeftRtl(Graphics g, string text, Font f, float left, float y)
            {
                float w = g.MeasureString(text, f, new PointF(0, 0), rtl).Width;
                g.DrawString(text, f, brush, left + w, y, rtl);
            }

            void AddDash() =>
                Add(16f * s, (g, y) => g.DrawLine(dashPen, x0, y + 8f * s, x1, y + 8f * s));

            // ---------- سربرگ: نام باشگاه + عنوان فیش ----------
            var club = (doc.ClubName ?? "").Trim();
            if (club.Length > 0)
            {
                var cf = Fit(club, fClub, tw);
                float h = LH(cf);
                Add(h + 2 * s, (g, y) => DrawC(g, club, cf, y));
            }

            var title = string.IsNullOrWhiteSpace(doc.Title) ? "رسید" : doc.Title.Trim();
            var tf = Fit(title, fTitle, tw);
            float titleH = LH(tf);
            Add(titleH + 5 * s, (g, y) => DrawC(g, title, tf, y));

            AddDash();

            // ---------- ساعت و تاریخ در یک خط (ساعت سمت راست، تاریخ سمت چپ) ----------
            var timeLine = "ساعت: " + (doc.Time ?? "");
            var dateLine = "تاریخ: " + (doc.Date ?? "");
            var mfTime = Fit(timeLine, fMeta, tw * 0.46f);
            var mfDate = Fit(dateLine, fMeta, tw * 0.46f);
            float metaH = Math.Max(LH(mfTime), LH(mfDate)) + 2 * s;
            Add(metaH, (g, y) =>
            {
                DrawRtl(g, timeLine, mfTime, x1, y);
                DrawLeftRtl(g, dateLine, mfDate, x0, y);
            });

            // ---------- نام عضو ----------
            if (!string.IsNullOrWhiteSpace(doc.Party) || !string.IsNullOrWhiteSpace(doc.PartySub))
            {
                AddDash();
                if (!string.IsNullOrWhiteSpace(doc.Party))
                {
                    var line = "نام: " + doc.Party!.Trim();
                    var pf = Fit(line, fParty, tw);
                    float h = LH(pf);
                    Add(h + 2 * s, (g, y) => DrawRtl(g, line, pf, x1, y));
                }
                if (!string.IsNullOrWhiteSpace(doc.PartySub))
                {
                    var line = doc.PartySub!.Trim();
                    var pf = Fit(line, fCell, tw);
                    float h = LH(pf);
                    Add(h + 2 * s, (g, y) => DrawRtl(g, line, pf, x1, y));
                }
            }

            // ---------- جدول کالا / فی / تعداد / مبلغ ----------
            var items = (doc.Rows ?? new List<ReceiptRowDto>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => new { Name = r.Name.Trim(), Qty = Math.Max(1, r.Qty), Unit = Math.Max(0, r.Amount) })
                .ToList();

            if (items.Count > 0)
            {
                AddDash();

                var cw = new[] { tw * 0.40f, tw * 0.21f, tw * 0.14f, tw * 0.25f };   // کالا، فی، تعداد، مبلغ (از راست)
                var heads = new[] { "کالا", "فی", "تعداد", "مبلغ" };
                float headH = LH(fHead) + 12 * s;
                float cellPadX = 7 * s;

                var rowLines = new List<List<string>>();
                var rowHeights = new List<float>();
                foreach (var it in items)
                {
                    var lines = WrapText(pg, it.Name, fCellName, cw[0] - cellPadX * 2, rtl);
                    if (lines.Count == 0) lines.Add("");
                    rowLines.Add(lines);
                    rowHeights.Add(Math.Max(30 * s, lines.Count * LH(fCellName) + 12 * s));
                }

                float tableH = headH + rowHeights.Sum();

                Add(tableH, (g, y) =>
                {
                    g.DrawRectangle(pen, x0, y, tw, tableH);
                    g.DrawLine(pen, x0, y + headH, x1, y + headH);

                    // سرستون‌ها از راست به چپ
                    float cx = x1;
                    for (int c = 0; c < heads.Length; c++)
                    {
                        DrawInCell(g, heads[c], fHead, cx - cw[c], cw[c], y + (headH - LH(fHead)) / 2f);
                        cx -= cw[c];
                    }

                    float cy = y + headH;
                    float numberY, colLeft;

                    for (int r = 0; r < items.Count; r++)
                    {
                        var it = items[r];
                        float rh = rowHeights[r];

                        // ستون کالا (راست‌ترین) — با شکستن خط
                        float nameY = cy + (rh - rowLines[r].Count * LH(fCellName)) / 2f;
                        foreach (var ln in rowLines[r])
                        {
                            DrawRtl(g, ln, fCellName, x1 - cellPadX, nameY);
                            nameY += LH(fCellName);
                        }

                        numberY = cy + (rh - LH(fCell)) / 2f;

                        colLeft = x1 - cw[0];
                        DrawInCell(g, FormatMoney(it.Unit), fCell, colLeft - cw[1], cw[1], numberY);

                        colLeft -= cw[1];
                        DrawInCell(g, it.Qty.ToString(CultureInfo.InvariantCulture), fCell, colLeft - cw[2], cw[2], numberY);

                        DrawInCell(g, FormatMoney(it.Unit * it.Qty), fCell, x0, cw[3], numberY);

                        if (r < items.Count - 1)
                            g.DrawLine(dotPen, x0, cy + rh, x1, cy + rh);
                        cy += rh;
                    }
                });
            }

            // ---------- کادر مبلغ کل ----------
            var sumRows = new List<(string Label, string Value)>
            {
                (string.IsNullOrWhiteSpace(doc.TotalLabel) ? "مبلغ کل" : doc.TotalLabel!.Trim(), FormatMoney(doc.Total) + " ریال")
            };
            if (!string.IsNullOrWhiteSpace(doc.RefNo))
                sumRows.Add(("شماره فاکتور", doc.RefNo!.Trim()));
            if (!string.IsNullOrWhiteSpace(doc.PaymentMethod))
                sumRows.Add(("روش پرداخت", doc.PaymentMethod!.Trim()));

            float sumRowH = LH(fSum) + 12 * s;
            float boxPadY = 8 * s;
            float boxPadX = 10 * s;
            float sumH = sumRows.Count * sumRowH + boxPadY * 2;

            Add(8 * s + sumH, (g, y) =>
            {
                float by = y + 8 * s;
                RoundRect(g, pen, x0, by, tw, sumH, 7 * s);
                float ry = by + boxPadY;
                foreach (var row in sumRows)
                {
                    float labelW = MW(row.Label + ":", fSum);
                    var vf = Fit(row.Value, fSum, tw - boxPadX * 2 - labelW - 12 * s);
                    DrawRtl(g, row.Label + ":", fSum, x1 - boxPadX, ry + (sumRowH - LH(fSum)) / 2f);
                    DrawLeftRtl(g, row.Value, vf, x0 + boxPadX, ry + (sumRowH - LH(vf)) / 2f);
                    ry += sumRowH;
                }
            });

            // ---------- توضیح ----------
            if (!string.IsNullOrWhiteSpace(doc.Note))
            {
                var note = doc.Note!.Trim();
                var nf = Fit(note, fNote, tw);
                float nh = LH(nf);
                Add(8 * s + nh + 6 * s, (g, y) => DrawC(g, note, nf, y + 8 * s));
            }
            else
            {
                AddDash();
            }

            // ---------- پاورقی ----------
            var footLines = new[]
            {
                "ایمن تردد تندیس مرکز تخصصی",
                "هوشمندسازی اماکن ورزشی",
                "www.ittandis.com",
                "۰۲۱-۳۷۵۴۹۰۰۰"
            };
            float footLineH = LH(fFoot) + 2 * s;
            float footH = footLines.Length * footLineH + 14 * s;

            Add(8 * s + footH, (g, y) =>
            {
                float by = y + 8 * s;
                RoundRect(g, pen, x0, by, tw, footH, 7 * s);
                float fy = by + 7 * s;
                foreach (var line in footLines)
                {
                    DrawC(g, line, fFoot, fy);
                    fy += footLineH;
                }
            });

            // ---------- رسم ----------
            float totalH = pad * 2;
            foreach (var b in blocks) totalH += b.H;

            var bmp = new Bitmap(width, Math.Max(140, (int)Math.Ceiling(totalH)));
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float y = pad;
                foreach (var b in blocks)
                {
                    b.Draw(g, y);
                    y += b.H;
                }
            }

            foreach (var f in fonts) f.Dispose();
            return bmp;
        }

        private static void RoundRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
        {
            r = Math.Max(0f, Math.Min(r, Math.Min(w, h) / 2f));
            using var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }

        private static string FormatMoney(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

        private static float Measure(Graphics g, string text, Font font, StringFormat fmt)
            => string.IsNullOrEmpty(text) ? 0f : g.MeasureString(text, font, new PointF(0, 0), fmt).Width;

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

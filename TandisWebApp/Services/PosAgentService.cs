using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.Models;
using SSP1126.PcPos.BaseClasses;
using SSP1126.PcPos.Infrastructure;

namespace TandisWebApp.Services
{
    /// <summary>
    /// ایجنت POS وب‌اپ (ارسال مبلغ به دستگاه کارخوان):
    ///  1) تراکنش‌های «در انتظار» وب‌اپ (RefType با پیشوند WEB_ و بدون ResponseCode) را از DB می‌خواند
    ///  2) با SDK رسمی SEP (SSP1126.PcPos) مبلغ را روی دستگاه ارسال می‌کند (SetLan + PcStarterPurchase)
    ///  3) جواب دستگاه (کد پاسخ، تریس، RRN، شماره ترمینال، کارت) را در ACC_PosTransaction ثبت می‌کند
    ///  وب‌اپ با poll کردن وضعیت، نتیجه را به کاربر نشان می‌دهد.
    ///
    /// ⚠️ نکته‌ی مهم: SDK روی .NET 10 بدون «CodePagesEncodingProvider» کار نمی‌کند
    ///    (پیام‌های دستگاه با encoding کدپیج خوانده می‌شوند و بدون آن پاسخ بی‌صدا نادیده گرفته می‌شود).
    ///    ثبت آن در همین کلاس انجام شده است.
    /// </summary>
    public class PosAgentService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PosAgentService> _logger;

        // دستگاه کارخوان تک‌کاناله است → ارسال‌ها باید صف‌بندی و یکی‌یکی انجام شوند
        private static bool _busy;

        public PosAgentService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PosAgentService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;

            // ✅ لازم برای خواندن پاسخ دستگاه روی .NET 10 (بدون آن رویداد PosResultReceived هرگز فعال نمی‌شود)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        /// <summary>آیا ایجنت فعال باشد؟ (اگر وب‌اپ روی سروری بدون دستگاه POS اجرا شود، false کنید)</summary>
        private bool Enabled => _config.GetValue<bool?>("PosPayment:AgentEnabled") ?? true;

        /// <summary>شناسه دستگاه POS (کدام ردیف Gen_PosTbl) - مانند PosPayment:PosId وب‌اپ</summary>
        private byte ConfiguredPosId => (byte)(_config.GetValue<int?>("PosPayment:PosId") ?? 1);

        /// <summary>
        /// اطلاعات دستگاه را از جدول Gen_PosTbl می‌خواند (PosID, PosName, TerminalID, PosIPaddrss, Psp).
        /// هیچ IP هاردکدی در برنامه نیست؛ اگر ردیف یا IP خالی باشد null برمی‌گردد.
        /// </summary>
        private async Task<Gen_PosTbl?> LoadPosAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FullSportDbContext>();
            return await db.Gen_PosTbls.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PosID == ConfiguredPosId);
        }

        /// <summary>مهلت انتظار پاسخ دستگاه (ثانیه) - باید از TTL تراکنش کمتر باشد</summary>
        private int TimeoutSeconds => _config.GetValue<int?>("PosPayment:AgentTimeoutSeconds") ?? 90;

        /// <summary>مهلت انتظار تراکنش (دقیقه) - مانند PosPayment:TtlMinutes وب‌اپ</summary>
        private int TtlMinutes => _config.GetValue<int?>("PosPayment:TtlMinutes") ?? 5;

        /// <summary>هر چند میلی‌ثانیه دیتابیس را بررسی کند</summary>
        private int PollMs => _config.GetValue<int?>("PosPayment:AgentPollMs") ?? 700;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Enabled)
            {
                _logger.LogWarning("ایجنت POS غیرفعال است (PosPayment:AgentEnabled=false)");
                return;
            }

            _logger.LogInformation("ایجنت POS شروع به کار کرد — تایم‌اوت={Timeout}s", TimeoutSeconds);
            await LogPosInfoAsync();

            // بازیابی بعد از ری‌استارت: تراکنش‌های نیمه‌کاره دوباره در انتظار می‌شوند
            await ResetStuckAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_busy)
                    {
                        var txn = await PickPendingAsync();
                        if (txn != null)
                            await ProcessAsync(txn, stoppingToken);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در حلقه‌ی ایجنت POS");
                }

                try { await Task.Delay(PollMs, stoppingToken); }
                catch (OperationCanceledException) { }
            }
        }

        /// <summary> لاگ اطلاعات دستگاه تعریف‌شده در Gen_PosTbl هنگام بالا آمدن ایجنت </summary>
        private async Task LogPosInfoAsync()
        {
            try
            {
                var pos = await LoadPosAsync();
                if (pos == null)
                    _logger.LogError("دستگاه POS با شناسه {PosId} در Gen_PosTbl تعریف نشده است — ایجنت نمی‌تواند ارسال کند", ConfiguredPosId);
                else
                    _logger.LogInformation("دستگاه POS: {Name} (PosId={PosId}) ترمینال={Tid} IP={Ip} PSP={Psp}",
                        pos.PosName ?? "-", pos.PosID, pos.TerminalID?.ToString() ?? "-",
                        pos.PosIPaddrss ?? "-", pos.Psp ?? "-");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در خواندن اطلاعات دستگاه از Gen_PosTbl");
            }
        }

        /// <summary> تراکنش‌های نیمه‌کاره بعد از ری‌استارت (PROCESSING بدون جواب) را آزاد می‌کند </summary>
        private async Task ResetStuckAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FullSportDbContext>();
                var affected = await _db_exec(db);
                if (affected > 0)
                    _logger.LogWarning("{Count} تراکنش نیمه‌کاره‌ی POS بعد از ری‌استارت آزاد شد", affected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بازیابی تراکنش‌های نیمه‌کاره POS");
            }
        }

        private static Task<int> _db_exec(FullSportDbContext db)
            => db.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.ACC_PosTransaction SET Message = NULL " +
                "WHERE RefType LIKE 'WEB[_]%' AND ResponseCode IS NULL AND Message = N'PROCESSING'");

        /// <summary> قدیمی‌ترین تراکنش در انتظار را برمی‌گرداند و آن را اتمیک «در حال ارسال» می‌کند </summary>
        private async Task<ACC_PosTransaction?> PickPendingAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FullSportDbContext>();

            var cutoff = DateTime.Now.AddMinutes(-TtlMinutes);

            var candidate = await db.ACC_PosTransactions
                .AsNoTracking()
                .Where(t => t.RefType != null && t.RefType.StartsWith("WEB_")
                         && t.ResponseCode == null
                         && (t.CreationDate == null || t.CreationDate > cutoff)
                         && (t.Message == null || t.Message != "PROCESSING"))
                .OrderBy(t => t.PcPosTransactionID)
                .FirstOrDefaultAsync();

            if (candidate == null) return null;

            // Claim اتمیک: فقط یک نفر برنده می‌شود
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.ACC_PosTransaction
                SET Message = N'PROCESSING'
                WHERE PcPosTransactionID = {candidate.PcPosTransactionID}
                  AND ResponseCode IS NULL
                  AND (Message IS NULL OR Message <> N'PROCESSING')");

            if (claimed != 1) return null;

            _busy = true;
            return candidate;
        }

        /// <summary> ارسال مبلغ به دستگاه و ثبت جواب </summary>
        private async Task ProcessAsync(ACC_PosTransaction txn, CancellationToken ct)
        {
            var amount = (long)(txn.MainAmount ?? 0);
            if (amount <= 0)
            {
                await FinalizeAsync(txn, "98", "مبلغ نامعتبر", null);
                _busy = false;
                return;
            }

            // --- اطلاعات دستگاه از دیتابیس (بدون هیچ IP هاردکدی) ---
            Gen_PosTbl? pos;
            try
            {
                pos = await LoadPosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در خواندن Gen_PosTbl");
                await FinalizeAsync(txn, "96", "خطا در خواندن اطلاعات دستگاه POS", null);
                _busy = false;
                return;
            }

            if (pos == null || string.IsNullOrWhiteSpace(pos.PosIPaddrss))
            {
                _logger.LogError("دستگاه POS (PosId={PosId}) در Gen_PosTbl تعریف نشده یا IP خالی است", ConfiguredPosId);
                await FinalizeAsync(txn, "96", "دستگاه POS در تنظیمات (Gen_PosTbl) تعریف نشده است", null);
                _busy = false;
                return;
            }

            var psp = (pos.Psp ?? "").Trim().ToUpperInvariant();
            if (psp.Length > 0 && psp != "SEP")
            {
                // SDK فعلی (SSP1126.PcPos) مخصوص PSP «SEP» است
                _logger.LogError("PSP دستگاه {PosId} برابر {Psp} است — ایجنت فعلی فقط SEP را پشتیبانی می‌کند", pos.PosID, pos.Psp);
                await FinalizeAsync(txn, "96", $"PSP دستگاه ({pos.Psp}) پشتیبانی نمی‌شود — فقط SEP", null);
                _busy = false;
                return;
            }

            var posIp = pos.PosIPaddrss!;

            _logger.LogInformation("ارسال مبلغ {Amount} ریال به دستگاه POS {Name} (IP={Ip} ترمینال={Tid} تراکنش {Id}, عضو {Member})",
                amount, pos.PosName ?? "-", posIp, pos.TerminalID?.ToString() ?? "-", txn.PcPosTransactionID, txn.MemberID);

            PcPosFactory? factory = null;
            try
            {
                factory = new PcPosFactory();

                var tcs = new TaskCompletionSource<PosResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                factory.PosResultReceived += r =>
                {
                    if (r != null && !string.IsNullOrEmpty(r.ResponseCode))
                        tcs.TrySetResult(r);
                };
                // رویداد کارت‌خوانی فقط اطلاع‌رسانی است
                factory.CardSwiped += r =>
                    _logger.LogInformation("کارت خوانده شد: {Card} ترمینال {Tid}", r?.CardNumberMask ?? "-", r?.TerminalId ?? "-");

                if (!factory.SetLan(posIp))
                    throw new InvalidOperationException("SetLan ناموفق بود: " + posIp);

                factory.Initialization(ResponseLanguage.Persian, TimeoutSeconds, AsyncType.Async);

                // ارسال مبلغ (دقیقاً همان فراخوانی‌ای که تستر SEP استفاده می‌کند)
                var immediate = factory.PcStarterPurchase(
                    amount.ToString(), string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, null, 0);

                PosResult? result = null;
                if (immediate != null && !string.IsNullOrEmpty(immediate.ResponseCode))
                {
                    result = immediate;                       // جواب همزمان
                }
                else
                {
                    var done = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds), ct);
                    result = done;                            // جواب از رویداد
                }

                _logger.LogInformation("جواب دستگاه: کد={Code} شرح={Desc} تریس={Trace}",
                    result.ResponseCode, result.ResponseDescription, result.TraceNumber ?? "-");

                await FinalizeAsync(txn, result.ResponseCode ?? "99",
                    result.ResponseDescription ?? "", result);
            }
            catch (OperationCanceledException)
            {
                await FinalizeAsync(txn, "99", "ارسال لغو شد", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارتباط با دستگاه POS");
                await FinalizeAsync(txn, "99", "عدم ارتباط با دستگاه: " + ex.Message, null);
            }
            finally
            {
                try { factory?.Dispose(); } catch { /* ignore */ }
                _busy = false;
            }
        }

        /// <summary> ثبت نتیجه روی تراکنش (ResponseCode → وضعیت نهایی برای poll وب‌اپ) </summary>
        private async Task FinalizeAsync(ACC_PosTransaction txn, string code, string message, PosResult? r)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FullSportDbContext>();

                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE dbo.ACC_PosTransaction
                    SET ResponseCode = {code},
                        Message = {message},
                        CardNumberMask = {(r?.CardNumberMask ?? null)},
                        CardNumberHash = {(r?.CardNumberHash_Sha1 ?? null)},
                        TerminalID = {(r?.TerminalId ?? null)},
                        TraceNumber = {(r?.TraceNumber ?? null)},
                        SerialID = {(r?.SerialId ?? null)},
                        RRN = {(r?.RRN ?? null)},
                        TransactionDate = {(r?.TxnDate ?? null)}
                    WHERE PcPosTransactionID = {txn.PcPosTransactionID} AND ResponseCode IS NULL");

                _logger.LogInformation("تراکنش {Id} نهایی شد: کد={Code}", txn.PcPosTransactionID, code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ثبت جواب تراکنش {Id}", txn.PcPosTransactionID);
            }
        }
    }
}

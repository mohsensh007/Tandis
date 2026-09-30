using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// شرایط و تعهدات باشگاه
    /// متن توسط ادمین تنظیم می‌شود و در صفحه ثبت‌نام نمایش داده می‌شود
    /// </summary>
    public class ClubRuleService
    {
        private readonly FullSportDbContext _db;

        public ClubRuleService(FullSportDbContext db)
        {
            _db = db;
        }

        /// <summary>متن فعال شرایط (برای صفحه ثبت‌نام)</summary>
        public async Task<string> GetActiveRulesAsync()
        {
            var r = await _db.Gen_ClubRules
                .AsNoTracking()
                .Where(x => x.IsActive == true)
                .OrderByDescending(x => x.RulesID)
                .FirstOrDefaultAsync();

            return r?.RulesText ?? "";
        }

        /// <summary>ذخیره شرایط توسط ادمین</summary>
        public async Task<(bool ok, string msg)> SaveRulesAsync(string text, short userID)
        {
            var r = await _db.Gen_ClubRules
                .OrderByDescending(x => x.RulesID)
                .FirstOrDefaultAsync();

            if (r == null)
            {
                _db.Gen_ClubRules.Add(new Gen_ClubRule
                {
                    RulesText = text,
                    IsActive = true,
                    UserID = userID,
                    CreationTime = DateTime.Now
                });
            }
            else
            {
                r.RulesText = text;
                r.IsActive = true;
                r.Modifier = userID;
                r.ModificationTime = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            return (true, "شرایط باشگاه با موفقیت ذخیره شد");
        }
    }
}
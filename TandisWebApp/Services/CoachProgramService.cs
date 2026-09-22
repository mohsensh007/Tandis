using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    public class CoachProgramService
    {
        private readonly FullSportDbContext _db;
        private static readonly PersianCalendar Pc = new PersianCalendar();

        public CoachProgramService(FullSportDbContext db) { _db = db; }

        private static string ToShamsi(DateTime? d)
            => d == null ? "" : $"{Pc.GetYear(d.Value):0000}/{Pc.GetMonth(d.Value):00}/{Pc.GetDayOfMonth(d.Value):00}";

        private static DateTime? FromShamsi(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var p = s.Trim().Split('/');
            if (p.Length != 3) return null;
            if (!int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var m) || !int.TryParse(p[2], out var d)) return null;
            return Pc.ToDateTime(y, m, d, 0, 0, 0, 0);
        }

        /// <summary>ردیف‌های combo رو به یک آیتم با ExtraItems تبدیل می‌کنه</summary>
        private static List<ProgramItemEditDto> AssembleComboItems(List<ProgramItemEditDto> raw)
        {
            var result = new List<ProgramItemEditDto>();
            var members = new Dictionary<int, List<ProgramItemEditDto>>();

            foreach (var r in raw)
            {
                if (r.ComboGroup == null) { result.Add(r); continue; }
                if (r.SeqInCombo == 1) result.Add(r);
                else
                {
                    if (!members.ContainsKey(r.ComboGroup.Value))
                        members[r.ComboGroup.Value] = new List<ProgramItemEditDto>();
                    members[r.ComboGroup.Value].Add(r);
                }
            }

            foreach (var r in result)
            {
                if (r.ComboGroup != null && members.TryGetValue(r.ComboGroup.Value, out var list))
                {
                    r.ExtraItems = list
                        .OrderBy(m => m.SeqInCombo)
                        .Select(m => new ProgramSubItemDto
                        {
                            ItemID = m.ItemID,
                            ItemDesc = m.ItemDesc,
                            RepCount = m.RepCount,
                            WCount = m.WCount
                        }).ToList();
                }
            }
            return result;
        }

        /// <summary>آیتم موجود یا ساخت آیتم جدید</summary>
        private async Task<int> ResolveItemIDAsync(int itemID, string? newDesc)
        {
            if (itemID > 0) return itemID;
            if (string.IsNullOrWhiteSpace(newDesc)) return 0;
            var newItem = new Gen_PrgmItem { ItemDesc = newDesc.Trim(), CreationTime = DateTime.Now };
            _db.Gen_PrgmItems.Add(newItem);
            await _db.SaveChangesAsync();
            return newItem.ItemID;
        }

        /// <summary>لیست برنامه‌های یک مربی</summary>
        public async Task<List<CoachProgramRowDto>> GetProgramListAsync(int coachMemberID)
        {
            var raw = await (
                from p in _db.SportPrgs
                where p.CoachID == coachMemberID
                join m in _db.Gen_Members on p.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join pr in _db.Gen_Persons on m.PersonID equals pr.PersonID into pj
                from pr in pj.DefaultIfEmpty()
                orderby p.PrgID descending
                select new
                {
                    p.PrgID,
                    MemberID = p.MemberID ?? 0,
                    Name = (pr.FirstName + " " + pr.LastName),
                    p.StartDate,
                    p.EndDate,
                    ItemCount = _db.SportPrgDtls.Count(d => d.PrgID == p.PrgID)
                }).ToListAsync();

            return raw.Select(x => new CoachProgramRowDto
            {
                PrgID = x.PrgID,
                MemberID = x.MemberID,
                StudentName = string.IsNullOrWhiteSpace(x.Name) ? "-" : x.Name!,
                StartDateShamsi = ToShamsi(x.StartDate),
                EndDateShamsi = ToShamsi(x.EndDate),
                ItemCount = x.ItemCount,
                IsActive = x.EndDate != null && x.EndDate >= DateTime.Now.Date
            }).ToList();
        }

        /// <summary>شاگردان فعال مربی</summary>
        public async Task<List<StudentOptionDto>> GetStudentOptionsAsync(int coachMemberID)
        {
            var today = ToShamsi(DateTime.Now.Date);
            return await (
                from ams in _db.Acc_MemberSports
                join ss in _db.Gen_SportSanses on ams.SportSanseID equals ss.SportSanseID
                where ss.CoachMemberID == coachMemberID
                   && ams.IsActive == true
                   && ams.EndDate != null
                   && ams.EndDate.CompareTo(today) >= 0
                join m in _db.Gen_Members on ams.MemberID equals m.MemberID
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                select new StudentOptionDto
                {
                    MemberID = m.MemberID,
                    FullName = (p.FirstName + " " + p.LastName),
                    SportName = ss.Gen_Sport_Category != null ? ss.Gen_Sport_Category.SportName ?? "" : "",
                    SanseName = ss.SanseName ?? ""
                })
                .Distinct()
                .OrderBy(x => x.FullName)
                .ToListAsync();
        }

        /// <summary>بانک حرکات</summary>
        public async Task<List<ProgramItemEditDto>> GetItemOptionsAsync()
            => await _db.Gen_PrgmItems
                .OrderBy(x => x.ItemDesc)
                .Select(x => new ProgramItemEditDto { ItemID = x.ItemID, ItemDesc = x.ItemDesc ?? "" })
                .ToListAsync();

        /// <summary>خواندن برنامه برای فرم ویرایش</summary>
        public async Task<CoachProgramEditDto?> GetProgramEditAsync(int prgID, int coachMemberID)
        {
            var head = await _db.SportPrgs.FirstOrDefaultAsync(p => p.PrgID == prgID && p.CoachID == coachMemberID);
            if (head == null) return null;

            var rawItems = await (
                from d in _db.SportPrgDtls
                where d.PrgID == prgID
                join i in _db.Gen_PrgmItems on d.ItemID equals i.ItemID into ij
                from i in ij.DefaultIfEmpty()
                orderby d.SortOrder, d.SportPrgDtlID
                select new ProgramItemEditDto
                {
                    ItemID = d.ItemID ?? 0,
                    ItemDesc = i != null ? i.ItemDesc ?? "" : "",
                    SetCount = d.SetCount,
                    WCount = d.WCount,
                    RepCount = d.RepCount,
                    RestSeconds = d.RestSeconds,
                    ExerciseType = d.ExerciseType,
                    DayTitle = d.DayTitle,
                    Note = d.Note,
                    ComboGroup = d.ComboGroup,
                    SeqInCombo = d.SeqInCombo,
                    DropCount = d.DropCount,
                    DropWeightPct = d.DropWeightPct,
                    PyramidDir = d.PyramidDir,
                    WeightStep = d.WeightStep,
                    PauseCount = d.PauseCount,
                    PauseRest = d.PauseRest,
                    Tempo = d.Tempo
                }).ToListAsync();

            var studentName = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == head.MemberID
                select (p.FirstName + " " + p.LastName)
            ).FirstOrDefaultAsync();

            return new CoachProgramEditDto
            {
                PrgID = head.PrgID,
                MemberID = head.MemberID ?? 0,
                StudentName = studentName ?? "",
                StartDateShamsi = ToShamsi(head.StartDate),
                EndDateShamsi = ToShamsi(head.EndDate),
                Items = AssembleComboItems(rawItems)
            };
        }

        /// <summary>ذخیره برنامه با اعتبارسنجی کامل هر تکنیک</summary>
        public async Task<(bool ok, string msg, int prgID)> SaveProgramAsync(int coachMemberID, SaveProgramRequest req)
        {
            if (req.MemberID <= 0) return (false, "شاگرد را انتخاب کنید", 0);
            if (req.Items == null || req.Items.Count == 0) return (false, "حداقل یک حرکت اضافه کنید", 0);

            var start = FromShamsi(req.StartDateShamsi);
            var end = FromShamsi(req.EndDateShamsi);
            if (start == null || end == null) return (false, "تاریخ شروع و پایان را درست وارد کنید", 0);
            if (end < start) return (false, "تاریخ پایان باید بعد از تاریخ شروع باشد", 0);

            SportPrg head;
            if (req.PrgID > 0)
            {
                head = await _db.SportPrgs.FirstOrDefaultAsync(p => p.PrgID == req.PrgID && p.CoachID == coachMemberID);
                if (head == null) return (false, "برنامه یافت نشد", 0);
                head.MemberID = req.MemberID;
                head.StartDate = start;
                head.EndDate = end;
                head.Modificationtime = DateTime.Now;

                var oldDetails = await _db.SportPrgDtls.Where(d => d.PrgID == head.PrgID).ToListAsync();
                _db.SportPrgDtls.RemoveRange(oldDetails);
            }
            else
            {
                head = new SportPrg
                {
                    MemberID = req.MemberID,
                    CoachID = coachMemberID,
                    StartDate = start,
                    EndDate = end,
                    CreationTime = DateTime.Now
                };
                _db.SportPrgs.Add(head);
            }

            await _db.SaveChangesAsync();

            int order = 1;
            int comboSeq = 1;

            foreach (var it in req.Items)
            {
                var type = it.ExerciseType ?? 1;

                // ===== ✅ اعتبارسنجی بر اساس نوع تکنیک =====
                if (type == 2)
                {
                    if (it.DropCount == null || it.DropCount < 1 || it.DropCount > 6)
                        return (false, "دراپ ست: تعداد دراپ (۱ تا ۶) الزامی است", 0);
                    if (it.DropWeightPct != null && (it.DropWeightPct < 5 || it.DropWeightPct > 50))
                        return (false, "دراپ ست: درصد کاهش وزن باید بین ۵ تا ۵۰ باشد", 0);
                }
                else if (type == 3 || type == 4 || type == 5)
                {
                    var cnt = it.ExtraItems?.Count ?? 0;
                    if (type == 3 && cnt != 1) return (false, "سوپرست: دقیقاً ۲ حرکت لازم است", 0);
                    if (type == 4 && cnt != 2) return (false, "تری‌ست: دقیقاً ۳ حرکت لازم است", 0);
                    if (type == 5 && (cnt < 3 || cnt > 5)) return (false, "ست غول: بین ۴ تا ۶ حرکت لازم است", 0);
                }
                else if (type == 6)
                {
                    if (it.PyramidDir == null || it.PyramidDir < 1 || it.PyramidDir > 3)
                        return (false, "پیرامید: جهت (صعودی/معکوس/کامل) الزامی است", 0);
                }
                else if (type == 7)
                {
                    if (it.PauseCount == null || it.PauseCount < 1 || it.PauseCount > 5)
                        return (false, "استراحت-مکث: تعداد مینی‌ست (۱ تا ۵) الزامی است", 0);
                    if (it.PauseRest != null && (it.PauseRest < 5 || it.PauseRest > 60))
                        return (false, "استراحت-مکث: مکث باید بین ۵ تا ۶۰ ثانیه باشد", 0);
                }
                else if (type == 8)
                {
                    if (string.IsNullOrWhiteSpace(it.Tempo) || !Regex.IsMatch(it.Tempo.Trim(), @"^\d(-\d){2,3}$"))
                        return (false, "تمپو نامعتبر است (نمونه درست: 3-1-2-0)", 0);
                }

                // ===== حرکت اصلی =====
                var itemID = await ResolveItemIDAsync(it.ItemID, it.NewItemDesc);
                if (itemID == 0) continue;

                var isCombo = type == 3 || type == 4 || type == 5;
                int? group = isCombo ? comboSeq++ : null;

                _db.SportPrgDtls.Add(new SportPrgDtl
                {
                    PrgID = head.PrgID,
                    ItemID = itemID,
                    SetCount = it.SetCount,
                    WCount = it.WCount,
                    RepCount = it.RepCount,
                    RestSeconds = it.RestSeconds,
                    ExerciseType = type,
                    DayTitle = it.DayTitle,
                    Note = it.Note,
                    SortOrder = order++,
                    ComboGroup = group,
                    SeqInCombo = isCombo ? (byte)1 : null,
                    DropCount = it.DropCount,
                    DropWeightPct = it.DropWeightPct,
                    PyramidDir = it.PyramidDir,
                    WeightStep = it.WeightStep,
                    PauseCount = it.PauseCount,
                    PauseRest = it.PauseRest,
                    Tempo = it.Tempo
                });

                // ===== حرکت‌های هم‌گروه (combo) =====
                if (isCombo && it.ExtraItems != null)
                {
                    byte seq = 2;
                    foreach (var ex in it.ExtraItems)
                    {
                        var exID = await ResolveItemIDAsync(ex.ItemID, ex.NewItemDesc);
                        if (exID == 0) continue;
                        _db.SportPrgDtls.Add(new SportPrgDtl
                        {
                            PrgID = head.PrgID,
                            ItemID = exID,
                            RepCount = ex.RepCount,
                            WCount = ex.WCount,
                            ExerciseType = type,
                            DayTitle = it.DayTitle,
                            SortOrder = order++,
                            ComboGroup = group,
                            SeqInCombo = seq++
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            return (true, "برنامه با موفقیت ذخیره شد", head.PrgID);
        }

        /// <summary>حذف برنامه</summary>
        public async Task<(bool ok, string msg)> DeleteProgramAsync(int prgID, int coachMemberID)
        {
            var head = await _db.SportPrgs.FirstOrDefaultAsync(p => p.PrgID == prgID && p.CoachID == coachMemberID);
            if (head == null) return (false, "برنامه یافت نشد");
            _db.SportPrgs.Remove(head);
            await _db.SaveChangesAsync();
            return (true, "برنامه حذف شد");
        }

        /// <summary>جزئیات برنامه + گروه‌بندی روزها</summary>
        public async Task<CoachProgramDetailsDto?> GetProgramDetailsAsync(int prgID, int coachMemberID)
        {
            var head = await (
                from p in _db.SportPrgs
                where p.PrgID == prgID && p.CoachID == coachMemberID
                join m in _db.Gen_Members on p.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join pr in _db.Gen_Persons on m.PersonID equals pr.PersonID into pj
                from pr in pj.DefaultIfEmpty()
                select new { p.PrgID, p.StartDate, p.EndDate, Name = (pr.FirstName + " " + pr.LastName) }
            ).FirstOrDefaultAsync();

            if (head == null) return null;

            var rawItems = await (
                from d in _db.SportPrgDtls
                where d.PrgID == prgID
                join i in _db.Gen_PrgmItems on d.ItemID equals i.ItemID into ij
                from i in ij.DefaultIfEmpty()
                orderby d.SortOrder, d.SportPrgDtlID
                select new ProgramItemEditDto
                {
                    ItemID = d.ItemID ?? 0,
                    ItemDesc = i != null ? i.ItemDesc ?? "" : "",
                    SetCount = d.SetCount,
                    WCount = d.WCount,
                    RepCount = d.RepCount,
                    RestSeconds = d.RestSeconds,
                    ExerciseType = d.ExerciseType,
                    DayTitle = d.DayTitle,
                    Note = d.Note,
                    ComboGroup = d.ComboGroup,
                    SeqInCombo = d.SeqInCombo,
                    DropCount = d.DropCount,
                    DropWeightPct = d.DropWeightPct,
                    PyramidDir = d.PyramidDir,
                    WeightStep = d.WeightStep,
                    PauseCount = d.PauseCount,
                    PauseRest = d.PauseRest,
                    Tempo = d.Tempo
                }).ToListAsync();

            var result = new CoachProgramDetailsDto
            {
                PrgID = head.PrgID,
                StudentName = string.IsNullOrWhiteSpace(head.Name) ? "-" : head.Name!,
                StartDateShamsi = ToShamsi(head.StartDate),
                EndDateShamsi = ToShamsi(head.EndDate),
                IsActive = head.EndDate != null && head.EndDate >= DateTime.Now.Date
            };

            var items = AssembleComboItems(rawItems);
            var groups = items
                .GroupBy(x => string.IsNullOrWhiteSpace(x.DayTitle) ? "سایر حرکات" : x.DayTitle!)
                .ToList();
            foreach (var g in groups)
                result.Days.Add(new ProgramDayGroupDto { DayTitle = g.Key, Items = g.ToList() });

            return result;
        }

        /// <summary>برنامه‌های فعال عضو</summary>
        public async Task<List<MemberProgramDto>> GetMemberProgramsAsync(int memberID)
        {
            var heads = await (
                from p in _db.SportPrgs
                where p.MemberID == memberID && p.EndDate != null && p.EndDate >= DateTime.Now.Date
                join cm in _db.Gen_Members on p.CoachID equals cm.MemberID into cmj
                from cm in cmj.DefaultIfEmpty()
                join cp in _db.Gen_Persons on cm.PersonID equals cp.PersonID into cpj
                from cp in cpj.DefaultIfEmpty()
                orderby p.PrgID descending
                select new { p.PrgID, p.StartDate, p.EndDate, Coach = (cp.FirstName + " " + cp.LastName) }
            ).ToListAsync();

            var result = new List<MemberProgramDto>();
            foreach (var h in heads)
            {
                var rawItems = await (
                    from d in _db.SportPrgDtls
                    where d.PrgID == h.PrgID
                    join i in _db.Gen_PrgmItems on d.ItemID equals i.ItemID into ij
                    from i in ij.DefaultIfEmpty()
                    orderby d.SortOrder, d.SportPrgDtlID
                    select new ProgramItemEditDto
                    {
                        ItemID = d.ItemID ?? 0,
                        ItemDesc = i != null ? i.ItemDesc ?? "" : "",
                        SetCount = d.SetCount,
                        WCount = d.WCount,
                        RepCount = d.RepCount,
                        RestSeconds = d.RestSeconds,
                        ExerciseType = d.ExerciseType,
                        DayTitle = d.DayTitle,
                        Note = d.Note,
                        ComboGroup = d.ComboGroup,
                        SeqInCombo = d.SeqInCombo,
                        DropCount = d.DropCount,
                        DropWeightPct = d.DropWeightPct,
                        PyramidDir = d.PyramidDir,
                        WeightStep = d.WeightStep,
                        PauseCount = d.PauseCount,
                        PauseRest = d.PauseRest,
                        Tempo = d.Tempo
                    }).ToListAsync();

                result.Add(new MemberProgramDto
                {
                    PrgID = h.PrgID,
                    CoachName = string.IsNullOrWhiteSpace(h.Coach) ? "مربی" : h.Coach!,
                    StartDateShamsi = ToShamsi(h.StartDate),
                    EndDateShamsi = ToShamsi(h.EndDate),
                    Items = AssembleComboItems(rawItems)
                });
            }
            return result;
        }

        /// <summary>برنامه تکی عضو</summary>
        public async Task<MemberProgramDto?> GetMemberProgramAsync(int memberID, int prgID)
        {
            var head = await (
                from p in _db.SportPrgs
                where p.PrgID == prgID && p.MemberID == memberID
                join cm in _db.Gen_Members on p.CoachID equals cm.MemberID into cmj
                from cm in cmj.DefaultIfEmpty()
                join cp in _db.Gen_Persons on cm.PersonID equals cp.PersonID into cpj
                from cp in cpj.DefaultIfEmpty()
                select new { p.PrgID, Coach = (cp.FirstName + " " + cp.LastName), p.StartDate, p.EndDate }
            ).FirstOrDefaultAsync();

            if (head == null) return null;

            var rawItems = await (
                from d in _db.SportPrgDtls
                where d.PrgID == prgID
                join i in _db.Gen_PrgmItems on d.ItemID equals i.ItemID into ij
                from i in ij.DefaultIfEmpty()
                orderby d.SortOrder, d.SportPrgDtlID
                select new ProgramItemEditDto
                {
                    ItemID = d.ItemID ?? 0,
                    ItemDesc = i != null ? i.ItemDesc ?? "" : "",
                    SetCount = d.SetCount,
                    WCount = d.WCount,
                    RepCount = d.RepCount,
                    RestSeconds = d.RestSeconds,
                    ExerciseType = d.ExerciseType,
                    DayTitle = d.DayTitle,
                    Note = d.Note,
                    ComboGroup = d.ComboGroup,
                    SeqInCombo = d.SeqInCombo,
                    DropCount = d.DropCount,
                    DropWeightPct = d.DropWeightPct,
                    PyramidDir = d.PyramidDir,
                    WeightStep = d.WeightStep,
                    PauseCount = d.PauseCount,
                    PauseRest = d.PauseRest,
                    Tempo = d.Tempo
                }).ToListAsync();

            return new MemberProgramDto
            {
                PrgID = head.PrgID,
                CoachName = string.IsNullOrWhiteSpace(head.Coach) ? "مربی" : head.Coach!,
                StartDateShamsi = ToShamsi(head.StartDate),
                EndDateShamsi = ToShamsi(head.EndDate),
                Items = AssembleComboItems(rawItems)
            };
        }

        /// <summary>همه شاگردان فعال مربی</summary>
        public async Task<List<CoachStudentRowDto>> GetAllStudentsAsync(int coachMemberID)
        {
            var today = ToShamsi(DateTime.Now.Date);
            return await (
                from ams in _db.Acc_MemberSports
                join ss in _db.Gen_SportSanses on ams.SportSanseID equals ss.SportSanseID
                where ss.CoachMemberID == coachMemberID
                   && ams.IsActive == true
                   && ams.EndDate != null
                   && ams.EndDate.CompareTo(today) >= 0
                join m in _db.Gen_Members on ams.MemberID equals m.MemberID
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                orderby p.FirstName, p.LastName
                select new CoachStudentRowDto
                {
                    MemberID = m.MemberID,
                    FullName = (p.FirstName + " " + p.LastName),
                    Mobile = p.Mobile ?? "",
                    SportName = ss.Gen_Sport_Category != null ? ss.Gen_Sport_Category.SportName ?? "" : "",
                    SanseName = ss.SanseName ?? "",
                    EndDate = ams.EndDate ?? ""
                }).ToListAsync();
        }
    }
}
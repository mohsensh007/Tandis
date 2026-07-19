using Microsoft.EntityFrameworkCore;
using TandisWebApp.Models;

namespace TandisWebApp.Data
{
    /// <summary>
    /// کانتکست دیتابیس Tandis Web App
    /// به دیتابیس FullSportDB متصل می‌شود (همان دیتابیس کیوسک)
    /// </summary>
    public class FullSportDbContext : DbContext
    {
        public FullSportDbContext(DbContextOptions<FullSportDbContext> options)
            : base(options)
        {
        }

        // ==================== Gen - جداول اصلی ====================
        public DbSet<Gen_Person> Gen_Persons { get; set; } = null!;
        public DbSet<Gen_Member> Gen_Members { get; set; } = null!;
        public DbSet<Gen_Sport_Category> Gen_Sport_Categories { get; set; } = null!;
        public DbSet<Gen_SportSanse> Gen_SportSanses { get; set; } = null!;
        public DbSet<Gen_SportSanseDetail> Gen_SportSanseDetails { get; set; } = null!;
        public DbSet<Gen_MembershipType> Gen_MembershipTypes { get; set; } = null!;
        public DbSet<Gen_Period> Gen_Periods { get; set; } = null!;
        public DbSet<Gen_Salon> Gen_Salons { get; set; } = null!;
        public DbSet<Gen_Shift> Gen_Shifts { get; set; } = null!;
        public DbSet<Gen_Contract> Gen_Contracts { get; set; } = null!;
        public DbSet<Gen_San> Gen_Sans { get; set; } = null!;
        public DbSet<Gen_Tarefe> Gen_Tarefes { get; set; } = null!;
        public DbSet<Gen_Service> Gen_Services { get; set; } = null!;
        public DbSet<Gen_Stuff> Gen_Stuffs { get; set; } = null!;
        public DbSet<Gen_Stuff_Category> Gen_Stuff_Categories { get; set; } = null!;
        public DbSet<Gen_DayOfWeek> Gen_DayOfWeeks { get; set; } = null!;
        public DbSet<Gen_PosTbl> Gen_PosTbls { get; set; } = null!;
        public DbSet<Gen_Setting> Gen_Settings { get; set; } = null!;

        // ==================== Acc - حسابداری و ثبت‌نام ====================
        public DbSet<Acc_MemberSport> Acc_MemberSports { get; set; } = null!;
        public DbSet<ACC_Ticket> ACC_Tickets { get; set; } = null!;
        public DbSet<ACC_Traffic> ACC_Traffics { get; set; } = null!;
        public DbSet<ACC_MemberService> ACC_MemberServices { get; set; } = null!;
        public DbSet<Acc_BuffetFactor> Acc_BuffetFactors { get; set; } = null!;
        public DbSet<Acc_BuffetFactorDetail> Acc_BuffetFactorDetails { get; set; } = null!;
        public DbSet<ACC_PosTransaction> ACC_PosTransactions { get; set; } = null!;
        public DbSet<ACC_Freez> ACC_Freezs { get; set; } = null!;

        // ==================== Cash - تسویه حساب ====================
        public DbSet<Cash_CreditStatment> Cash_CreditStatments { get; set; } = null!;
        public DbSet<Cash_DebitStatement> Cash_DebitStatements { get; set; } = null!;
        public DbSet<Cash_RefundStatement> Cash_RefundStatements { get; set; } = null!;
        public DbSet<Cash_CreditType> Cash_CreditTypes { get; set; } = null!;
        public DbSet<Cash_DebitType> Cash_DebitTypes { get; set; } = null!;
        public DbSet<Cash_RefundType> Cash_RefundTypes { get; set; } = null!;

        // ==================== Sec / Kiosk ====================
        public DbSet<Sec_Systems> Sec_Systems { get; set; } = null!;
        public DbSet<Kiosk_ChangePassLog> Kiosk_ChangePassLogs { get; set; } = null!;
        public DbSet<DiscountCardType> DiscountCardTypes { get; set; } = null!;

        // ==================== Cmb - کمبوباکس‌های تاریخ ====================
        public DbSet<CmbDay> CmbDays { get; set; } = null!;
        public DbSet<CmbMonth> CmbMonths { get; set; } = null!;
        public DbSet<CmbYear> CmbYears { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // نام ستون محاسباتی FullName (در دیتابیس Computed است)
            modelBuilder.Entity<Gen_Person>()
                .Property(p => p.FullName)
                .HasComputedColumnSql("(ISNULL([FirstName],'') + ' ' + ISNULL([LastName],''))", stored: false);

            // FreeCapacity محاسباتی است
            modelBuilder.Entity<Gen_SportSanse>()
                .Property(p => p.FreeCapacity)
                .HasComputedColumnSql("([dbo].[FN_GetClassFreeCapacity]([SportSanseID],[ClassCapacity]))", stored: false);

            // دقت برای مبالغ Money
            modelBuilder.Entity<ACC_PosTransaction>()
                .Property(p => p.MainAmount).HasColumnType("money");
            modelBuilder.Entity<ACC_PosTransaction>()
                .Property(p => p.AffectiveAmount).HasColumnType("money");

            // دقت برای مبلغ ترافیک
            modelBuilder.Entity<ACC_Traffic>()
                .Property(p => p.Amount).HasColumnType("decimal(18,0)");
        }
    }
}

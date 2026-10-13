-- ============================================================
-- تحلیل بدن اعضا (پنل عضو)
-- پروفایل ثابت هر عضو + تاریخچه اندازه‌گیری‌ها برای نمودار تغییرات
-- ============================================================

IF OBJECT_ID(N'dbo.Ath_BodyProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ath_BodyProfile
    (
        MemberID        INT           NOT NULL CONSTRAINT PK_Ath_BodyProfile PRIMARY KEY,
        HeightCm        DECIMAL(5,1)  NULL,                -- قد (سانتی‌متر)
        ActivityLevel   TINYINT       NOT NULL CONSTRAINT DF_Ath_BodyProfile_Activity DEFAULT 2,  -- 1..5
        SportHistory    TINYINT       NOT NULL CONSTRAINT DF_Ath_BodyProfile_History DEFAULT 0,   -- 0..5
        Goal            TINYINT       NOT NULL CONSTRAINT DF_Ath_BodyProfile_Goal DEFAULT 3,      -- 1=چربی‌سوزی 2=عضله‌سازی 3=حفظ تناسب
        SessionsPerWeek TINYINT       NOT NULL CONSTRAINT DF_Ath_BodyProfile_Sessions DEFAULT 3,
        CreationDate    DATETIME      NULL,
        ModificationTime DATETIME     NULL
    );
END
GO

IF OBJECT_ID(N'dbo.Ath_BodyRecord', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ath_BodyRecord
    (
        RecordID        INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ath_BodyRecord PRIMARY KEY,
        MemberID        INT           NOT NULL,
        RecordDate      CHAR(10)      NOT NULL,            -- شمسی YYYY/MM/DD
        RecordDateTime  DATETIME      NULL,
        WeightKg        DECIMAL(5,1)  NULL,                -- وزن (کیلوگرم)
        WaistCm         DECIMAL(5,1)  NULL,                -- دور کمر
        AbdomenCm       DECIMAL(5,1)  NULL,                -- دور شکم
        ChestCm         DECIMAL(5,1)  NULL,                -- دور سینه
        ArmCm           DECIMAL(5,1)  NULL,                -- دور بازو
        ThighCm         DECIMAL(5,1)  NULL,                -- دور ران
        HipCm           DECIMAL(5,1)  NULL                 -- دور باسن
    );

    CREATE NONCLUSTERED INDEX IX_Ath_BodyRecord_Member
        ON dbo.Ath_BodyRecord (MemberID, RecordDate);

    ALTER TABLE dbo.Ath_BodyRecord WITH CHECK
        ADD CONSTRAINT FK_Ath_BodyRecord_Profile
        FOREIGN KEY (MemberID) REFERENCES dbo.Ath_BodyProfile (MemberID);
END
GO

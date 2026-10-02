-- ============================================================
-- پرداخت با POS از وب‌اپ (Agent از روی صف تراکنش‌ها کار می‌کند)
-- ستون‌های جدید nullable هستند تا اپ کیوسک (LINQ to SQL) بدون
-- تغییر کار کند (INSERT کیوسک این ستون‌ها را نمی‌فرستد → NULL)
-- ============================================================

ALTER TABLE dbo.ACC_PosTransaction ADD CreationDate DATETIME NULL;
ALTER TABLE dbo.ACC_PosTransaction ADD MemberID INT NULL;
GO

-- ایندکس برای پیدا کردن سریع تراکنش‌های در انتظار توسط Agent
CREATE NONCLUSTERED INDEX IX_ACC_PosTransaction_WebPending
ON dbo.ACC_PosTransaction (RefType, ResponseCode)
INCLUDE (CreationDate, MainAmount, MemberID);
GO

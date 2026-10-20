# TandsiWebApp

پنل وب باشگاه تندیس با ASP.NET Core MVC و .NET 10.

## پیش‌نیاز

- .NET SDK `10.0.103`؛ نسخه در `global.json` pin شده است.
- SQL Server برای اجرای واقعی برنامه و endpointهای متصل به دیتابیس.
- برای build لینوکس، Docker و دسترسی به imageهای .NET 10.

## تنظیمات محلی

تنظیمات حساس را در environment یا user-secrets قرار دهید؛ از commit کردن connection string و JWT secret واقعی خودداری کنید. نمونه متغیرها در `.env.example` است.

```powershell
$env:ConnectionStrings__FullSportDB = "Server=.;Database=FullSportDB;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False"
$env:JwtSettings__SecretKey = "یک-کلید-تصادفی-حداقل-۳۲-کاراکتری"
```

## restore، build و اجرا

```bash
dotnet restore TandisWebApp.sln --locked-mode --ignore-failed-sources -p:NuGetAudit=false
dotnet build TandisWebApp.sln --no-restore
dotnet run --project TandisWebApp/TandisWebApp.csproj --no-restore
```

برنامه در تنظیمات توسعه روی `http://localhost:5104` اجرا می‌شود.

معادل‌های کوتاه در `Makefile` قرار دارند:

```bash
make restore
make build
make run
```

## تست آفلاین

این پروژه یک smoke-check بدون دیتابیس، HTTP خارجی یا test framework اضافه دارد. داده‌های مصنوعی را برای محاسبات آخرین رکورد، حذف/ثبت، پاسخ camelCase، نمودار، مودال‌های راهنما، حالت موبایل مالی و آیکون‌های محلی بررسی می‌کند.

```bash
make test
```

یا:

```bash
dotnet run --project tests/TandisWebApp.Checks/TandisWebApp.Checks.csproj --no-restore
```

## Docker

```bash
docker build -t tandis-webapp .
docker run --rm -p 8080:8080 --env-file .env tandis-webapp
```

`Dockerfile` برنامه را با image رسمی SDK می‌سازد و image runtime جدا اجرا می‌کند. برای اجرای کامل، SQL Server و متغیرهای محیطی معتبر باید در دسترس container باشند.

## صفحه تحلیل بدن

- نتایج همیشه بر اساس آخرین ورودی ثبت‌شده (`RecordID`) هستند.
- آخرین وزن و همه دورهای بدن در کارت جداگانه کنار محاسبات نمایش داده می‌شوند.
- نمودار SVG بدون dependency خارجی، responsive و دارای animation با پشتیبانی `prefers-reduced-motion` است.
- آیکون تعجب هر کارت، توضیح اختصاصی همان شاخص را در modal باز می‌کند.
- فرم‌ها، کارت‌ها، تاریخچه و گزارش‌های عضو برای عرض‌های کوچک با تم شیشه‌ای بنفش/فیروزه‌ای سایت هماهنگ شده‌اند.

## محدودیت‌های واقعی

- تست آفلاین، رندر بصری واقعی مرورگر و اتصال به SQL Server را جایگزین نمی‌کند.
- warningهای nullable و دو warning پکیج موجود پروژه از قبل در build دیده می‌شوند و این تغییرات آن‌ها را ایجاد نکرده‌اند.
- DLLهای سخت‌افزاری POS/Locker در runtime باید در محیط مقصد و با تنظیمات دستگاه مربوطه در دسترس باشند.

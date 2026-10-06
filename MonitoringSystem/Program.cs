using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Hubs;
using MonitoringSystem.Data;
using MonitoringSystem.Filters;
// HAPUS: using static NuGet.Packaging.PackagingConstants; // tidak dipakai

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<MonitoringSystem.Models.ScaffoldedDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(
    options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllers();
builder.Services.AddScoped<MonitoringSystem.Services.BreakTimeService>();
builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<AuthorizeFilter>();
    });

builder.Services.AddHostedService<PlanUpdaterService>()
    .Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

builder.Services.AddSignalR();

// PLC ROHIB (Plclogger): dijalankan bersama Panamon & diakses lewat /plcrohib-app/ (menu More > PLC ROHIB)
builder.Services.AddHttpClient("PlcRohib");
builder.Services.AddHostedService<MonitoringSystem.Services.PlcRohibLauncher>();

// Import SAP Plan otomatis dari "Daily prod plan" (setiap 3 jam, default DryRun) - lihat Services/SapPlanImport
builder.Services.Configure<MonitoringSystem.Services.SapPlanImport.SapPlanImportOptions>(builder.Configuration.GetSection("SapPlanImport"));
builder.Services.AddSingleton<MonitoringSystem.Services.SapPlanImport.SapPlanImportRunner>();
builder.Services.AddHostedService<MonitoringSystem.Services.SapPlanImport.SapPlanImportWorker>();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
});

// ✅ FIX 1: Compression HANYA di Production (dev konflik dengan BrowserLink → penyebab loading lama!)
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });
}

var app = builder.Build();

// ✅ FIX 2: Warm up database (tetap dipertahankan, bagus)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Auto-apply pending migrations
        await db.Database.MigrateAsync();
        
        await db.Database.ExecuteSqlRawAsync("SELECT 1");

        // Auto-migrate database columns for shift quantities in ProductionRecords table
        string addColumnsSql = @"
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionRecords') AND name = 'QtyShift1')
        BEGIN
            ALTER TABLE ProductionRecords ADD QtyShift1 INT NULL;
        END
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionRecords') AND name = 'QtyShift2')
        BEGIN
            ALTER TABLE ProductionRecords ADD QtyShift2 INT NULL;
        END
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionRecords') AND name = 'QtyShift3')
        BEGIN
            ALTER TABLE ProductionRecords ADD QtyShift3 INT NULL;
        END
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionRecords') AND name = 'QtyShiftNS')
        BEGIN
            ALTER TABLE ProductionRecords ADD QtyShiftNS INT NULL;
        END";
        await db.Database.ExecuteSqlRawAsync(addColumnsSql);

        // Koreksi hasil produksi untuk PWK. Data sumber OEESN/MasterData tidak pernah diubah;
        // hanya nilai yang berbeda dari sumber disimpan sebagai detail milik PWK_ACTUAL.
        string createPwKProductionDetailSql = @"
        IF OBJECT_ID('dbo.PWK_ACTUAL_ProductionDetail', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.PWK_ACTUAL_ProductionDetail
            (
                Id                  BIGINT IDENTITY(1,1) NOT NULL
                    CONSTRAINT PK_PWK_ACTUAL_ProductionDetail PRIMARY KEY,
                PWKActualId         INT NOT NULL,
                SlotStart           DATETIME2(0) NOT NULL,
                ProductId           NVARCHAR(50) NOT NULL,
                SourceModel         NVARCHAR(100) NOT NULL,
                SourceSerialStart   NVARCHAR(100) NULL,
                SourceSerialEnd     NVARCHAR(100) NULL,
                SourceDailyPlan     INT NOT NULL,
                SourceDailyActual   INT NOT NULL,
                SourceDefect        INT NOT NULL,
                Model               NVARCHAR(100) NOT NULL,
                SerialStart         NVARCHAR(100) NULL,
                SerialEnd           NVARCHAR(100) NULL,
                DailyPlan           INT NOT NULL,
                DailyActual         INT NOT NULL,
                Defect              INT NOT NULL,
                Keterangan          NVARCHAR(1000) NULL,
                CreatedBy           NVARCHAR(100) NULL,
                CreatedAt           DATETIME2(0) NOT NULL
                    CONSTRAINT DF_PWK_ACTUAL_ProductionDetail_CreatedAt DEFAULT GETDATE(),
                UpdatedBy           NVARCHAR(100) NULL,
                UpdatedAt           DATETIME2(0) NULL,
                CONSTRAINT FK_PWK_ACTUAL_ProductionDetail_Header
                    FOREIGN KEY (PWKActualId) REFERENCES dbo.PWK_ACTUAL(Id) ON DELETE CASCADE,
                CONSTRAINT UQ_PWK_ACTUAL_ProductionDetail
                    UNIQUE (PWKActualId, SlotStart, ProductId)
            );
        END

        IF COL_LENGTH('dbo.PWK_ACTUAL_ProductionDetail', 'DisplayStart') IS NULL
            ALTER TABLE dbo.PWK_ACTUAL_ProductionDetail ADD DisplayStart TIME(0) NULL;

        IF COL_LENGTH('dbo.PWK_ACTUAL_ProductionDetail', 'DisplayEnd') IS NULL
            ALTER TABLE dbo.PWK_ACTUAL_ProductionDetail ADD DisplayEnd TIME(0) NULL;";
        await db.Database.ExecuteSqlRawAsync(createPwKProductionDetailSql);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ DB warm-up skipped (DB not reachable): {ex.Message}");
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseResponseCompression(); // ✅ FIX 3: Pindah ke sini, hanya aktif di Production
}

app.UseHttpsRedirection();

// ✅ FIX 4: Cache static files tetap dipertahankan
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
    }
});

app.UseRouting();
app.UseCookiePolicy(); // ✅ FIX 5: TAMBAH INI — CookiePolicy harus di-use, bukan hanya di-configure!
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<LossTimeHub>("/dataHub");
app.MapControllers();
app.MapRazorPages();
MonitoringSystem.Services.PlcRohibProxy.MapPlcRohibProxy(app);

app.Run();

using Microsoft.EntityFrameworkCore;
using SubPanamon.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<SubPanamonDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Dijalankan sendiri (Kestrel / dotnet run) di bawah sub-path, mis. /subpanamon.
// Kalau di-deploy sebagai sub-application IIS, PathBase sudah diatur IIS: kosongkan "PathBase".
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
    app.UsePathBase(pathBase);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

// Halaman awal langsung ke PWK Actual
app.MapGet("/", (HttpContext ctx) => Results.Redirect($"{ctx.Request.PathBase}/PWKActual"));
app.MapRazorPages();

app.Run();

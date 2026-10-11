using AlpacaMedic.Data;
using AlpacaMedic.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:7070");
var path = builder.Configuration["DatabasePath"] ?? "/data/alpacamedic.db";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
builder.Services.AddDbContext<RiskDb>(o => o.UseSqlite($"Data Source={path}"));
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<AttackAnalysisService>();
builder.Services.AddScoped<AttackPathService>();
builder.Services.AddScoped<RiskAnalysisService>();
builder.Services.AddOptions<RiskLevels>().Bind(builder.Configuration.GetSection("RiskLevels"))
    .Validate(v => v.LowMax >= 1 && v.LowMax < v.HighMin && v.HighMin < v.CriticalMin && v.CriticalMin <= 25,
        "RiskLevels must have increasing boundaries between 1 and 25.").ValidateOnStart();
builder.Services.AddScoped<AlpacaMedic.Controllers.AnalysisExceptionFilter>();
builder.Services.AddScoped<CisPriorityService>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RiskDb>();
    await db.Database.MigrateAsync();
    await AttackImporter.Initialize(db, Path.Combine(app.Environment.ContentRootPath, "Data", "Seed", "enterprise-attack.json"),
        scope.ServiceProvider.GetRequiredService<ILogger<AttackImporter>>());
    await SeedData.Initialize(db);
}

app.UseExceptionHandler("/error");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});
app.UseStaticFiles();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

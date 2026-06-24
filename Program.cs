using QuestPDF.Infrastructure;
using TalleresRMP.Data;
using TalleresRMP.Services;

var builder = WebApplication.CreateBuilder(args);

// Licencia Community de QuestPDF (requerida antes de generar cualquier PDF).
QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TursoConnection>();
builder.Services.AddScoped<MantenimientoRepository>();
builder.Services.AddScoped<MantenimientoCacheService>();
builder.Services.AddScoped<ProformaPdfService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

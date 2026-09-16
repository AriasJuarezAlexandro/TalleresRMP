using QuestPDF.Infrastructure;
using TalleresRMP.Services;

var builder = WebApplication.CreateBuilder(args);

// Licencia Community de QuestPDF (requerida antes de generar cualquier PDF).
QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TursoService>();
builder.Services.AddSingleton<ProformaPdfService>();
builder.Services.AddScoped<MantenimientoCacheService>();
builder.Services.AddScoped<UsuarioService>();

// Sesión simple para el login (sin librerías de auth externas).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

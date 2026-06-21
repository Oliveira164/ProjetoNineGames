var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ── Sessão ──────────────────────────────────────────────────────────────────
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".NineGames.Session";
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// !! A sessão DEVE vir antes do mapeamento de rotas/MVC
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

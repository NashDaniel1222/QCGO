var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// HttpClient for image proxying
builder.Services.AddHttpClient();

// Authentication (cookie)
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.Cookie.Name = "QCGO.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

// Bind MongoDB settings from configuration and register SpotService
builder.Services.Configure<QCGO.Models.MongoSettings>(builder.Configuration.GetSection("Mongo"));
// Resolve MongoSettings and register SpotService
builder.Services.AddSingleton(sp => {
    var settings = new QCGO.Models.MongoSettings();
    builder.Configuration.GetSection("Mongo").Bind(settings);
    return settings;
});
builder.Services.AddSingleton<QCGO.Services.SpotService>();
builder.Services.AddSingleton<QCGO.Services.AccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

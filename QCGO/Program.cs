var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// HttpClient for image proxying
builder.Services.AddHttpClient();

// Bind MongoDB settings from configuration and register SpotService
builder.Services.Configure<QCGO.Models.MongoSettings>(builder.Configuration.GetSection("Mongo"));
// Resolve MongoSettings and register SpotService
builder.Services.AddSingleton(sp => {
    var settings = new QCGO.Models.MongoSettings();
    builder.Configuration.GetSection("Mongo").Bind(settings);
    return settings;
});
builder.Services.AddSingleton<QCGO.Services.SpotService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

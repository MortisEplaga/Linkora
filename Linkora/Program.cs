using Linkora.Repositories;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 52_428_800; // 50 MB
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 52_428_800;
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToAccessDenied = context => {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<Linkora.Hubs.MessageHub>("/hubs/messages");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

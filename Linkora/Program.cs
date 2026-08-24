using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
builder.Services.AddScoped<ISupportRepository, SupportRepository>();
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISellerRepository, SellerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISelectOptionRepository, SelectOptionRepository>();
builder.Services.AddScoped<ICompareRepository, CompareRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IAdminService, AdminService>(); 
builder.Services.AddScoped<INotificationRealTimeSender, SignalRNotificationSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IMaksekeskusService, MaksekeskusService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
builder.Services.AddHostedService<ArchiveOldProductsService>();
builder.Services.AddSignalR();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 52_428_800;
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 52_428_800;
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/Account/GoogleCallback";
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.SignInScheme = "Cookies";
    });

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost
});
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.MapControllerRoute(
    name: "terms",
    pattern: "terms",
    defaults: new { controller = "Home", action = "Terms" });
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
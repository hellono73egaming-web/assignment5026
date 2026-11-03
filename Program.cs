using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity with our ApplicationUser + roles
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;   // secure login: lock after 5 wrong tries
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;        // one account per email
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// External logins (Google / Facebook / Microsoft). Keys live in User Secrets, NOT in source code.
// A provider is only switched on if its keys exist, so the app still runs without them.
var auth = builder.Services.AddAuthentication();

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleId) && !string.IsNullOrEmpty(googleSecret))
{
    auth.AddGoogle(options =>
    {
        options.ClientId = googleId;
        options.ClientSecret = googleSecret;
    });
}

var facebookId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrEmpty(facebookId) && !string.IsNullOrEmpty(facebookSecret))
{
    auth.AddFacebook(options =>
    {
        options.AppId = facebookId;
        options.AppSecret = facebookSecret;
        options.Fields.Add("name");
        options.Fields.Add("email");
    });
}

var microsoftId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftId) && !string.IsNullOrEmpty(microsoftSecret))
{
    auth.AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftId;
        options.ClientSecret = microsoftSecret;
    });
}

// Role-based access: whole folders locked to a role
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/AdminPanel", "AdminOnly");
    options.Conventions.AuthorizeFolder("/StaffPanel", "StaffOrAdmin");
    options.Conventions.AuthorizeFolder("/CustomerPanel", "CustomerOnly");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    options.AddPolicy("StaffOrAdmin", p => p.RequireRole(Roles.Staff, Roles.Admin));
    options.AddPolicy("CustomerOnly", p => p.RequireRole(Roles.Customer));
});

// Session - stores the shopping cart between page visits
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

// Business logic services, registered by interface (one instance per web request).
// Pages ask for the interface (e.g. IOrderService); DI supplies the implementation.
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Create roles, default accounts and sample data
await DbSeeder.SeedAsync(app.Services);

app.Run();

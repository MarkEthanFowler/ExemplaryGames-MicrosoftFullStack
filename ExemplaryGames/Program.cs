using Microsoft.EntityFrameworkCore;
using ExemplaryGames.Models;
using ExemplaryGames.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Adding cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Users/Login"; //where to send unauthenticated users
    options.LogoutPath = "/Users/Logout";
    options.AccessDeniedPath = "/Users/Login";
});

//password hasher (Identity core class, but we're not using full Identity)
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

//add memory and login limiter
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();

//register all services blazor server needs
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Configure the HTTP request pipeline.
//Exists in development mode only
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//This makes it so we have an error page that exists outside of development
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

//sets up the SignalR endpoint Blazor Server uses to communicate with connected browsers
app.MapBlazorHub();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using BankingApp.API.Middleware;
using BankingApp.Core.Entities;
using BankingApp.Core.Interfaces;
using BankingApp.Infrastructure.Data;
using BankingApp.Infrastructure.Repositories;
using BankingApp.Infrastructure.Services;
using BankingApp.Infrastructure.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// configure serilog to read settings from appsettings.json and write to both console and a daily rolling log file
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/banking-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// register ef core with sql server — migrations are defined in the infrastructure project
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("BankingApp.Infrastructure")));

// configure asp.net identity with relaxed password rules — uppercase and special chars not required
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit = true;
    opt.Password.RequiredLength = 6;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// set up jwt bearer authentication — all parameters are read from appsettings.json
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// redis is optional — if unavailable the app continues without caching
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConn))
{
    try
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(redisConn);
        // register as singleton so the same multiplexer is reused across all requests
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
        Log.Information("Redis connected successfully.");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Redis unavailable. Reports will be calculated without cache.");
    }
}

// repositories — scoped so each request gets its own instance tied to the same db context
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IRecurringPaymentRepository, RecurringPaymentRepository>();
builder.Services.AddScoped<IBillRepository, BillRepository>();
builder.Services.AddScoped<IMonthlyReportRepository, MonthlyReportRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();

// services — scoped to match repository lifetime and avoid cross-request state leaks
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IRecurringPaymentService, RecurringPaymentService>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ITagService, TagService>();

// background worker that processes recurring payments on a schedule
builder.Services.AddHostedService<RecurringPaymentWorker>();

builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// open cors policy — lock this down in production to specific origins
builder.Services.AddCors(opt => opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// global exception handler — catches unhandled exceptions and returns a consistent error response
app.UseMiddleware<ExceptionMiddleware>();
// logs every http request automatically via serilog
app.UseSerilogRequestLogging();

// in production, use the built-in error page and enable hsts for https enforcement
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
// authentication must come before authorization in the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

// run migrations and seed data on startup — wrapped in try/catch so a seed failure doesn't crash the app
using (var scope = app.Services.CreateScope())
{
    try
    {
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.MigrateAsync();
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex) { Log.Error(ex, "Error during database seed."); }
}

app.Run();
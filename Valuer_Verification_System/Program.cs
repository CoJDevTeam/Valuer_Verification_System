using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Services.Notice;
using Valuer_Verification_System.Services.Permissions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VvsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews();


//-----------------Email--------------------------------------
builder.Services.Configure<Valuer_Verification_System.Infrastructure.Configuration.EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddScoped<IEmailService,
    EmailService>();

//----------------------------------------Storage---------------------------------------
builder.Services.Configure<Valuer_Verification_System.Infrastructure.Configuration.StorageSettings>(
    builder.Configuration.GetSection("Storage"));

builder.Services.AddSingleton<Valuer_Verification_System.Infrastructure.Storage.IStorageService,
    Valuer_Verification_System.Infrastructure.Storage.StorageService>();

//-------------------------Date Configurations-------------------------------------
builder.Services.Configure<Valuer_Verification_System.Infrastructure.Configuration.HolidaySettings>(
    builder.Configuration.GetSection("Holidays"));

builder.Services.AddScoped<Valuer_Verification_System.Domain.Dates.IWeekWindowCalculator,
    Valuer_Verification_System.Domain.Dates.WeekWindowCalculator>();

builder.Services.AddScoped<Valuer_Verification_System.Domain.WeekWindows.IWeekWindowService,
    Valuer_Verification_System.Domain.WeekWindows.WeekWindowService>();

//-------------------------------Upload---------------------------------------
builder.Services.AddScoped<Valuer_Verification_System.Services.Upload.IExtractUploadService,
    Valuer_Verification_System.Services.Upload.ExtractUploadService>();

//------------------------------------------Valuer Inbox---------------------------------
builder.Services.AddScoped<Valuer_Verification_System.Services.Valuer.IValuerVerificationService,
    Valuer_Verification_System.Services.Valuer.ValuerVerificationService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Valuer.IValuerDashboardService,
    Valuer_Verification_System.Services.Valuer.ValuerDashboardService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Valuer.IValuerRowDetailsService,
    Valuer_Verification_System.Services.Valuer.ValuerRowDetailsService>();


//-------------------------------Manager Reassignment---------------------------------
builder.Services.AddScoped<Valuer_Verification_System.Services.Manager.IManagerReassignmentService,
    Valuer_Verification_System.Services.Manager.ManagerReassignmentService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Manager.IManagerDashboardService,
    Valuer_Verification_System.Services.Manager.ManagerDashboardService>();


//---------------------------------Notice ---------------------------------
builder.Services.AddScoped<Valuer_Verification_System.Services.Notice.INoticeGuardService,
    Valuer_Verification_System.Services.Notice.NoticeGuardService>();

builder.Services.AddScoped<INoticeCleaningService, NoticeCleaningService>();
builder.Services.AddScoped<INoticePdfService, NoticePdfService>();
builder.Services.AddScoped<INoticePrintService, NoticePrintService>();
builder.Services.AddScoped<Valuer_Verification_System.Services.Stats.ISection78StatsService,
    Valuer_Verification_System.Services.Stats.Section78StatsService>();
builder.Services.AddScoped<Valuer_Verification_System.Services.Stats.ISection78StatsService,
    Valuer_Verification_System.Services.Stats.Section78StatsService>();
builder.Services.AddScoped<Valuer_Verification_System.Services.Notice.INoticeSendService,
    Valuer_Verification_System.Services.Notice.NoticeSendService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Stats.IStatsExportService,
    Valuer_Verification_System.Services.Stats.StatsExportService>();

//------------------------------Home--------------------------------------------

builder.Services.AddScoped<Valuer_Verification_System.Services.Home.IDataTeamDashboardService,
    Valuer_Verification_System.Services.Home.DataTeamDashboardService>();

//----------------------------Notifications--------------------------------
builder.Services.AddScoped<Valuer_Verification_System.Services.Notifications.INotificationService,
    Valuer_Verification_System.Services.Notifications.NotificationService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Notifications.INotificationReminderJob,
    Valuer_Verification_System.Services.Notifications.NotificationReminderJob>();

builder.Services.AddHostedService<Valuer_Verification_System.Services.Notifications.NotificationHostedService>();

builder.Services.AddScoped<Valuer_Verification_System.Services.Notifications.INotificationService,
    Valuer_Verification_System.Services.Notifications.NotificationService>();
// ── NEW: IHttpContextAccessor (required by UserService) ───────────────────────
builder.Services.AddHttpContextAccessor();



// ── NEW: User + Permission services ──────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IPermissionService, PermissionService>();

// ✅ Bind RolePermissions from appsettings.json
builder.Services.Configure<RolePermissionsOptions>(
    builder.Configuration.GetSection(RolePermissionsOptions.SectionName));

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddRazorPages();

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


app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using QuestPDF.Infrastructure;
using SOMS.Data;
using SOMS.Services;

namespace SOMS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var somsTheme = new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#3B5BDB",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#64748B",
                Background = "#FFFFFF",
                Surface = "#F8F9FA",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#0F172A",
                DrawerBackground = "#1E293B",
                DrawerText = "#F1F5F9",
                DrawerIcon = "#94A3B8",
                TextPrimary = "#0F172A",
                TextSecondary = "#64748B",
                ActionDefault = "#64748B",
                Divider = "#E2E8F0",
                TableLines = "#E2E8F0",
                TableHover = "#F1F5F9",
                Error = "#EF4444",
                Warning = "#F59E0B",
                Success = "#22C55E",
                Info = "#3B82F6",
                OverlayDark = "rgba(15,23,42,0.6)"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = new[] { "Segoe UI", "system-ui", "sans-serif" },
                    FontSize = "14px",
                    LineHeight = "1.5"
                },
                H6 = new H6Typography { FontSize = "16px", FontWeight = "600" },
                Body1 = new Body1Typography { FontSize = "14px" },
                Body2 = new Body2Typography { FontSize = "13px" },
                Caption = new CaptionTypography { FontSize = "12px" }
            },
            LayoutProperties = new LayoutProperties
            {
                DrawerWidthLeft = "220px",
                AppbarHeight = "56px",
                DefaultBorderRadius = "4px"
            }
        };

        builder.Services.AddSingleton(somsTheme);
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            config.SnackbarConfiguration.ShowCloseIcon = true;
        });
        builder.Services.AddDbContext<AppDbContext>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddScoped<MemberService>();
        builder.Services.AddScoped<EventService>();
        builder.Services.AddScoped<AttendanceService>();
        builder.Services.AddScoped<InteractionService>();
        builder.Services.AddScoped<EngagementService>();
        builder.Services.AddScoped<DocumentService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddScoped<SettingsService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        QuestPDF.Settings.License = LicenseType.Community;

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (dbContext.Database.GetMigrations().Any())
        {
            dbContext.Database.Migrate();
        }
        else
        {
            dbContext.Database.EnsureCreated();
        }

        return app;
    }
}

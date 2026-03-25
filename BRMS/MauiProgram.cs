using BRMS.Data;
using BRMS.Helpers;
using BRMS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MudBlazor;
using MudBlazor.Services;
using QuestPDF.Infrastructure;

namespace BRMS;

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

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "brms.db");

        var brmsTheme = new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#3B5BDB",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#64748B",
                Background = "#F8F9FA",
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#0F172A",
                DrawerBackground = "#1E293B",
                DrawerText = "#F1F5F9",
                DrawerIcon = "#94A3B8",
                TextPrimary = "#0F172A",
                TextSecondary = "#64748B",
                ActionDefault = "#94A3B8",
                Divider = "#E2E8F0",
                TableLines = "#E2E8F0",
                TableHover = "#F8FAFC",
                Success = "#16A34A",
                Info = "#2563EB",
                Warning = "#D97706",
                Error = "#DC2626"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = new[] { "Segoe UI", "Segoe UI Variable", "Tahoma", "sans-serif" },
                    FontSize = "14px",
                    LineHeight = "1.5"
                },
                H4 = new H4Typography
                {
                    FontFamily = new[] { "Segoe UI", "Segoe UI Variable", "Tahoma", "sans-serif" },
                    FontWeight = "700"
                },
                H5 = new H5Typography
                {
                    FontFamily = new[] { "Segoe UI", "Segoe UI Variable", "Tahoma", "sans-serif" },
                    FontWeight = "700"
                },
                H6 = new H6Typography
                {
                    FontFamily = new[] { "Segoe UI", "Segoe UI Variable", "Tahoma", "sans-serif" },
                    FontWeight = "600"
                }
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "4px",
                DrawerWidthLeft = "220px",
                AppbarHeight = "56px"
            }
        };

        builder.Services.AddSingleton(brmsTheme);
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass =
                Defaults.Classes.Position.BottomRight;
        });
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<CsvImportHelper>();
        builder.Services.AddScoped<ResidentService>();
        builder.Services.AddScoped<HouseholdService>();
        builder.Services.AddScoped<EventService>();
        builder.Services.AddScoped<AttendanceService>();
        builder.Services.AddScoped<InteractionService>();
        builder.Services.AddScoped<EngagementService>();
        builder.Services.AddScoped<BlotterService>();
        builder.Services.AddScoped<ClearanceService>();
        builder.Services.AddScoped<DocumentService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<BackupService>();
        builder.Services.AddSingleton<SessionHelper>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        QuestPDF.Settings.License = LicenseType.Community;

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();

        try
        {
            SeedData.InitializeAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SeedData initialization failed: {ex.Message}");
            Console.WriteLine(ex);
        }

        return app;
    }
}

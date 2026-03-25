using BRMS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BRMS;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
        window.Destroying += HandleWindowDestroying;
        return window;
    }

    private async void HandleWindowDestroying(object? sender, EventArgs e)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
            await backupService.BackupOnShutdownAsync();
        }
        catch
        {
        }
    }
}

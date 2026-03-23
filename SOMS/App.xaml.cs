using Microsoft.Extensions.DependencyInjection;
using SOMS.Services;

namespace SOMS;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "SOMS" };
        window.Stopped += OnWindowStopped;
        window.Destroying += OnWindowDestroying;
        return window;
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _ = TriggerBackupAsync();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        _ = TriggerBackupAsync();
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        _ = TriggerBackupAsync();
    }

    private async Task TriggerBackupAsync()
    {
        using var scope = _services.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
        await backupService.BackupAsync();
    }
}

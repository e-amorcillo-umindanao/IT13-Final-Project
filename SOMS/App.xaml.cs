namespace SOMS
{
    public partial class App : Application
    {
        private readonly SOMS.Services.BackupService _backupService;

        public App(SOMS.Services.BackupService backupService)
        {
            InitializeComponent();
            _backupService = backupService;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage());
            window.Stopped += HandleWindowStopped;
            window.Destroying += HandleWindowDestroying;
            return window;
        }

        protected override async void OnSleep()
        {
            base.OnSleep();
            await _backupService.BackupAsync();
        }

        private async void HandleWindowStopped(object? sender, EventArgs e)
        {
            await _backupService.BackupAsync();
        }

        private async void HandleWindowDestroying(object? sender, EventArgs e)
        {
            await _backupService.BackupAsync();
        }
    }
}

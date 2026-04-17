using CulinaryApp.Services;

namespace CulinaryApp
{
    public partial class App : Application
    {
       
        private HeartbeatService _heartbeatService;

        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }

        protected override void OnStart()
        {
            base.OnStart();

            _heartbeatService = new HeartbeatService();
            _ = _heartbeatService.StartHeartbeatAsync();
        }
    }
}
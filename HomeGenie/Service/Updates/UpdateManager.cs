namespace HomeGenie.Service.Updates
{
    public class UpdateManager
    {
        public UpdateChecker UpdateChecker { get; }
        public UpdateInstaller UpdateInstaller { get; }
        public ReleaseInfo CurrentRelease { get; }

        public UpdateManager(HomeGenieService homeGenieService)
        {
            UpdateChecker = new UpdateChecker();
            UpdateInstaller = new UpdateInstaller(homeGenieService);

            CurrentRelease = UpdateChecker.GetCurrentRelease();
        }

        public void Start()
        {
            UpdateChecker.Start();
        }

        public void Stop()
        {
            UpdateChecker.Stop();
        }
    }
}

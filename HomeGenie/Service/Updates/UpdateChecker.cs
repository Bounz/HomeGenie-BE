using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using HomeGenie.Service.Updates.GitHub;
using Newtonsoft.Json;

namespace HomeGenie.Service.Updates
{
    public class UpdateChecker
    {
        public delegate void UpdateProgressEvent(object sender, UpdateProgressEventArgs args);

        public UpdateProgressEvent UpdateProgress;

        private readonly string _githubReleases = "https://api.github.com/repos/Bounz/HomeGenie-BE/releases";

        private readonly string _dockerHubReleases =
            "https://registry.hub.docker.com/v2/repositories/bounz/homegenie/tags";

        private ReleaseInfo _currentRelease;
        private List<ReleaseInfo> _newReleases;
        private readonly Timer _checkForUpdatesTimer;

        private static readonly HttpClient HttpClient = new HttpClient();

        public List<ReleaseInfo> NewReleases => _newReleases;
        public ReleaseInfo NewestRelease => _newReleases?.FirstOrDefault();
        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;

        static UpdateChecker()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HomeGenieUpdater", "1.1"));
        }

        // TODO: add automatic interval check and "UpdateAvailable", "UpdateChecking" events

        public UpdateChecker()
        {
            _checkForUpdatesTimer = new Timer(TimeSpan.FromHours(24.0).TotalMilliseconds) {AutoReset = true};
            _checkForUpdatesTimer.Elapsed += CheckForUpdates_Elapsed;

            _newReleases = new List<ReleaseInfo>();
        }

        private void CheckForUpdates_Elapsed(object sender, ElapsedEventArgs e)
        {
            Check();
        }

        public bool Check()
        {
            UpdateProgress?.Invoke(this, new UpdateProgressEventArgs(UpdateProgressStatus.Started));

            try
            {
                _newReleases = Task.Run(GetNewReleasesAsync).Result;
                UpdateProgress?.Invoke(this, new UpdateProgressEventArgs(UpdateProgressStatus.Completed));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UpdateProgress?.Invoke(this, new UpdateProgressEventArgs(UpdateProgressStatus.Error));
            }

            return _newReleases.Any();
        }

        public void Start()
        {
            Check();
            _checkForUpdatesTimer.Start();
        }

        public void Stop()
        {
            _checkForUpdatesTimer.Stop();
        }

        public ReleaseInfo GetCurrentRelease()
        {
            return _currentRelease = UpdatesHelper.GetReleaseInfoFromFile(UpdatesHelper.ReleaseFile);
        }

        private async Task<List<ReleaseInfo>> GetNewReleasesAsync()
        {
            var releases = new List<ReleaseInfo>();

            var latestReleases = await GetLatestGitHubReleaseAsync(CurrentVersion);
            foreach (var gitHubRelease in latestReleases)
            {
                var relFile = gitHubRelease.Assets.FirstOrDefault(x => x.BrowserDownloadUrl.EndsWith(".tgz") ||
                                                                       x.BrowserDownloadUrl.EndsWith(".zip"));
                if (relFile == null)
                    continue;

                var release = new ReleaseInfo
                {
                    Version = gitHubRelease.TagName,
                    Description = gitHubRelease.Name,
                    ReleaseNote = gitHubRelease.Description,
                    DownloadUrl = relFile.BrowserDownloadUrl,
                    ReleaseDate = gitHubRelease.CreatedAt
                };
                releases.Add(release);
            }

            return releases;
        }

        private async Task<List<GitHubRelease>> GetLatestGitHubReleaseAsync(Version currentVersion,
            bool checkForPrereleases = false)
        {
#if DEBUG
            checkForPrereleases = true;
#endif
            var releasesString = await HttpClient.GetStringAsync(_githubReleases);
            try
            {
                var gitHubReleases = JsonConvert.DeserializeObject<List<GitHubRelease>>(releasesString);
                var orderedReleases = gitHubReleases
                    .OrderByDescending(x => x.CreatedAt)
                    .Where(x => x.Version >= currentVersion);
                var latestReleases = checkForPrereleases
                    ? orderedReleases
                    : orderedReleases.Where(x => x.Prerelease = false);

                return latestReleases.ToList();
            }
            catch (Exception e)
            {
                // TODO: write log
                Console.WriteLine(e.Message);
                return new List<GitHubRelease>();
            }
        }

        public bool IsUpdateAvailable
        {
            get
            {
                var update = false;
                if (_newReleases != null)
                {
                    foreach (var releaseInfo in _newReleases)
                    {
                        if (_currentRelease != null && _currentRelease.ReleaseDate < releaseInfo.ReleaseDate)
                        {
                            update = true;
                            break;
                        }
                    }
                }

                return update;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;
using HomeGenie.Service.Updates.GitHub;
using Newtonsoft.Json;

namespace HomeGenie.Service.Updates
{
    public class UpdateChecker
    {
        public delegate void UpdateProgressEvent(object sender, UpdateProgressEventArgs args);
        public UpdateProgressEvent UpdateProgress;

        private readonly string _githubReleases = "https://api.github.com/repos/Bounz/HomeGenie-BE/releases";

        private ReleaseInfo _currentRelease;
        private List<ReleaseInfo> _newReleases;
        private readonly Timer _checkForUpdatesTimer;

        private static readonly HttpClient HttpClient = new HttpClient();
        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;

        static UpdateChecker()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HomeGenieUpdater", "1.1"));
        }

        // TODO: this is just a temporary hack not meant to be used in production enviroment
        private static bool Validator(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        )
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            // Work-around missing certificates
            var remoteCertificateHash = certificate.GetCertHashString();
            var acceptedCertificates = new List<string>
            {
                // Amazon AWS github files hosting
                "89E471D8A4977D0D9C6E67E557BF36A74A5A01DB",
                // github.com
                "D79F076110B39293E349AC89845B0380C19E2F8B",
                // api.github.com
                "CF059889CAFF8ED85E5CE0C2E4F7E6C3C750DD5C",
                "358574EF6735A7CE406950F3C0F680CF803B2E19"
            };
            // try to load acceptedCertificates from file "certaccept.xml"
            try
            {
                var xmlSerializer = new XmlSerializer(typeof(List<string>));
                using (var stringReader = new StringReader(File.ReadAllText("certaccept.xml")))
                {
                    var cert = (List<string>)xmlSerializer.Deserialize(stringReader);
                    acceptedCertificates.Clear();
                    acceptedCertificates.AddRange(cert);
                }
            } catch { }

            // verify against accepted certificate hash strings
            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors
                && acceptedCertificates.Contains(remoteCertificateHash))
            {
                Console.WriteLine("Applied 'SSL certificates issue' work-around.");
                return true;
            }
            else
            {
                Console.WriteLine("SSL validation error! Remote hash is: {0}", remoteCertificateHash);
                return false;
            }
        }


        // TODO: add automatic interval check and "UpdateAvailable", "UpdateChecking" events

        public UpdateChecker()
        {
            _checkForUpdatesTimer = new Timer(TimeSpan.FromHours(24.0).TotalMilliseconds) {AutoReset = true};
            _checkForUpdatesTimer.Elapsed += CheckForUpdates_Elapsed;

            _newReleases = new List<ReleaseInfo>();

            // TODO: SSL connection certificate validation:
            // TODO: this is just an hack to fix certificate issues happening sometimes on api.github.com site,
            // TODO: not meant to be used in production enviroment
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // TODO: this is just an hack to fix certificate issues on mono < 4.0,
                ServicePointManager.ServerCertificateValidationCallback = Validator;
            }
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

        public List<ReleaseInfo> NewReleases => _newReleases;

        public ReleaseInfo NewestRelease => _newReleases?.FirstOrDefault();

        private async Task<List<ReleaseInfo>> GetNewReleasesAsync()
        {
            var releases = new List<ReleaseInfo>();

            var latestReleases = await GetLatestGitHubReleaseAsync(CurrentVersion);
            foreach (var gitHubRelease in latestReleases)
            {
                var relFile = gitHubRelease.Assets.FirstOrDefault(x => x.BrowserDownloadUrl.EndsWith(".tgz"));
                if(relFile == null)
                    continue;

                var release = new ReleaseInfo
                {
                    Version = gitHubRelease.TagName,
                    Description = gitHubRelease.Name,
                    ReleaseNote = gitHubRelease.Description,
                    RequireRestart = false,
                    UpdateBreak = true,
                    DownloadUrl = relFile.BrowserDownloadUrl,
                    ReleaseDate = gitHubRelease.CreatedAt
                };
                releases.Add(release);
            }

            return releases;
        }

        private async Task<List<GitHubRelease>> GetLatestGitHubReleaseAsync(DateTime filterDate, bool checkForPrereleases = false)
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
                    .Where(x => x.CreatedAt >= filterDate);
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

        private async Task<List<GitHubRelease>> GetLatestGitHubReleaseAsync(Version currentVersion, bool checkForPrereleases = false)
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

        private void CheckForUpdates_Elapsed(object sender, ElapsedEventArgs e)
        {
            Check();
            if (IsUpdateAvailable)
            {
                // TODO: ...
            }
        }
    }
}

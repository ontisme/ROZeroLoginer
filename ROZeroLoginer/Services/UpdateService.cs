using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace ROZeroLoginer.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime PublishDate { get; set; }
        public bool IsNewVersion { get; set; }
    }

    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonProperty("prerelease")]
        public bool Prerelease { get; set; }

        [JsonProperty("draft")]
        public bool Draft { get; set; }
    }

    public class UpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/ontisme/ROZeroLoginer/releases/latest";
        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ROZeroLoginer");
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                LogService.Instance.Info("[UpdateService] 開始檢查版本更新");

                var response = await _httpClient.GetAsync(GITHUB_API_URL);
                if (!response.IsSuccessStatusCode)
                {
                    LogService.Instance.Warning("[UpdateService] GitHub API 請求失敗: {0}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var release = JsonConvert.DeserializeObject<GitHubRelease>(jsonContent);

                if (release == null || release.Draft || release.Prerelease)
                {
                    LogService.Instance.Info("[UpdateService] 未找到正式版本或版本為草稿/預發布版本");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                var latestVersion = ParseVersion(release.TagName);

                LogService.Instance.Info("[UpdateService] 當前版本: {0}, 最新版本: {1}", currentVersion, latestVersion);

                var isNewVersion = CompareVersions(latestVersion, currentVersion) > 0;

                return new UpdateInfo
                {
                    Version = release.TagName,
                    ReleaseNotes = release.Body,
                    DownloadUrl = release.HtmlUrl,
                    PublishDate = release.PublishedAt,
                    IsNewVersion = isNewVersion
                };
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "[UpdateService] 檢查更新時發生錯誤");
                return null;
            }
        }

        private Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version;
        }

        private Version ParseVersion(string versionString)
        {
            // 移除可能的 'v' 前綴
            if (versionString.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionString = versionString.Substring(1);
            }

            if (Version.TryParse(versionString, out Version version))
            {
                return version;
            }

            // 如果解析失敗，嘗試添加缺少的版本號部分
            var parts = versionString.Split('.');
            if (parts.Length == 2)
            {
                versionString += ".0";
            }
            else if (parts.Length == 1)
            {
                versionString += ".0.0";
            }

            return Version.TryParse(versionString, out version) ? version : new Version(0, 0, 0);
        }

        private int CompareVersions(Version version1, Version version2)
        {
            return version1.CompareTo(version2);
        }

        public void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "[UpdateService] 開啟下載頁面失敗");
            }
        }
    }
}
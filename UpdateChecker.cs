using System.Net.Http.Headers;
using System.Text.Json;

namespace qbPortWeaver
{
    public static class UpdateChecker
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/martsg666/qbPortWeaver/releases/latest";
        private const int HTTP_TIMEOUT_SECONDS = 10;

        // Returns the latest release tag and URL if a newer version exists on GitHub; null if up-to-date or on any error
        public static async Task<(string Version, string Url)?> CheckForUpdateAsync(string currentVersion)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("qbPortWeaver", currentVersion));
                client.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS);

                using var response = await client.GetAsync(GITHUB_API_URL);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagElement) ||
                    !root.TryGetProperty("html_url", out var urlElement))
                    return null;

                string tagName = tagElement.GetString() ?? "";
                string htmlUrl = urlElement.GetString() ?? "";

                // Strip leading 'v' or 'V' if present
                string versionString = tagName.TrimStart('v', 'V');

                if (Version.TryParse(versionString, out var latestVersion) &&
                    Version.TryParse(currentVersion, out var current) &&
                    latestVersion > current)
                {
                    return (tagName, htmlUrl);
                }

                return null;
            }
            catch (Exception ex)
            {
                // Update check is best-effort — log but do not propagate (no network, API down, etc.)
                LogManager.Instance.LogDebug($"UpdateChecker.CheckForUpdateAsync: {ex.Message}");
                return null;
            }
        }
    }
}

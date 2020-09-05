using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class AppSettings
    {
        public static string ClientID => _jsonAppSettings.ClientID;
        public static string ClientSecret => _jsonAppSettings.ClientSecret;
        public static string OAuthToken => _jsonAppSettings.OAuthToken;
        public static int MaxConcurrentDownloads => _jsonAppSettings.MaxConcurrentDownloads;
        public static string OutputPath => _jsonAppSettings.OutputPath;
        public static string CommentFormat => _jsonAppSettings.CommentFormat;


        private static JsonAppSettings _jsonAppSettings;
        private static string _settingsPath;


        public static async Task LoadAsync(string settingsPath)
        {
            Console.Write("Loading settings and verifying (or getting new) OAuth token");

            _settingsPath = settingsPath;

            var jsonString = File.ReadAllText(settingsPath);
            _jsonAppSettings = JsonSerializer.Deserialize<JsonAppSettings>(jsonString);

            // Validate ClientID and ClientSecret
            if (_jsonAppSettings.ClientID.Length == 0 || _jsonAppSettings.ClientSecret.Length == 0) {
                throw new ArgumentException($"You must specify ClientID and ClientSecret in your: {settingsPath}");
            }
            // Validate or get new OAuthToken
            if (_jsonAppSettings.OAuthToken.Length == 0 || await ValidateTokenAsync() == false) {
                _jsonAppSettings.OAuthToken = await GetNewOAuthTokenAsync();
                await SaveAsync();
            }
            // Validate MaxConcurrentDownloads
            if (_jsonAppSettings.MaxConcurrentDownloads <= 0) {
                _jsonAppSettings.MaxConcurrentDownloads = 1;
                Console.WriteLine("WARNING! MaxConcurrentDownloads was <= 0, using MaxConcurrentDownloads=1");
            }
            // Validate CommentFormat
            if (_jsonAppSettings.CommentFormat.Length == 0) {
                throw new ArgumentException($"You must specify CommentFormat in your: {settingsPath}");
            }

            Console.WriteLine(" Done.");
        }

        // NOTE: I don't think there is a need to do it async
        public static async Task SaveAsync()
        {
            using FileStream fs = new FileStream(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fs, _jsonAppSettings, new JsonSerializerOptions { WriteIndented = true });
        }


        private static async Task<bool> ValidateTokenAsync()
        {
            var jsonAppOAuthTokenValidate = await TwitchClient.GetJsonAsync<JsonAppOAuthTokenValidate>(TwitchClient.RequestType.OAuthValidate);

            return jsonAppOAuthTokenValidate is not null;
        }

        private static async Task<string> GetNewOAuthTokenAsync()
        {
            var jsonAppOAuthToken = await TwitchClient.GetJsonAsync<JsonAppOAuthTokenResponse>(TwitchClient.RequestType.OAuthGetNew);

            return jsonAppOAuthToken!.OAuthToken;
        }


        private class JsonAppSettings
        {
            public string ClientID { get; set; }
            public string ClientSecret { get; set; }
            public string OAuthToken { get; set; }
            public int MaxConcurrentDownloads { get; set; }
            public string OutputPath { get; set; }
            public string CommentFormat { get; set; }
        }

        // https://dev.twitch.tv/docs/authentication/getting-tokens-oauth#oauth-client-credentials-flow
        // NOTE: Commented out properties were not actually presented in response?
        private class JsonAppOAuthTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string OAuthToken { get; set; }
            //[JsonPropertyName("refresh_token")]
            //public string RefreshToken { get; set; }
            [JsonPropertyName("expires_in")]
            public long ExpiresIn { get; set; }
            //[JsonPropertyName("scope")]
            //public string[] Scope { get; set; }
            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }
        }

        private class JsonAppOAuthTokenValidate
        {
            [JsonPropertyName("client_id")]
            public string ClientID { get; set; }
            [JsonPropertyName("scope")]
            public string[] Scope { get; set; }
            [JsonPropertyName("expires_in")]
            public long ExpiresIn { get; set; }
        }
    }
}

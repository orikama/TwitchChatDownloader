using System;
using System.IO;
using System.Net.Http.Json;
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

        private static JsonAppSettings _jsonAppSettings;
        private static string _settingsPath;


        public static async Task Load(string settingsPath)
        {
            _settingsPath = settingsPath;

            var jsonString = File.ReadAllText(settingsPath);
            _jsonAppSettings = JsonSerializer.Deserialize<JsonAppSettings>(jsonString);

            if (_jsonAppSettings.ClientID.Length == 0 || _jsonAppSettings.ClientSecret.Length == 0) {
                throw new ArgumentException($"ClientID or ClientSecret were empty in {settingsPath}");
            }

            if (_jsonAppSettings.OAuthToken.Length == 0 || await IsTokenValid() == false) {
                _jsonAppSettings.OAuthToken = await GetNewOAuthToken();
            }
        }

        public static async Task Save()
        {
            using FileStream fs = new FileStream(_settingsPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fs, _jsonAppSettings, new JsonSerializerOptions { WriteIndented = true });
        }


        private static async Task<bool> IsTokenValid()
        {
            var responseOAuthValidation = await TwitchClient.SendAsync(TwitchClient.RequestType.OAuthValidate);
            var jsonOAuthValidtaion = await responseOAuthValidation.Content.ReadFromJsonAsync<JsonAppOAuthTokenValidate>();

            Console.WriteLine($"OAuth token expires in {jsonOAuthValidtaion.ExpiresIn}s");

            return responseOAuthValidation.IsSuccessStatusCode;
        }

        // TODO: Not tested
        private static async Task<string> GetNewOAuthToken()
        {
            //string uriGetOAuthToken = $"token?client_id={clientID}&client_secret={clientSecret}&grant_type=client_credentials";

            var responseOAuthToken = await TwitchClient.SendAsync(TwitchClient.RequestType.OAuthGetNew);
            var jsonAppOAuthToken = await responseOAuthToken.Content.ReadFromJsonAsync<JsonAppOAuthTokenResponse>();

            return jsonAppOAuthToken.OAuthToken;
        }


        private class JsonAppSettings
        {
            public string ClientID { get; set; }
            public string ClientSecret { get; set; }
            public string OAuthToken { get; set; }
            public int MaxConcurrentDownloads { get; set; }
            public string OutputPath { get; set; }
        }

        // https://dev.twitch.tv/docs/authentication/getting-tokens-oauth#oauth-client-credentials-flow
        // NOTE: Commented properties were not actually presented in response?
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

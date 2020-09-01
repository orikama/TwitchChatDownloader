using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class AppSettings
    {
        public string ClientID => _jsonAppSettings.ClientID;
        public string ClientSecret => _jsonAppSettings.ClientSecret;
        public string OAuthToken => _jsonAppSettings.OAuthToken;
        public int MaxConcurrentDownloads => _jsonAppSettings.MaxConcurrentDownloads;
        public string OutputPath => _jsonAppSettings.OutputPath;

        private readonly HttpClient _httpClient;
        private JsonAppSettings _jsonAppSettings;
        private string _settingsPath;

        private static readonly string s_uriOAuthValidate = "https://id.twitch.tv/oauth2/validate";


        public AppSettings(HttpClient httpClient) => _httpClient = httpClient;

        public async Task Load(string settingsPath)
        {
            _settingsPath = settingsPath;

            var jsonString = File.ReadAllText(settingsPath);
            _jsonAppSettings = JsonSerializer.Deserialize<JsonAppSettings>(jsonString);

            if (_jsonAppSettings.ClientID.Length == 0 || _jsonAppSettings.ClientSecret.Length == 0) {
                throw new ArgumentException($"ClientID or ClientSecret were empty in {settingsPath}");
            }

            if (_jsonAppSettings.OAuthToken.Length == 0 || await IsTokenValid(_jsonAppSettings.OAuthToken) == false) {
                _jsonAppSettings.OAuthToken = await GetNewOAuthToken(_jsonAppSettings.ClientID, _jsonAppSettings.ClientSecret);
            }
        }

        public async Task Save()
        {
            using FileStream fs = new FileStream(_settingsPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fs, _jsonAppSettings, new JsonSerializerOptions { WriteIndented = true });
        }


        private async Task<bool> IsTokenValid(string oauthToken)
        {
            HttpRequestMessage httpRequest = new(HttpMethod.Get, s_uriOAuthValidate);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", oauthToken);

            var responseOAuthValidation = await _httpClient.SendAsync(httpRequest);
            var jsonOAuthValidtaion = await responseOAuthValidation.Content.ReadFromJsonAsync<JsonAppOAuthTokenValidate>();

            return responseOAuthValidation.IsSuccessStatusCode;
        }

        private async Task<string> GetNewOAuthToken(string clientID, string clientSecret)
        {
            string uriGetOAuthToken = $"token?client_id={clientID}&client_secret={clientSecret}&grant_type=client_credentials";

            var responseOAuthToken = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, uriGetOAuthToken));
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

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchClient
    {
        // NOTE: I can't use HttpClient.BaseAddress because of this OAuth stuff
        private const string kBaseUriOAuth = "https://id.twitch.tv/oauth2/";
        private const string kBaseUriUsers = "https://api.twitch.tv/helix/users?";
        private const string kBaseUriVideos = "https://api.twitch.tv/helix/videos?";
        private const string kBaseUriComments = "https://api.twitch.tv/v5/videos/";


        private static readonly HttpClient s_httpClient = new();

        // NOTE: How does this thing even work? I need to load settings first to get AppSettings.OAuthToken.
        //  But loading happens after static initialization. Apparently I don't know something about 'static'
        private static readonly AuthenticationHeaderValue s_authHeader = new("Bearer", AppSettings.OAuthToken);
        private static readonly MediaTypeWithQualityHeaderValue s_mediaType = new("application/vnd.twitchv.v5+json");


        public enum RequestType : ushort
        {
            User, Video, Comment, OAuthValidate, OAuthGetNew
        }


        public static async Task<HttpResponseMessage> GetAsync(RequestType type, string query = "")
        {
            var httpRequest = MakeHttpRequest(type, query);
            var response = await s_httpClient.SendAsync(httpRequest);
            httpRequest.Dispose();

            return response;
        }

        public static async Task<T?> GetJsonAsync<T>(RequestType type, string query = "") where T : class
        {
            var response = await GetAsync(type, query);
            if(response.IsSuccessStatusCode == false) {
                return null; 
            }

            return await response.Content.ReadFromJsonAsync<T>();
        }


        private static HttpRequestMessage MakeHttpRequest(RequestType type, string query = "")
        {
            HttpRequestMessage httpRequest;

            // NOTE: This looks great
            switch (type) {
                case RequestType.OAuthValidate:
                    httpRequest = new(HttpMethod.Get, kBaseUriOAuth + "validate");
                    httpRequest.Headers.Authorization = s_authHeader;
                    break;
                case RequestType.User:
                    httpRequest = new(HttpMethod.Get, kBaseUriUsers + query);
                    httpRequest.Headers.Add("Client-ID", AppSettings.ClientID);
                    httpRequest.Headers.Authorization = s_authHeader;
                    break;
                case RequestType.Video:
                    httpRequest = new(HttpMethod.Get, kBaseUriVideos + query);
                    httpRequest.Headers.Add("Client-ID", AppSettings.ClientID);
                    httpRequest.Headers.Authorization = s_authHeader;
                    break;
                case RequestType.Comment:
                    httpRequest = new(HttpMethod.Get, kBaseUriComments + query);
                    httpRequest.Headers.Add("Client-ID", AppSettings.ClientID);
                    httpRequest.Headers.Accept.Add(s_mediaType);
                    break;
                case RequestType.OAuthGetNew:
                    string uriGetOAuthToken = $"token?client_id={AppSettings.ClientID}&client_secret={AppSettings.ClientSecret}&grant_type=client_credentials";
                    httpRequest = new(HttpMethod.Post, kBaseUriOAuth + uriGetOAuthToken);
                    break;
                default:
                    httpRequest = null;
                    break;
            }

            return httpRequest;
        }
    }
}

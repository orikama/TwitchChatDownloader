using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
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


        private static readonly HttpClient s_httpClient;

        // NOTE: How does this thing even work? I need to load settings first to get AppSettings.OAuthToken.
        //  But loading happens after static initialization. Apparently I don't know something about 'static'
        private static readonly AuthenticationHeaderValue s_authHeader = new("Bearer", AppSettings.OAuthToken);
        private static readonly MediaTypeWithQualityHeaderValue s_mediaType = new("application/vnd.twitchv.v5+json");


        public enum RequestType : ushort
        {
            User, Video, Comment, OAuthValidate, OAuthGetNew
        }


        static TwitchClient()
        {
            s_httpClient = new();
            s_httpClient.Timeout = TimeSpan.FromSeconds(20);
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
            HttpResponseMessage response;

            while (true) {
                try {
                    response = await GetAsync(type, query);
                    break;
                }
                catch (HttpRequestException e) {
                    // NOTE: At first I thought that I need to handle WebException when ther was no Internet connection,
                    //  but it turned out that for this case I need to hadnle SocketException.
                    //  I don't know how I can get WebException manually.
                    //if (e.InnerException is WebException webException && webException.Status == WebExceptionStatus.NameResolutionFailure) {
                    //    Console.WriteLine("NameResolutionFailure");
                    //    await Task.Delay(5000);
                    //}
                    // 11001 - No such host is known (most likely no Internet connection)
                    if (e.InnerException is SocketException se && se.ErrorCode == 11001) {
                        await Task.Delay(5000);
                    } else {
                        throw;
                    }
                }
            }

            if (response.IsSuccessStatusCode == false) {
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

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class TwitchUser
    {
        private const string kBaseUrlUsers = "https://api.twitch.tv/helix/users";

        private readonly HttpClient _httpClient; // TODO: remove it
        private readonly AppSettings _appSettings; // TODO: remove it


        public TwitchUser(AppSettings appSettings, HttpClient httpClient)
        {
            _appSettings = appSettings;
            _httpClient = httpClient;
        }

        // NOTE: I think userNames should be already concatenated("<user1>,<user2>") by this point
        //   Turns out the answer is no, query should be in "?login=<login1>&login=<login2>" format
        public async Task<UserInfos> GetUsersByNames(string[] userNames)
        {
            UserInfos users = new(userNames.Length);
            AuthenticationHeaderValue authHeader = new("Bearer", _appSettings.OAuthToken);

            var query = string.Join("&login=", userNames);
            var requestUrl = $"{kBaseUrlUsers}?login={query}";

            HttpRequestMessage httpRequest = new(HttpMethod.Get, requestUrl);
            httpRequest.Headers.Add("Client-ID", _appSettings.ClientID);
            httpRequest.Headers.Authorization = authHeader;

            var resposnseUsers = await _httpClient.SendAsync(httpRequest);
            var jsonUsers = (await resposnseUsers.Content.ReadFromJsonAsync<JsonUsersResponse>()).Users;

            // NOTE: Use LINQ?
            //  or WithIndex() extension https://thomaslevesque.com/2019/11/18/using-foreach-with-index-in-c/
            // NOTE: Main reason for using SOA was that I need UserIDs array later, although its probably a bad decision
            for (int i = 0; i < jsonUsers.Length; ++i) {
                users.UserID[i] = jsonUsers[i].UserID;
                users.DisplayName[i] = jsonUsers[i].DisplayName;
            }

            return users;
        }


        public class UserInfos
        {
            public readonly string[] UserID;
            public readonly string[] DisplayName;

            public UserInfos(int size)
            {
                UserID = new string[size];
                DisplayName = new string[size];
            }
        }


        private class JsonUsersResponse
        {
            [JsonPropertyName("data")]
            public JsonUser[] Users { get; set; }

            public class JsonUser
            {
                [JsonPropertyName("id")]
                public string UserID { get; set; }   // long
                [JsonPropertyName("login")]
                public string Login { get; set; }   // long
                [JsonPropertyName("display_name")]
                public string DisplayName { get; set; }
                [JsonPropertyName("type")]
                public string Type { get; set; }
                [JsonPropertyName("broadcaster_type")]
                public string BroadcasterType { get; set; }
                [JsonPropertyName("description")]
                public string Description { get; set; }
                [JsonPropertyName("profile_image_url")]
                public Uri ProfileImageUrl { get; set; }
                [JsonPropertyName("offline_image_url")]
                public Uri OfflienImageUrl { get; set; }
                [JsonPropertyName("view_count")]
                public int ViewCount { get; set; }    // may be int ?
                [JsonPropertyName("email")]
                public string Email { get; set; }
            }
        }

    }
}

using System;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchUser
    {
        public static async Task<UserInfos> GetUsersByNames(string[] userNames)
        {
            UserInfos users = new(userNames.Length);

            var logins = string.Join("&login=", userNames);
            var query = $"login={logins}";

            var resposnseUsers = await TwitchClient.SendAsync(TwitchClient.RequestType.User, query);
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
            // TODO: naming
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

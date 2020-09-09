using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    public class TwitchUser
    {
        public static async Task<List<UserInfo>> GetUsersByNamesAsync(string[] userNames)
        {
            var logins = string.Join("&login=", userNames);
            var query = $"login={logins}";

            var jsonUsers = (await TwitchClient.GetJsonAsync<JsonUsersResponse>(TwitchClient.RequestType.User, query)).Users;
            if(userNames.Length != jsonUsers.Length) {
                Console.WriteLine($"WARNING! {userNames.Length - jsonUsers.Length} of the specified Channel names were not found");
            }

            List<UserInfo> users = new(userNames.Length);
            users.AddRange(jsonUsers.Select(jsonUser => new UserInfo(jsonUser.UserID, jsonUser.DisplayName)));

            return users;
        }


        public class UserInfo
        {
            public readonly string UserID;
            public readonly string DisplayName;

            public UserInfo(string userID, string displayName)
            {
                UserID = userID;
                DisplayName = displayName;
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
                public int ViewCount { get; set; }
                [JsonPropertyName("email")]
                public string Email { get; set; }
            }
        }
    }
}

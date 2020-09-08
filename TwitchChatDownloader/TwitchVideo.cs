using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    public static class TwitchVideo
    {
        public static async Task<List<UserVideos>> GetVideosByUserIDsAsync(List<TwitchUser.UserInfo> users, int? firstVideos)
        {
            List<UserVideos> usersVideos = new(users.Count);
            int amountOfVideosForUser = firstVideos ?? 20;

            foreach (var user in users) {

                List<UserVideos.VideoInfo> videos = new();

                int videosLeft = amountOfVideosForUser;
                int first = Math.Min(videosLeft, 100);
                string userQ = $"user_id={user.UserID}";
                string afterQ = string.Empty;
                string? cursor;

                do {
                    string firstQ = $"&first={first}";
                    string query = $"{userQ}{firstQ}{afterQ}";

                    var jsonVideos = await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, query);

                    GetVideos(videos, jsonVideos.Videos);

                    videosLeft -= jsonVideos.Videos.Length;
                    first = Math.Min(videosLeft, 100);
                    cursor = jsonVideos.Pagination.Cursor;
                    afterQ = $"&after={cursor}";
                } while (cursor != null && first > 0);

                videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
                usersVideos.Add(new UserVideos(user.DisplayName, videos));
            }

            //usersVideos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return usersVideos;
        }


        // TODO: I need to fix thi method the same way I fixed GetVideosByUserIDs(), but I'm lazy.
        //  Right now it can get only ~50-60 videos
        public static async Task<List<UserVideos>> GetVideosByVideoIDsAsync(string videoIDs)
        {
            string query = $"id={videoIDs}";
            var jsonVideos = await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, query);
            var grouppedVideos = jsonVideos.Videos.GroupBy(video => video.UserName);

            List<UserVideos> usersVideos = new();

            foreach (var userVideos in grouppedVideos) {
                List<UserVideos.VideoInfo> videos = new();

                GetVideos(videos, userVideos);

                videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
                usersVideos.Add(new UserVideos(userVideos.Key, videos));
            }

            //videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return usersVideos;
        }


        private static void GetVideos(List<UserVideos.VideoInfo> videos, IEnumerable<JsonVideosResponse.JsonVideo> jsonVideos)
        {
            // NOTE: LINQ?
            foreach (var jsonVideo in jsonVideos) {
                if (LogsDB.Contains(jsonVideo.UserName, jsonVideo.VideoID) == false) {
                    videos.Add(new UserVideos.VideoInfo(jsonVideo.VideoID, jsonVideo.Duration, jsonVideo.CreatedAt));
                }
            }
        }


        public class UserVideos
        {
            public readonly string UserDisplayName;
            public readonly List<VideoInfo> Videos;

            public UserVideos(string userDisplayName, List<VideoInfo> videos)
            {
                UserDisplayName = userDisplayName;
                Videos = videos;
            }

            public class VideoInfo
            {
                public readonly string VideoID;
                public readonly string Duration;
                public readonly int DurationSeconds;
                public readonly DateTime CreatedAt;

                public VideoInfo(string videoID, string duration, DateTime createdAt)
                {
                    VideoID = videoID;
                    Duration = duration;
                    CreatedAt = createdAt;
                    DurationSeconds = StringDurationToSeconds(duration);
                }

                private static readonly Regex s_durationRegex = new(@"((?<h>\d+)h)?((?<m>\d+)m)?((?<s>\d+)s)", RegexOptions.Compiled);
                private static int StringDurationToSeconds(string duration)
                {
                    var matches = s_durationRegex.Match(duration);
                    int.TryParse(matches.Groups["h"].Value, out int h);
                    int.TryParse(matches.Groups["m"].Value, out int m);
                    int.TryParse(matches.Groups["s"].Value, out int s);

                    return (h * 60 + m) * 60 + s;
                }
            }
        }

        // NOTE: Add to all Json classes 'data' keyword with C# 9 ?
        private class JsonVideosResponse
        {
            [JsonPropertyName("data")]
            public JsonVideo[] Videos { get; set; }
            [JsonPropertyName("pagination")]
            public JsonPagination Pagination { get; set; }


            public class JsonPagination
            {
                [JsonPropertyName("cursor")]
                public string? Cursor { get; set; }
            }

            public class JsonVideo
            {
                [JsonPropertyName("id")]
                public string VideoID { get; set; }   // Apply JsonConverter to make it 'long' type ?
                [JsonPropertyName("user_id")]
                public string UserID { get; set; }   // long
                [JsonPropertyName("user_name")]
                public string UserName { get; set; }
                [JsonPropertyName("title")]
                public string Title { get; set; }
                [JsonPropertyName("description")]
                public string Description { get; set; }
                [JsonPropertyName("created_at")]
                public DateTime CreatedAt { get; set; }
                [JsonPropertyName("published_at")]
                public DateTime PublishedAt { get; set; }
                [JsonPropertyName("url")]
                public Uri URL { get; set; }
                [JsonPropertyName("thumbnail_url")]
                public Uri ThumbnailURL { get; set; }
                [JsonPropertyName("viewable")]
                public string Viewable { get; set; }
                [JsonPropertyName("view_count")]
                public int ViewCount { get; set; }
                [JsonPropertyName("language")]
                public string Language { get; set; }
                [JsonPropertyName("type")]
                public string Type { get; set; }
                [JsonPropertyName("duration")]
                public string Duration { get; set; }
            }
        }
    }
}

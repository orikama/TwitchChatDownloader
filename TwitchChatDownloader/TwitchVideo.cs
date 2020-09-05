using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchVideo
    {
        public static async Task<List<VideoInfo>> GetVideosByUserIDs(string[] userIDs, int? firstVideos)
        {
            List<VideoInfo> videos = new(userIDs.Length);

            string first = firstVideos.HasValue ? $"first={firstVideos}&" : string.Empty;
            string query = $"{first}user_id=";

            foreach (var userID in userIDs) {
                var jsonVideos = (await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, $"{query}{userID}"))!.Videos;

                GetVideos(videos, jsonVideos);
            }

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }

        public static async Task<List<VideoInfo>> GetVideosByVideoIDs(string videoIDs)
        {
            // NOTE: I can use videoIDs.Count(c => c == ',') to get list capacity, but is it worth it?
            List<VideoInfo> videos = new();
            string query = $"id={videoIDs}";

            var jsonVideos = (await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, query))!.Videos;

            GetVideos(videos, jsonVideos);

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }


        private static void GetVideos(List<VideoInfo> videos, JsonVideosResponse.JsonVideo[] jsonVideos)
        {
            // NOTE: LINQ?
            foreach (var jsonVideo in jsonVideos) {
                videos.Add(new VideoInfo
                {
                    StreamerName = jsonVideo.UserName,
                    DurationSeconds = jsonVideo.Duration,
                    VideoID = long.Parse(jsonVideo.VideoID)
                });
            }
        }


        public class VideoInfo
        {
            public string StreamerName;
            public string DurationSeconds;
            public long VideoID;
        }

        // NOTE: Add to this Json classes 'data' keyword with C# 9 ?
        private class JsonVideosResponse
        {
            [JsonPropertyName("data")]
            public JsonVideo[] Videos { get; set; }
            [JsonPropertyName("pagination")]
            public JsonVideosPagination Pagination { get; set; }

            public class JsonVideosPagination
            {
                [JsonPropertyName("cursor")]
                public string Cursor { get; set; }
            }

            public class JsonVideo
            {
                [JsonPropertyName("id")]
                public string VideoID { get; set; }   // long
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

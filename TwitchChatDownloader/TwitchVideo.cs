using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchVideo
    {
        private static readonly string[] s_timeSpanParseFormats = { @"%h\h%m\m%s\s", @"%m\m%s\s", @"%s\s" };


        public static async Task<List<VideoInfo>> GetVideosByUserIDs(string[] userIDs, int? firstVideos)
        {
            List<VideoInfo> videos = new(userIDs.Length);

            string first = firstVideos.HasValue ? $"first={firstVideos}&" : string.Empty;
            string query = $"{first}user_id=";

            foreach (var userID in userIDs) {
                var jsonVideos = await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, $"{query}{userID}");

                foreach (var jsonVideo in jsonVideos.Videos) {
                    // TODO: Test with different values
                    var timeSpan = TimeSpan.ParseExact(jsonVideo.Duration, s_timeSpanParseFormats, CultureInfo.InvariantCulture);
                    //Console.WriteLine($"Dur: {jsonVideo.Videos[0].Duration}\tts: {timeSpan.TotalSeconds}");

                    videos.Add(new VideoInfo
                    {
                        StreamerName = jsonVideo.UserName,
                        DurationSeconds = Convert.ToInt32(timeSpan.TotalSeconds),
                        VideoID = long.Parse(jsonVideo.VideoID)
                    });
                }
            }

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }

        public static async Task<List<VideoInfo>> GetVideosByVideoIDs(string videoIDs)
        {
            List<VideoInfo> videos = new(); //new(videoIDs.Length); FIXME: Its a string now
            string query = $"id={videoIDs}";

            var jsonVideos = await TwitchClient.GetJsonAsync<JsonVideosResponse>(TwitchClient.RequestType.Video, query);

            foreach (var jsonVideo in jsonVideos.Videos) {
                // TODO: Test with different values
                var timeSpan = TimeSpan.ParseExact(jsonVideo.Duration, s_timeSpanParseFormats, CultureInfo.InvariantCulture);
                //Console.WriteLine($"Dur: {jsonVideo.Videos[0].Duration}\tts: {timeSpan.TotalSeconds}");

                videos.Add(new VideoInfo
                {
                    StreamerName = jsonVideo.UserName,
                    DurationSeconds = Convert.ToInt32(timeSpan.TotalSeconds),
                    VideoID = long.Parse(jsonVideo.VideoID)
                });
            }

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }


        public class VideoInfo
        {
            public string StreamerName;
            public int DurationSeconds;
            public long VideoID;
        }


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

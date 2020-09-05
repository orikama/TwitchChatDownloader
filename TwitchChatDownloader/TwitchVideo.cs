using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchVideo
    {
        public static async Task<List<VideoInfo>> GetVideosByUserIDs(string[] userIDs, int? firstVideos)
        {
            List<VideoInfo> videos = new(userIDs.Length);
            int amountOfVideosForUser = firstVideos ?? 20;

            foreach (var userID in userIDs) {

                int videosLeft = amountOfVideosForUser;
                int first = Math.Min(videosLeft, 100);
                string userQ = $"user_id={userID}";
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
            }

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }


        // TODO: I need to fix thi method the same way I fixed GetVideosByUserIDs(), but I'm lazy.
        //  Right now it can get only ~50-60 videos
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
                videos.Add(new VideoInfo(jsonVideo.UserName, jsonVideo.Duration, jsonVideo.VideoID));
            }
        }


        public class VideoInfo
        {
            public readonly string StreamerName;
            public readonly string Duration;
            public readonly int DurationSeconds;
            public readonly long VideoID;

            public VideoInfo(string streamerName, string duration, string videoID)
            {
                StreamerName = streamerName;
                Duration = duration;
                DurationSeconds = StringDurationToSeconds(duration);
                VideoID = long.Parse(videoID);
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
                public string? Cursor { get; set; }
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

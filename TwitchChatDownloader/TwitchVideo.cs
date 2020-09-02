using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class TwitchVideo
    {
        private const string kBaseUrlVideos = "https://api.twitch.tv/helix/videos";
        // TODO: Add support for videos with duration < 1m ?
        private static readonly string[] s_timeSpanParseFormats = { @"%h\h%m\m%s\s", @"%m\m%s\s" };

        private readonly HttpClient _httpClient; // TODO: remove it
        private readonly AppSettings _appSettings; // TODO: remove it


        public TwitchVideo(AppSettings appSettings, HttpClient httpClient)
        {
            _appSettings = appSettings;
            _httpClient = httpClient;
        }

        public async Task<List<VideoInfo>> GetVideosByUserIDs(string[] userIDs, int? firstVideos)
        {
            List<VideoInfo> videos = new(userIDs.Length);
            AuthenticationHeaderValue authHeader = new("Bearer", _appSettings.OAuthToken);

            string first = firstVideos.HasValue ? string.Empty : $"first={firstVideos}&";
            string targetUrl = $"{kBaseUrlVideos}?{first}user_id=";

            foreach (var userID in userIDs) {
                HttpRequestMessage httpRequest = new(HttpMethod.Get, $"{targetUrl}{userID}");
                httpRequest.Headers.Add("Client-ID", _appSettings.ClientID);
                httpRequest.Headers.Authorization = authHeader;

                var resposnseVideos = await _httpClient.SendAsync(httpRequest);
                var jsonVideos = await resposnseVideos.Content.ReadFromJsonAsync<JsonVideosResponse>();

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

        public async Task<List<VideoInfo>> GetVideosByVideoIDs(string videoIDs)
        {
            List<VideoInfo> videos = new(videoIDs.Length);
            AuthenticationHeaderValue authHeader = new("Bearer", _appSettings.OAuthToken);
            string videoUrl = $"{kBaseUrlVideos}?id={videoIDs}";

            HttpRequestMessage httpRequest = new(HttpMethod.Get, videoUrl);
            httpRequest.Headers.Add("Client-ID", _appSettings.ClientID);
            httpRequest.Headers.Authorization = authHeader;

            var resposnseVideo = await _httpClient.SendAsync(httpRequest);
            var jsonVideo = await resposnseVideo.Content.ReadFromJsonAsync<JsonVideosResponse>();

            foreach (var video in jsonVideo.Videos) {
                // TODO: Test with different values
                var timeSpan = TimeSpan.ParseExact(video.Duration, s_timeSpanParseFormats, CultureInfo.InvariantCulture);
                //Console.WriteLine($"Dur: {jsonVideo.Videos[0].Duration}\tts: {timeSpan.TotalSeconds}");

                videos.Add(new VideoInfo
                {
                    StreamerName = video.UserName,
                    DurationSeconds = Convert.ToInt32(timeSpan.TotalSeconds),
                    VideoID = long.Parse(video.VideoID)
                });
            }

            //videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }

        //private async Task<List<VideoInfo>> GetVideosByVideoIDs(long[] videoIDs)
        //{
        //    List<VideoInfo> videos = new(videoIDs.Length);
        //    AuthenticationHeaderValue authHeader = new("Bearer", _appSettings.OAuthToken);
        //    string videoUrl = $"{kBaseUrlVideos}?id=";

        //    foreach (long videoID in videoIDs) {
        //        HttpRequestMessage httpRequest = new(HttpMethod.Get, videoUrl + videoID.ToString());
        //        httpRequest.Headers.Add("Client-ID", _appSettings.ClientID);
        //        httpRequest.Headers.Authorization = authHeader;


        //        var resposnseVideo = await _httpClient.SendAsync(httpRequest);
        //        var jsonVideo = await resposnseVideo.Content.ReadFromJsonAsync<JsonVideosResponse>();

        //        // TODO: Test with different values
        //        var timeSpan = TimeSpan.ParseExact(jsonVideo.Videos[0].Duration, s_timeSpanParseFormats, CultureInfo.InvariantCulture);
        //        //Console.WriteLine($"Dur: {jsonVideo.Videos[0].Duration}\tts: {timeSpan.TotalSeconds}");

        //        videos.Add(new VideoInfo
        //        {
        //            StreamerName = jsonVideo.Videos[0].UserName,
        //            DurationSeconds = Convert.ToInt32(timeSpan.TotalSeconds),
        //            VideoID = videoID
        //        });
        //    }

        //    //videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
        //    return videos;
        //}


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

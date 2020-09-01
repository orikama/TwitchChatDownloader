using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class App
    {
        private static readonly HttpClient s_httpClient = new();
        private static readonly AppSettings s_appSettings = new(s_httpClient); // NOTE: remove static?


        private const string kBaseUrlVideos = "https://api.twitch.tv/helix/videos";
        //private static readonly string baseUrlComments = //$"https://api.twitch.tv/v5/videos/{videoID}/comments";
        private const string kMediaType = "application/vnd.twitchv.v5+json";


        private static readonly BlockingCollection<Tuple<StreamWriter, JsonComments>> _commentsPipe = new(100);


        public async Task Init(string settingsPath)
        {
            await s_appSettings.Load(settingsPath);

            if (Directory.Exists(s_appSettings.OutputPath) == false) {
                Directory.CreateDirectory(s_appSettings.OutputPath);
            }
        }

        public async Task DownloadChatLogs(long[] videoIDs)
        {
            List<Task> tasks = new(s_appSettings.MaxConcurrentDownloads);
            List<VideoInfo> videos = await GetVideos(videoIDs);

            var commentsProcessor = Task.Run(WriteComments);

            using (ConsoleProgressBar progressBar = new(s_appSettings.MaxConcurrentDownloads)) {
                foreach (var video in videos) {

                    string fileName = $"{video.StreamerName}_{video.VideoID}";

                    progressBar.Add(video.VideoID, fileName, video.DurationSeconds);

                    tasks.Add(Task.Run(() => DownloadChat(video.VideoID, $@"{s_appSettings.OutputPath}\{fileName}.txt", progressBar)));

                    if (tasks.Count == s_appSettings.MaxConcurrentDownloads) {
                        var t = await Task.WhenAny(tasks);
                        tasks.Remove(t);
                    }
                }

                // NOTE: nice one devs
                Task.WaitAll(tasks.ToArray());
                _commentsPipe.CompleteAdding();
            }

            commentsProcessor.Wait();
        }

        private async Task<List<VideoInfo>> GetVideos(long[] videoIDs)
        {
            List<VideoInfo> videos = new(videoIDs.Length);
            AuthenticationHeaderValue authHeader = new("Bearer", s_appSettings.OAuthToken);
            string videoUrl = $"{kBaseUrlVideos}?id=";

            foreach (long videoID in videoIDs) {
                HttpRequestMessage httpRequest = new(HttpMethod.Get, videoUrl + videoID.ToString());
                httpRequest.Headers.Add("Client-ID", s_appSettings.ClientID);
                httpRequest.Headers.Authorization = authHeader;


                var resposnseVideo = await s_httpClient.SendAsync(httpRequest);
                var jsonVideo = await resposnseVideo.Content.ReadFromJsonAsync<JsonGetVideosResponse>();

                // TODO: Test with different values
                var timeSpan = TimeSpan.ParseExact(jsonVideo.Videos[0].Duration, @"%h\h%m\m%s\s", CultureInfo.InvariantCulture);
                //Console.WriteLine($"Dur: {jsonVideo.Videos[0].Duration}\tts: {timeSpan.TotalSeconds}");

                videos.Add(new VideoInfo
                {
                    StreamerName = jsonVideo.Videos[0].UserName,
                    DurationSeconds = Convert.ToInt32(timeSpan.TotalSeconds),
                    VideoID = videoID
                });
            }

            videos.Sort((b, a) => a.DurationSeconds.CompareTo(b.DurationSeconds));
            return videos;
        }

        private async Task DownloadChat(long videoID, string outputPath, ConsoleProgressBar progressBar)
        {
            string clientID = s_appSettings.ClientID;
            string targetUri = $"https://api.twitch.tv/v5/videos/{videoID}/comments";
            MediaTypeWithQualityHeaderValue mediaType = new(kMediaType);

            string? nextCursor = null;
            string query = "";
            StreamWriter sw = new(outputPath);

            do {
                HttpRequestMessage httpRequest = new(HttpMethod.Get, targetUri + query);
                httpRequest.Headers.Add("Client-ID", clientID);
                httpRequest.Headers.Accept.Add(mediaType);

                //Stopwatch stw = new();
                //stw.Start();

                var responseComments = await s_httpClient.SendAsync(httpRequest);
                var jsonComments = await responseComments.Content.ReadFromJsonAsync<JsonComments>();

                //stw.Stop();
                //Console.WriteLine($"Done. Time: {stw.Elapsed}");

                _commentsPipe.Add(new Tuple<StreamWriter, JsonComments>(sw, jsonComments));

                nextCursor = jsonComments.Next;
                query = $"?cursor={nextCursor}";

                progressBar.Report(videoID, Convert.ToInt32(jsonComments.Comments[^1].ContentOffsetSeconds));
            } while (nextCursor is not null);
        }


        private void WriteComments()
        {
            foreach (var part in _commentsPipe.GetConsumingEnumerable()) {
                var sw = part.Item1;
                var jc = part.Item2;
                foreach (var comment in jc.Comments) {
                    sw.WriteLine(comment.Commenter.Name + ": " + comment.Message.Body);
                }

                if (jc.Next is null)
                    sw.Close();
            }
        }


        class VideoInfo
        {
            public string StreamerName;
            public int DurationSeconds;
            public long VideoID;
        }

        class JsonGetVideosResponse
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
                public string ID { get; set; }   // long
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
                public long ViewCount { get; set; }    // may be int ?
                [JsonPropertyName("language")]
                public string Language { get; set; }
                [JsonPropertyName("type")]
                public string Type { get; set; }
                [JsonPropertyName("duration")]
                public string Duration { get; set; }
            }
        }

        public class JsonComments
        {
            [JsonPropertyName("comments")]
            public JsonComment[] Comments { get; set; }
            // TODO: Turns out there is a _prev field also
            [JsonPropertyName("_next")]
            public string? Next { get; set; }

            //class JsonChat
            //{
            public class JsonComment
            {
                [JsonPropertyName("_id")]
                public string ID { get; set; }
                [JsonPropertyName("created_at")]
                public DateTime CreatedAt { get; set; }
                [JsonPropertyName("updated_at")]
                public DateTime UpdatedAt { get; set; }
                [JsonPropertyName("channel_id")]
                public string ChannelID { get; set; } // long
                [JsonPropertyName("content_type")]
                public string ContentType { get; set; }
                [JsonPropertyName("content_id")]
                public string ContentID { get; set; } // long
                [JsonPropertyName("content_offset_seconds")]
                public double ContentOffsetSeconds { get; set; } // TimeSpan
                [JsonPropertyName("commenter")]
                public JsonCommenter Commenter { get; set; }
                [JsonPropertyName("source")]
                public string Source { get; set; }
                [JsonPropertyName("state")]
                public string State { get; set; }
                [JsonPropertyName("message")]
                public JsonMessage Message { get; set; }
            }

            public class JsonCommenter
            {
                [JsonPropertyName("display_name")]
                public string DisplayName { get; set; }
                [JsonPropertyName("_id")]
                public string ID { get; set; } // long
                [JsonPropertyName("name")]
                public string Name { get; set; }
                [JsonPropertyName("type")]
                public string Type { get; set; }
                [JsonPropertyName("bio")]
                public string? BIO { get; set; }
                [JsonPropertyName("created_at")]
                public DateTime CreatedAt { get; set; }
                [JsonPropertyName("updated_at")]
                public DateTime UpdatedAt { get; set; }
                [JsonPropertyName("logo")]
                public Uri Logo { get; set; }
            }

            public class JsonMessage
            {
                [JsonPropertyName("body")]
                public string Body { get; set; }
                [JsonPropertyName("fragments")]
                public JsonFragments[] Fragments { get; set; }
                [JsonPropertyName("is_action")]
                public bool IsAction { get; set; }
                [JsonPropertyName("user_badges")]
                public JsonUserBadge[] UserBadges { get; set; }
                [JsonPropertyName("user_color")]
                public string UserColor { get; set; }
                [JsonPropertyName("user_notice_params")]
                private object UserNoticeParams { get; set; }
            }

            public class JsonUserBadge
            {
                [JsonPropertyName("_id")]
                public string ID { get; set; }
                [JsonPropertyName("version")]
                public string Version { get; set; } // int
            }

            public class JsonFragments
            {
                [JsonPropertyName("text")]
                public string Text { get; set; }
            }
        }

    }
}


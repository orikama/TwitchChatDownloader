using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // TODO: Replcae with service locator?
        private static readonly TwitchVideo _twitchVideo = new(s_appSettings, s_httpClient);
        private static readonly TwitchUser _twitchUser = new(s_appSettings, s_httpClient);

        //private const string kBaseUrlVideos = "https://api.twitch.tv/helix/videos";
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

        public async Task DownloadChatLogs(string[] userNames, int? firstVideos)
        {
            var users = await _twitchUser.GetUsersByNames(userNames);
            var videos = await _twitchVideo.GetVideosByUserIDs(users.UserID, firstVideos);

            await GetMessagesFromVideos(videos);
        }

        public async Task DownloadChatLogs(string videoIDs)
        {
            var videos = await _twitchVideo.GetVideosByVideoIDs(videoIDs);

            await GetMessagesFromVideos(videos);
        }


        private async Task GetMessagesFromVideos(List<TwitchVideo.VideoInfo> videos)
        {
            List<Task> tasks = new(s_appSettings.MaxConcurrentDownloads);
            string[] fileNames = new string[videos.Count];

            Console.WriteLine($"\nDownloading {videos.Count} video(s)\n");

            var commentsProcessor = Task.Run(WriteComments);

            using (ConsoleProgressBar progressBar = new(s_appSettings.MaxConcurrentDownloads)) {
                // NOTE: This shit is so fuckin ugly
                for (int i = 0; i < videos.Count; ++i) {
                    var v = videos[i];
                    fileNames[i] = $"{v.StreamerName}_{v.VideoID}";
                    progressBar.Add(v.VideoID, fileNames[i], v.DurationSeconds);
                }

                for (int i = 0; i < videos.Count; ++i) {
                    var index = i;
                    tasks.Add(Task.Run(() => DownloadChat(
                        videos[index].VideoID, $@"{s_appSettings.OutputPath}/{fileNames[index]}.txt", progressBar)));

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

        private async Task DownloadChat(long videoID, string outputPath, ConsoleProgressBar progressBar)
        {
            string clientID = s_appSettings.ClientID;
            string targetUri = $"https://api.twitch.tv/v5/videos/{videoID}/comments";
            MediaTypeWithQualityHeaderValue mediaType = new(kMediaType);

            string? nextCursor;
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

                int offset = Convert.ToInt32(jsonComments.Comments[^1].ContentOffsetSeconds);
                progressBar.Report(videoID, offset);
            } while (nextCursor is not null);

            progressBar.Report(videoID, -1);
        }


        private void WriteComments()
        {
            foreach (var part in _commentsPipe.GetConsumingEnumerable()) {
                var sw = part.Item1;
                var jc = part.Item2;
                foreach (var comment in jc.Comments) {
                    sw.WriteLine($"{comment.ContentOffsetSeconds}\t{comment.Commenter.Name}: {comment.Message.Body}");
                }

                if (jc.Next is null)
                    sw.Close();
            }
        }


        public class JsonComments
        {
            [JsonPropertyName("comments")]
            public JsonComment[] Comments { get; set; }
            // TODO: Turns out there is a _prev field also
            [JsonPropertyName("_next")]
            public string? Next { get; set; }


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

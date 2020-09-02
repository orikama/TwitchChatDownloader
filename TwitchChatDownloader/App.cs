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
        private static readonly TwitchComment _twitchComment = new(s_appSettings, s_httpClient);

        //private const string kBaseUrlVideos = "https://api.twitch.tv/helix/videos";
        //private static readonly string baseUrlComments = //$"https://api.twitch.tv/v5/videos/{videoID}/comments";



        private static readonly BlockingCollection<Tuple<StreamWriter, TwitchComment.JsonComments>> _commentsPipe = new(100);


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
                    tasks.Add(Task.Run(() => _twitchComment.GetComments(
                        videos[index].VideoID, $@"{s_appSettings.OutputPath}/{fileNames[index]}.txt", progressBar, _commentsPipe)));

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

    }
}

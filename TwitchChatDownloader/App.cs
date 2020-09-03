using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class App
    {
        private static readonly BlockingCollection<Tuple<StreamWriter, TwitchComment.JsonComments>> _commentsPipe = new(100);


        public async Task Init(string settingsPath)
        {
            await AppSettings.Load(settingsPath);

            if (Directory.Exists(AppSettings.OutputPath) == false) {
                Directory.CreateDirectory(AppSettings.OutputPath);
            }
        }

        public async Task DownloadChatLogs(string[] userNames, int? firstVideos)
        {
            var users = await TwitchUser.GetUsersByNames(userNames);
            var videos = await TwitchVideo.GetVideosByUserIDs(users.UserID, firstVideos);

            await GetMessagesFromVideos(videos);
        }

        public async Task DownloadChatLogs(string videoIDs)
        {
            var videos = await TwitchVideo.GetVideosByVideoIDs(videoIDs);

            await GetMessagesFromVideos(videos);
        }


        private async Task GetMessagesFromVideos(List<TwitchVideo.VideoInfo> videos)
        {
            List<Task> tasks = new(AppSettings.MaxConcurrentDownloads);
            string[] fileNames = new string[videos.Count];

            Console.WriteLine($"\nGetting {videos.Count} video(s)\n");

            var commentsProcessor = Task.Run(WriteComments);

            using (ConsoleProgressBar progressBar = new(AppSettings.MaxConcurrentDownloads)) {
                // NOTE: This shit is so fuckin ugly
                for (int i = 0; i < videos.Count; ++i) {
                    var v = videos[i];
                    fileNames[i] = $"{v.StreamerName}_{v.VideoID}";
                    progressBar.Add(v.VideoID, fileNames[i], v.DurationSeconds);
                }

                for (int i = 0; i < videos.Count; ++i) {
                    var index = i;
                    tasks.Add(Task.Run(() => TwitchComment.GetComments(
                        videos[index].VideoID, $@"{AppSettings.OutputPath}/{fileNames[index]}.txt", progressBar, _commentsPipe)));

                    // FIXME:
                    if (tasks.Count == AppSettings.MaxConcurrentDownloads) {
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

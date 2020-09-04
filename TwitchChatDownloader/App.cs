using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;


namespace TwitchChatDownloader
{
    class App
    {
        private static readonly BlockingCollection<Tuple<StreamWriter, TwitchComment.JsonComments>> _commentsPipe = new(100);
        private static Func<TwitchComment.JsonComments.JsonComment, string> CommentFormatter;


        public async Task InitAsync(string settingsPath)
        {
            await AppSettings.LoadAsync(settingsPath);

            if (Directory.Exists(AppSettings.OutputPath) == false) {
                Directory.CreateDirectory(AppSettings.OutputPath);
            }

            CommentFormatter = await BuildLambdaAsync(AppSettings.CommentFormat);
        }

        public async Task DownloadChatLogsAsync(string[] userNames, int? firstVideos)
        {
            var users = await TwitchUser.GetUsersByNamesAsync(userNames);
            var videos = await TwitchVideo.GetVideosByUserIDs(users.UserID, firstVideos);

            await GetMessagesFromVideosAsync(videos);
        }

        public async Task DownloadChatLogsAsync(string videoIDs)
        {
            var videos = await TwitchVideo.GetVideosByVideoIDs(videoIDs);

            await GetMessagesFromVideosAsync(videos);
        }


        private async Task GetMessagesFromVideosAsync(List<TwitchVideo.VideoInfo> videos)
        {
            List<Task> tasks = new(AppSettings.MaxConcurrentDownloads);
            string[] fileNames = new string[videos.Count];

            Console.WriteLine($"\nGetting {videos.Count} video(s) chat logs\n");

            var commentsProcessor = Task.Run(WriteComments);

            using (ConsoleProgressBar progressBar = new(AppSettings.MaxConcurrentDownloads)) {
                // NOTE: This shit is so fuckin ugly
                for (int i = 0; i < videos.Count; ++i) {
                    var v = videos[i];
                    fileNames[i] = $"{v.StreamerName}_{v.VideoID}";
                    progressBar.Add(v.VideoID, fileNames[i], v.DurationSeconds);
                }

                for (int i = 0; i < videos.Count; ++i) {
                    int index = i;
                    tasks.Add(Task.Run(() => TwitchComment.GetCommentsAsync(
                        videos[index].VideoID, $@"{AppSettings.OutputPath}/{fileNames[index]}.txt", progressBar, _commentsPipe)));

                    if (i + 1 < videos.Count && tasks.Count == AppSettings.MaxConcurrentDownloads) {
                        var t = await Task.WhenAny(tasks);
                        tasks.Remove(t);
                    }
                }

                Task.WaitAll(tasks.ToArray());
                _commentsPipe.CompleteAdding();
            }

            commentsProcessor.Wait();

            Console.Write("Done.");
        }


        private void WriteComments()
        {
            foreach (var (streamWriter, jsonComments) in _commentsPipe.GetConsumingEnumerable()) {
                foreach (var comment in jsonComments.Comments) {
                    streamWriter.WriteLine(CommentFormatter(comment));
                }

                if (jsonComments.Next is null) {
                    streamWriter.Close();
                }
            }
        }

        private async Task<Func<TwitchComment.JsonComments.JsonComment, string>> BuildLambdaAsync(string format)
        {
            Console.Write("Compiling 'CommentFormat' string");

            var lambdaFormat = $"comment => $\"{format}\"";
            var options = ScriptOptions.Default.AddReferences(typeof(TwitchComment.JsonComments.JsonComment).Assembly);
            var lambda = await CSharpScript.EvaluateAsync<Func<TwitchComment.JsonComments.JsonComment, string>>(lambdaFormat, options);

            Console.WriteLine(" Done.");

            return lambda;
        }
    }
}

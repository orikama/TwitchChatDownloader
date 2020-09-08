using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;


namespace TwitchChatDownloader
{
    class App
    {
        private readonly BlockingCollection<Tuple<StreamWriter, TwitchComment.JsonComments>> _commentsPipe = new(100);

        private Func<TwitchComment.JsonComments.JsonComment, string> CommentFormatter;

        private readonly CancellationTokenSource _cts = new();


        public async Task InitAsync(string settingsPath)
        {
            await AppSettings.LoadAsync(settingsPath);

            LogsDB.Load();
            CommentFormatter = await BuildLambdaAsync(AppSettings.CommentFormat);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public async Task DownloadChatLogsAsync(string[] userNames, int? first)
        {
            var users = await TwitchUser.GetUsersByNamesAsync(userNames);
            var videos = await TwitchVideo.GetVideosByUserIDsAsync(users, first);

            await GetMessagesFromVideosAsync(videos);
        }

        public async Task DownloadChatLogsAsync(string videoIDs)
        {
            var videos = await TwitchVideo.GetVideosByVideoIDsAsync(videoIDs);

            await GetMessagesFromVideosAsync(videos);
        }


        private async Task GetMessagesFromVideosAsync(List<TwitchVideo.UserVideos> userVideos)
        {
            int videosCount = 0;
            foreach (var user in userVideos) {
                videosCount += user.Videos.Count;
            }
            string[] fileNames = new string[videosCount];

            Console.WriteLine($"\nGetting {videosCount} video(s) chat logs\n");

            List<Task> tasks = new(AppSettings.MaxConcurrentDownloads);
            var commentsProcessor = Task.Factory.StartNew(WriteComments, TaskCreationOptions.LongRunning);

            using (ConsoleProgressBar progressBar = new(AppSettings.MaxConcurrentDownloads)) {

                //NOTE: This shit is so fuckin ugly
                int i = 0;
                foreach (var user in userVideos) {
                    foreach (var video in user.Videos) {
                        fileNames[i] = $"{user.UserDisplayName}_{video.VideoID}";
                        progressBar.Add(fileNames[i], video.VideoID, video.Duration, video.DurationSeconds);
                        ++i;
                    }
                }

                i = 0;

                foreach (var user in userVideos) {
                    var pathToUserFolder = Path.Combine(AppSettings.PathToOriginalLogs, user.UserDisplayName);
                    _ = Directory.CreateDirectory(pathToUserFolder);

                    foreach (var video in user.Videos) {
                        string pathToLogsFile = Path.Combine(pathToUserFolder, $"{fileNames[i]}.txt");

                        tasks.Add(Task.Run(() => TwitchComment.GetCommentsAsync(
                            user.UserDisplayName, video, pathToLogsFile, progressBar, _commentsPipe, _cts.Token)));
                        ++i;

                        if (i != videosCount && tasks.Count == AppSettings.MaxConcurrentDownloads) {
                            var t = await Task.WhenAny(tasks);
                            tasks.Remove(t);
                        }

                        if (_cts.IsCancellationRequested) {
                            break;
                        }
                    }

                    if (_cts.IsCancellationRequested) {
                        break;
                    }
                }

                Task.WaitAll(tasks.ToArray());
                _commentsPipe.CompleteAdding();
            }

            commentsProcessor.Wait();
            await LogsDB.SaveAsync();

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

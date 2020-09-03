using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;


// Based on https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54

namespace TwitchChatDownloader
{
    /// <summary>
    /// An ASCII progress bar
    /// </summary>
    public class ConsoleProgressBar : IDisposable //, IProgress<int>
    {
        private const int kBlockCount = 20;
        private const int kFileNamePadding = 35;
        private const int kDisplayDurationPadding = 10;

        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0);
        private readonly Timer _timer;
        private bool _disposed = false;

        private static readonly string[] s_timeSpanParseFormats = { @"%h\h%m\m%s\s", @"%m\m%s\s", @"%s\s" };

        private class DownloadProgress
        {
            public int CurrentOffset = default;
            public readonly int VideoDuration;
            public readonly string FileName;
            public readonly string DisplayDuration;

            public DownloadProgress(string fileName, string duration)
            {
                // TODO: Test with different values
                var durationSeconds = TimeSpan.ParseExact(duration, s_timeSpanParseFormats, CultureInfo.InvariantCulture);
                //Console.WriteLine($"Dur: {duration}\tts: {durationSeconds.TotalSeconds}");

                FileName = fileName.PadRight(kFileNamePadding);
                DisplayDuration = duration.PadRight(kDisplayDurationPadding);
                VideoDuration = Convert.ToInt32(durationSeconds.TotalSeconds);
            }
        }
        private readonly Dictionary<long, DownloadProgress> _downloads;
        private int _currentDownloads = 0;


        public ConsoleProgressBar(int maxConcurrentDownloads)
        {
            _downloads = new(maxConcurrentDownloads);
            _timer = new(TimerHandler, null, _animationInterval, _animationInterval);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (Console.IsOutputRedirected) {
                ResetTimer();
            }
        }

        public void Add(long videoID, string fileName, string duration)
        {
            _downloads.Add(videoID, new DownloadProgress(fileName, duration));
        }

        public void Report(long videoID, int value)
        {
            value = Math.Min(value, _downloads[videoID].VideoDuration);
            Interlocked.Exchange(ref _downloads[videoID].CurrentOffset, value);
        }

        private void TimerHandler(object _)
        {
            lock (_timer) {
                if (_disposed) return;

                ClearPreviousOutput(_currentDownloads);
                PrintProgress();
            }
        }

        private void ClearPreviousOutput(int lines)
        {
            int currentLineCursor = Console.CursorTop - lines;
            Console.SetCursorPosition(0, currentLineCursor);
            Console.Write(new string(' ', Console.WindowWidth * lines));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private void PrintProgress()
        {
            foreach (var videoID in _downloads.Keys.ToImmutableArray()) {
                var progress = _downloads[videoID];
                if (progress.CurrentOffset == -1) {
                    Console.WriteLine(progress.FileName + "Done.");
                    _downloads.Remove(videoID);
                }
            }

            _currentDownloads = 0;

            foreach (var video in _downloads.Values) {
                if (video.CurrentOffset != 0) {
                    Console.Write(video.FileName);
                    if (video.CurrentOffset == video.VideoDuration) {
                        Console.WriteLine(" Downloading comments that were posted after the VOD finished");
                    } else {
                        double percent = (double)video.CurrentOffset / video.VideoDuration;
                        int progressBlockCount = (int)(percent * kBlockCount);

                        string s = new string('=', progressBlockCount) + ">";
                        string text = string.Format(" [{0}] {1,8:P2}", s.PadRight(kBlockCount), percent);

                        Console.WriteLine(video.DisplayDuration + text);
                    }
                    ++_currentDownloads;
                }
            }
        }

        private void ResetTimer()
        {
            _timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (_timer) {
                _disposed = true;
                ClearPreviousOutput(_currentDownloads);
                PrintProgress();
            }
        }
    }
}

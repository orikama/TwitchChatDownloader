using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;


// Based on https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54

namespace TwitchChatDownloader
{
    /// <summary>
    /// An ASCII progress bar
    /// </summary>
    public class ConsoleProgressBar : IDisposable //, IProgress<int>
    {
        private const int kBlockCount = 10;
        private const string kAnimation = @"|/-\";

        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0);
        private readonly Timer timer;

        private class DownloadProgress
        {
            public int currentValue;
            public int endValue;
            public string fileName;
        }
        private readonly Dictionary<long, DownloadProgress> _downloads;

        private bool _disposed = false;
        private int _animationIndex = 0;


        public ConsoleProgressBar(int maxConcurrentDownloads)
        {
            _downloads = new(maxConcurrentDownloads);
            //timer = new Timer(TimerHandler, new AutoResetEvent(false), _animationInterval, _animationInterval);
            timer = new(TimerHandler, null, _animationInterval, _animationInterval);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (Console.IsOutputRedirected) {
                ResetTimer();
            }
        }

        public void Add(long videoID, string fileName, int endValue)
        {
            Console.WriteLine();
            _downloads.Add(videoID, new DownloadProgress { currentValue = 0, endValue = endValue, fileName = fileName });
        }

        public void Report(long vdieoID, int value)
        {
            // Make sure value is in range
            value = Math.Clamp(value, 0, _downloads[vdieoID].endValue);

            Interlocked.Exchange(ref _downloads[vdieoID].currentValue, value);
        }

        private void TimerHandler(object state)
        {
            lock (timer) {
                if (_disposed) return;

                ClearPreviousOutput(_downloads.Count);
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
            var videoIDs = _downloads.Keys.ToImmutableArray();

            foreach (var videoID in videoIDs) {
                var progress = _downloads[videoID];
                if (progress.currentValue == progress.endValue) {
                    _downloads.Remove(videoID);
                    Console.WriteLine(progress.fileName + " Done.");
                }
            }

            foreach (var video in _downloads) {
                double p = (double)video.Value.currentValue / video.Value.endValue;
                int progressBlockCount = (int)(p * kBlockCount);
                int percent = (int)(p * 100);

                string text = string.Format("  [{0}{1}] {2,3}% {3}",
                    new string('#', progressBlockCount), new string('-', kBlockCount - progressBlockCount),
                    percent,
                    kAnimation[_animationIndex % kAnimation.Length]);

                Console.WriteLine(video.Value.fileName + text);
            }

            if (_downloads.Count != 0)
                ++_animationIndex;
        }

        private void ResetTimer()
        {
            timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer) {
                _disposed = true;
                //UpdateText(string.Empty);
            }
        }
    }
}

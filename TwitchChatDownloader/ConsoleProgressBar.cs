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
        private const int kBlockCount = 20;
        private const int kFileNamePadding = 35;
        private const int kDisplayDurationPadding = 10;

        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0);
        private readonly Timer _timer;
        private bool _disposed = false;


        private class DownloadProgress
        {
            public readonly string FileName;
            public readonly string DisplayDuration;
            public readonly int DurationSeconds;
            public int CurrentOffset = default;

            public DownloadProgress(string fileName, string duration, int durationSeconds)
            {
                // NOTE: I can just use FileName + DisplayDuration string
                FileName = fileName.PadRight(kFileNamePadding);
                DisplayDuration = duration.PadRight(kDisplayDurationPadding);
                DurationSeconds = durationSeconds;
            }
        }
        private readonly Dictionary<string, DownloadProgress> _downloads;
        private int _currentDownloads = 0;

        private bool _isDisconnected = false;


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

        public void Add(string fileName, string videoID, string duration, int durationSeconds)
        {
            _downloads.Add(videoID, new DownloadProgress(fileName, duration, durationSeconds));
        }

        public void Report(string videoID, int value)
        {
            value = Math.Min(value, _downloads[videoID].DurationSeconds);
            Interlocked.Exchange(ref _downloads[videoID].CurrentOffset, value);
            // NOTE: And now I need call this every time? Even when 99.99% of the time _isDisconnected will be = 0
            //  Good one. And probably I can just write _isDisconnected = 0;
            //Interlocked.CompareExchange(ref _isDisconnected, 0, 1);
            // NOTE: Fuck atomic, it should work.
            _isDisconnected = false;
        }

        public void ReportDisconnect()
        {
            // NOTE: Should this be atomic at all?
            //Interlocked.CompareExchange(ref _isDisconnected, 1, 0);
            _isDisconnected = true;
        }

        private void TimerHandler(object _)
        {
            lock (_timer) {
                if (_disposed) return;

                ClearPreviousOutput(_currentDownloads);
                PrintProgress();
                PrintStatusBar();
            }
        }

        private void ClearPreviousOutput(int lines)
        {
            int currentLineCursor = Console.CursorTop - lines;
            // Clear progress output
            Console.SetCursorPosition(0, currentLineCursor);
            Console.Write(new string(' ', Console.WindowWidth * lines));
            // Clear status bar
            Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
            Console.Write(new string(' ', Console.BufferWidth));

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
                    if (video.CurrentOffset == video.DurationSeconds) {
                        Console.WriteLine("Getting comments that were posted after the VOD finished");
                    } else {
                        double percent = (double)video.CurrentOffset / video.DurationSeconds;
                        int progressBlockCount = (int)(percent * kBlockCount);

                        string s = new string('=', progressBlockCount) + ">";
                        string text = string.Format(" [{0}] {1,8:P2}", s.PadRight(kBlockCount), percent);

                        Console.WriteLine(video.DisplayDuration + text);
                    }
                    ++_currentDownloads;
                }
            }
        }

        private void PrintStatusBar()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);

            if (_isDisconnected) {
                Console.Write("Lost Internet connection. Reconecting ...");
            }

            Console.SetCursorPosition(0, currentLineCursor);
        }

        private void ResetTimer()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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

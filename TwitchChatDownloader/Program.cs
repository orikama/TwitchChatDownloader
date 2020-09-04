using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class Program
    {
        // 722853909 - bench
        // 726899014 - small
        //private static readonly long[] videoIDs = { 726899014, 725941824, 724951642, 722853909, 724072801, 721832986 };
        //private static readonly string[] userIDs = { "admiralbulldog", "moonmoon" };

        static void Main(string[] args)
        {
            RootCommand rootCommand = new()
            {
                new Option<string>(
                    new[] { "--settings", "-s" },
                    getDefaultValue: () => "settings.json",
                    description: "Path to json settings"
                ),
                new Option<string?>(
                    new[] { "--video", "-v" },
                    description: "Video IDs seprated by commas. 100 Video IDs max"
                ),
                new Option<string?>(
                    new[] { "--channel", "-c" },
                    description: "Channel name(s) separated by commas"
                ),
                new Option<int?>(
                    new[] { "--first", "-f" },
                    description: "Get <first> videos from <channel>(s). Must be <= 100. Gets 20 for every <channel> if not specified (twitch default)"
                )
            };
            rootCommand.AddValidator(commandResult =>
            {
                bool containsVideo = commandResult.Children.Contains("video");
                bool containsChannel = commandResult.Children.Contains("channel");
                bool containsFirst = commandResult.Children.Contains("first");

                if ((containsVideo || containsChannel) == false) {
                    return "Either '--video' or '--channel' option must be specified.";
                }
                if (containsVideo && (containsChannel || containsFirst)) {
                    return "Options '--video' and ('--channel' or '--first') cannot be used together.";
                }

                if (containsVideo) {
                    var videoValue = commandResult.ValueForOption<string>("video");
                    if (videoValue!.Split(',').Length > 100)
                        return "No more than 100 Video IDs must be specified for --video option";
                }

                return null;
            });

            rootCommand.Description = "Downloads twitch.tv chat logs.";
            rootCommand.Handler = CommandHandler.Create<string, string?, string?, int?>(RunApp);

            rootCommand.InvokeAsync(args).Wait();
        }

        static async Task RunApp(string settings, string? video, string? channel, int? first)
        {
            try {
                //Console.CursorVisible = false;

                App app = new();
                await app.InitAsync(settings);

                Stopwatch sw = new();
                sw.Start();

                if (video is not null)
                    await app.DownloadChatLogsAsync(video);
                else if (channel is not null)
                    await app.DownloadChatLogsAsync(channel.Split(','), first);

                sw.Stop();
                Console.WriteLine($" Time: {sw.Elapsed}");

                //Console.CursorVisible = true;
            }
            catch (Exception e) {
                Console.WriteLine($"\n\tERROR!!!\n{e.Message}");
            }
        }
    }
}

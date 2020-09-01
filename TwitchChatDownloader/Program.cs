using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    class Program
    {
        private static readonly long[] videoIDs = { 726899014, 725941824, 724951642, 722853909, 724072801, 721832986 };


        static void Main(string[] args)
        {
            RootCommand rootCommand = new()
            {
                new Option<string>(
                    new[] { "--settings", "-s" },
                    getDefaultValue: () => "settings.json",
                    description: "A path to json settings"
                )
            };
            rootCommand.Description = "Downloads twitch.tv chat logs.";
            rootCommand.Handler = CommandHandler.Create<string>(RunApp);

            rootCommand.InvokeAsync(args).Wait();
        }

        static async Task RunApp(string settings)
        {
            App app = new();
            await app.Init(settings);

            Stopwatch sw = new();
            sw.Start();

            await app.DownloadChatLogs(videoIDs);

            sw.Stop();
            Console.WriteLine($"\nDone. Time: {sw.Elapsed}");
        }
    }
}

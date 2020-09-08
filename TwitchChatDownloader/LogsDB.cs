using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class LogsDB
    {
        private const string kDBFileName = "LogsDB.json";

        private static string s_pathToDBFile;
        private static JsonLogsDB s_logsDB;


        public static void Load()
        {
            Console.Write("Loading logs database (or making a new one)");

            s_pathToDBFile = Path.Combine(AppSettings.PathToOutputFolder, kDBFileName);

            if (File.Exists(s_pathToDBFile)) {
                var json = File.ReadAllText(s_pathToDBFile);
                s_logsDB = JsonSerializer.Deserialize<JsonLogsDB>(json);
            } else {
                s_logsDB = new JsonLogsDB
                {
                    Users = new()
                };
            }

            Console.WriteLine(" Done.");
        }

        // NOTE: Move Save/Load methods from this and AppSettings class to 'Utils' namespace/class ?
        public static async Task SaveAsync()
        {
            using FileStream fs = new FileStream(s_pathToDBFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fs, s_logsDB, new JsonSerializerOptions { WriteIndented = true });
        }

        public static void Add(string userName, TwitchVideo.UserVideos.VideoInfo video)
        {
            // NOTE: Looks great (:
            //  I can reduce the scope of the lock to Videos.Add(), if I add all users in advance
            lock (s_logsDB) {
                if (s_logsDB.Users.ContainsKey(userName) == false) {
                    s_logsDB.Users.Add(userName, new JsonLogsDB.User());
                    s_logsDB.Users[userName].Videos = new();
                }
                s_logsDB.Users[userName].Videos.Add(video.VideoID, new JsonLogsDB.User.Video { Duration = video.Duration, CreatedAt = video.CreatedAt });
            }

            // NOTE: This should be in the end, but since I don't have a 'Ctrl+C' handler rn
            //SaveAsync().Wait();
        }

        public static bool Contains(string userName, string videoID)
        {
            if (s_logsDB.Users.TryGetValue(userName, out JsonLogsDB.User user)) {
                if (user.Videos != null) {
                    return user.Videos.ContainsKey(videoID);
                }
            }

            return false;
        }


        public class JsonLogsDB
        {
            public SortedDictionary<string, User> Users { get; set; }

            public class User
            {
                public SortedDictionary<string, Video> Videos { get; set; }

                public class Video
                {
                    public string Duration { get; set; }
                    public DateTime CreatedAt { get; set; }
                }
            }
        }
    }
}

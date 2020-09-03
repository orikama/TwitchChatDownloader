using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace TwitchChatDownloader
{
    static class TwitchComment
    {
        public static async Task GetComments(long videoID, string outputPath, ConsoleProgressBar progressBar, BlockingCollection<Tuple<StreamWriter, TwitchComment.JsonComments>> commentsPipe)
        {
            //string targetUri = $"https://api.twitch.tv/v5/videos/{videoID}/comments";

            string? nextCursor;
            string video = $"{videoID}/comments";
            string query = "";

            StreamWriter sw = new(outputPath);

            do {
                string requestUri = video + query;

                //Stopwatch stw = new();
                //stw.Start();

                var responseComments = await TwitchClient.SendAsync(TwitchClient.RequestType.Comment, requestUri);
                var jsonComments = await responseComments.Content.ReadFromJsonAsync<JsonComments>();

                //stw.Stop();
                //Console.WriteLine($"Done. Time: {stw.Elapsed}");

                commentsPipe.Add(new Tuple<StreamWriter, JsonComments>(sw, jsonComments));

                nextCursor = jsonComments.Next;
                query = $"?cursor={nextCursor}";

                int offset = Convert.ToInt32(jsonComments.Comments[^1].ContentOffsetSeconds);
                progressBar.Report(videoID, offset);
            } while (nextCursor is not null);

            progressBar.Report(videoID, -1);
        }

        //private void WriteComments()
        //{
        //    foreach (var part in _commentsPipe.GetConsumingEnumerable()) {
        //        var sw = part.Item1;
        //        var jc = part.Item2;
        //        foreach (var comment in jc.Comments) {
        //            sw.WriteLine($"{comment.ContentOffsetSeconds}\t{comment.Commenter.Name}: {comment.Message.Body}");
        //        }

        //        if (jc.Next is null)
        //            sw.Close();
        //    }
        //}

        //public class CommentInfo
        //{
        //    public string CommenterName;
        //    public string Message;
        //    public double OffsetSeconds;
        //}


        public class JsonComments
        {
            [JsonPropertyName("comments")]
            public JsonComment[] Comments { get; set; }
            // TODO: Turns out there is a _prev field also
            [JsonPropertyName("_next")]
            public string? Next { get; set; }


            public class JsonComment
            {
                [JsonPropertyName("_id")]
                public string ID { get; set; }
                [JsonPropertyName("created_at")]
                public DateTime CreatedAt { get; set; }
                [JsonPropertyName("updated_at")]
                public DateTime UpdatedAt { get; set; }
                [JsonPropertyName("channel_id")]
                public string ChannelID { get; set; } // long
                [JsonPropertyName("content_type")]
                public string ContentType { get; set; }
                [JsonPropertyName("content_id")]
                public string ContentID { get; set; } // long
                [JsonPropertyName("content_offset_seconds")]
                public double ContentOffsetSeconds { get; set; } // TimeSpan
                [JsonPropertyName("commenter")]
                public JsonCommenter Commenter { get; set; }
                [JsonPropertyName("source")]
                public string Source { get; set; }
                [JsonPropertyName("state")]
                public string State { get; set; }
                [JsonPropertyName("message")]
                public JsonMessage Message { get; set; }
            }

            public class JsonCommenter
            {
                [JsonPropertyName("display_name")]
                public string DisplayName { get; set; }
                [JsonPropertyName("_id")]
                public string ID { get; set; } // long
                [JsonPropertyName("name")]
                public string Name { get; set; }
                [JsonPropertyName("type")]
                public string Type { get; set; }
                [JsonPropertyName("bio")]
                public string? BIO { get; set; }
                [JsonPropertyName("created_at")]
                public DateTime CreatedAt { get; set; }
                [JsonPropertyName("updated_at")]
                public DateTime UpdatedAt { get; set; }
                [JsonPropertyName("logo")]
                public Uri Logo { get; set; }
            }

            public class JsonMessage
            {
                [JsonPropertyName("body")]
                public string Body { get; set; }
                [JsonPropertyName("fragments")]
                public JsonFragments[] Fragments { get; set; }
                [JsonPropertyName("is_action")]
                public bool IsAction { get; set; }
                [JsonPropertyName("user_badges")]
                public JsonUserBadge[] UserBadges { get; set; }
                [JsonPropertyName("user_color")]
                public string UserColor { get; set; }
                [JsonPropertyName("user_notice_params")]
                private object UserNoticeParams { get; set; }
            }

            public class JsonUserBadge
            {
                [JsonPropertyName("_id")]
                public string ID { get; set; }
                [JsonPropertyName("version")]
                public string Version { get; set; } // int
            }

            public class JsonFragments
            {
                [JsonPropertyName("text")]
                public string Text { get; set; }
            }
        }
    }
}

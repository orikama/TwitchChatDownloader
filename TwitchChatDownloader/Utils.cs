using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;


namespace TwitchChatDownloader
{
    static class Utils
    {
        public static T LoadJsonFile<T>(string path) where T : class
        {
            var jsonFile = File.ReadAllText(path);

            return JsonSerializer.Deserialize<T>(jsonFile);
        }

        public static async Task SaveJsonFileAsync<T>(string path, T jsonObject) where T : class
        {
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fs, jsonObject, new JsonSerializerOptions { WriteIndented = true });
        }

        //private async Task<Func<TwitchComment.JsonComments.JsonComment, string>> BuildLambdaAsync(string format)
        public static async Task<T> CompileLambdaAsync<T>(string expression)
        {
            Console.Write("Compiling 'CommentFormat' string");

            var lambdaExpression = $"comment => $\"{expression}\"";
            var options = ScriptOptions.Default.AddReferences(typeof(TwitchComment.JsonComments.JsonComment).Assembly);
            var lambda = await CSharpScript.EvaluateAsync<T>(lambdaExpression, options);

            Console.WriteLine(" Done.");

            return lambda;
        }
    }
}

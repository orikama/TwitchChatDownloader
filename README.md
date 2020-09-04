# TwitchChatDownloader
[Twitch-Chat-Downloader](https://github.com/PetterKraabol/Twitch-Chat-Downloader) was too slow for me, and this is not an app problem, it's just how Twitch API works.
The only way to speed things up is to download multiple VOD logs at the same time. And this is what I did with this app.

## Features
- Download multiple Twitch chat logs in parallel
- Specify custom format for output logs (with C# scripting support)

## Getting Started

### Building
You need to get `Visual Studio 2019 16.7` and `.Net v5.0.0-preview.7` to build this repo.  
I don't know if any other combination of different Visual Studio and .Net versions will work.

### Command line agruments
Type `TwitchChatDownloader.exe -h` to get list of all options.  

There are two ways to get Twitch VOD logs:  
- Get logs for specified video IDs  
`TwitchChatDownloader --video <video ID>,<video ID>`
- Get first `<number>` logs from specified channel names  
`TwitchChatDownloader --channel <channel name>,<channel name> --first <number>`

You can use different formats to specify multiple arguments for options:
- `-v <video ID>,<video ID>`
- `-v=<video ID>,<video ID>`
- `-v:<video ID>,<video ID>`

### Settings.json
- You must specify `ClientID` and `ClientSecret` to make this app work. You can get them from your [twitch developer console](https://dev.twitch.tv/console)
- `OAuthToken` will be set (and saved) automatically after you run `TwitchChatDownloader` for the first time
- `MaxConcurrentDownloads` number of simultaneous downloads of chat logs. I haven't tested it with values >4
- `OutputPath` path to the output folder
- `CommentFormat` a string specifying the output format of each chat message (more below)

### CommentFormat string
This option allow you to specify what fields from chat logs you want to get from Twitch HTTP API response.  

All variables should be in `{comment.<The variable you need>}` format. You can get the list of all available variables from the `JsonComment` class at the bottom of [TwitchComment.cs](https://github.com/orikama/TwitchChatDownloader/blob/master/TwitchChatDownloader/TwitchComment.cs) source file.

To simply get messages in `<commenter name> <message>` format you can use the following string:
```
"{comment.Commenter.DisplayName} {comment.Message.Body}"
```

Or you can use C# to get more fancier output:
```
"{comment.CreatedAt.ToString().PadRight(22)} {comment.Commenter.Name.PadRight(20)} {comment.Message.Body.ToUpper()}"
```
Which gives you:

![CommentFormat example output](https://github.com/orikama/TwitchChatDownloader/blob/master/readme-fig/CommentFormatExample.png)

You can technically use more complicated C# expressions inside {} brackets, but I can't think of any good example.

## Dependencies
- `System.CommandLine` [![Nuget](https://img.shields.io/nuget/v/System.CommandLine.svg)](https://nuget.org/packages/System.CommandLine)  
- `Microsoft.CodeAnalysis.CSharp.Scripting` [![Nuget](https://img.shields.io/nuget/v/Microsoft.CodeAnalysis.CSharp.Scripting)](https://nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting)

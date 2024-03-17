using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;

namespace ytdl;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var urlArgument = new Argument<string>(name: "url", description: "YouTube URL.");

        var dirOption = new Option<string>(name: "--output", description: "Output directory.")
        {
            IsRequired = true
        };
        dirOption.AddAlias("-o");
        dirOption.AddValidator(o =>
        {
            var dir = o.GetValueForOption<string>(dirOption);
            if (!Path.Exists(dir))
                o.ErrorMessage = $"Option '--output' path {dir} does not exists.";
        });

        var limitOption = new Option<int>(name: "--limit", description: "Limit number of downloads.");
        limitOption.SetDefaultValue(-1);
        var overwriteOption = new Option<bool>(name: "--overwrite", description: "Overwrite video, if present.");

        var qualityOption = new Option<string>(name: "--quality", description: "Video quality (less or equal to). Choose: 4320p, 2160p, 1440p, 1080p, 720p, 480p, 360p or 240p.");
        qualityOption.AddAlias("-q");
        qualityOption.SetDefaultValue("720p");
        qualityOption.AddValidator(o =>
        {
            var valid = new HashSet<string> { "4320p", "2160p", "1440p", "1080p", "720p", "480p", "360p", "240p" };
            var qualityLabel = o.GetValueForOption<string>(qualityOption) ?? "720p";
            if (!valid.Contains(qualityLabel))
                o.ErrorMessage = $"Option '--quality' has invalid value {qualityLabel}.";
        });

        var conversionPresetOption = new Option<string>(name: "--conversion-preset", description: "Conversion preset. Choose: VerySlow (best), Slow, Medium, Fast, VeryFast or UltraFast.");
        conversionPresetOption.AddAlias("-p");
        conversionPresetOption.SetDefaultValue("Medium");
        conversionPresetOption.AddValidator(o =>
        {
            var preset = o.GetValueForOption<string>(conversionPresetOption) ?? "Medium";
            try
            {
                Enum.Parse(typeof(ConversionPreset), preset);
            }
            catch
            {
                o.ErrorMessage = $"Option '--conversion-preset' has invalid value {preset}";
            }
        });

        var rootCommand = new RootCommand("YouTube Downloader.");

        var subCommandDownloadVideo = new Command("video", "Download Video.")
        {
            urlArgument,
            dirOption,
            qualityOption,
            conversionPresetOption,
        };
        subCommandDownloadVideo.AddAlias("v");
        rootCommand.Add(subCommandDownloadVideo);

        subCommandDownloadVideo.SetHandler(async (url, dir, qualityLabel, conversionPreset) =>
            {
                var path = await DownloadYouTubeVideo(new YoutubeClient(), url!, dir!, qualityLabel, conversionPreset);

                if (Path.Exists(path))
                {
                    var tfile = TagLib.File.Create(@path);
                    var desc = tfile.Tag.Description;

                    var comment = tfile.Tag.Comment;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonComment = JsonSerializer.Serialize(JsonObject.Parse(comment), options);
                    Console.WriteLine(jsonComment);
                }
            },
            urlArgument, dirOption, qualityOption, conversionPresetOption
        );

        var subCommandDownloadPlaylist = new Command("list", "Download Playlist.")
        {
            urlArgument,
            dirOption,
            limitOption,
            overwriteOption,
            qualityOption,
            conversionPresetOption,
        };
        subCommandDownloadPlaylist.AddAlias("l");
        rootCommand.Add(subCommandDownloadPlaylist);

        subCommandDownloadPlaylist.SetHandler(async (url, dir, limit, noSkip, qualityLabel, conversionPreset) =>
            {
                await DownloadYouTubePlaylist(url!, dir!, limit, noSkip, qualityLabel, conversionPreset);
            },
            urlArgument, dirOption, limitOption, overwriteOption, qualityOption, conversionPresetOption
        );

        return await rootCommand.InvokeAsync(args);
    }

    static IReadOnlySet<string> FindPresentIds(string dir)
    {
        var result = new HashSet<string>();

        string pattern = @"\[([\da-zA-Z-_]+)\].mp4";

        foreach (string p in Directory.GetFiles(@dir))
        {
            if (!Path.GetExtension(p).Equals(".mp4"))
                continue;

            var match = Regex.Matches(p, pattern);
            if (match.Count > 0)
            {
                var ytId = match.First().Groups[1].Value;
                result.Add(ytId);
            }
        }
        return result;
    }

    static string MetadataOf(YoutubeExplode.Videos.Video video, IVideoStreamInfo streamInfo)
    {
        var videoId = video.Id;
        var videoTitle = video.Title;
        var videoAuthor = video.Author;
        var videoDuration = video.Duration;
        var videoURL = video.Url;
        var videoUploadedAt = video.UploadDate;

        var meta = new
        {
            ytid = videoId.Value,
            title = videoTitle,
            channel = new
            {
                id = videoAuthor.ChannelId.Value,
                title = videoAuthor.ChannelTitle,
                url = videoAuthor.ChannelUrl,
            },
            duration = videoDuration,
            url = videoURL,
            uploadedAt = videoUploadedAt,
            quality = streamInfo.VideoQuality.Label,
            format = streamInfo.Container.Name,
        };

        return JsonSerializer.Serialize(meta);
    }

    static string FixTitle(string title)
    {
        return (
            Regex.Replace(title, @"\p{Cs}", "")  // Remove emojis
            .Replace("/", "-")
            .Replace("\u00A9", "")  // (c)
            .Replace("\u00AE", "")  // (r)
            .Replace("\u2122", "")  // tm
        );
    }

    static readonly HashSet<char> InvalidFileNameChars = new(new char[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' });

    static string EscapeFileName(string filename)
    {
        var buf = new StringBuilder();

        foreach (var c in filename)
            buf.Append(!InvalidFileNameChars.Contains(c) ? c : '_');

        return buf.ToString();
    }

    static double ConvertVideoQualityLabelToDouble(string label)
    {
        return double.Parse(label.Replace("p", "."));
    }

    static int ConvertVideoQualityLabelToInt(string label)
    {
        var matches = Regex.Matches(@label, @"^(\d{3,4})p");
        if (matches.Count > 0)
        {
            var val = matches.First().Groups[1].Value;
            return int.Parse(val);
        }
        throw new ArgumentException("Invalid video quality label.");
    }

    static async Task<string> DownloadYouTubeVideo(YoutubeClient youtube, string videoUrl, string dir, string qualityLabel, string conversionPreset)
    {
        var video = await youtube.Videos.GetAsync(videoUrl);

        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

        var audioStreamInfo = streamManifest
            .GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestBitrate();

        var videoStreamInfo = streamManifest
            .GetVideoOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .OrderByDescending(s => ConvertVideoQualityLabelToDouble(s.VideoQuality.Label))
            .Where(s => s.VideoQuality.MaxHeight <= ConvertVideoQualityLabelToInt(qualityLabel))
            .First();

        var channelTitle = FixTitle(video.Author.ChannelTitle);
        var title = FixTitle(video.Title);
        var filename = $"{channelTitle} - {title} ({videoStreamInfo.VideoQuality.Label}) [{video.Id}].{Container.Mp4.Name}";
        var outputPath = Path.Join(Path.GetFullPath(dir), EscapeFileName(filename));

        Console.Write($"=> {video.Id} - {video.Author.ChannelTitle} - {video.Title} ... ");

        var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
        var preset = (ConversionPreset)Enum.Parse(typeof(ConversionPreset), conversionPreset);
        await youtube.Videos.DownloadAsync(
            streamInfos,
            new ConversionRequestBuilder(outputPath).SetContainer(Container.Mp4).SetPreset(preset).Build()
        );

        var tfile = TagLib.File.Create(@outputPath);
        tfile.Tag.Title = video.Title;
        tfile.Tag.Description = video.Description;
        tfile.Tag.Comment = MetadataOf(video, videoStreamInfo);
        tfile.Save();

        Console.Write("OK\n");

        return outputPath;
    }

    static async Task<int> DownloadYouTubePlaylist(string playlistUrl, string dir, int limit, bool noSkip, string qualityLabel, string conversionPreset)
    {
        var visited = noSkip ? new HashSet<string>() : FindPresentIds(dir);
        var youtube = new YoutubeClient();

        Playlist playlist;
        try
        {
            playlist = await youtube.Playlists.GetAsync(playlistUrl);
        }
        catch
        {
            Console.Error.WriteLine($"Playlist-URL {playlistUrl} not found!");
            return -1;
        }

        var total = 0;
        await foreach (Batch<PlaylistVideo> batch in youtube.Playlists.GetVideoBatchesAsync(playlist.Id))
        {
            foreach (PlaylistVideo playlistVideo in batch.Items)
            {
                if (total == limit)
                    return 1;

                if (visited.Contains(playlistVideo.Id))
                    continue;

                try
                {
                    var video = await youtube.Videos.GetAsync(playlistVideo.Id);
                    await DownloadYouTubeVideo(youtube, video.Id, dir, qualityLabel, conversionPreset);
                    total += 1;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"=> {playlistVideo.Id} - {e.Message} ... Failed");
                    continue;
                }

            }
        }
        return 1;
    }
}

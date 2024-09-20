# README

A CLI for downloading YT Videos or Playlists using [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode).

> This project is mostly unfinished and does not support async downloads.

## Quickstart

```
# OS: Ubuntu
snap install ffmpeg
sudo snap connect ffmpeg:removable-media

nix-shell -p dotnetCorePackages.dotnet_8.sdk

make build
# Target: ./bin/Release/net8.0/linux-x64/publish/YoutubeDownloaderCli
```

## Usage

```
YoutubeDownloaderCli l "https://www.youtube.com/playlist?list=<playlist-id>" -o /tmp/playlists/playlist --limit 100 --quality 1080p
```

## Docs

```
$ YoutubeDownloaderCli -h
Description:
  YouTube Downloader.

Usage:
  YoutubeDownloaderCli [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  v, video <url>  Download Video.
  l, list <url>   Download Playlist.
```

Video

```
$ YoutubeDownloaderCli video -h
Description:
  Download Video.

Usage:
  YoutubeDownloaderCli video [<url>] [options]

Arguments:
  <url>  YouTube URL.

Options:
  -o, --output <output> (REQUIRED)             Output directory.
  -q, --quality <quality>                      Video quality (less or equal to). Choose: 4320p, 2160p, 1440p, 1080p, 720p, 480p, 360p or 240p. [default: 720p]
  -p, --conversion-preset <conversion-preset>  Conversion preset. Choose: VerySlow (best), Slow, Medium, Fast, VeryFast or UltraFast. [default: Medium]
  -?, -h, --help                               Show help and usage information
```

Playlist

```
$ YoutubeDownloaderCli list -h
Description:
  Download Playlist.

Usage:
  YoutubeDownloaderCli list [<url>] [options]

Arguments:
  <url>  YouTube URL.

Options:
  -o, --output <output> (REQUIRED)             Output directory.
  --limit <limit>                              Limit number of downloads. [default: -1]
  --overwrite                                  Overwrite video, if present.
  -q, --quality <quality>                      Video quality (less or equal to). Choose: 4320p, 2160p, 1440p, 1080p, 720p, 480p, 360p or 240p. [default: 720p]
  -p, --conversion-preset <conversion-preset>  Conversion preset. Choose: VerySlow (best), Slow, Medium, Fast, VeryFast or UltraFast. [default: Medium]
  -?, -h, --help                               Show help and usage information
```

## See also

* https://github.com/Tyrrrz/YoutubeExplode

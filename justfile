# Show this help
help:
        just -l

# Build binary
build:
        dotnet publish --configuration Release --sc --os linux --arch x64 YoutubeDownloaderCli.csproj

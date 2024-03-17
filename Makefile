.PHONY: help
help:
	@echo "Targets:"
	@echo "  build - Build self contained binary."


.PHONY: build
build:
	dotnet publish --configuration Release --sc --os linux --arch x64 YoutubeDownloaderCli.csproj

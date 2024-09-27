{ pkgs ? import <nixpkgs> {}
}:
pkgs.mkShell {
  name = "projects.youtubedownloadercli";
  buildInputs = [
    pkgs.dotnetCorePackages.dotnet_8.sdk
    pkgs.just
  ];
}

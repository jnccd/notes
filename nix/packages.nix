{
  # Nix packages for notes.
  #
  # Only the two things that actually get run are packaged: the Avalonia desktop
  # app and the ASP.NET Core server. The interface library, the Android head and
  # the vendored submodules are build inputs, not outputs.
  pkgs,
  lib,
}:

let
  version = "0.1.0";

  # Avalonia renders through Skia. This app pulls in
  # SkiaSharp.NativeAssets.Linux.NoDependencies, so Skia itself arrives without
  # the fontconfig/freetype it would otherwise need, but the X11/GL stack still
  # has to be on LD_LIBRARY_PATH or startup dies with
  # "Unable to load shared library 'libSkiaSharp'".
  avaloniaNativeLibs = with pkgs; [
    fontconfig
    freetype
    libGL
    harfbuzz
    icu
    zlib

    libx11
    libice
    libsm
    libxcb
    libxrandr
    libxi
    libxcursor
    libxext
    libxrender
    libxkbcommon
  ];

  # Everything but build output. The flake's copy of the tree already excludes
  # bin/obj because they are gitignored, but a local `nix build` on a dirty tree
  # would otherwise pick up hundreds of MB of stale build products and make the
  # result depend on whatever was compiled last.
  src = lib.cleanSourceWith {
    src = ../.;
    filter =
      path: _: !(builtins.elem (baseNameOf path) [
        "bin"
        "obj"
      ]);
  };

  # Shared by both packages. Self-contained so the host needs no matching .NET
  # runtime installed.
  #
  # nuget-deps.json MUST be generated RID-aware (`dotnet restore -r linux-x64`).
  # buildDotnetModule always applies a runtimeId - it defaults to the host's RID
  # even when you do not pass one - and this graph mixes target frameworks (the
  # desktop app is net10.0 while EzAuth is net8.0), so a self-contained publish
  # needs Microsoft.NETCore.App.Host.linux-x64 for both bands. Restoring without
  # -r omits the 8.0 one and the build dies on
  # "Unable to find package Microsoft.NETCore.App.Host.linux-x64 (= 8.0.27)".
  common = {
    inherit version src;
    dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
    selfContainedBuild = true;
    runtimeId = "linux-x64";
    nugetDeps = ../nuget-deps.json;
  };
in
{
  # `nix build` / `nix build .#` -> the desktop app.
  desktop = pkgs.buildDotnetModule (
    common
    // {
      pname = "notes";
      projectFile = "NotesAvalonia.Desktop/NotesAvalonia.Desktop.csproj";
      dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;

      # The csproj turns on NativeAOT for Release, but that is there for the
      # Android head; on Linux it only adds a C toolchain and a very slow
      # compile, and the desktop app is run managed in practice anyway
      # (`dotnet .../NotesAvalonia.Desktop.dll`, which is what the autostart
      # does). Publish it managed and self-contained instead.
      dotnetBuildFlags = [ "-p:PublishAot=false" ];
      dotnetPublishFlags = [ "-p:PublishAot=false" ];

      # buildDotnetModule installs the published app under $out/lib/<pname>;
      # this puts the executable in $out/bin and wraps it with runtimeDeps'
      # LD_LIBRARY_PATH.
      executables = [ "NotesAvalonia.Desktop" ];
      runtimeDeps = avaloniaNativeLibs;

      meta = {
        description = "Notes desktop app (Avalonia)";
        license = lib.licenses.mit;
        platforms = lib.platforms.linux;
        mainProgram = "NotesAvalonia.Desktop";
      };
    }
  );

  server = pkgs.buildDotnetModule (
    common
    // {
      pname = "notes-server";
      projectFile = "NotesServer/NotesServer.csproj";
      dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;
      executables = [ "NotesServer" ];

      meta = {
        description = "Notes server (ASP.NET Core)";
        license = lib.licenses.mit;
        platforms = lib.platforms.linux;
        mainProgram = "NotesServer";
      };
    }
  );
}

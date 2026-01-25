{
  description = "Nix Dev shell for Avalonia .NET Desktop/Android development";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.05";

  outputs = { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs {
        inherit system;
        config = {
          allowUnfree = true;
          android_sdk.accept_license = true;
        };
      };

      androidComposition = pkgs.androidenv.composeAndroidPackages {
        platformVersions = [ "33" "34" ]; # Include both android version 13 + 14 SDKs
        buildToolsVersions = [ "34.0.0" ];
        includeEmulator = false;
        includeSources = false;
        includeNDK = true;
        useGoogleAPIs = false;
        useGoogleTVAddOns = false;
      };

      androidSdk = androidComposition.androidsdk;
      androidNdk = pkgs.androidenv.androidPkgs.ndk-bundle;
    in {
      devShells.${system} = {
        default = pkgs.mkShell {
          buildInputs = with pkgs; [
            bash
            curl
            unzip
            openjdk17
            bubblewrap
            android-tools
          ];

          shellHook = ''
            set -e
            export DOTNET_ROOT="$HOME/.dotnet"
            export PATH="$DOTNET_ROOT:$PATH"

            echo "🧰 Entering FHS environment..."
            if [ -z "$IN_FHS_SHELL" ]; then
              export IN_FHS_SHELL=1
              exec ${
                pkgs.buildFHSEnvBubblewrap {
                  name = "dotnet-fhs";
                  targetPkgs = pkgs:
                    with pkgs; [
                      bash
                      curl
                      unzip
                      openjdk17
                      android-tools
                      androidSdk
                      androidNdk
                      zlib
                      icu
                      krb5
                      openssl
                      glibc

                      # Desktop deps
                      fontconfig
                      freetype
                      libpng
                      xorg.libX11
                      libGL
                      harfbuzz
                      expat
                      mesa
                      gcc
                      glib
                      gtk3
                      glibc
                      xorg.libICE
                      xorg.libSM
                      xorg.libXrandr
                      xorg.libXi
                      icu
                    ];
                  runScript = pkgs.writeShellScript "dotnet-fhs-start" ''
                    set -e
                    export DOTNET_ROOT="$HOME/.dotnet"
                    export PATH="$DOTNET_ROOT:$PATH"

                    export LD_LIBRARY_PATH=${
                      with pkgs;
                      pkgs.lib.makeLibraryPath [
                        fontconfig
                        freetype
                        libpng
                        xorg.libX11
                        libGL
                        harfbuzz
                        expat
                        mesa
                        gcc
                        glib
                        gtk3
                        glibc
                        xorg.libICE
                        xorg.libSM
                        xorg.libXrandr
                        xorg.libXi
                        icu
                      ]
                    }:$LD_LIBRARY_PATH

                    export ANDROID_SDK_ROOT=${androidSdk}/libexec/android-sdk
                    export ANDROID_NDK_ROOT=${androidNdk}
                    export PATH=$ANDROID_SDK_ROOT/tools:$ANDROID_SDK_ROOT/platform-tools:$PATH

                    if [ ! -x "$DOTNET_ROOT/dotnet" ]; then
                      echo "📦 Installing Microsoft .NET SDK..."
                      mkdir -p "$DOTNET_ROOT"
                      curl -sSL https://dot.net/v1/dotnet-install.sh \
                        | bash -s -- --channel 8.0 --install-dir "$DOTNET_ROOT"
                    else
                      echo "✅ Using existing .NET SDK from $DOTNET_ROOT"
                    fi

                    echo "💡 .NET + Android ready!"
                    exec bash -l
                  '';
                }
              }/bin/dotnet-fhs
            fi
          '';
        };
      };
    };
}

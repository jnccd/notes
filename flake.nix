{
  description = "Nix Shell Wrapper";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-26.05";
    numtide-utils = {
      url = "github:numtide/flake-utils";
    };
    jnccd-utils = {
      url = "github:jnccd/nix-utils";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs =
    { self, nixpkgs, ... }@inputs:
    inputs.numtide-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs { inherit system; };
      in
      {
        devShells = rec {
          # Deployment
          service =
            with pkgs;
            mkShell {
              packages = [
                icu
                dotnet-sdk_8
                dotnet-ef
              ];
            };
          desktop = inputs.jnccd-utils.lib.mkUnfrozenDotnetShell {
            inherit system nixpkgs;
            dotnetVersion = "10.0";
            androidSdkVersions = [
              "34"
              "35"
            ];
            command = "cd notes ; bash ./start_desktop_app.sh";
          };

          # Dev
          dev = inputs.jnccd-utils.lib.mkUnfrozenDotnetShell {
            inherit system nixpkgs;
            dotnetVersion = "10.0";
            androidSdkVersions = [
              "34"
              "35"
            ];
          };

          default = dev;
        };
      }
    );
}

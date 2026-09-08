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
        workloadsHashX86_64Linux = "sha256-AcfemNC9S9Lk9AeW+EokaKYJpf3aDGywMTsi821Mo9M=";
      in
      {
        devShells = rec {
          # Deployment
          service =
            with pkgs;
            mkShell {
              packages = [
                icu
                dotnet-sdk_10
                dotnet-ef
              ];
            };
          desktop = inputs.jnccd-utils.lib.mkDotnetWithWorkloadsShell {
            inherit system nixpkgs;
            dotnetVersion = "10.0";
            workloads = [
              "android"
              "wasm-tools"
            ];
            androidSdkVersions = [
              "34"
              "35"
              "36"
            ];
            workloadsHash = workloadsHashX86_64Linux;
          };

          # Dev
          dev = inputs.jnccd-utils.lib.mkDotnetWithWorkloadsShell {
            inherit system nixpkgs;
            dotnetVersion = "10.0";
            workloads = [
              "android"
              "wasm-tools"
            ];
            androidSdkVersions = [
              "34"
              "35"
              "36"
            ];
            workloadsHash = workloadsHashX86_64Linux;
          };

          default = dev;
        };
      }
    );
}

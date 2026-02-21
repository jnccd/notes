{
  description = "Nix Shell Wrapper";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.11";
    numtide-utils.url = "github:numtide/flake-utils";
    jnccd-utils.url = "github:jnccd/nix-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let pkgs = import nixpkgs { inherit system; };
      in {
        devShells = rec {
          service = import ./shell.nix { inherit pkgs; };
          gui-dev = inputs.jnccd-utils.lib.mkUnfrozenDotnetShell {
            inherit system nixpkgs;
            dotnetVersion = "9.0";
            androidSdkVersions = [ "34" "35" ];
          };

          default = gui-dev;
        };
      });
}

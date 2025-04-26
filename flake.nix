# nix develop . --experimental-features 'nix-command flakes'
# nix develop --experimental-features 'nix-command flakes' --command bash -c "./update_and_start.sh"
{
  description = "Nix Shell Wrapper";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-24.11";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem
      (system: let
          pkgs = import nixpkgs {inherit system;};
        in {
          devShells.default = import ./shell.nix { inherit pkgs; };
        }
      );
}
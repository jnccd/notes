#nix-shell --command "./update_and_start.sh"
{ pkgs ? import <nixpkgs> {} }:
with pkgs;
mkShell {
  packages = [
    dotnet-sdk_6
  ];
}
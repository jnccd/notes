#nix-shell --command "npm run build"
{ pkgs ? import <nixpkgs> {} }:
with pkgs;
mkShell {
  packages = [
    dotnet-sdk_6
  ];
}
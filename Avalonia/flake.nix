{
  description = "Nix Dev shell for Avalonia .NET Desktop/Android development";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.11";
    numtide-utils.url = "github:numtide/flake-utils";
    jnccd-utils.url = "github:jnccd/nix-utils";
  };

  outputs = { self, nixpkgs, ... }@inputs:
    (inputs.numtide-utils.lib.eachSystem [ "x86_64-linux" "aarch64-linux" ]
      (system: {
        devShells.default = inputs.jnccd-utils.lib.createDotnetAndroidDevShell {
          inherit system nixpkgs;
          dotnetVersion = "9.0";
          androidSdkVersions = [ "34" "35" ];
        };
      }));
}

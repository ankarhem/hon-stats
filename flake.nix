{
  description = "HoN Reborn community stats site";

  inputs = {
    deploy-rs.url = "github:serokell/deploy-rs";
    deploy-rs.inputs.nixpkgs.follows = "nixpkgs";
    flake-parts.url = "github:hercules-ci/flake-parts";
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    git-hooks.url = "github:cachix/git-hooks.nix";
    git-hooks.inputs.nixpkgs.follows = "nixpkgs";
    treefmt-nix.url = "github:numtide/treefmt-nix";
  };

  outputs =
    inputs@{ self, ... }:
    inputs.flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [
        inputs.git-hooks.flakeModule
        inputs.treefmt-nix.flakeModule
      ];
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "aarch64-darwin"
        "x86_64-darwin"
      ];
      perSystem =
        {
          config,
          lib,
          pkgs,
          system,
          ...
        }:
        {
          treefmt = {
            programs.nixfmt.enable = true;
            programs.nixfmt.package = pkgs.nixfmt;
            programs.csharpier.enable = true;
          };
          pre-commit.settings.hooks = {
            treefmt.enable = true;
          };

          apps.deploy = {
            type = "app";
            program = "${inputs.deploy-rs.packages.${system}.default}/bin/deploy";
          };

          packages.hon-stats = pkgs.buildDotnetModule {
            pname = "hon-stats";
            version = "0.1.0";
            src = ./.;
            projectFile = "src/HonStats.Web/HonStats.Web.csproj";
            nugetDeps = ./nuget-deps.json;
            dotnet-sdk = pkgs.dotnet-sdk_10;
            dotnet-runtime = pkgs.dotnet-runtime_10;
            runtimeDeps = [ pkgs.sqlite.out ];
            postPatch = "rm -f .config/dotnet-tools.json";
          };

          checks = lib.optionalAttrs (system == "x86_64-linux") (
            inputs.deploy-rs.lib.${system}.deployChecks self.deploy
          );

          devShells.default = pkgs.mkShell {
            inherit (config.pre-commit) shellHook;
            packages =
              with pkgs;
              [
                dotnet-sdk_10
                dotnet-ef
                omnisharp-roslyn
                nuget
                just
              ]
              ++ config.pre-commit.settings.enabledPackages;
          };
          _module.args.pkgs = import inputs.nixpkgs {
            inherit system;
            overlays = lib.attrValues self.overlays;
          };
        };

      flake.deploy.nodes.homelab = {
        hostname = "homelab";
        sshUser = "root";
        user = "root";
        profiles.hon-stats = {
          path =
            let
              p = self.packages.x86_64-linux.hon-stats;
            in
            inputs.deploy-rs.lib.x86_64-linux.activate.custom p ''
              ln -sfn "${p}" /var/lib/hon-stats/current
              systemctl restart hon-stats.service
            '';
        };
      };

      flake.overlays.default = final: prev: { };
    };
}

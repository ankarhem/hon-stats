# List the available recipes
default:
  just --list

# Run the application. Use --watch for hot reloading: just run --watch
run *args:
  #!/usr/bin/env bash
  if [[ " $* " == *" --watch "* ]]; then
    dotnet watch run --project src/HonStats.Web/HonStats.Web.csproj
  else
    dotnet run --project src/HonStats.Web/HonStats.Web.csproj
  fi

# Run all pre-commit validation (format, build, unit tests, E2E).
# `dotnet build` and `dotnet test` surface all warnings in their output —
# fix any that appear before committing.
validate: fmt build test e2e

# Format all files via the flake's treefmt config.
fmt:
    nix fmt

# Build the .NET solution / project.
build:
    dotnet build -warnaserror

# Run unit tests (fast, offline).
test:
    dotnet test tests/HonStats.App.Tests/HonStats.App.Tests.csproj -warnaserror

# Run the Playwright E2E suite (launches the app + headless Chromium; needs
# live juvio credentials in user-secrets).
e2e:
    dotnet test tests/HonStats.Web.E2E/HonStats.Web.E2E.csproj -warnaserror

# Regenerate the NuGet dependency lock file (needed when packages are added/updated).
update-nuget-deps:
    nix build .#hon-stats.fetch-deps --print-out-paths && ./result /tmp/nuget-deps.raw.json && rm result
    nix run nixpkgs#jq -- 'map(select(.pname != "dotnet-ef"))' /tmp/nuget-deps.raw.json > nuget-deps.json
    rm /tmp/nuget-deps.raw.json

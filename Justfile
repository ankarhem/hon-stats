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

# Run all pre-commit validation (format, build, test).
# `dotnet build` and `dotnet test` surface all warnings in their output —
# fix any that appear before committing.
validate: fmt build test

# Format all files via the flake's treefmt config.
fmt:
    nix fmt

# Build the .NET solution / project.
build:
    dotnet build -warnaserror

# Run all tests.
test:
    dotnet test -warnaserror

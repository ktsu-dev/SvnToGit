# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SvnToGit is a .NET tool for migrating SVN repositories to Git while preserving commit history, branches, and tags using git-svn. It consists of a core library and an interactive TUI console application.

## Build and Test Commands

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build SvnToGit.Core/SvnToGit.Core.csproj

# Run tests
dotnet test

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~SampleTests.Echo_ReturnsProvidedValue"

# Run the console app
dotnet run --project SvnToGit.ConsoleApp
```

## Architecture

**Project Structure:**
- `SvnToGit.Core/` - Core migration library containing the `SvnToGitMigrator` class
- `SvnToGit.ConsoleApp/` - Interactive TUI application using Spectre.Console
- `SvnToGit.Test/` - MSTest-based test project

**Key Components:**
- `SvnToGitMigrator` - Main migration orchestrator that wraps git-svn operations
- `SvnMigrationConfig` - Configuration record for migration settings (paths, authors file, exclusions)
- `MigrationProgress` - Progress reporting record with phase tracking
- `ProcessRunner` - Wrapper around `ktsu.RunCommand` for executing git commands

**Migration Flow:**
1. Validate configuration and check git-svn availability
2. Clone SVN repository using `git svn clone --stdlayout`
3. Convert remote branches to local branches
4. Run `git gc --aggressive` to optimize the repository

## SDK and Dependencies

This project uses the ktsu.Sdk custom MSBuild SDK (defined in `global.json`):
- `ktsu.Sdk.Lib` - For the core library (multi-targeted)
- `ktsu.Sdk.ConsoleApp` - For the console application
- `ktsu.Sdk.Test` - For the test project (inherits from MSTest.Sdk)

Dependencies are managed centrally via `Directory.Packages.props`.

## Coding Standards

- Use `StringComparison` parameter overloads for `string.Contains` and `string.Replace`
- Forward `CancellationToken` to async methods or explicitly pass `CancellationToken.None`
- Avoid catching generic exceptions; catch specific exception types
- Prefer `Collection<T>`, `ReadOnlyCollection<T>`, or `KeyedCollection<K,V>` over `List<T>`
- Use `ConfigureAwait(false)` in library code for async methods

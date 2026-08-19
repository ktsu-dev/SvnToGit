# ktsu.SvnToGit

A guided .NET 10 CLI that migrates a Subversion repository to Git, wrapping `git svn` with an interactive Spectre.Console front-end.

[![License](https://img.shields.io/github/license/ktsu-dev/SvnToGit.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.SvnToGit.Core?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.SvnToGit.Core)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.SvnToGit.Core?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.SvnToGit.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.SvnToGit.Core?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.SvnToGit.Core)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/SvnToGit?label=Commits&logo=github)](https://github.com/ktsu-dev/SvnToGit/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/SvnToGit?label=Contributors&logo=github)](https://github.com/ktsu-dev/SvnToGit/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/SvnToGit/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/SvnToGit/actions)

## What it does

`SvnToGit.ConsoleApp` is a menu-driven TUI that walks a user through:

1. **Clone** — runs `git svn clone <svn-path> <git-path> --stdlayout`, with optional `--authors-file` and `--preserve-empty-dirs`.
2. **Cleanup refs** — converts the remote `git-svn` branches into local Git branches.
3. **Finalize** — runs `git gc --aggressive`.
4. **Report** — exit status with the path of the new Git repository.

Built on top of `git svn`, so anything `git svn` can do, this tool can do — but with a guided front end, validation, and progress reporting via Spectre.Console.

## Prerequisites

- Git with `git-svn` support on the `PATH`.
- An SVN repository reachable as a local path or URL.
- Disk space for the cloned Git repo.
- .NET 10 SDK.

## Installation

This repository ships two projects:

- `SvnToGit.ConsoleApp` — the runnable TUI.
- `SvnToGit.Core` — the migration library (`SvnToGitMigrator`) that the CLI consumes; reusable in other apps.

```bash
git clone <repo>
cd SvnToGit
dotnet build
```

## Usage

```bash
dotnet run --project SvnToGit.ConsoleApp
```

The TUI presents four options:

| Option | Effect |
|---|---|
| `migrate` | Run the full migration, prompting for paths. |
| `validate` | Validate the configured paths and exit without migrating. |
| `help` | Built-in help, including the authors-file format. |
| `exit` | Quit. |

### Configuration prompts (during `migrate`)

| Prompt | Required | Notes |
|---|---|---|
| SVN repository path | yes | Local path or URL. |
| Git repository path | no | Defaults to `<svn-dir>-git`. |
| Authors file | no | One mapping per line: `svnuser = Full Name <email@example.com>`. |
| Preserve empty directories | no | Defaults to `yes`. |
| Advanced options | no | Toggles exclude-tags / exclude-branches lists. |

> The `ExcludeTags` and `ExcludeBranches` fields are accepted in the prompts but are not currently passed through to `git svn` — see open issues.

## Related

KtsuTools exposes a non-interactive flag-driven version of the same migration as `ktsu svn-migrate`. This repo's TUI is the canonical interactive experience.

## License

MIT License. Copyright (c) ktsu.dev

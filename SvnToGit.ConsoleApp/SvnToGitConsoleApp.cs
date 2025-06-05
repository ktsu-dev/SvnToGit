// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.ConsoleApp;

using System.Collections.ObjectModel;
using ktsu.Extensions;
using ktsu.SvnToGit.Core;
using Spectre.Console;

/// <summary>
/// TUI-based SVN to Git migration application
/// </summary>
public static class SvnToGitConsoleApp
{
	/// <summary>
	/// Main entry point
	/// </summary>
	public static async Task Main()
	{
		// Display welcome banner
		AnsiConsole.Write(
			new FigletText("SVN to Git")
				.Centered()
				.Color(Color.Blue));

		AnsiConsole.Write(
			new Panel(new Text("Welcome to the SVN to Git Migration Tool\nThis tool helps you migrate SVN repositories to Git while preserving history.", style: Style.Parse("dim")))
				.Border(BoxBorder.Rounded)
				.BorderColor(Color.Yellow)
				.Header("[yellow]Migration Tool[/]"));

		AnsiConsole.WriteLine();

		try
		{
			while (true)
			{
				var choice = ShowMainMenu();

				switch (choice)
				{
					case "migrate":
						await PerformMigration().ConfigureAwait(false);
						break;
					case "validate":
						await ValidateConfiguration().ConfigureAwait(false);
						break;
					case "help":
						ShowHelp();
						break;
					case "exit":
						AnsiConsole.MarkupLine("[green]Thanks for using SVN to Git Migration Tool![/]");
						return;
					default:
						break;
				}

				AnsiConsole.WriteLine();
				AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
				Console.ReadKey();
				AnsiConsole.Clear();
			}
		}
		catch (OperationCanceledException)
		{
			AnsiConsole.MarkupLine("[yellow]Operation was cancelled.[/]");
		}
		catch (InvalidOperationException ex)
		{
			AnsiConsole.WriteException(ex);
		}
		catch (IOException ex)
		{
			AnsiConsole.WriteException(ex);
		}
	}

	private static string ShowMainMenu()
	{
		AnsiConsole.Write(new Rule("[blue]Main Menu[/]").RuleStyle("grey").LeftJustified());

		return AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("What would you like to do?")
				.PageSize(10)
				.AddChoices([
					"migrate", "validate", "help", "exit"
				])
				.UseConverter(choice => choice switch
				{
					"migrate" => "🔄 Start Migration",
					"validate" => "✅ Validate Configuration",
					"help" => "❓ Help & Information",
					"exit" => "🚪 Exit",
					_ => choice
				}));
	}

	private static async Task PerformMigration()
	{
		AnsiConsole.Write(new Rule("[green]SVN to Git Migration[/]").RuleStyle("green").LeftJustified());

		// Collect migration configuration
		var config = CollectMigrationConfiguration();

		if (config == null)
		{
			AnsiConsole.MarkupLine("[red]Migration cancelled.[/]");
			return;
		}

		// Validate configuration
		var migrator = new SvnToGitMigrator(config);
		var validationErrors = migrator.ValidateConfiguration();

		if (validationErrors.Count > 0)
		{
			AnsiConsole.MarkupLine("[red]Configuration validation failed:[/]");
			foreach (var error in validationErrors)
			{
				AnsiConsole.MarkupLine($"[red]• {error}[/]");
			}

			return;
		}

		// Confirm migration
		if (!await AnsiConsole.ConfirmAsync($"Are you ready to migrate [yellow]{config.SvnRepositoryPath}[/] to [yellow]{config.GitRepositoryPath}[/]?").ConfigureAwait(false))
		{
			AnsiConsole.MarkupLine("[yellow]Migration cancelled.[/]");
			return;
		}

		// Perform migration with progress tracking
		await AnsiConsole.Progress()
			.Columns(
			[
				new TaskDescriptionColumn(),
				new ProgressBarColumn(),
				new PercentageColumn(),
				new RemainingTimeColumn(),
				new SpinnerColumn(),
			])
			.StartAsync(async ctx =>
			{
				var task = ctx.AddTask("[green]Migrating repository[/]");
				task.MaxValue = 100;

				var progress = new Progress<MigrationProgress>(p =>
				{
					task.Value = p.ProgressPercentage;
					task.Description = $"[green]{p.Phase}[/]: {p.CurrentStep}";

					if (p.Errors.Count > 0)
					{
						task.Description = $"[red]{p.Phase}[/]: {p.CurrentStep}";
					}
				});

				var result = await migrator.MigrateAsync(progress).ConfigureAwait(false);

				if (result.Success)
				{
					task.Description = "[green]Migration completed successfully![/]";
					AnsiConsole.MarkupLine($"[green]✅ Repository successfully migrated to: {result.GitRepositoryPath}[/]");
				}
				else
				{
					task.Description = "[red]Migration failed[/]";
					AnsiConsole.MarkupLine("[red]❌ Migration failed with the following errors:[/]");
					foreach (var error in result.Errors)
					{
						AnsiConsole.MarkupLine($"[red]• {error}[/]");
					}
				}
			}).ConfigureAwait(false);
	}

	private static async Task ValidateConfiguration()
	{
		AnsiConsole.Write(new Rule("[yellow]Configuration Validation[/]").RuleStyle("yellow").LeftJustified());

		var config = CollectMigrationConfiguration();

		if (config == null)
		{
			AnsiConsole.MarkupLine("[red]Validation cancelled.[/]");
			return;
		}

		await AnsiConsole.Status()
			.Spinner(Spinner.Known.Star)
			.SpinnerStyle(Style.Parse("green"))
			.StartAsync("Validating configuration...", async ctx =>
			{
				await Task.Delay(1000).ConfigureAwait(false); // Simulate validation time

				var migrator = new SvnToGitMigrator(config);
				var errors = migrator.ValidateConfiguration();

				if (errors.Count == 0)
				{
					ctx.Status("Configuration is valid ✅");
					await Task.Delay(500).ConfigureAwait(false);
				}
				else
				{
					ctx.Status("Configuration has errors ❌");
					await Task.Delay(500).ConfigureAwait(false);
				}
			}).ConfigureAwait(false);

		var migrator = new SvnToGitMigrator(config);
		var validationErrors = migrator.ValidateConfiguration();

		if (validationErrors.Count == 0)
		{
			AnsiConsole.Write(
				new Panel("[green]✅ Configuration is valid and ready for migration![/]")
					.Border(BoxBorder.Rounded)
					.BorderColor(Color.Green));
		}
		else
		{
			var errorPanel = new Panel(
				string.Join("\n", validationErrors.Select(e => $"[red]• {e}[/]")))
				.Border(BoxBorder.Rounded)
				.BorderColor(Color.Red)
				.Header("[red]❌ Configuration Errors[/]");

			AnsiConsole.Write(errorPanel);
		}
	}

	private static SvnMigrationConfig? CollectMigrationConfiguration()
	{
		try
		{
			var svnPath = AnsiConsole.Ask<string>("[blue]Enter SVN repository path:[/]");

			var defaultGitPath = Path.Combine(Path.GetDirectoryName(svnPath) ?? ".",
				Path.GetFileName(svnPath) + "-git");

			var gitPath = AnsiConsole.Ask("[blue]Enter Git repository path:[/]", defaultGitPath);

			var useAuthorsFile = AnsiConsole.Confirm("Do you want to use an authors file for user mapping?");
			string? authorsFile = null;
			if (useAuthorsFile)
			{
				authorsFile = AnsiConsole.Ask<string>("[blue]Enter authors file path:[/]");
			}

			var preserveEmptyDirs = AnsiConsole.Confirm("Preserve empty directories?", true);

			// Advanced options
			var showAdvanced = AnsiConsole.Confirm("Configure advanced options?", false);
			var excludeTags = new Collection<string>();
			var excludeBranches = new Collection<string>();

			if (showAdvanced)
			{
				var excludeTagsInput = AnsiConsole.Ask("[blue]Tags to exclude (comma-separated, or press Enter for none):[/]", string.Empty);
				if (!string.IsNullOrWhiteSpace(excludeTagsInput))
				{
					excludeTags = excludeTagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Select(t => t.Trim()).ToCollection();
				}

				var excludeBranchesInput = AnsiConsole.Ask("[blue]Branches to exclude (comma-separated, or press Enter for none):[/]", string.Empty);
				if (!string.IsNullOrWhiteSpace(excludeBranchesInput))
				{
					excludeBranches = excludeBranchesInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Select(b => b.Trim()).ToCollection();
				}
			}

			return new SvnMigrationConfig
			{
				SvnRepositoryPath = svnPath,
				GitRepositoryPath = gitPath,
				AuthorsFile = authorsFile,
				PreserveEmptyDirectories = preserveEmptyDirs,
				ExcludeTags = excludeTags,
				ExcludeBranches = excludeBranches
			};
		}
		catch (OperationCanceledException)
		{
			AnsiConsole.MarkupLine("[red]Configuration input cancelled.[/]");
			return null;
		}
		catch (InvalidOperationException)
		{
			AnsiConsole.MarkupLine("[red]Invalid operation during configuration input.[/]");
			return null;
		}
		catch (ArgumentException)
		{
			AnsiConsole.MarkupLine("[red]Invalid argument provided during configuration.[/]");
			return null;
		}
	}

	private static void ShowHelp()
	{
		AnsiConsole.Write(new Rule("[cyan]Help & Information[/]").RuleStyle("cyan").LeftJustified());

		var helpPanel = new Panel(
			new Markup("""
			[bold]SVN to Git Migration Tool[/]

			This tool migrates SVN repositories to Git repositories while preserving commit history,
			branches, and tags using git-svn.

			[bold yellow]Prerequisites:[/]
			• Git must be installed with SVN support (git-svn)
			• Access to the SVN repository you want to migrate
			• Sufficient disk space for the migration

			[bold yellow]Features:[/]
			• Preserves complete commit history
			• Migrates branches and tags
			• Supports user mapping via authors file
			• Real-time progress tracking
			• Configuration validation

			[bold yellow]Authors File Format:[/]
			If you want to map SVN usernames to Git author information, create a text file with:
			[grey]svnuser = Full Name <email@example.com>[/]

			[bold yellow]Migration Process:[/]
			1. Validate configuration and prerequisites
			2. Clone SVN repository using git-svn
			3. Convert remote references to local branches
			4. Clean up and optimize the Git repository

			[bold red]Important Notes:[/]
			• Large repositories may take significant time to migrate
			• Ensure you have backups before starting migration
			• The tool requires git-svn to be available in your PATH
			"""))
			.Border(BoxBorder.Rounded)
			.BorderColor(Color.Cyan1)
			.Header("[cyan]📖 Documentation[/]");

		AnsiConsole.Write(helpPanel);
	}
}

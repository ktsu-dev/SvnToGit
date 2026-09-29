// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using ktsu.SvnToGit.Cli;
using ktsu.SvnToGit.Core;
using Spectre.Console;

/// <summary>
/// Tests that the CLI shows paths, URLs and git output containing square brackets verbatim, rather than
/// letting Spectre.Console read them as style tags and throw.
/// </summary>
[TestClass]
public class SvnToGitCliMarkupTests
{
	private const string BracketedPath = "/srv/svn/repo [old]";
	private const string Ipv6Url = "http://[::1]/svn/repo";
	private const string GitSvnError = "Cloning failed: fatal: bad config line 1 in [svn-remote \"svn\"]";

	[TestMethod]
	public void ErrorLine_ValidationErrorWithBracketedPath_IsShownVerbatim()
	{
		string error = $"Authors file does not exist: {BracketedPath}";

		Assert.AreEqual($"• {error}", Render(SvnToGitCli.ErrorLine(error)));
	}

	[TestMethod]
	public void ErrorLine_GitErrorWithBrackets_IsShownVerbatim() =>
		Assert.AreEqual($"• {GitSvnError}", Render(SvnToGitCli.ErrorLine(GitSvnError)));

	[TestMethod]
	public void ErrorLine_UnbalancedBracket_IsShownVerbatim() =>
		Assert.AreEqual("• /srv/svn/a[b", Render(SvnToGitCli.ErrorLine("/srv/svn/a[b")));

	[TestMethod]
	public void ConfirmMigrationPrompt_BracketedPathAndIpv6Url_AreShownVerbatim()
	{
		SvnMigrationConfig config = new()
		{
			SvnRepositoryPath = Ipv6Url,
			GitRepositoryPath = BracketedPath,
		};

		Assert.AreEqual($"Are you ready to migrate {Ipv6Url} to {BracketedPath}?", Render(SvnToGitCli.ConfirmMigrationPrompt(config)));
	}

	[TestMethod]
	public void ProgressDescription_ErrorStepWithBrackets_IsShownVerbatim()
	{
		MigrationProgress progress = new("Error", $"Git command failed: {GitSvnError}", 0, default, default)
		{
			Errors = [GitSvnError],
		};

		Assert.AreEqual($"Error: Git command failed: {GitSvnError}", Render(SvnToGitCli.ProgressDescription(progress)));
	}

	[TestMethod]
	public void SuccessLine_BracketedPath_IsShownVerbatim() =>
		Assert.AreEqual($"✅ Repository successfully migrated to: {BracketedPath}", Render(SvnToGitCli.SuccessLine(BracketedPath)));

	[TestMethod]
	public void ProgressDescription_StepWithoutErrors_IsShownVerbatim() =>
		Assert.AreEqual($"Cloning: {BracketedPath}", Render(SvnToGitCli.ProgressDescription(new MigrationProgress("Cloning", BracketedPath, 30, default, default))));

	[TestMethod]
	public void SuccessLine_NoPath_RendersWithoutAPath() =>
		Assert.AreEqual("✅ Repository successfully migrated to: ", Render(SvnToGitCli.SuccessLine(null)));

	[TestMethod]
	public void ReportResult_FailureWithBracketedGitError_PrintsEveryError()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		ProgressTask task = new(0, "Migrating", 100);
		MigrationResult result = new(false, null, null)
		{
			Errors = [GitSvnError, BracketedPath],
		};

		SvnToGitCli.ReportResult(console, task, result);

		string output = writer.ToString();
		Assert.Contains("Migration failed with the following errors:", output);
		Assert.Contains($"• {GitSvnError}", output);
		Assert.Contains($"• {BracketedPath}", output);
		Assert.AreEqual("[red]Migration failed[/]", task.Description);
	}

	[TestMethod]
	public void ReportResult_SuccessWithBracketedPath_PrintsThePath()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		ProgressTask task = new(0, "Migrating", 100);
		MigrationResult result = new(true, BracketedPath, "done");

		SvnToGitCli.ReportResult(console, task, result);

		Assert.Contains($"✅ Repository successfully migrated to: {BracketedPath}", writer.ToString());
		Assert.AreEqual("[green]Migration completed successfully![/]", task.Description);
	}

	[TestMethod]
	public async Task RunMigrationAsync_GitFailsWithBracketedError_ReportsTheError()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svntogit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		try
		{
			SvnMigrationConfig config = new()
			{
				SvnRepositoryPath = directory,
				GitRepositoryPath = Path.Combine(directory, "git"),
			};
			SvnToGitMigrator migrator = new(config, (_, arguments, _) => Task.FromResult(
				arguments.Contains("clone") ? new ProcessResult(128, string.Empty, GitSvnError) : new ProcessResult(0, string.Empty, string.Empty)));
			(IAnsiConsole console, StringWriter writer) = CreateConsole();

			await SvnToGitCli.RunMigrationAsync(console, migrator).ConfigureAwait(false);

			Assert.Contains($"• Cloning failed: {GitSvnError}", writer.ToString());
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public void ReportResult_SuccessWithWarnings_PrintsEachWarningVerbatim()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		ProgressTask task = new(0, "Migrating", 100);
		string warning = $"Finalization: git gc failed: {GitSvnError}";
		MigrationResult result = new(true, BracketedPath, "done")
		{
			Warnings = [warning],
		};

		SvnToGitCli.ReportResult(console, task, result);

		string output = writer.ToString();
		Assert.Contains($"✅ Repository successfully migrated to: {BracketedPath}", output);
		Assert.Contains($"⚠ {warning}", output);
	}

	[TestMethod]
	public void ReportResult_FailureWithWarnings_PrintsErrorsAndWarnings()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		ProgressTask task = new(0, "Migrating", 100);
		MigrationResult result = new(false, null, null)
		{
			Errors = ["Cloning failed"],
			Warnings = [BracketedPath],
		};

		SvnToGitCli.ReportResult(console, task, result);

		string output = writer.ToString();
		Assert.Contains("• Cloning failed", output);
		Assert.Contains($"⚠ {BracketedPath}", output);
	}

	[TestMethod]
	public void ReportResult_SuccessWithoutWarnings_PrintsNoWarningLine()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		ProgressTask task = new(0, "Migrating", 100);

		SvnToGitCli.ReportResult(console, task, new MigrationResult(true, BracketedPath, "done"));

		Assert.DoesNotContain("⚠", writer.ToString());
	}

	[TestMethod]
	public async Task RunMigrationAsync_GarbageCollectionFails_ReportsTheWarning()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svntogit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		try
		{
			SvnMigrationConfig config = new()
			{
				SvnRepositoryPath = directory,
				GitRepositoryPath = Path.Combine(directory, "git"),
			};
			SvnToGitMigrator migrator = new(config, (_, arguments, _) => Task.FromResult(
				arguments.Contains("gc") ? new ProcessResult(128, string.Empty, "fatal: Unable to create '.git/gc.pid.lock': File exists") : new ProcessResult(0, string.Empty, string.Empty)));
			(IAnsiConsole console, StringWriter writer) = CreateConsole();

			await SvnToGitCli.RunMigrationAsync(console, migrator).ConfigureAwait(false);

			string output = writer.ToString();
			Assert.Contains("Repository successfully migrated", output);
			Assert.Contains("git gc failed: fatal: Unable to create '.git/gc.pid.lock': File exists", output);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public void WriteErrors_ValidationErrorsWithBrackets_PrintsEachVerbatim()
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();

		SvnToGitCli.WriteErrors(console, [$"Authors file does not exist: {BracketedPath}", "/srv/svn/a[b"]);

		string output = writer.ToString();
		Assert.Contains($"• Authors file does not exist: {BracketedPath}", output);
		Assert.Contains("• /srv/svn/a[b", output);
	}

	/// <summary>
	/// Renders markup to plain text the way the CLI would show it, throwing as the CLI would on bad markup.
	/// </summary>
	private static string Render(string markup)
	{
		(IAnsiConsole console, StringWriter writer) = CreateConsole();
		using (writer)
		{
			console.Write(new Markup(markup));
			return writer.ToString();
		}
	}

	/// <summary>
	/// Creates a console that writes plain text, without colour or ANSI codes, to the returned writer.
	/// </summary>
	private static (IAnsiConsole Console, StringWriter Writer) CreateConsole()
	{
		StringWriter writer = new();
		IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
		{
			Ansi = AnsiSupport.No,
			ColorSystem = ColorSystemSupport.NoColors,
			Out = new AnsiConsoleOutput(writer),
		});
		console.Profile.Width = 500;
		return (console, writer);
	}
}

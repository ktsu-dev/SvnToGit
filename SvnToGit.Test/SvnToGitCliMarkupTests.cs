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

	/// <summary>
	/// Renders markup to plain text the way the CLI would show it, throwing as the CLI would on bad markup.
	/// </summary>
	private static string Render(string markup)
	{
		using StringWriter writer = new();
		IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
		{
			Ansi = AnsiSupport.No,
			ColorSystem = ColorSystemSupport.NoColors,
			Out = new AnsiConsoleOutput(writer),
		});
		console.Profile.Width = 500;

		console.Write(new Markup(markup));
		return writer.ToString();
	}
}

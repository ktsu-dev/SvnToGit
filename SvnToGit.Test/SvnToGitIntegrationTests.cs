// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using System.ComponentModel;
using ktsu.SvnToGit.Core;

/// <summary>
/// Runs <see cref="SvnToGitMigrator.MigrateAsync"/> end to end against a real SVN repository, using the
/// installed <c>svnadmin</c>, <c>svn</c> and <c>git svn</c>. Each test is inconclusive where they are missing.
/// </summary>
[TestClass]
public class SvnToGitIntegrationTests
{
	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public async Task MigrateAsync_LocalRepositoryGivenAsAPlainPath_MigratesTheTrunkHistory()
	{
		await AssertSvnToolsAvailableAsync().ConfigureAwait(false);

		string directory = CreateTempDirectory();

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await CreateSvnRepositoryAsync(directory, svnRepository).ConfigureAwait(false);

			string gitRepository = Path.Combine(directory, "git");
			SvnToGitMigrator migrator = new(new SvnMigrationConfig
			{
				SvnRepositoryPath = svnRepository,
				GitRepositoryPath = gitRepository,
			});

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			Assert.AreEqual("hello", (await File.ReadAllTextAsync(Path.Combine(gitRepository, "readme.txt"), TestContext.CancellationToken).ConfigureAwait(false)).Trim());

			string log = await RunAsync("git", "-C", gitRepository, "log", "--format=%s", "master").ConfigureAwait(false);
			Assert.Contains("Add readme", log);
			Assert.Contains("Create the standard layout", log);

			string branches = await RunAsync("git", "-C", gitRepository, "branch", "--list", "feature").ConfigureAwait(false);
			Assert.Contains("feature", branches);
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	/// <summary>
	/// Creates an SVN repository at <paramref name="svnRepository"/> with the standard layout, a
	/// <c>readme.txt</c> committed to trunk, and a <c>feature</c> branch copied from trunk.
	/// </summary>
	private static async Task CreateSvnRepositoryAsync(string directory, string svnRepository)
	{
		await RunAsync("svnadmin", "create", svnRepository).ConfigureAwait(false);
		string url = new Uri(Path.GetFullPath(svnRepository)).AbsoluteUri;

		await RunAsync("svn", "mkdir", "--quiet", "-m", "Create the standard layout", $"{url}/trunk", $"{url}/branches", $"{url}/tags").ConfigureAwait(false);

		string source = Path.Combine(directory, "source");
		Directory.CreateDirectory(source);
		await File.WriteAllTextAsync(Path.Combine(source, "readme.txt"), "hello").ConfigureAwait(false);
		await RunAsync("svn", "import", "--quiet", "-m", "Add readme", source, $"{url}/trunk").ConfigureAwait(false);

		await RunAsync("svn", "copy", "--quiet", "-m", "Create the feature branch", $"{url}/trunk", $"{url}/branches/feature").ConfigureAwait(false);
	}

	private static async Task AssertSvnToolsAvailableAsync()
	{
		await AssertToolAvailableAsync("svnadmin", "--version", "--quiet").ConfigureAwait(false);
		await AssertToolAvailableAsync("svn", "--version", "--quiet").ConfigureAwait(false);
		await AssertToolAvailableAsync("git", "svn", "--version").ConfigureAwait(false);
	}

	private static async Task AssertToolAvailableAsync(string fileName, params string[] arguments)
	{
		ProcessResult? result = null;
		try
		{
			result = await ProcessRunner.RunCommandAsync(fileName, arguments).ConfigureAwait(false);
		}
		catch (Win32Exception)
		{
			// The executable is not installed
		}

		if (result is not { ExitCode: 0 })
		{
			Assert.Inconclusive($"{fileName} {string.Join(' ', arguments)} is not available, so the SVN integration test cannot run.");
		}
	}

	private static async Task<string> RunAsync(string fileName, params string[] arguments)
	{
		ProcessResult result = await ProcessRunner.RunCommandAsync(fileName, arguments).ConfigureAwait(false);
		Assert.AreEqual(0, result.ExitCode, $"{fileName} {string.Join(' ', arguments)}: {result.StandardError}");
		return result.StandardOutput;
	}

	/// <summary>
	/// Deletes a directory that may hold git or SVN repositories, whose files are marked read-only.
	/// </summary>
	private static void DeleteDirectory(string directory)
	{
		foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
		{
			File.SetAttributes(file, FileAttributes.Normal);
		}

		Directory.Delete(directory, recursive: true);
	}

	private static string CreateTempDirectory()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svntogit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		return directory;
	}
}

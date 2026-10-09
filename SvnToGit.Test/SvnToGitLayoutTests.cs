// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using ktsu.SvnToGit.Core;

/// <summary>
/// Migrates real SVN repositories that <c>git svn clone --stdlayout</c> imports nothing from, to check the
/// migration reports a failure rather than an empty repository as success. Skipped when Subversion or
/// git-svn is not installed.
/// </summary>
[TestClass]
public class SvnToGitLayoutTests
{
	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public async Task MigrateAsync_FlatLayoutRepository_ReportsThatNoCommitsWereImported()
	{
		await SkipUnlessSvnIsAvailableAsync().ConfigureAwait(false);
		string directory = CreateTempDirectory();

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await RunAsync("svnadmin", "create", svnRepository).ConfigureAwait(false);
			string svnUrl = new Uri(svnRepository).AbsoluteUri;
			await ImportFileAsync(directory, $"{svnUrl}/a.txt").ConfigureAwait(false);

			MigrationResult result = await MigrateAsync(svnUrl, Path.Combine(directory, "git")).ConfigureAwait(false);

			AssertNoCommitsImportedFailure(result);
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_UrlPointsAtTrunk_ReportsThatNoCommitsWereImported()
	{
		await SkipUnlessSvnIsAvailableAsync().ConfigureAwait(false);
		string directory = CreateTempDirectory();

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await RunAsync("svnadmin", "create", svnRepository).ConfigureAwait(false);
			string svnUrl = new Uri(svnRepository).AbsoluteUri;
			await RunAsync("svn", "mkdir", "-q", "-m", "layout", $"{svnUrl}/trunk", $"{svnUrl}/branches", $"{svnUrl}/tags").ConfigureAwait(false);
			await ImportFileAsync(directory, $"{svnUrl}/trunk/a.txt").ConfigureAwait(false);

			MigrationResult result = await MigrateAsync($"{svnUrl}/trunk", Path.Combine(directory, "git")).ConfigureAwait(false);

			AssertNoCommitsImportedFailure(result);
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	private static void AssertNoCommitsImportedFailure(MigrationResult result)
	{
		Assert.IsFalse(result.Success);
		Assert.HasCount(1, result.Errors);
		Assert.AreEqual($"Cloning failed: {SvnToGitMigrator.NoCommitsImportedError}", result.Errors[0]);
	}

	private Task<MigrationResult> MigrateAsync(string svnUrl, string gitPath) =>
		new SvnToGitMigrator(new SvnMigrationConfig { SvnRepositoryPath = svnUrl, GitRepositoryPath = gitPath })
			.MigrateAsync(cancellationToken: TestContext.CancellationToken);

	private static async Task ImportFileAsync(string directory, string url)
	{
		string file = Path.Combine(directory, "a.txt");
		await File.WriteAllTextAsync(file, "hi").ConfigureAwait(false);
		await RunAsync("svn", "import", "-q", "-m", "add a", file, url).ConfigureAwait(false);
	}

	private static async Task SkipUnlessSvnIsAvailableAsync()
	{
		foreach ((string fileName, string[] arguments) in new[] { ("svnadmin", new[] { "--version" }), ("svn", ["--version"]), ("git", ["svn", "--version"]) })
		{
			bool available;
			try
			{
				available = (await ProcessRunner.RunCommandAsync(fileName, arguments).ConfigureAwait(false)).ExitCode == 0;
			}
			catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
			{
				available = false;
			}

			if (!available)
			{
				Assert.Inconclusive($"{fileName} {string.Join(' ', arguments)} is not available");
			}
		}
	}

	private static async Task RunAsync(string fileName, params string[] arguments)
	{
		ProcessResult result = await ProcessRunner.RunCommandAsync(fileName, arguments).ConfigureAwait(false);
		Assert.AreEqual(0, result.ExitCode, result.StandardError);
	}

	/// <summary>
	/// Deletes a directory that may hold a git repository, whose object files git marks read-only.
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

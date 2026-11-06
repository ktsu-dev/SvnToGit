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
	private static readonly string[] ExpectedBranchesWithGitSvn = ["master", "feature", "git-svn"];

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

	[TestMethod]
	[DoNotParallelize] // Points GIT_CONFIG_GLOBAL, which every git child process inherits, at a temp file
	public async Task MigrateAsync_TagGpgSignInGlobalConfig_CreatesLightweightTags()
	{
		await AssertSvnToolsAvailableAsync().ConfigureAwait(false);

		string directory = CreateTempDirectory();
		string? originalGlobalConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await CreateSvnRepositoryAsync(directory, svnRepository).ConfigureAwait(false);
			string url = new Uri(Path.GetFullPath(svnRepository)).AbsoluteUri;
			await RunAsync("svn", "copy", "--quiet", "-m", "Tag v1", $"{url}/trunk", $"{url}/tags/v1").ConfigureAwait(false);

			// tag.gpgSign makes a plain `git tag` annotated and signed. A non-interactive editor makes git
			// fail with "no tag message?" instead of waiting on a terminal, as it does for the CLI's users.
			string globalConfig = Path.Combine(directory, "gitconfig");
			await File.WriteAllTextAsync(globalConfig, "[user]\n\tname = Test\n\temail = test@example.com\n[tag]\n\tgpgSign = true\n[core]\n\teditor = true\n", TestContext.CancellationToken).ConfigureAwait(false);
			Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", globalConfig);

			string gitRepository = Path.Combine(directory, "git");
			SvnToGitMigrator migrator = new(new SvnMigrationConfig
			{
				SvnRepositoryPath = svnRepository,
				GitRepositoryPath = gitRepository,
			});

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			string tagType = await RunAsync("git", "-C", gitRepository, "cat-file", "-t", "v1").ConfigureAwait(false);
			Assert.AreEqual("commit", tagType.Trim(), "v1 should be a lightweight tag naming a commit, not a tag object");
		}
		finally
		{
			Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", originalGlobalConfig);
			DeleteDirectory(directory);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_SvnBranchNamedGitSvn_BecomesALocalBranch()
	{
		await AssertSvnToolsAvailableAsync().ConfigureAwait(false);

		string directory = CreateTempDirectory();

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await CreateSvnRepositoryAsync(directory, svnRepository).ConfigureAwait(false);
			string url = new Uri(Path.GetFullPath(svnRepository)).AbsoluteUri;
			await RunAsync("svn", "copy", "--quiet", "-m", "Create the git-svn branch", $"{url}/trunk", $"{url}/branches/git-svn").ConfigureAwait(false);

			string gitRepository = Path.Combine(directory, "git");
			SvnToGitMigrator migrator = new(new SvnMigrationConfig
			{
				SvnRepositoryPath = svnRepository,
				GitRepositoryPath = gitRepository,
			});

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			Assert.IsEmpty(result.Warnings, string.Join(Environment.NewLine, result.Warnings));
			string branches = await RunAsync("git", "-C", gitRepository, "for-each-ref", "--format=%(refname:short)", "refs/heads").ConfigureAwait(false);
			CollectionAssert.AreEquivalent(ExpectedBranchesWithGitSvn, branches.Split('\n', StringSplitOptions.RemoveEmptyEntries));
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_SvnBranchAndTagWithTheSameName_LeavesNoAmbiguousRef()
	{
		await AssertSvnToolsAvailableAsync().ConfigureAwait(false);

		string directory = CreateTempDirectory();

		try
		{
			string svnRepository = Path.Combine(directory, "svn");
			await CreateSvnRepositoryAsync(directory, svnRepository).ConfigureAwait(false);
			string url = new Uri(Path.GetFullPath(svnRepository)).AbsoluteUri;
			await RunAsync("svn", "copy", "--quiet", "-m", "Tag feature", $"{url}/branches/feature", $"{url}/tags/feature").ConfigureAwait(false);

			string gitRepository = Path.Combine(directory, "git");
			SvnToGitMigrator migrator = new(new SvnMigrationConfig
			{
				SvnRepositoryPath = svnRepository,
				GitRepositoryPath = gitRepository,
			});

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			Assert.Contains("Cleanup: SVN tag feature was migrated as tag svn-feature, because a branch named feature already exists", result.Warnings);
			string tags = await RunAsync("git", "-C", gitRepository, "for-each-ref", "--format=%(refname:short)", "refs/tags").ConfigureAwait(false);
			CollectionAssert.AreEquivalent(ExpectedRenamedTag, tags.Split('\n', StringSplitOptions.RemoveEmptyEntries));

			// With refs/heads/feature alone, the name resolves without git warning that it is ambiguous
			ProcessResult revParse = await ProcessRunner.RunCommandAsync("git", ["-C", gitRepository, "rev-parse", "feature"], TestContext.CancellationToken).ConfigureAwait(false);
			Assert.AreEqual(0, revParse.ExitCode, revParse.StandardError);
			Assert.DoesNotContain("ambiguous", revParse.StandardError);
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	private static readonly string[] ExpectedRenamedTag = ["svn-feature"];

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

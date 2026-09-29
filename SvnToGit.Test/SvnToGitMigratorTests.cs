// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using ktsu.SvnToGit.Core;

/// <summary>
/// Tests that <see cref="SvnToGitMigrator.MigrateAsync"/> reports the outcome of the git commands it runs,
/// using a stubbed command runner so no real git or SVN repository is needed.
/// </summary>
[TestClass]
public class SvnToGitMigratorTests
{
	private static readonly string[] ExpectedLocalBranches = ["master", "feature", "trunk-fixes", "fix-git-svn-import"];

	private static readonly string[] ExpectedTags = ["1.0", "2.0"];

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public async Task MigrateAsync_CloneFails_ReturnsFailureWithTheCloneError()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: args => args[0] == "svn" && args[1] == "clone"));

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsFalse(result.Success);
			Assert.IsNull(result.GitRepositoryPath);
			Assert.HasCount(1, result.Errors);
			Assert.Contains("Cloning", result.Errors[0]);
			Assert.Contains("authentication failed", result.Errors[0]);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_CloneFails_DoesNotRunLaterPhasesOrReportCompletion()
	{
		string directory = CreateTempDirectory();

		try
		{
			List<string> commands = [];
			List<MigrationProgress> reports = [];
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: args => args[0] == "svn" && args[1] == "clone", commands));

			await migrator.MigrateAsync(new SynchronousProgress(reports.Add), TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsFalse(commands.Any(c => c.Contains(" gc ", StringComparison.Ordinal)), string.Join(Environment.NewLine, commands));
			Assert.IsFalse(reports.Any(r => r.Phase == "Complete"));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_BranchCreationFails_ReturnsFailureNamingTheBranch()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: args => args.Contains("branch") && !args.Contains("-r")));

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsFalse(result.Success);
			Assert.HasCount(1, result.Errors);
			Assert.Contains("Cleanup", result.Errors[0]);
			Assert.Contains("feature", result.Errors[0]);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_GarbageCollectionFails_SucceedsWithAWarning()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: args => args.Contains("gc")));

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success);
			Assert.IsEmpty(result.Errors);
			Assert.HasCount(1, result.Warnings);
			Assert.Contains("git gc", result.Warnings[0]);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_AllCommandsSucceed_ReturnsSuccess()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: _ => false));

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success);
			Assert.IsEmpty(result.Errors);
			Assert.IsEmpty(result.Warnings);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_WithExclusions_PassesIgnoreRefsToTheClone()
	{
		string directory = CreateTempDirectory();

		try
		{
			List<string> commands = [];
			SvnMigrationConfig config = CreateConfig(directory) with
			{
				ExcludeBranches = ["experimental"],
				ExcludeTags = ["v1.0"],
			};
			SvnToGitMigrator migrator = new(config, StubRunner(failWhen: _ => false, commands));

			await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			string clone = commands.Single(c => c.StartsWith("git svn clone ", StringComparison.Ordinal));
			Assert.Contains(@"--ignore-refs=^refs/remotes/origin/(?:experimental|tags/v1\.0)$", clone);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_WithoutExclusions_DoesNotPassIgnoreRefs()
	{
		string directory = CreateTempDirectory();

		try
		{
			List<string> commands = [];
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: _ => false, commands));

			await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsFalse(commands.Any(c => c.Contains("--ignore-refs", StringComparison.Ordinal)), string.Join(Environment.NewLine, commands));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_WithExclusions_DoesNotCreateBranchesForExcludedRefs()
	{
		string directory = CreateTempDirectory();

		try
		{
			List<string> commands = [];
			SvnMigrationConfig config = CreateConfig(directory) with
			{
				ExcludeBranches = ["experimental"],
				ExcludeTags = ["v1.0"],
			};
			string remotes = "  origin/trunk\n  origin/feature\n  origin/experimental\n  origin/tags/v1.0\n  origin/tags/v2.0\n";
			SvnToGitMigrator migrator = new(config, StubRunner(failWhen: _ => false, commands, remotes));

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success);
			string[] created = [.. commands.Where(c => (c.Contains(" branch ", StringComparison.Ordinal) && !c.Contains(" branch -r ", StringComparison.Ordinal)) || c.Contains(" tag ", StringComparison.Ordinal))];
			string log = string.Join(Environment.NewLine, commands);
			Assert.HasCount(2, created, log);
			Assert.IsTrue(created.Any(c => c.Contains(" branch feature origin/feature ", StringComparison.Ordinal)), log);
			Assert.IsTrue(created.Any(c => c.Contains(" tag v2.0 origin/tags/v2.0 ", StringComparison.Ordinal)), log);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_CreatesLocalBranchesWithoutMovingHeadOffTrunk()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnMigrationConfig config = CreateConfig(directory);
			await CreateGitSvnCloneAsync(config.GitRepositoryPath).ConfigureAwait(false);
			SvnToGitMigrator migrator = new(config, RealGitRunnerWithStubbedSvn());

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			Assert.AreEqual("refs/heads/master", await GitAsync(config.GitRepositoryPath, "symbolic-ref", "HEAD").ConfigureAwait(false));
			string localBranches = await GitAsync(config.GitRepositoryPath, "branch", "--format=%(refname:short)").ConfigureAwait(false);
			CollectionAssert.AreEquivalent(ExpectedLocalBranches, localBranches.Split('\n'));
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	[TestMethod]
	public async Task MigrateAsync_TurnsSvnTagsIntoGitTagsRatherThanBranches()
	{
		string directory = CreateTempDirectory();

		try
		{
			SvnMigrationConfig config = CreateConfig(directory);
			await CreateGitSvnCloneAsync(config.GitRepositoryPath).ConfigureAwait(false);
			SvnToGitMigrator migrator = new(config, RealGitRunnerWithStubbedSvn());

			MigrationResult result = await migrator.MigrateAsync(cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
			string tags = await GitAsync(config.GitRepositoryPath, "tag", "--list").ConfigureAwait(false);
			CollectionAssert.AreEquivalent(ExpectedTags, tags.Split('\n'));

			// 1.0 was a plain copy, so its tag commit changes nothing and the tag names the copied trunk revision
			Assert.AreEqual(
				await GitAsync(config.GitRepositoryPath, "rev-parse", "master").ConfigureAwait(false),
				await GitAsync(config.GitRepositoryPath, "rev-parse", "1.0^{commit}").ConfigureAwait(false));

			// 2.0 changed a file in the tag, so the tag keeps that commit
			Assert.AreEqual(
				await GitAsync(config.GitRepositoryPath, "rev-parse", "refs/remotes/origin/tags/2.0").ConfigureAwait(false),
				await GitAsync(config.GitRepositoryPath, "rev-parse", "2.0^{commit}").ConfigureAwait(false));
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	[TestMethod]
	public void BuildIgnoreRefsRegex_EscapesRegexCharactersAndSkipsBlankNames()
	{
		string? regex = SvnToGitMigrator.BuildIgnoreRefsRegex(["release+1", " ", ""], ["1.0 (old)"]);

		Assert.AreEqual(@"^refs/remotes/origin/(?:release\+1|tags/1\.0\ \(old\))$", regex);
	}

	[TestMethod]
	public void BuildIgnoreRefsRegex_NothingExcluded_ReturnsNull() =>
		Assert.IsNull(SvnToGitMigrator.BuildIgnoreRefsRegex([" "], []));

	private static SvnMigrationConfig CreateConfig(string directory) => new()
	{
		SvnRepositoryPath = directory,
		GitRepositoryPath = Path.Combine(directory, "git"),
	};

	/// <summary>
	/// Returns a runner that succeeds for every git command except those matching <paramref name="failWhen"/>,
	/// and lists <paramref name="remoteBranches"/> so the cleanup phase has branches to create.
	/// </summary>
	private static Func<string, IEnumerable<string>, CancellationToken, Task<ProcessResult>> StubRunner(
		Func<string[], bool> failWhen,
		List<string>? commands = null,
		string remoteBranches = "  origin/trunk\n  origin/feature\n") =>
		(fileName, arguments, _) =>
		{
			string[] args = [.. arguments];
			commands?.Add($"{fileName} {string.Join(' ', args)} ");

			if (failWhen(args))
			{
				return Task.FromResult(new ProcessResult(128, string.Empty, "authentication failed"));
			}

			string output = args.Contains("branch") && args.Contains("-r") ? remoteBranches : string.Empty;
			return Task.FromResult(new ProcessResult(0, output, string.Empty));
		};

	/// <summary>
	/// Returns a runner that answers the git-svn commands itself, so no SVN server or git-svn install is
	/// needed, and runs every other git command for real.
	/// </summary>
	private static Func<string, IEnumerable<string>, CancellationToken, Task<ProcessResult>> RealGitRunnerWithStubbedSvn() =>
		(fileName, arguments, cancellationToken) =>
		{
			string[] args = [.. arguments];
			return args.Length > 0 && args[0] == "svn"
				? Task.FromResult(new ProcessResult(0, string.Empty, string.Empty))
				: ProcessRunner.RunCommandAsync(fileName, args, cancellationToken);
		};

	/// <summary>
	/// Builds the repository <c>git svn clone --stdlayout</c> leaves behind for an SVN repository with trunk,
	/// branches <c>feature</c>, <c>trunk-fixes</c> and <c>fix-git-svn-import</c>, and tags <c>1.0</c> and
	/// <c>2.0</c>: <c>master</c> checked out on trunk, and one remote ref each. As git-svn records them, each
	/// tag is a commit on top of trunk: <c>1.0</c> an empty copy, and <c>2.0</c> a copy with a change of its own.
	/// </summary>
	private static async Task CreateGitSvnCloneAsync(string path)
	{
		Directory.CreateDirectory(path);
		await GitAsync(path, "init", "--initial-branch=master").ConfigureAwait(false);
		await CommitAsync(path, "trunk").ConfigureAwait(false);
		foreach (string remote in new[] { "trunk", "feature", "trunk-fixes", "fix-git-svn-import" })
		{
			await GitAsync(path, "update-ref", $"refs/remotes/origin/{remote}", "HEAD").ConfigureAwait(false);
		}

		string trunk = await GitAsync(path, "rev-parse", "HEAD").ConfigureAwait(false);
		await GitAsync(path, "update-ref", "refs/remotes/origin/tags/1.0", await CommitTreeAsync(path, trunk, "Create tag 1.0").ConfigureAwait(false)).ConfigureAwait(false);

		await GitAsync(path, "checkout", "--detach", "--quiet").ConfigureAwait(false);
		await File.WriteAllTextAsync(Path.Combine(path, "version.txt"), "2.0").ConfigureAwait(false);
		await GitAsync(path, "add", "version.txt").ConfigureAwait(false);
		await CommitAsync(path, "Create tag 2.0").ConfigureAwait(false);
		await GitAsync(path, "update-ref", "refs/remotes/origin/tags/2.0", "HEAD").ConfigureAwait(false);
		await GitAsync(path, "checkout", "--quiet", "master").ConfigureAwait(false);
	}

	private static Task<string> CommitAsync(string path, string message) =>
		GitAsync(path, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit", "--allow-empty", "-m", message);

	/// <summary>
	/// Creates a commit on top of <paramref name="parent"/> with the same tree, without moving any branch
	/// </summary>
	private static Task<string> CommitTreeAsync(string path, string parent, string message) =>
		GitAsync(path, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit-tree", $"{parent}^{{tree}}", "-p", parent, "-m", message);

	private static async Task<string> GitAsync(string path, params string[] arguments)
	{
		ProcessResult result = await ProcessRunner.RunCommandAsync("git", ["-C", path, .. arguments]).ConfigureAwait(false);
		Assert.AreEqual(0, result.ExitCode, result.StandardError);
		return result.StandardOutput.Trim();
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

	/// <summary>
	/// Reports progress on the calling thread, unlike <see cref="Progress{T}"/>, so the reports are
	/// all present when the migration returns.
	/// </summary>
	private sealed class SynchronousProgress(Action<MigrationProgress> report) : IProgress<MigrationProgress>
	{
		public void Report(MigrationProgress value) => report(value);
	}
}

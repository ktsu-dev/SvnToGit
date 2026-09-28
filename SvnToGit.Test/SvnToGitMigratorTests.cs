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
			SvnToGitMigrator migrator = new(CreateConfig(directory), StubRunner(failWhen: args => args.Contains("checkout")));

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
			string[] checkouts = [.. commands.Where(c => c.Contains(" checkout -b ", StringComparison.Ordinal))];
			string log = string.Join(Environment.NewLine, commands);
			Assert.HasCount(2, checkouts, log);
			Assert.IsTrue(checkouts.Any(c => c.Contains(" origin/feature ", StringComparison.Ordinal)), log);
			Assert.IsTrue(checkouts.Any(c => c.Contains(" origin/tags/v2.0 ", StringComparison.Ordinal)), log);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
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

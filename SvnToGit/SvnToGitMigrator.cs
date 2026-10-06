// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Core;

using System.Text.RegularExpressions;

/// <summary>
/// Main class for migrating SVN repositories to Git
/// </summary>
public class SvnToGitMigrator
{
	private readonly SvnMigrationConfig _config;
	private readonly Func<string, IEnumerable<string>, CancellationToken, Task<ProcessResult>> _runCommand;

	/// <summary>
	/// Initializes a new instance of the <see cref="SvnToGitMigrator"/> class
	/// </summary>
	/// <param name="config">Migration configuration</param>
	public SvnToGitMigrator(SvnMigrationConfig config)
		: this(config, ProcessRunner.RunCommandAsync)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SvnToGitMigrator"/> class with the command runner it
	/// uses to invoke git, so tests can substitute one that reports a failure
	/// </summary>
	/// <param name="config">Migration configuration</param>
	/// <param name="runCommand">Runs an executable with the given arguments</param>
	internal SvnToGitMigrator(SvnMigrationConfig config, Func<string, IEnumerable<string>, CancellationToken, Task<ProcessResult>> runCommand)
	{
		_config = config;
		_runCommand = runCommand;
	}

	/// <summary>
	/// Validates the migration configuration
	/// </summary>
	/// <returns>List of validation errors, empty if valid</returns>
	public IReadOnlyList<string> ValidateConfiguration()
	{
		List<string> errors = [];

		if (string.IsNullOrWhiteSpace(_config.SvnRepositoryPath))
		{
			errors.Add("SVN repository path is required");
		}
		else if (ResolveSvnUrl(_config.SvnRepositoryPath) is null)
		{
			errors.Add($"SVN repository must be an http://, https://, svn://, svn+ssh:// or file:// URL, or an existing local directory: {_config.SvnRepositoryPath}");
		}

		if (string.IsNullOrWhiteSpace(_config.GitRepositoryPath))
		{
			errors.Add("Git repository path is required");
		}

		if (!string.IsNullOrWhiteSpace(_config.AuthorsFile) && !File.Exists(_config.AuthorsFile))
		{
			errors.Add($"Authors file does not exist: {_config.AuthorsFile}");
		}

		// Check if git-svn is available
		if (!IsGitSvnAvailable())
		{
			errors.Add("git-svn is not available. Please install Git with SVN support");
		}

		return errors.AsReadOnly();
	}

	/// <summary>
	/// Performs the SVN to Git migration
	/// </summary>
	/// <param name="progress">Progress callback</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Migration result</returns>
	public async Task<MigrationResult> MigrateAsync(
		IProgress<MigrationProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		IReadOnlyList<string> errors = ValidateConfiguration();
		if (errors.Count > 0)
		{
			return new MigrationResult(false, null, null)
			{
				Errors = [.. errors]
			};
		}

		try
		{
			// Phase 1: Initialization
			progress?.Report(new MigrationProgress("Initialization", "Preparing migration environment", 10, default, default));

			// Create output directory if it doesn't exist
			Directory.CreateDirectory(_config.GitRepositoryPath);

			// Phase 2: Clone SVN repository using git-svn
			progress?.Report(new MigrationProgress("Cloning", "Cloning SVN repository with git-svn", 30, default, default));

			GitCommandResult cloneResult = await CloneSvnRepositoryAsync(progress, cancellationToken).ConfigureAwait(false);
			if (!cloneResult.Success)
			{
				return Failed("Cloning", cloneResult.StandardError);
			}

			// Phase 3: Clean up git-svn references
			progress?.Report(new MigrationProgress("Cleanup", "Converting git-svn references to regular Git", 70, default, default));

			List<string> warnings = [];
			GitCommandResult cleanupResult = await CleanupGitSvnReferencesAsync(warnings, progress, cancellationToken).ConfigureAwait(false);
			if (!cleanupResult.Success)
			{
				return Failed("Cleanup", cleanupResult.StandardError);
			}

			// Phase 4: Finalization
			progress?.Report(new MigrationProgress("Finalization", "Finalizing repository", 90, default, default));

			// A failed git gc leaves a complete but unoptimized repository, so it is a warning rather than a failure
			GitCommandResult finalizeResult = await FinalizeRepositoryAsync(progress, cancellationToken).ConfigureAwait(false);
			if (!finalizeResult.Success)
			{
				warnings.Add($"Finalization: git gc failed: {finalizeResult.StandardError}");
			}

			progress?.Report(new MigrationProgress("Complete", "Migration completed successfully", 100, default, true));

			return new MigrationResult(true, _config.GitRepositoryPath, "SVN repository successfully migrated to Git with preserved history.")
			{
				Warnings = warnings.AsReadOnly()
			};
		}
		catch (OperationCanceledException)
		{
			return new MigrationResult(false, null, null)
			{
				Errors = ["Migration was cancelled"]
			};
		}
	}

	private static MigrationResult Failed(string phase, string error) =>
		new(false, null, null)
		{
			Errors = [$"{phase} failed: {error}"]
		};

	private bool IsGitSvnAvailable()
	{
		try
		{
			Task<ProcessResult> task = _runCommand("git", ["svn", "--version"], CancellationToken.None);
			task.Wait();
			return task.Result.ExitCode == 0;
		}
		catch (InvalidOperationException)
		{
			// Process failed to start
			return false;
		}
		catch (System.ComponentModel.Win32Exception)
		{
			// Git executable not found
			return false;
		}
		catch (FileNotFoundException)
		{
			// Git executable not found
			return false;
		}
		catch (AggregateException)
		{
			// Task.Wait() throws AggregateException
			return false;
		}
	}

	private async Task<GitCommandResult> CloneSvnRepositoryAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		List<string> gitSvnArgs =
		[
			"svn",
			"clone",
			ResolveSvnUrl(_config.SvnRepositoryPath) ?? _config.SvnRepositoryPath,
			_config.GitRepositoryPath,
			"--stdlayout"
		];

		if (!string.IsNullOrWhiteSpace(_config.AuthorsFile))
		{
			gitSvnArgs.Add($"--authors-file={_config.AuthorsFile}");
		}

		if (_config.PreserveEmptyDirectories)
		{
			gitSvnArgs.Add("--preserve-empty-dirs");
		}

		string? ignoreRefs = BuildIgnoreRefsRegex(_config.ExcludeBranches, _config.ExcludeTags);
		if (ignoreRefs is not null)
		{
			gitSvnArgs.Add($"--ignore-refs={ignoreRefs}");
		}

		return await RunGitCommandAsync(gitSvnArgs, progress, cancellationToken).ConfigureAwait(false);
	}

	private async Task<GitCommandResult> CleanupGitSvnReferencesAsync(List<string> warnings, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// git-svn has already made a local branch from trunk, and a rerun or an SVN branch named after it
		// would collide, so know which names are taken before creating any
		List<string> localBranchesArgs = ["-C", _config.GitRepositoryPath, "for-each-ref", "--format=%(refname:short)", "refs/heads"];
		GitCommandResult localBranchesResult = await RunGitCommandAsync(localBranchesArgs, progress, cancellationToken).ConfigureAwait(false);
		if (!localBranchesResult.Success)
		{
			return localBranchesResult;
		}

		HashSet<string> takenBranchNames = [.. localBranchesResult.StandardOutput
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.Trim())];

		// Convert remote branches to local branches
		List<string> branchesArgs = ["-C", _config.GitRepositoryPath, "branch", "-r"];
		GitCommandResult result = await RunGitCommandAsync(branchesArgs, progress, cancellationToken).ConfigureAwait(false);

		if (!result.Success)
		{
			return result;
		}

		// Parse remote refs. Trunk is already the checked-out branch, and git-svn is the single-ref
		// remote a non-stdlayout clone makes; both are matched exactly, so a branch whose name merely
		// contains "trunk" or "git-svn" is still migrated.
		List<string> remoteRefs = [.. result.StandardOutput
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.Trim())
			.Where(line => line.StartsWith(GitSvnRemotePrefix, StringComparison.Ordinal))
			.Where(line => line is not TrunkRemoteRef and not GitSvnRemoteRef)
			.Where(line => !IsExcludedRemoteRef(line))];

		foreach (string remoteRef in remoteRefs)
		{
			string name = remoteRef[GitSvnRemotePrefix.Length..];
			GitCommandResult refResult;
			if (name.StartsWith(TagsPrefix, StringComparison.Ordinal))
			{
				refResult = await CreateTagAsync(name[TagsPrefix.Length..], remoteRef, progress, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				string branchName = UnusedBranchName(name, takenBranchNames);
				if (branchName != name)
				{
					warnings.Add($"Cleanup: SVN branch {name} was migrated as {branchName}, because a branch named {name} already exists");
				}

				refResult = await CreateBranchAsync(branchName, remoteRef, progress, cancellationToken).ConfigureAwait(false);
				takenBranchNames.Add(branchName);
			}

			if (!refResult.Success)
			{
				return refResult;
			}
		}

		return result;
	}

	/// <summary>
	/// Returns <paramref name="name"/>, or when that is taken the first free <c>svn-</c>-prefixed variant of it
	/// </summary>
	internal static string UnusedBranchName(string name, ICollection<string> takenNames)
	{
		if (!takenNames.Contains(name))
		{
			return name;
		}

		string candidate = $"svn-{name}";
		for (int suffix = 2; takenNames.Contains(candidate); suffix++)
		{
			candidate = $"svn-{name}-{suffix}";
		}

		return candidate;
	}

	private async Task<GitCommandResult> CreateBranchAsync(string branchName, string remoteRef, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// git branch creates the ref without checking it out, so HEAD stays on the trunk branch git-svn set up
		List<string> createBranchArgs = ["-C", _config.GitRepositoryPath, "branch", branchName, remoteRef];
		GitCommandResult branchResult = await RunGitCommandAsync(createBranchArgs, progress, cancellationToken).ConfigureAwait(false);
		return branchResult.Success
			? branchResult
			: branchResult with { StandardError = $"could not create branch {branchName}: {branchResult.StandardError}" };
	}

	private async Task<GitCommandResult> CreateTagAsync(string tagName, string remoteRef, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// An SVN tag is a copy, which git-svn records as a commit on top of the copied revision. When
		// that commit changes nothing, tag the revision it copied, as the usual git-svn idiom does.
		string target = await IsEmptyCopyCommitAsync(remoteRef, cancellationToken).ConfigureAwait(false)
			? $"{remoteRef}^"
			: remoteRef;

		List<string> createTagArgs = ["-C", _config.GitRepositoryPath, "tag", tagName, target];
		GitCommandResult tagResult = await RunGitCommandAsync(createTagArgs, progress, cancellationToken).ConfigureAwait(false);
		return tagResult.Success
			? tagResult
			: tagResult with { StandardError = $"could not create tag {tagName}: {tagResult.StandardError}" };
	}

	/// <summary>
	/// Whether <paramref name="commitRef"/> has a parent with the same tree, meaning the commit only
	/// records the SVN copy and adds no change of its own
	/// </summary>
	private async Task<bool> IsEmptyCopyCommitAsync(string commitRef, CancellationToken cancellationToken)
	{
		// Asked directly rather than through RunGitCommandAsync: a commit with no parent makes this fail,
		// which is an answer, not an error to report
		ProcessResult trees = await _runCommand(
			"git",
			["-C", _config.GitRepositoryPath, "rev-parse", $"{commitRef}^{{tree}}", $"{commitRef}^^{{tree}}"],
			cancellationToken).ConfigureAwait(false);

		string[] lines = trees.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		return trees.ExitCode == 0 && lines.Length == 2 && lines[0].Trim() == lines[1].Trim();
	}

	private async Task<GitCommandResult> FinalizeRepositoryAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// Run git gc to clean up the repository
		List<string> gcArgs = ["-C", _config.GitRepositoryPath, "gc", "--aggressive"];
		return await RunGitCommandAsync(gcArgs, progress, cancellationToken).ConfigureAwait(false);
	}

	private async Task<GitCommandResult> RunGitCommandAsync(
		IEnumerable<string> arguments,
		IProgress<MigrationProgress>? progress,
		CancellationToken cancellationToken)
	{
		try
		{
			ProcessResult result = await _runCommand("git", arguments, cancellationToken).ConfigureAwait(false);

			if (result.ExitCode != 0)
			{
				progress?.Report(new MigrationProgress("Error", $"Git command failed: {result.StandardError}", 0, default, default)
				{
					Errors = [result.StandardError]
				});

				return new GitCommandResult
				{
					Success = false,
					StandardOutput = result.StandardOutput,
					StandardError = result.StandardError
				};
			}

			return new GitCommandResult
			{
				Success = true,
				StandardOutput = result.StandardOutput,
				StandardError = result.StandardError
			};
		}
		catch (InvalidOperationException ex)
		{
			progress?.Report(new MigrationProgress("Error", $"Failed to execute git command: {ex.Message}", 0, default, default)
			{
				Errors = [ex.Message]
			});

			return new GitCommandResult
			{
				Success = false,
				StandardOutput = string.Empty,
				StandardError = ex.Message
			};
		}
		catch (System.ComponentModel.Win32Exception ex)
		{
			progress?.Report(new MigrationProgress("Error", $"Git executable not found: {ex.Message}", 0, default, default)
			{
				Errors = [ex.Message]
			});

			return new GitCommandResult
			{
				Success = false,
				StandardOutput = string.Empty,
				StandardError = ex.Message
			};
		}
		catch (FileNotFoundException ex)
		{
			progress?.Report(new MigrationProgress("Error", $"Git executable not found: {ex.Message}", 0, default, default)
			{
				Errors = [ex.Message]
			});

			return new GitCommandResult
			{
				Success = false,
				StandardOutput = string.Empty,
				StandardError = ex.Message
			};
		}
	}

	/// <summary>
	/// Builds the Perl regular expression that <c>git svn clone --ignore-refs</c> matches against the
	/// remote refs it would create, so excluded branches and tags are never fetched
	/// </summary>
	/// <param name="excludeBranches">Names of SVN branches to leave out</param>
	/// <param name="excludeTags">Names of SVN tags to leave out</param>
	/// <returns>The regular expression, or <see langword="null"/> when nothing is excluded</returns>
	internal static string? BuildIgnoreRefsRegex(IEnumerable<string> excludeBranches, IEnumerable<string> excludeTags)
	{
		List<string> alternatives = [
			.. ExclusionNames(excludeBranches).Select(Regex.Escape),
			.. ExclusionNames(excludeTags).Select(tag => $"tags/{Regex.Escape(tag)}"),
		];

		return alternatives.Count == 0
			? null
			: $"^refs/remotes/{GitSvnRemotePrefix}(?:{string.Join("|", alternatives)})$";
	}

	/// <summary>
	/// Whether a remote ref listed by <c>git branch -r</c>, such as <c>origin/experimental</c> or
	/// <c>origin/tags/v1.0</c>, names a branch or tag the configuration excludes
	/// </summary>
	private bool IsExcludedRemoteRef(string remoteRef)
	{
		string name = remoteRef[GitSvnRemotePrefix.Length..];
		return name.StartsWith(TagsPrefix, StringComparison.Ordinal)
			? ExclusionNames(_config.ExcludeTags).Contains(name[TagsPrefix.Length..], StringComparer.Ordinal)
			: ExclusionNames(_config.ExcludeBranches).Contains(name, StringComparer.Ordinal);
	}

	private static IEnumerable<string> ExclusionNames(IEnumerable<string> names) =>
		names.Select(name => name.Trim()).Where(name => name.Length > 0);

	/// <summary>
	/// The prefix git-svn gives its remote refs by default since Git 2.0
	/// </summary>
	private const string GitSvnRemotePrefix = "origin/";

	private const string TagsPrefix = "tags/";

	private const string TrunkRemoteRef = GitSvnRemotePrefix + "trunk";

	private const string GitSvnRemoteRef = GitSvnRemotePrefix + "git-svn";

	/// <summary>
	/// Resolves the configured SVN repository to the URL that git-svn expects
	/// </summary>
	/// <param name="svnRepositoryPath">An SVN URL, or the path of a local SVN repository</param>
	/// <returns>
	/// The URL unchanged when it has a scheme git-svn can reach, a <c>file://</c> URL for an existing local
	/// directory, or <see langword="null"/> when it is neither
	/// </returns>
	internal static string? ResolveSvnUrl(string svnRepositoryPath)
	{
		// Require an explicit scheme, because a bare Unix path such as /srv/svn parses as an absolute file URI
		if (svnRepositoryPath.Contains("://", StringComparison.Ordinal)
			&& Uri.TryCreate(svnRepositoryPath, UriKind.Absolute, out Uri? uri))
		{
			return SupportedSvnSchemes.Contains(uri.Scheme) ? svnRepositoryPath : null;
		}

		return Directory.Exists(svnRepositoryPath)
			? new Uri(Path.GetFullPath(svnRepositoryPath)).AbsoluteUri
			: null;
	}

	private static readonly HashSet<string> SupportedSvnSchemes = new(StringComparer.OrdinalIgnoreCase)
	{
		"http", "https", "svn", "svn+ssh", "file",
	};

	private sealed record GitCommandResult
	{
		public required bool Success { get; init; }
		public required string StandardOutput { get; init; }
		public required string StandardError { get; init; }
	}
}

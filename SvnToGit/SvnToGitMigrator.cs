// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Core;

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
		else if (!Directory.Exists(_config.SvnRepositoryPath))
		{
			errors.Add($"SVN repository path does not exist: {_config.SvnRepositoryPath}");
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

			GitCommandResult cleanupResult = await CleanupGitSvnReferencesAsync(progress, cancellationToken).ConfigureAwait(false);
			if (!cleanupResult.Success)
			{
				return Failed("Cleanup", cleanupResult.StandardError);
			}

			// Phase 4: Finalization
			progress?.Report(new MigrationProgress("Finalization", "Finalizing repository", 90, default, default));

			// A failed git gc leaves a complete but unoptimized repository, so it is a warning rather than a failure
			GitCommandResult finalizeResult = await FinalizeRepositoryAsync(progress, cancellationToken).ConfigureAwait(false);
			List<string> warnings = [];
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
			_config.SvnRepositoryPath,
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

		return await RunGitCommandAsync(gitSvnArgs, progress, cancellationToken).ConfigureAwait(false);
	}

	private async Task<GitCommandResult> CleanupGitSvnReferencesAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// Convert remote branches to local branches
		List<string> branchesArgs = ["-C", _config.GitRepositoryPath, "branch", "-r"];
		GitCommandResult result = await RunGitCommandAsync(branchesArgs, progress, cancellationToken).ConfigureAwait(false);

		if (!result.Success)
		{
			return result;
		}

		// Parse remote branches and create local ones
		List<string> remoteBranches = [.. result.StandardOutput
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.Trim())
			.Where(line => line.IndexOf("git-svn", StringComparison.Ordinal) < 0 && line.IndexOf("trunk", StringComparison.Ordinal) < 0 && line.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))];

		foreach (string remoteBranch in remoteBranches)
		{
			string branchName = remoteBranch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
				? remoteBranch[7..]
				: remoteBranch;
			List<string> createBranchArgs = ["-C", _config.GitRepositoryPath, "checkout", "-b", branchName, remoteBranch];
			GitCommandResult branchResult = await RunGitCommandAsync(createBranchArgs, progress, cancellationToken).ConfigureAwait(false);
			if (!branchResult.Success)
			{
				return branchResult with { StandardError = $"could not create branch {branchName}: {branchResult.StandardError}" };
			}
		}

		return result;
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

	private sealed record GitCommandResult
	{
		public required bool Success { get; init; }
		public required string StandardOutput { get; init; }
		public required string StandardError { get; init; }
	}
}

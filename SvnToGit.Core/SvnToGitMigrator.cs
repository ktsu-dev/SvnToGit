// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

/// <summary>
/// Main class for migrating SVN repositories to Git
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SvnToGitMigrator"/> class
/// </remarks>
/// <param name="config">Migration configuration</param>
public class SvnToGitMigrator(SvnMigrationConfig config)
{
	private readonly SvnMigrationConfig _config = config ?? throw new ArgumentNullException(nameof(config));

	/// <summary>
	/// Validates the migration configuration
	/// </summary>
	/// <returns>List of validation errors, empty if valid</returns>
	public IReadOnlyList<string> ValidateConfiguration()
	{
		var errors = new List<string>();

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
		var errors = ValidateConfiguration();
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

			await CloneSvnRepositoryAsync(progress, cancellationToken).ConfigureAwait(false);

			// Phase 3: Clean up git-svn references
			progress?.Report(new MigrationProgress("Cleanup", "Converting git-svn references to regular Git", 70, default, default));

			await CleanupGitSvnReferencesAsync(progress, cancellationToken).ConfigureAwait(false);

			// Phase 4: Finalization
			progress?.Report(new MigrationProgress("Finalization", "Finalizing repository", 90, default, default));

			await FinalizeRepositoryAsync(progress, cancellationToken).ConfigureAwait(false);

			progress?.Report(new MigrationProgress("Complete", "Migration completed successfully", 100, default, true));

			return new MigrationResult(true, _config.GitRepositoryPath, "SVN repository successfully migrated to Git with preserved history.");
		}
		catch (OperationCanceledException)
		{
			return new MigrationResult(false, null, null)
			{
				Errors = ["Migration was cancelled"]
			};
		}
	}

	private static bool IsGitSvnAvailable()
	{
		try
		{
			var result = ProcessRunner.RunCommandAsync("git", ["svn", "--version"]);
			return result.ExitCode == 0;
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
	}

	private async Task CloneSvnRepositoryAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		var gitSvnArgs = new List<string>
		{
			"svn",
			"clone",
			_config.SvnRepositoryPath,
			_config.GitRepositoryPath,
			"--stdlayout"
		};

		if (!string.IsNullOrWhiteSpace(_config.AuthorsFile))
		{
			gitSvnArgs.Add($"--authors-file={_config.AuthorsFile}");
		}

		if (_config.PreserveEmptyDirectories)
		{
			gitSvnArgs.Add("--preserve-empty-dirs");
		}

		await RunGitCommandAsync(gitSvnArgs, progress, cancellationToken).ConfigureAwait(false);
	}

	private async Task CleanupGitSvnReferencesAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// Convert remote branches to local branches
		var branchesArgs = new List<string> { "-C", _config.GitRepositoryPath, "branch", "-r" };
		var result = await RunGitCommandAsync(branchesArgs, progress, cancellationToken).ConfigureAwait(false);

		// Parse remote branches and create local ones
		if (result.Success)
		{
			var remoteBranches = result.StandardOutput
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(line => line.Trim())
				.Where(line => !line.Contains("git-svn") && !line.Contains("trunk") && line.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var remoteBranch in remoteBranches)
			{
				var branchName = remoteBranch.Replace("origin/", "", StringComparison.OrdinalIgnoreCase);
				var createBranchArgs = new List<string> { "-C", _config.GitRepositoryPath, "checkout", "-b", branchName, remoteBranch };
				await RunGitCommandAsync(createBranchArgs, progress, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task FinalizeRepositoryAsync(IProgress<MigrationProgress>? progress, CancellationToken cancellationToken)
	{
		// Run git gc to clean up the repository
		var gcArgs = new List<string> { "-C", _config.GitRepositoryPath, "gc", "--aggressive" };
		await RunGitCommandAsync(gcArgs, progress, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<GitCommandResult> RunGitCommandAsync(
		IEnumerable<string> arguments,
		IProgress<MigrationProgress>? progress,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = await ProcessRunner.RunCommandAsync("git", arguments, cancellationToken).ConfigureAwait(false);

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

	private record GitCommandResult
	{
		public required bool Success { get; init; }
		public required string StandardOutput { get; init; }
		public required string StandardError { get; init; }
	}
}

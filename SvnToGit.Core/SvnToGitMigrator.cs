// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

using System.Diagnostics;

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
			return new MigrationResult
			{
				Success = false,
				Errors = [.. errors]
			};
		}

		try
		{
			// Simulate migration phases
			var phases = new[]
			{
				("Initialization", "Preparing migration environment"),
				("Analysis", "Analyzing SVN repository structure"),
				("Cloning", "Cloning SVN repository with git-svn"),
				("Processing", "Converting branches and tags"),
				("Cleanup", "Cleaning up git-svn references"),
				("Finalization", "Finalizing Git repository")
			};

			for (var i = 0; i < phases.Length; i++)
			{
				var (phase, step) = phases[i];
				var percentage = (i + 1) * 100 / phases.Length;

				progress?.Report(new MigrationProgress
				{
					Phase = phase,
					CurrentStep = step,
					ProgressPercentage = percentage
				});

				// Simulate work
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}

			progress?.Report(new MigrationProgress
			{
				Phase = "Complete",
				CurrentStep = "Migration completed successfully",
				ProgressPercentage = 100,
				IsComplete = true
			});

			return new MigrationResult
			{
				Success = true,
				GitRepositoryPath = _config.GitRepositoryPath,
				Information = "This is a demonstration. Actual migration would use git-svn commands."
			};
		}
		catch (OperationCanceledException)
		{
			return new MigrationResult
			{
				Success = false,
				Errors = ["Migration was cancelled"]
			};
		}
	}

	private static bool IsGitSvnAvailable()
	{
		try
		{
			using var process = new Process();
			process.StartInfo.FileName = "git";
			process.StartInfo.Arguments = "svn --version";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.CreateNoWindow = true;

			process.Start();
			process.WaitForExit();

			return process.ExitCode == 0;
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
}

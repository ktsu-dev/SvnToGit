// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Core;

using System.Text;
using ktsu.RunCommand;

/// <summary>
/// Wrapper for running external processes using ktsu.RunCommand
/// </summary>
public static class ProcessRunner
{
	/// <summary>
	/// Runs a command asynchronously
	/// </summary>
	/// <param name="fileName">The executable to run</param>
	/// <param name="arguments">Command line arguments</param>
	/// <returns>Process result</returns>
	public static Task<ProcessResult> RunCommandAsync(string fileName, IEnumerable<string> arguments) =>
		RunCommandAsync(fileName, arguments, CancellationToken.None);

	/// <summary>
	/// Runs a command asynchronously
	/// </summary>
	/// <param name="fileName">The executable to run</param>
	/// <param name="arguments">Command line arguments</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Process result</returns>
	public static async Task<ProcessResult> RunCommandAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(arguments);

		StringBuilder stdoutBuilder = new();
		StringBuilder stderrBuilder = new();

		// Create output handler to capture stdout and stderr
		OutputHandler outputHandler = new(
			onStandardOutput: data => stdoutBuilder.Append(data),
			onStandardError: data => stderrBuilder.Append(data));

		// Execute the command, passing the executable and its arguments separately so that a path
		// containing spaces is never split, and no manual quoting is required
		int exitCode = await RunCommand.ExecuteAsync(fileName, arguments, outputHandler, cancellationToken).ConfigureAwait(false);

		// Check for cancellation
		cancellationToken.ThrowIfCancellationRequested();

		return new ProcessResult(exitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
	}
}

/// <summary>
/// Result of running a process
/// </summary>
/// <param name="ExitCode">Exit code of the process</param>
/// <param name="StandardOutput">Standard output from the process</param>
/// <param name="StandardError">Standard error from the process</param>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

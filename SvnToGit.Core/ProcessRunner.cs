// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;
using ktsu.RunCommand;

/// <summary>
/// Wrapper for running external processes using ktsu.RunCommand
/// </summary>
public static class ProcessRunner
{
	/// <summary>
	/// Runs a command synchronously
	/// </summary>
	/// <param name="fileName">The executable to run</param>
	/// <param name="arguments">Command line arguments</param>
	/// <returns>Process result</returns>
	public static async Task<ProcessResult> RunCommandAsync(string fileName, IEnumerable<string> arguments)
	{
		var output = "";
		var error = "";

		var command = fileName + " " + string.Join(" ", arguments);
		var task = RunCommand.ExecuteAsync(fileName, new OutputHandler(onStandardOutput: s => output += s, onStandardError: s => error += s)).ConfigureAwait(false);
		await task;
		return new ProcessResult
		{
			ExitCode = task.,
			StandardOutput = output,
			StandardError = error,
		};
	}

	/// <summary>
	/// Runs a command asynchronously
	/// </summary>
	/// <param name="fileName">The executable to run</param>
	/// <param name="arguments">Command line arguments</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Process result</returns>
	public static async Task<ProcessResult> RunCommandAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken = default)
	{
		var result = await ktsu.RunCommand.Run.CommandAsync(fileName, arguments.ToArray(), cancellationToken).ConfigureAwait(false);
		return new ProcessResult
		{
			ExitCode = result.ExitCode,
			StandardOutput = result.StandardOutput,
			StandardError = result.StandardError
		};
	}
}

/// <summary>
/// Result of running a process
/// </summary>
public record ProcessResult
{
	/// <summary>
	/// Exit code of the process
	/// </summary>
	public required int ExitCode { get; init; }

	/// <summary>
	/// Standard output from the process
	/// </summary>
	public required string StandardOutput { get; init; }

	/// <summary>
	/// Standard error from the process
	/// </summary>
	public required string StandardError { get; init; }
}

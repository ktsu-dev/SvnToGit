// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using System.Runtime.InteropServices;
using ktsu.SvnToGit.Core;

/// <summary>
/// Tests that <see cref="ProcessRunner"/> hands the executable and its arguments to the process
/// as separate values, so neither a path nor an argument is split on its spaces.
/// </summary>
[TestClass]
public class ProcessRunnerTests
{
	[TestMethod]
	public async Task RunCommandAsync_ExecutablePathContainsSpaces_RunsTheExecutable()
	{
		// Arrange: git and svn are commonly installed under a path containing spaces,
		// such as C:\Program Files\Git\cmd\git.exe
		string directory = CreateTempDirectoryWithSpaces();

		try
		{
			string executable = WriteEchoScript(directory);

			// Act
			ProcessResult result = await ProcessRunner.RunCommandAsync(executable, ["hello"]).ConfigureAwait(false);

			// Assert
			Assert.AreEqual(0, result.ExitCode, result.StandardError);
			StringAssert.Contains(result.StandardOutput, "hello");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task RunCommandAsync_ArgumentContainsSpaces_PassesItAsOneArgument()
	{
		// Arrange
		string directory = CreateTempDirectoryWithSpaces();

		try
		{
			string executable = WriteEchoScript(directory);

			// Act
			ProcessResult result = await ProcessRunner.RunCommandAsync(executable, ["two words"]).ConfigureAwait(false);

			// Assert
			Assert.AreEqual(0, result.ExitCode, result.StandardError);
			StringAssert.Contains(result.StandardOutput, "two words");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static string CreateTempDirectoryWithSpaces()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svn to git {Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		return directory;
	}

	/// <summary>
	/// Writes a script that echoes its first argument, and returns its full path.
	/// </summary>
	private static string WriteEchoScript(string directory)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			string batch = Path.Combine(directory, "echo first.cmd");
			File.WriteAllText(batch, "@echo off\r\necho %~1\r\n");
			return batch;
		}

		string script = Path.Combine(directory, "echo first.sh");
		File.WriteAllText(script, "#!/bin/sh\nprintf '%s\\n' \"$1\"\n");
		File.SetUnixFileMode(
			script,
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
			UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
			UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
		return script;
	}
}

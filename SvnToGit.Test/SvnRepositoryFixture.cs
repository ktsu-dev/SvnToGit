// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

/// <summary>
/// Temporary directories that stand in for a local SVN repository in tests that never run git-svn
/// </summary>
internal static class SvnRepositoryFixture
{
	/// <summary>
	/// Creates an empty temporary directory
	/// </summary>
	public static string CreateTempDirectory()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svntogit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		return directory;
	}

	/// <summary>
	/// Creates a temporary directory holding the markers <c>svnadmin create</c> leaves, a <c>format</c> file and a
	/// <c>db</c> directory, which are what git-svn's <c>ra_local</c> layer opens and what validation looks for
	/// </summary>
	public static string CreateRepositoryDirectory()
	{
		string directory = CreateTempDirectory();
		File.WriteAllText(Path.Combine(directory, "format"), "5\n");
		Directory.CreateDirectory(Path.Combine(directory, "db"));
		return directory;
	}
}

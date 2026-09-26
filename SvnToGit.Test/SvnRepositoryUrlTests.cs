// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.SvnToGit.Test;

using ktsu.SvnToGit.Cli;
using ktsu.SvnToGit.Core;

/// <summary>
/// Tests that an SVN repository can be given as a URL on a server, as git-svn expects, or as a local directory.
/// </summary>
[TestClass]
public class SvnRepositoryUrlTests
{
	[TestMethod]
	[DataRow("https://svn.example.com/repos/project")]
	[DataRow("http://svn.example.com/repos/project")]
	[DataRow("svn://svn.example.com/project")]
	[DataRow("svn+ssh://user@svn.example.com/project")]
	[DataRow("file:///srv/svn/project")]
	public void ValidateConfiguration_SvnUrl_ReportsNoRepositoryError(string repository)
	{
		SvnToGitMigrator migrator = new(new SvnMigrationConfig
		{
			SvnRepositoryPath = repository,
			GitRepositoryPath = Path.Combine(Path.GetTempPath(), "project-git"),
		});

		IReadOnlyList<string> errors = migrator.ValidateConfiguration();

		Assert.IsFalse(errors.Any(e => e.StartsWith("SVN repository", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
	}

	[TestMethod]
	[DataRow("ftp://svn.example.com/project")]
	[DataRow("not-a-directory-that-exists")]
	public void ValidateConfiguration_NeitherUrlNorDirectory_ReportsRepositoryError(string path)
	{
		SvnToGitMigrator migrator = new(new SvnMigrationConfig
		{
			SvnRepositoryPath = path,
			GitRepositoryPath = Path.Combine(Path.GetTempPath(), "project-git"),
		});

		IReadOnlyList<string> errors = migrator.ValidateConfiguration();

		Assert.IsTrue(errors.Any(e => e.StartsWith("SVN repository", StringComparison.Ordinal) && e.Contains(path, StringComparison.Ordinal)));
	}

	[TestMethod]
	public void ResolveSvnUrl_SvnUrl_IsPassedThroughUnchanged()
	{
		Assert.AreEqual("svn://svn.example.com/project", SvnToGitMigrator.ResolveSvnUrl("svn://svn.example.com/project"));
	}

	[TestMethod]
	public void ResolveSvnUrl_ExistingLocalDirectory_BecomesFileUrl()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"svn repo {Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		try
		{
			string? url = SvnToGitMigrator.ResolveSvnUrl(directory);

			Assert.IsNotNull(url);
			Assert.StartsWith("file:///", url);
			Assert.AreEqual(Path.GetFullPath(directory), new Uri(url).LocalPath);
		}
		finally
		{
			Directory.Delete(directory);
		}
	}

	[TestMethod]
	public void DefaultGitPath_ServerUrl_IsNamedAfterTheLastSegment()
	{
		Assert.AreEqual(Path.Combine(".", "some-project-git"), SvnToGitCli.DefaultGitPath("https://svn.apache.org/repos/asf/some-project/"));
	}

	[TestMethod]
	public void DefaultGitPath_LocalPath_IsBesideTheRepository()
	{
		string repository = Path.Combine(Path.GetTempPath(), "repos", "project");

		Assert.AreEqual(Path.Combine(Path.GetTempPath(), "repos", "project-git"), SvnToGitCli.DefaultGitPath(repository));
	}
}

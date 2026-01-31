// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ktsu.SvnToGit.Test;

using ktsu.SvnToGit.Core;

[TestClass]
public class SvnMigrationConfigTests
{
	[TestMethod]
	public void SvnMigrationConfig_CanBeCreated()
	{
		// Arrange & Act
		SvnMigrationConfig config = new()
		{
			SvnRepositoryPath = "/path/to/svn",
			GitRepositoryPath = "/path/to/git"
		};

		// Assert
		Assert.AreEqual("/path/to/svn", config.SvnRepositoryPath);
		Assert.AreEqual("/path/to/git", config.GitRepositoryPath);
	}

	[TestMethod]
	public void SvnMigrationConfig_PreserveEmptyDirectories_DefaultsToTrue()
	{
		// Arrange & Act
		SvnMigrationConfig config = new()
		{
			SvnRepositoryPath = "/path/to/svn",
			GitRepositoryPath = "/path/to/git"
		};

		// Assert
		Assert.IsTrue(config.PreserveEmptyDirectories);
	}
}

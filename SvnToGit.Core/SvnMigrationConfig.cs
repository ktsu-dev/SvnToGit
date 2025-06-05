// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Configuration for SVN repository migration
/// </summary>
public record SvnMigrationConfig
{
	/// <summary>
	/// Path to the SVN repository
	/// </summary>
	public required string SvnRepositoryPath { get; init; }

	/// <summary>
	/// Path where the Git repository will be created
	/// </summary>
	public required string GitRepositoryPath { get; init; }

	/// <summary>
	/// Authors file path for mapping SVN users to Git users
	/// </summary>
	public string? AuthorsFile { get; init; }

	/// <summary>
	/// Whether to preserve empty directories
	/// </summary>
	public bool PreserveEmptyDirectories { get; init; } = true;

	/// <summary>
	/// Tags to exclude from migration
	/// </summary>
	public Collection<string> ExcludeTags { get; init; } = [];

	/// <summary>
	/// Branches to exclude from migration
	/// </summary>
	public Collection<string> ExcludeBranches { get; init; } = [];
}


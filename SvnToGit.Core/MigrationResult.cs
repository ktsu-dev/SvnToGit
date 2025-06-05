// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

/// <summary>
/// Result of SVN to Git migration
/// </summary>
public record MigrationResult
{
	/// <summary>
	/// Whether the migration succeeded
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Path to the created Git repository (if successful)
	/// </summary>
	public string? GitRepositoryPath { get; init; }

	/// <summary>
	/// List of errors that occurred during migration
	/// </summary>
	public IReadOnlyList<string> Errors { get; init; } = [];

	/// <summary>
	/// Additional information about the migration
	/// </summary>
	public string? Information { get; init; }
}


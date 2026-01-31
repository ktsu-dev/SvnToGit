// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

/// <summary>
/// Result of SVN to Git migration
/// </summary>
/// <param name="Success"> Whether the migration succeeded </param>
/// <param name="GitRepositoryPath"> Path to the created Git repository (if successful) </param>
/// <param name="Information"> Additional information about the migration </param>
public record MigrationResult(bool Success, string? GitRepositoryPath, string? Information)
{
	/// <summary>
	/// Gets the list of errors encountered during migration
	/// </summary>
	public IReadOnlyList<string> Errors { get; init; } = [];
}

// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Progress information for SVN to Git migration
/// </summary>
public record MigrationProgress
{
	/// <summary>
	/// Current phase of migration
	/// </summary>
	public required string Phase { get; init; }

	/// <summary>
	/// Current step being processed
	/// </summary>
	public required string CurrentStep { get; init; }

	/// <summary>
	/// Progress percentage (0-100)
	/// </summary>
	public int ProgressPercentage { get; init; }

	/// <summary>
	/// Total number of commits processed
	/// </summary>
	public int CommitsProcessed { get; init; }

	/// <summary>
	/// Whether the migration is complete
	/// </summary>
	public bool IsComplete { get; init; }

	/// <summary>
	/// Any errors that occurred
	/// </summary>
	public Collection<string> Errors { get; init; } = [];
}


// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SvnToGit.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Progress information for SVN to Git migration
/// </summary>
/// <param name="Phase"> Current phase of migration </param>
/// <param name="CurrentStep"> Current step being processed </param>
/// <param name="ProgressPercentage"> Progress percentage (0-100) </param>
/// <param name="CommitsProcessed"> Total number of commits processed </param>
/// <param name="IsComplete"> Whether the migration is complete </param>
/// <param name="Errors"> </param>
public record MigrationProgress(string Phase, string CurrentStep, int ProgressPercentage, int CommitsProcessed, bool IsComplete, IReadOnlyList<string> Errors) { }


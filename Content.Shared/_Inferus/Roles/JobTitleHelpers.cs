using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Inferus.Roles;

/// <summary>
/// Resolves the display title for a job, including player-selected alternates.
/// </summary>
public static class JobTitleHelpers
{
    /// <summary>
    /// All selectable title LocIds for a job: primary <see cref="JobPrototype.Name"/> first, then alternates.
    /// </summary>
    public static IReadOnlyList<LocId> GetAllTitleOptions(JobPrototype job)
    {
        if (job.AlternateTitles is not { Count: > 0 } alts)
            return new List<LocId> { job.Name };

        var list = new List<LocId>(1 + alts.Count) { job.Name };
        foreach (var alt in alts)
        {
            if (alt != job.Name && !list.Contains(alt))
                list.Add(alt);
        }

        return list;
    }

    /// <summary>
    /// Localized display title for <paramref name="jobId"/> using the profile's selection, if any.
    /// Falls back to the job's default localized name.
    /// </summary>
    public static string GetDisplayTitle(
        ProtoId<JobPrototype> jobId,
        HumanoidCharacterProfile? profile,
        IPrototypeManager prototypes)
    {
        if (!prototypes.TryIndex(jobId, out var job))
            return jobId.ToString();

        return GetDisplayTitle(job, profile);
    }

    public static string GetDisplayTitle(JobPrototype job, HumanoidCharacterProfile? profile)
    {
        if (profile?.JobTitles is { } titles &&
            titles.TryGetValue(job.ID, out var selected) &&
            !string.IsNullOrEmpty(selected))
        {
            // Only accept known options (default or listed alternate)
            var options = GetAllTitleOptions(job);
            if (options.Any(opt => (string)opt == selected))
                return Loc.GetString(selected);
        }

        return job.LocalizedName;
    }

    /// <summary>
    /// Whether this job has any alternate titles configured.
    /// </summary>
    public static bool HasAlternates(JobPrototype job) =>
        job.AlternateTitles is { Count: > 0 };
}

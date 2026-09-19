using System;

namespace Notes.Interface.DTO;

/// <summary>
/// The optional timeframe a note is due in, as stored on <see cref="NoteData.DueFrom"/> and
/// <see cref="NoteData.DueTo"/>.
///
/// Both ends are optional: a timeframe with only one end is open on the other side, and with no
/// timeframe at all the note is never due. The client shows its "due now" alarm while the current time
/// is inside the timeframe.
/// </summary>
public static class DueTimeframe
{
    public const string InfoFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Whether the note has any timeframe at all.</summary>
    public static bool Has(DateTimeOffset? from, DateTimeOffset? to) => from.HasValue || to.HasValue;

    /// <summary>True while <paramref name="at"/> is inside the timeframe.</summary>
    public static bool IsDue(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset at)
    {
        if (!Has(from, to))
            return false;

        if (from is { } start && at < start)
            return false;
        if (to is { } end && at > end)
            return false;
        return true;
    }

    /// <summary>The timeframe for the info menu, e.g. "2026-09-20 14:00 - 15:30" or "from 14:00" when
    /// only one end is set.</summary>
    public static string Describe(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (!Has(from, to))
            return "—";
        if (from is { } start && to is { } end)
            return $"{start.ToString(InfoFormat)} - {end.ToString(InfoFormat)}";
        if (from is { } onlyStart)
            return $"from {onlyStart.ToString(InfoFormat)}";
        return $"until {to!.Value.ToString(InfoFormat)}";
    }
}

using System;
using System.Web;

namespace Notes.Interface.DTO;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NoteData Data { get; set; } = new NoteData();
    public List<Note> SubNotes { get; set; } = new();

    /// <summary>Creates a brand new note and stamps its creation time (so the Created field is
    /// set immediately and travels with the note's Add change).</summary>
    public static Note EmptyNote()
    {
        var note = new Note();
        note.Data.Created = DateTimeOffset.Now;
        return note;
    }

    public List<NotePosition> RecursiveSubNotes(bool SkipChildrenOfClosedNotes = false, int depth = 0, Note? parent = null)
    {
        List<NotePosition> result = [];
        result.Add(new NotePosition(depth, this, parent));
        if (SkipChildrenOfClosedNotes && !this.Data.Expanded)
            return result;
        foreach (var note in this.SubNotes)
        {
            result.AddRange(note.RecursiveSubNotes(SkipChildrenOfClosedNotes, depth + 1, this));
        }
        return result;
    }

    public void DeleteFrom(Note? Parent)
    {
        if (Parent != null)
        {
            Parent.SubNotes.Remove(this);
            if (Parent.SubNotes.Count == 0)
                Parent.Data.Expanded = false;
        }
    }

    /// <summary>
    /// Serializes this subtree to the plain-text format (indentation + ▼/▶ + optional strike).
    /// When <paramref name="derefResolver"/> is provided, symlink notes show the content (and, when
    /// the link is expanded, the children) of their resolved target instead of their own empty
    /// data. Without a resolver the behavior is unchanged (links serialize as their own data).
    /// </summary>
    public string SubtreeToStyledString(Func<Guid, Note?>? derefResolver = null)
    {
        var lines = new List<string>();
        var path = new HashSet<Guid>();
        AppendStyledLines(this, 0, derefResolver, lines, path);
        return lines.Count == 0 ? "" : string.Join("\n", lines);
    }

    static void AppendStyledLines(Note node, int depth, Func<Guid, Note?>? derefResolver, List<string> lines, HashSet<Guid> path)
    {
        if (!path.Add(node.Id))
            return; // cycle guard (should not happen for a plain tree)
        try
        {
            string text;
            bool strike;
            bool expanded;
            List<Note> children;

            if (node.Data.LinkTargetId is Guid targetId && derefResolver != null)
            {
                var target = ResolveStyledTarget(targetId, derefResolver, path);
                if (target != null && !path.Contains(target.Id))
                {
                    text = target.Data.DecodedText ?? "";
                    strike = target.Data.Done || target.Data.Canceled;
                    expanded = node.Data.Expanded;               // per-link expansion
                    children = expanded ? target.SubNotes : []; // children come from the target
                }
                else
                {
                    text = "";
                    strike = false;
                    expanded = false;
                    children = [];
                }
            }
            else
            {
                text = node.Data.DecodedText ?? "";
                strike = node.Data.Done || node.Data.Canceled;
                expanded = node.Data.Expanded;
                children = expanded ? node.SubNotes : [];
            }

            var noteText = strike ? Strike(text) : text;
            lines.Add(Indent(depth) + (expanded ? "▼" : "▶") + noteText);

            foreach (var child in children)
                AppendStyledLines(child, depth + 1, derefResolver, lines, path);
        }
        finally
        {
            path.Remove(node.Id);
        }
    }

    static Note? ResolveStyledTarget(Guid targetId, Func<Guid, Note?> derefResolver, HashSet<Guid> path)
    {
        var seen = new HashSet<Guid>();
        Guid current = targetId;
        while (seen.Add(current))
        {
            if (path.Contains(current))
                return null; // would inline an ancestor - treat as unresolvable here
            var target = derefResolver(current);
            if (target == null)
                return null;
            if (target.Data.LinkTargetId is Guid next)
            {
                current = next;
                continue;
            }
            return target;
        }
        return null; // link-to-link cycle
    }

    static string Indent(int depth) => depth <= 0 ? "" : string.Concat(Enumerable.Repeat("  ", depth));

    static string Strike(string text) =>
        text.Select(c => c + "" + StrikeMarker).Aggregate((a, b) => a + b);

    const char StrikeMarker = (char)822;

    /// <summary>
    /// Parses text produced by <see cref="SubtreeToStyledString"/> back into a note tree
    /// (expanded ▼ / collapsed ▶ markers, done/canceled strikethrough, indentation). Returns
    /// false and an error description when the input does not match the format.
    /// </summary>
    public static bool TryParseStyledSubtree(string? styledText, out Note root, out string? error)
    {
        root = new Note();
        error = null;
        if (string.IsNullOrWhiteSpace(styledText))
        {
            error = "The text is empty.";
            return false;
        }

        var lines = styledText.Replace("\r", "").Split('\n');
        var stack = new Stack<Note>();   // ancestors awaiting children, indexed by relative depth
        Note? rootNote = null;
        int? baseDepth = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.Length == 0)
                continue; // tolerate blank lines (clipboard artifacts)

            int depth = 0;
            while (line.StartsWith("  "))
            {
                depth++;
                line = line[2..];
            }
            baseDepth ??= depth;
            int relativeDepth = depth - baseDepth.Value;
            if (relativeDepth < 0)
            {
                error = $"Line {i + 1}: indented less than the first note.";
                return false;
            }
            if (line.Length == 0 || (line[0] != '▼' && line[0] != '▶'))
            {
                error = $"Line {i + 1}: expected an expand (▼) or collapse (▶) marker.";
                return false;
            }

            var note = EmptyNote(); // stamps Created
            note.Data.Expanded = line[0] == '▼';

            var noteText = line[1..];
            // Done/canceled notes strike every character with a combining marker.
            bool struck = noteText.Length >= 2 && noteText.Length % 2 == 0;
            for (int j = 1; struck && j < noteText.Length; j += 2)
                struck = noteText[j] == StrikeMarker;
            if (struck)
            {
                var sb = new System.Text.StringBuilder(noteText.Length / 2);
                for (int j = 0; j < noteText.Length; j += 2)
                    sb.Append(noteText[j]);
                noteText = sb.ToString();
                note.Data.Done = true;
            }
            note.Data.DecodedText = noteText;

            while (stack.Count > relativeDepth)
                stack.Pop();
            if (stack.Count == 0)
            {
                if (rootNote != null)
                {
                    error = $"Line {i + 1}: only a single top-level note is supported.";
                    return false;
                }
                rootNote = note;
            }
            else
            {
                stack.Peek().SubNotes.Add(note);
            }
            stack.Push(note);
        }

        if (rootNote == null)
        {
            error = "No notes found in the text.";
            return false;
        }
        root = rootNote;
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is Note n && n.Id == this.Id;
    }
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Notes.Interface;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;
using NotesAvalonia.Helper;
using NotesAvalonia.Views;

namespace NotesAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public Note VirtualRoot { get; private set; } = new();
    public Note? FocusedNote { get; private set; } = null;

    public MainViewModel()
    {

    }

    public void LoadNew(List<Note> notes)
    {
        // Remove symlinks whose target no longer exists or would create a cycle.
        PruneBrokenLinks(notes);
        BackfillMissingCreatedDates(notes);
        VirtualRoot = new Note()
        {
            Data = { Expanded = true },
            SubNotes = notes
        };
        ReFlatten();
    }

    // One-time migration for notes created before the Created field existed: stamp them with the
    // local "now" and queue an update so the value is persisted and synced. Runs once per note -
    // after this the field is set and stays stable.
    static void BackfillMissingCreatedDates(List<Note> notes)
    {
        foreach (var note in notes)
            BackfillMissingCreatedDates(note);
    }

    static void BackfillMissingCreatedDates(Note note)
    {
        if (note.Data.Created == null)
        {
            note.Data.Created = DateTimeOffset.Now;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = note.Id,
                    Data = note.Data
                });
        }

        foreach (var subNote in note.SubNotes)
            BackfillMissingCreatedDates(subNote);
    }

    public void ReFlatten()
    {
        List<FlattenedNote> flattenedNotes;
        if (FocusedNote == null)
        {
            flattenedNotes = FlattenForDisplay(VirtualRoot, 0, null);
            if (flattenedNotes.Count > 0)
                flattenedNotes.RemoveAt(0); // Skip the virtual root
        }
        else
        {
            flattenedNotes = FlattenForDisplay(FocusedNote, 1, null);
        }

        FlattenedNoteVMs.Clear();
        foreach (var fn in flattenedNotes)
            FlattenedNoteVMs.Add(new FlattenedNoteViewModel(fn));
    }

    /// <summary>Searches the whole payload tree for a note by id (used to resolve symlinks).</summary>
    public Note? FindNote(Guid id) => FindNote(VirtualRoot, id);

    static Note? FindNote(Note node, Guid id)
    {
        if (node.Id == id)
            return node;
        foreach (var subNote in node.SubNotes)
        {
            var found = FindNote(subNote, id);
            if (found != null)
                return found;
        }
        return null;
    }

    // Follows a symlink (through possible link-to-link chains) to a real note. Returns null when
    // the chain is broken/cyclic or the target is an ancestor of the currently flattened path
    // (which would inline itself forever).
    Note? ResolveLinkTarget(Guid targetId, HashSet<Guid> flattenedPath)
    {
        var seen = new HashSet<Guid>();
        var current = targetId;
        while (seen.Add(current))
        {
            if (flattenedPath.Contains(current))
                return null; // would expand into itself
            var target = FindNote(current);
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

    // Link-aware pre-order flatten. For symlink rows the entry is the link note itself (structure),
    // DereferencedNote points at the resolved target (content), and when the link is expanded the
    // TARGET's children are shown - but with the link's target as their flattened Parent, so edits
    // through them hit the canonical note.
    List<FlattenedNote> FlattenForDisplay(Note note, uint depth, FlattenedNote? parent)
    {
        var result = new List<FlattenedNote>();
        var path = new HashSet<Guid>();
        FlattenForDisplay(note, depth, parent, result, path);
        return result;
    }

    void FlattenForDisplay(Note note, uint depth, FlattenedNote? parent, List<FlattenedNote> result, HashSet<Guid> path)
    {
        if (!path.Add(note.Id))
            return; // defensive: never expand a note twice on one path
        try
        {
            var entry = new FlattenedNote(note) { Depth = depth, Parent = parent };
            result.Add(entry);

            if (note.Data.LinkTargetId is Guid targetId)
            {
                var target = ResolveLinkTarget(targetId, path);
                if (target == null)
                    return; // broken/cyclic link: show the row, nothing below it

                entry.DereferencedNote = target;
                if (!note.Data.Expanded)
                    return;

                // Children of an expanded link belong to the target (canonical). Their flattened
                // parent is a hidden entry for the target so structural operations resolve to the
                // canonical parent, not to the link (the link's own SubNotes stay empty).
                var targetContext = new FlattenedNote(target) { Depth = depth, Parent = entry };
                foreach (var child in target.SubNotes)
                    FlattenForDisplay(child, depth + 1, targetContext, result, path);
            }
            else if (note.Data.Expanded)
            {
                foreach (var child in note.SubNotes)
                    FlattenForDisplay(child, depth + 1, entry, result, path);
            }
        }
        finally
        {
            path.Remove(note.Id);
        }
    }

    // Removes symlink notes that cannot resolve (missing target, link-to-link cycle or link to an
    // ancestor) so they never linger in the UI. Queues deletes so the server learns about it too.
    static void PruneBrokenLinks(List<Note> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        var index = new Dictionary<Guid, Note>();
        void IndexTree(Note n)
        {
            index[n.Id] = n;
            foreach (var c in n.SubNotes)
                IndexTree(c);
        }
        foreach (var n in notes)
            IndexTree(n);

        bool LinkResolvable(Note link, HashSet<Guid> ancestors)
        {
            var seen = new HashSet<Guid>();
            Guid current = link.Data.LinkTargetId!.Value;
            while (seen.Add(current))
            {
                if (ancestors.Contains(current))
                    return false;                    // target is the link itself or an ancestor
                if (!index.TryGetValue(current, out var target))
                    return false;                    // missing target
                if (target.Data.LinkTargetId is Guid next)
                {
                    current = next;
                    continue;
                }
                return true;
            }
            return false;                            // link-to-link cycle
        }

        void PruneNode(Note node, HashSet<Guid> ancestors)
        {
            ancestors.Add(node.Id);
            for (int i = node.SubNotes.Count - 1; i >= 0; i--)
            {
                var child = node.SubNotes[i];
                if (child.Data.LinkTargetId != null && !LinkResolvable(child, ancestors))
                {
                    node.SubNotes.RemoveAt(i);
                    if (mainView != null)
                        Config.Data.AddNoteChange(new NoteChange()
                        {
                            Type = NoteChangeType.Delete,
                            NoteId = child.Id,
                        });
                }
                else
                {
                    PruneNode(child, ancestors);
                }
            }
            ancestors.Remove(node.Id);
        }

        foreach (var n in notes)
            PruneNode(n, new HashSet<Guid>());
    }

    // Login flyout bindings
    public string? LoginServerUri
    {
        get
        {
            return Config.Data.ServerUri;
        }
        set { Config.Data.ServerUri = value; SetProperty(ref Config.Data.ServerUri, value); }
    }
    public string? LoginServerUsername
    {
        get { return Config.Data.Username; }
        set { Config.Data.Username = value; SetProperty(ref Config.Data.Username, value); }
    }
    [ObservableProperty]
    private string _loginPassword = "";

    public ObservableCollection<FlattenedNoteViewModel> FlattenedNoteVMs { get; } = new();

    [ObservableProperty]
    private string _connectionState = "Disconnected";
    [ObservableProperty]
    private string _debugText = "";
    public void AddDebugText(string text)
    {
        if (Globals.RunConfig == "Release")
            return;
        DebugText = DebugText
            .Split('\n')
            .Append(text)
            .TakeLast(Globals.IsDesktop ? 4 : 32)
            .Aggregate((a, b) => a + "\n" + b);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private string? _newItemContent;
    private bool CanAddItem() => !string.IsNullOrWhiteSpace(NewItemContent);
    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        // Notes.Add(new NoteViewModel() { Text = NewItemContent });
        // NewItemContent = null;
    }

    [RelayCommand]
    public void RemoveItem(FlattenedNoteViewModel toDeleteFlattenedNote)
    {
        var ogToDeleteFlattenedNote = toDeleteFlattenedNote.FlattenedNote.OriginalNote;
        var ogToDeleteFlattenedNoteParent = toDeleteFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        DeleteNote(ogToDeleteFlattenedNote, ogToDeleteFlattenedNoteParent);
    }

    // --- Deletion with symlink bookkeeping ---

    /// <summary>Deletes <paramref name="toDelete"/> from <paramref name="parent"/> and keeps the
    /// tree consistent: every symlink anywhere in the payload that (transitively) points into the
    /// deleted subtree is removed too, and delete changes are queued for all of it.</summary>
    public void DeleteNote(Note toDelete, Note? parent)
    {
        var doomed = new HashSet<Guid>();
        CollectNoteIds(toDelete, doomed);

        toDelete.DeleteFrom(parent);
        if (mainView != null)
            Config.Data.AddNoteChange(new NoteChange()
            {
                Type = NoteChangeType.Delete,
                NoteId = toDelete.Id
            });

        CleanupDanglingLinks(doomed);
        ReFlatten();
    }

    /// <summary>Removes symlinks whose (transitive) target lies in <paramref name="doomed"/> and
    /// queues delete changes for them. Call after removing notes yourself.</summary>
    public void CleanupDanglingLinks(HashSet<Guid> doomed)
    {
        if (VirtualRoot == null || doomed.Count == 0)
            return;
        RemoveDanglingLinks(VirtualRoot, doomed);
    }

    static void CollectNoteIds(Note note, HashSet<Guid> ids)
    {
        ids.Add(note.Id);
        foreach (var subNote in note.SubNotes)
            CollectNoteIds(subNote, ids);
    }

    void RemoveDanglingLinks(Note node, HashSet<Guid> doomed)
    {
        for (int i = node.SubNotes.Count - 1; i >= 0; i--)
        {
            var child = node.SubNotes[i];
            if (IsDanglingLink(child, doomed))
            {
                node.SubNotes.RemoveAt(i);
                if (mainView != null)
                    Config.Data.AddNoteChange(new NoteChange()
                    {
                        Type = NoteChangeType.Delete,
                        NoteId = child.Id
                    });
            }
            else if (!doomed.Contains(child.Id))
            {
                // The deleted subtree is already gone from the tree, no need to walk into it.
                RemoveDanglingLinks(child, doomed);
            }
        }
    }

    bool IsDanglingLink(Note link, HashSet<Guid> doomed)
    {
        if (link.Data.LinkTargetId is not Guid targetId)
            return false;
        var seen = new HashSet<Guid>();
        Guid current = targetId;
        while (seen.Add(current))
        {
            if (doomed.Contains(current))
                return true;                    // points (transitively) into the deleted subtree
            var target = FindNote(current);
            if (target == null)
                return true;                    // already broken - prune eagerly
            if (target.Data.LinkTargetId is Guid next)
            {
                current = next;
                continue;
            }
            return false;
        }
        return true;                            // link-to-link cycle - dangling
    }

    [RelayCommand]
    public void ExportItemToClipboard(FlattenedNoteViewModel flattenedNoteVM)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);
        if (topLevel != null)
        {
            // Symlink-aware export: links are shown with their target's content/children.
            var exportText = flattenedNoteVM.FlattenedNote.OriginalNote.SubtreeToStyledString(id => FindNote(id));
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(DataFormat.Text, exportText));
            topLevel.Clipboard?.SetDataAsync(dataTransfer);
        }
    }

    [RelayCommand]
    public void FocusNote(FlattenedNoteViewModel flattenedNoteVM)
    {
        if (FocusedNote == flattenedNoteVM.FlattenedNote.OriginalNote)
            FocusedNote = null;
        else
            FocusedNote = flattenedNoteVM.FlattenedNote.OriginalNote;

        ReFlatten();
    }

    [RelayCommand]
    public void ToggleNoteHidden(FlattenedNoteViewModel flattenedNoteVM)
    {
        flattenedNoteVM.Hidden = !flattenedNoteVM.Hidden;
    }

    [RelayCommand]
    public void ToggleNoteCanceled(FlattenedNoteViewModel flattenedNoteVM)
    {
        flattenedNoteVM.ToggleCanceled();
    }

    [RelayCommand]
    public void ToggleNoteSubtreeHidden(FlattenedNoteViewModel flattenedNoteVM)
    {
        var recursiveSubnotesResult = flattenedNoteVM.FlattenedNote.OriginalNote.RecursiveSubNotes();
        foreach (var snResult in recursiveSubnotesResult)
        {
            snResult.Note.Data.Hidden = !snResult.Note.Data.Hidden;
        }

        // Notify UI
        var subtreeFlattenedNotesVM = FlattenedNoteVMs.Where(x => recursiveSubnotesResult.Any(y => x.FlattenedNote.OriginalNote.Id == y.Note.Id));
        foreach (var fnvm in subtreeFlattenedNotesVM)
        {
            fnvm.Hidden = fnvm.FlattenedNote.OriginalNote.Data.Hidden;
        }
    }

    [RelayCommand]
    public void RemoveDoneSubnotes(FlattenedNoteViewModel flattenedNoteVM)
    {
        // Symlink-aware walk: "the subtree of this note" includes what links point at, so the
        // walk descends through links into their targets. Finished real notes are removed from
        // their canonical parent; a finished LINK is removed from its structural parent (the
        // canonical target itself stays - links never delete their targets).
        var row = flattenedNoteVM.FlattenedNote.OriginalNote;
        var visited = new HashSet<Guid> { row.Id };
        var deletions = new List<(Note Note, Note Owner)>();

        void CollectFrom(List<Note> ownerList, Note owner)
        {
            for (int i = 0; i < ownerList.Count; i++)
            {
                var rowNote = ownerList[i];
                if (!visited.Add(rowNote.Id))
                    continue;

                var content = DerefNote(rowNote);
                bool finished = (content ?? rowNote).Data.Done || (content ?? rowNote).Data.Canceled;
                if (finished)
                {
                    deletions.Add((rowNote, owner));
                    continue; // do not descend into finished content
                }

                // Children shown below a link belong to the target; real children to the node.
                bool isLink = rowNote.Data.LinkTargetId != null && content != null;
                if (isLink)
                    CollectFrom(content!.SubNotes, content);
                else
                    CollectFrom(rowNote.SubNotes, rowNote);
            }
        }

        var rootContent = DerefNote(row) ?? row;
        if (row.Data.LinkTargetId != null && rootContent != row)
            CollectFrom(rootContent.SubNotes, rootContent);
        else
            CollectFrom(row.SubNotes, row);

        var doomed = new HashSet<Guid>();
        foreach (var (victim, owner) in deletions)
        {
            if (owner.SubNotes.Remove(victim))
            {
                CollectNoteIds(victim, doomed);
                if (mainView != null)
                    Config.Data.AddNoteChange(new NoteChange()
                    {
                        Type = NoteChangeType.Delete,
                        NoteId = victim.Id
                    });
            }
        }

        // Remove symlinks that pointed into the deleted subtrees.
        CleanupDanglingLinks(doomed);
        ReFlatten();
    }

    // Resolves a note's symlink chain to a real note (the note itself when it is not a link).
    // Returns null for missing targets / link-to-link cycles.
    Note? DerefNote(Note node)
    {
        if (node.Data.LinkTargetId is not Guid targetId)
            return node;
        var seen = new HashSet<Guid>();
        Guid current = targetId;
        while (seen.Add(current))
        {
            var target = FindNote(current);
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

    [RelayCommand]
    public void AddChildNote(FlattenedNoteViewModel item)
    {
        // Children of a symlink are added to the canonical target note.
        var effective = item.EffectiveNote;

        // Remember where the action happened: after ReFlatten the same canonical subtree may be
        // rendered in several rows at once (symlink mirrors), and focus must land on the copy in
        // this instance rather than on another copy of the new note.
        int anchorIndex = FlattenedNoteVMs.IndexOf(item);
        var anchorChain = FlattenedChainIds(item.FlattenedNote);

        if (!item.Expanded)
        {
            // Expanding a note/link that has no children already creates its first editable child
            // and focuses it (see ToggleExpand) - in that case there is nothing more to add.
            if (effective.SubNotes.Count == 0)
            {
                ToggleExpand(item);
                return;
            }

            // Collapsed but has children: expand first so the new child becomes visible below them.
            item.Expanded = true;
        }

        // Append a new empty note as the last child.
        var newNote = Note.EmptyNote();
        var insertionIndex = effective.SubNotes.Count;
        effective.SubNotes.Add(newNote);
        if (mainView != null)
            Config.Data.AddNoteChange(new NoteChange()
            {
                Type = NoteChangeType.Add,
                NoteId = newNote.Id,
                Data = newNote.Data,
                ParentId = effective.Id,
                ChildInsertionIndex = insertionIndex,
            });

        ReFlatten();
        FocusFlattenedRow(newNote, anchorChain, anchorIndex);
    }

    /// <summary>Inserts a parsed subtree (see Note.TryParseStyledSubtree) as a sibling right after
    /// <paramref name="item"/>, queues the Add changes in pre-order and focuses the new root.</summary>
    public void AddSubtreeFromString(FlattenedNoteViewModel item, Note parsedRoot)
    {
        var ogNote = item.FlattenedNote.OriginalNote;
        var ogParent = item.FlattenedNote.Parent?.OriginalNote;
        if (ogParent == null)
            return; // the focused root note has no parent to insert after

        var insertionIndex = ogParent.SubNotes.IndexOf(ogNote) + 1;
        ogParent.SubNotes.Insert(insertionIndex, parsedRoot);

        if (mainView != null)
            EnqueueSubtreeAdds(parsedRoot, ogParent, insertionIndex);

        ReFlatten();

        // Focus the new subtree's root TextBox
        Dispatcher.UIThread.Post(() =>
        {
            var newTextbox = mainView?.GetLogicalDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(ic => (ic.DataContext as FlattenedNoteViewModel)?.FlattenedNote.OriginalNote == parsedRoot);
            if (newTextbox != null)
                newTextbox.Focusable = true;
            newTextbox?.Focus();
        });
    }

    // Pre-order so each note's Add change comes after its parent's (parents must exist first).
    static void EnqueueSubtreeAdds(Note note, Note parent, int childIndex)
    {
        Config.Data.AddNoteChange(new NoteChange()
        {
            Type = NoteChangeType.Add,
            NoteId = note.Id,
            Data = note.Data,
            ParentId = parent.Id,
            ChildInsertionIndex = childIndex,
        });

        for (int i = 0; i < note.SubNotes.Count; i++)
            EnqueueSubtreeAdds(note.SubNotes[i], note, i);
    }

    // Focuses the TextBox of the row displaying `note` that belongs to the SAME flattened instance
    // as the row the user acted on (anchorChain = ids of that row's flattened ancestor chain,
    // captured before a ReFlatten). Mirrored copies under symlinks have a different chain, so this
    // picks the right one even when the duplicate blocks sit next to each other. Falls back to the
    // copy nearest to anchorIndex, then to the first row of the note.
    void FocusFlattenedRow(Note note, List<Guid> anchorChain, int anchorIndex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextBox? newTextbox = null;
            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < FlattenedNoteVMs.Count; i++)
            {
                var vm = FlattenedNoteVMs[i];
                if (vm.FlattenedNote.OriginalNote != note)
                    continue;

                if (anchorChain != null && FlattenedChainIds(vm.FlattenedNote.Parent).SequenceEqual(anchorChain))
                {
                    bestIndex = i;
                    break; // exact instance match
                }
                if (anchorIndex >= 0)
                {
                    int distance = Math.Abs(i - anchorIndex);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }
            }
            if (bestIndex >= 0)
            {
                var preferredVm = FlattenedNoteVMs[bestIndex];
                newTextbox = mainView?.GetLogicalDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(x => ReferenceEquals(x.DataContext, preferredVm));
            }
            newTextbox ??= mainView?.GetLogicalDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(ic => (ic.DataContext as FlattenedNoteViewModel)?.FlattenedNote.OriginalNote == note);
            if (newTextbox != null)
            {
                newTextbox.Focusable = true;
                newTextbox.Focus();
            }
        });
    }

    // Ids of a flattened entry's ancestor chain (from the entry up to the root). Every display
    // instance of a canonical subtree has a distinct chain, so it identifies "which copy" a row is.
    static List<Guid> FlattenedChainIds(FlattenedNote? flattenedNote)
    {
        var ids = new List<Guid>();
        for (var current = flattenedNote; current != null; current = current.Parent)
            ids.Add(current.OriginalNote.Id);
        return ids;
    }

    [RelayCommand]
    public void ToggleExpand(FlattenedNoteViewModel item)
    {
        // Where the toggle happened; used later to focus the right instance of a newly created
        // child (the same canonical note may be mirrored under symlinks after the ReFlatten).
        int anchorIndex = FlattenedNoteVMs.IndexOf(item);
        var anchorChain = FlattenedChainIds(item.FlattenedNote);

        item.Expanded = !item.Expanded;
        if (mainView != null)
            Config.Data.AddNoteChange(new NoteChange()
            {
                Type = NoteChangeType.Update,
                NoteId = item.FlattenedNote.OriginalNote.Id,
                Data = item.FlattenedNote.OriginalNote.Data
            });

        // Content lives on the effective note (the target for symlinks, itself otherwise). Per-link
        // expansion is stored on the link node (item.Expanded), so children shown below an expanded
        // link are the target's children.
        var effective = item.EffectiveNote;
        bool isLink = effective != item.FlattenedNote.OriginalNote;

        if (item.Expanded && effective.SubNotes.Count == 0)
        {
            var newNote = Note.EmptyNote();
            effective.SubNotes.Add(newNote);
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Add,
                    NoteId = newNote.Id,
                    Data = newNote.Data,
                    ParentId = effective.Id,
                    ChildInsertionIndex = effective.SubNotes.IndexOf(newNote)
                });

            FocusFlattenedRow(newNote, anchorChain, anchorIndex);
        }
        if (!item.Expanded && !isLink)
        {
            // Collapsing a real note removes its empty children; collapsing a link must NOT prune
            // the canonical target's children.
            var note = item.FlattenedNote.OriginalNote;
            var toDeleteSubNotes = note.SubNotes.Where(x => string.IsNullOrWhiteSpace(x.Data.DecodedText)).ToList();
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.AddRange(toDeleteSubNotes.Select(subNote => new NoteChange()
                {
                    Type = NoteChangeType.Delete,
                    NoteId = subNote.Id,
                }));
            foreach (var toDeleteSubNote in toDeleteSubNotes)
                note.SubNotes.Remove(toDeleteSubNote);
        }
        ReFlatten();
    }

    // --- Symlinks ---

    /// <summary>
    /// Creates a new symlink note pointing at <paramref name="linkTo"/> and places it as a child
    /// of the item's effective note (<paramref name="asChild"/>) or as a sibling before/after the
    /// item. Returns false (and changes nothing) when that would put a link inside its own target
    /// subtree (a cycle).
    /// </summary>
    public bool CreateLinkTo(Note linkTo, FlattenedNoteViewModel item, bool asChild, bool insertBefore)
    {
        var link = Note.EmptyNote();
        link.Data.LinkTargetId = linkTo.Id;

        Note parent;
        int index;
        if (asChild)
        {
            var effective = item.EffectiveNote;
            if (ContainsNote(effective, linkTo))
                return false; // would link the note into its own subtree
            parent = effective;
            index = parent.SubNotes.Count;
            parent.SubNotes.Add(link);
        }
        else
        {
            var anchor = item.FlattenedNote.OriginalNote;
            var ogParent = item.FlattenedNote.Parent?.OriginalNote;
            if (ogParent == null)
                return false;
            if (ContainsNote(linkTo, anchor))
                return false; // would link an ancestor into its own subtree
            index = Math.Clamp(ogParent.SubNotes.IndexOf(anchor) + (insertBefore ? 0 : 1), 0, ogParent.SubNotes.Count);
            ogParent.SubNotes.Insert(index, link);
            parent = ogParent;
        }

        if (mainView != null)
            Config.Data.AddNoteChange(new NoteChange()
            {
                Type = NoteChangeType.Add,
                NoteId = link.Id,
                Data = link.Data,
                ParentId = parent.Id,
                ChildInsertionIndex = index,
            });

        ReFlatten();

        // Focus the new link's TextBox
        Dispatcher.UIThread.Post(() =>
        {
            var newTextbox = mainView?.GetLogicalDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(ic => (ic.DataContext as FlattenedNoteViewModel)?.FlattenedNote.OriginalNote == link);
            if (newTextbox != null)
                newTextbox.Focusable = true;
            newTextbox?.Focus();
        });
        return true;
    }

    static bool ContainsNote(Note node, Note target)
    {
        if (node == target)
            return true;
        foreach (var subNote in node.SubNotes)
            if (ContainsNote(subNote, target))
                return true;
        return false;
    }

    /// <summary>Copies a link reference (the canonical note's id) to the clipboard.</summary>
    [RelayCommand]
    public async Task CopyLink(FlattenedNoteViewModel flattenedNoteVM)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);
        if (topLevel?.Clipboard == null)
            return;
        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.Create(DataFormat.Text, flattenedNoteVM.EffectiveNote.Id.ToString()));
        await topLevel.Clipboard.SetDataAsync(dataTransfer);
    }

    /// <summary>Pastes a link reference from the clipboard as a child of this note.</summary>
    [RelayCommand]
    public async Task PasteLinkAsChild(FlattenedNoteViewModel flattenedNoteVM)
        => await PasteLink(flattenedNoteVM, asChild: true);

    /// <summary>Pastes a link reference from the clipboard as a sibling after this note.</summary>
    [RelayCommand]
    public async Task PasteLinkAsSibling(FlattenedNoteViewModel flattenedNoteVM)
        => await PasteLink(flattenedNoteVM, asChild: false);

    async Task PasteLink(FlattenedNoteViewModel item, bool asChild)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);
        if (topLevel?.Clipboard == null)
            return;
        var clipboardData = await topLevel.Clipboard.TryGetDataAsync();
        if (clipboardData == null)
            return;
        try
        {
            var clipboardText = await clipboardData.TryGetTextAsync();
            if (Guid.TryParse((clipboardText ?? "").Trim(), out var targetId))
            {
                var target = FindNote(targetId);
                if (target != null)
                    CreateLinkTo(target, item, asChild, insertBefore: false);
            }
        }
        finally
        {
            (clipboardData as IDisposable)?.Dispose();
        }
    }
}
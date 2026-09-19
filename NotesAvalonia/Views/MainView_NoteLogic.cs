using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Notes.Interface;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;
using NotesAvalonia.Helper;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    private void MainView_KeyDown(object? sender, KeyEventArgs e)
    {
        // Remove empty note on backspace
        if (e.Key == Key.Back) // Textboxes dont seem to catch this
        {
            var focusedTextbox = this.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(tb => tb.IsFocused);
            var nvm = focusedTextbox!.DataContext as FlattenedNoteViewModel;
            var note = nvm?.FlattenedNote.OriginalNote;
            var parentNote = nvm?.FlattenedNote.Parent?.OriginalNote;

            if (note?.SubNotes.Count > 0 || !string.IsNullOrWhiteSpace(note?.Data.DecodedText))
                return;

            var noteIndex = parentNote?.SubNotes.IndexOf(note!);
            viewModel?.DeleteNote(note!, parentNote); // also removes symlinks pointing into it

            if (noteIndex != null && noteIndex > 0)
            {
                var previousNote = parentNote?.SubNotes[(int)noteIndex - 1];
                Dispatcher.UIThread.Post(() =>
                {
                    var previousTextbox = this.GetLogicalDescendants()
                        .OfType<TextBox>()
                        .FirstOrDefault(tb => (tb.DataContext as FlattenedNoteViewModel)?.FlattenedNote.OriginalNote == previousNote);
                    if (previousTextbox != null)
                        previousTextbox.Focusable = true;
                    previousTextbox?.Focus();
                    if (previousTextbox != null)
                        previousTextbox.CaretIndex = previousTextbox.Text?.Length ?? 0;
                });
            }
        }
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        // Insert note on enter. Shift+Enter puts the new note inside the current note - as its first
        // child - instead of next to it.
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var tb = sender as TextBox;
            var nvm = tb!.DataContext as FlattenedNoteViewModel;
            var viewModel = (DataContext as MainViewModel)!;

            var ogNote = nvm!.FlattenedNote.OriginalNote;
            var ogParent = nvm!.FlattenedNote.Parent?.OriginalNote;

            bool asFirstChild = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            // Where the new note goes. A link row displays its target's content, so a child of that row
            // belongs to the target - the same note AddChildNote would add to.
            var targetNote = asFirstChild ? nvm.EffectiveNote : ogNote!;
            var targetParent = asFirstChild ? targetNote : ogParent!;

            // Ground truth for a sibling: right before ogNote (caret at the start) or right after it.
            // A first child always goes in front of the note's other children.
            var insertBefore = !asFirstChild && tb.CaretIndex == 0;
            var insertionIndex = asFirstChild ? 0 : targetParent.SubNotes.IndexOf(ogNote) + (insertBefore ? 0 : 1);

            var flattenedNotes = viewModel!.FlattenedNoteVMs;
            var newNote = Note.EmptyNote();

            // Children are only visible on an open note, so open it first - the view model queues the
            // change that syncs that, and without it the new row would not show up at all.
            bool openedNow = asFirstChild && !nvm.Expanded;
            if (openedNow)
                nvm.Expanded = true;

            targetParent.SubNotes.Insert(insertionIndex, newNote);

            // The same canonical subtree can be rendered in several flattened instances at once (e.g.
            // under an expanded symlink), and every instance has to gain the new row.
            //
            // A first child belongs to the note itself, so its row goes directly below each row of that
            // note. A sibling belongs to one specific parent instance, so it is matched by that context
            // and lands right before the anchor row (caret at the start) or right after the anchor row's
            // ENTIRE visible subtree (caret elsewhere) - not just after its own row, which would land
            // between an expanded note and its children.
            //
            // Only the instance the user typed in is focused afterwards.
            //
            // Opening the note, or adding below a link (whose children are the target's), changes the
            // shape of the list rather than just adding a row, so the list is rebuilt in those cases.
            bool rebuild = asFirstChild && (openedNow || !ReferenceEquals(nvm.EffectiveNote, ogNote));
            FlattenedNoteViewModel? primaryVm = null;

            if (rebuild)
            {
                viewModel.ReFlatten();
            }
            else
            {
                int SlotAfterAnchorRow(int anchorRowIndex)
                {
                    int idx = anchorRowIndex;
                    if (!insertBefore)
                    {
                        uint anchorDepth = flattenedNotes[idx].FlattenedNote.Depth;
                        while (idx + 1 < flattenedNotes.Count && flattenedNotes[idx + 1].FlattenedNote.Depth > anchorDepth)
                            idx++;
                        idx++;
                    }
                    return idx;
                }

                var primaryCtx = nvm.FlattenedNote.Parent;
                int primaryRowIndex = flattenedNotes.IndexOf(nvm);
                var slots = new List<(int Index, uint Depth, FlattenedNote? Parent, bool Primary)>();

                for (int i = 0; i < flattenedNotes.Count; i++)
                {
                    var row = flattenedNotes[i];

                    if (asFirstChild)
                    {
                        if (row.FlattenedNote.OriginalNote != targetNote)
                            continue;
                        slots.Add((i + 1, row.FlattenedNote.Depth + 1, row.FlattenedNote, i == primaryRowIndex));
                        continue;
                    }

                    var ctx = row.FlattenedNote.Parent;
                    if (row.FlattenedNote.OriginalNote != ogNote)
                        continue;
                    if (ctx == null || !ReferenceEquals(ctx.OriginalNote, ogParent))
                        continue;
                    // ctx is one display instance of ogNote (the Entered one, or a symlink mirror).
                    slots.Add((SlotAfterAnchorRow(i), row.FlattenedNote.Depth, ctx, ReferenceEquals(ctx, primaryCtx)));
                }

                // Insert from the end so earlier indices stay valid; remember the primary instance row.
                foreach (var slot in slots.OrderByDescending(s => s.Index))
                {
                    var vm = new FlattenedNoteViewModel(new FlattenedNote(newNote)
                    {
                        Depth = slot.Depth,
                        Parent = slot.Parent
                    });
                    flattenedNotes.Insert(slot.Index, vm);
                    if (slot.Primary)
                        primaryVm = vm;
                }
            }

            Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
            {
                Type = NoteChangeType.Add,
                NoteId = newNote.Id,
                Data = newNote.Data,
                ParentId = viewModel.ServerParentIdOf(targetParent),
                ChildInsertionIndex = insertionIndex,
            });

            Task.Run(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Focus the row in the instance the key was pressed in (not a mirrored copy under a
                    // symlink); fall back to any row of the new note if it is gone (or was rebuilt).
                    var newTextbox = this.GetLogicalDescendants()
                        .OfType<TextBox>()
                        .FirstOrDefault(x => primaryVm != null && ReferenceEquals(x.DataContext, primaryVm))
                        ?? this.GetLogicalDescendants().OfType<TextBox>()
                            .FirstOrDefault(x => x.DataContext is FlattenedNoteViewModel nvm && nvm.FlattenedNote.OriginalNote == newNote);
                    if (newTextbox != null)
                    {
                        newTextbox.Focusable = true;
                        newTextbox.Focus();
                    }
                });
            });
        }
    }

    private void AddSubtreeFromString_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: FlattenedNoteViewModel nvm })
            return;

        popupManager?.ShowTextInput(
            "Add Subtree from String",
            "Paste note text as produced by \"Export to Clipboard\":",
            "",
            result =>
            {
                if (string.IsNullOrWhiteSpace(result))
                    return;
                if (Note.TryParseStyledSubtree(result, out var parsedRoot, out var error))
                {
                    if (DataContext is MainViewModel model)
                        model.AddSubtreeFromString(nvm, parsedRoot);
                }
                else
                {
                    popupManager?.Show("Add Subtree from String", error ?? "Could not parse the text.", AlwaysAsFlyout: true);
                }
            });
    }
}
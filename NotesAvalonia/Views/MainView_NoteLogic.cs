using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Debug.WriteLine($"MainView_KeyDown: {e.Key} {e.KeyModifiers} {e.Handled}");

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
        // Insert note on enter
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var tb = sender as TextBox;
            var nvm = tb!.DataContext as FlattenedNoteViewModel;
            var viewModel = (DataContext as MainViewModel)!;

            var ogNote = nvm!.FlattenedNote.OriginalNote;
            var ogParent = nvm!.FlattenedNote.Parent?.OriginalNote;

            var insertBefore = tb.CaretIndex == 0;
            var insertionIndex = ogParent!.SubNotes.IndexOf(ogNote) + (insertBefore ? 0 : 1);

            // Ground truth: the new note becomes a sibling right before/after ogNote. In the
            // flattened view it belongs right before ogNote's row (caret at start) or right after
            // ogNote's ENTIRE visible subtree (caret elsewhere) - not just after ogNote's own row,
            // which would land between an expanded ogNote and its children.
            var flattenedNotes = viewModel!.FlattenedNoteVMs;
            var newNote = Note.EmptyNote();
            ogParent.SubNotes.Insert(insertionIndex, newNote);

            // The same canonical subtree can be rendered in several flattened instances at once
            // (e.g. under an expanded symlink). The new sibling row must be added to EVERY such
            // instance - but only the instance the Enter happened in must receive focus. For each
            // instance find the copy of ogNote (same canonical note under that instance's parent
            // context) and insert the new row at the same relative spot.
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
            var slots = new List<(int Index, uint Depth, FlattenedNote? Parent)>();
            FlattenedNoteViewModel? primaryVm = null;

            for (int i = 0; i < flattenedNotes.Count; i++)
            {
                var row = flattenedNotes[i];
                var ctx = row.FlattenedNote.Parent;
                if (row.FlattenedNote.OriginalNote != ogNote)
                    continue;
                if (ctx == null || !ReferenceEquals(ctx.OriginalNote, ogParent))
                    continue;
                // ctx is one display instance of ogNote (the Entered one, or a symlink mirror).
                slots.Add((SlotAfterAnchorRow(i), row.FlattenedNote.Depth, ctx));
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
                if (ReferenceEquals(slot.Parent, primaryCtx))
                    primaryVm = vm;
            }

            Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
            {
                Type = NoteChangeType.Add,
                NoteId = newNote.Id,
                Data = newNote.Data,
                ParentId = ogParent.Id,
                ChildInsertionIndex = insertionIndex,
            });

            Task.Run(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Focus the row in the instance the Enter happened in (not a mirrored copy
                    // under a symlink); fall back to any row of the new note if it is gone.
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
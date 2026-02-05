using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Notes.Interface;
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

            if (note?.SubNotes.Count > 0 || !string.IsNullOrWhiteSpace(note?.Text))
                return;

            var noteIndex = parentNote?.SubNotes.IndexOf(note!);
            parentNote?.SubNotes.Remove(note!);
            viewModel?.ReFlatten();
            unsavedChanges = true;

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
            var flattenedInsertionIndex = viewModel!.FlattenedNotes.ToList().FindIndex(x => x.FlattenedNote == nvm!.FlattenedNote) + (insertBefore ? 0 : 1);

            var newNote = Note.EmptyNote();
            var flattenedNewNote = newNote.Flatten(depth: nvm!.FlattenedNote.Depth, parent: nvm!.FlattenedNote.Parent).First();

            ogParent.SubNotes.Insert(insertionIndex, newNote);
            viewModel?.FlattenedNotes.Insert(flattenedInsertionIndex, new FlattenedNoteViewModel(flattenedNewNote) { });

            unsavedChanges = true;

            Task.Run(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var newTextbox = this.GetLogicalDescendants().OfType<TextBox>().First(x => x.DataContext is FlattenedNoteViewModel nvm && nvm.FlattenedNote.OriginalNote == newNote);
                    if (newTextbox != null)
                    {
                        newTextbox.Focusable = true;
                        newTextbox.Focus();
                    }
                });
            });
        }
    }
}
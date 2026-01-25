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
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

class CustomDragData : IDataObject
{
    public static string Format = "FlattenedNoteRef";
    public FlattenedNoteViewModel DraggedNote { get; set; }

    public CustomDragData(FlattenedNoteViewModel DraggedNote)
    {
        this.DraggedNote = DraggedNote;
    }

    public IEnumerable<string> GetDataFormats() => [CustomDragData.Format];

    public bool Contains(string dataFormat) => dataFormat == CustomDragData.Format;

    public object? Get(string dataFormat) => DraggedNote;
}

public partial class MainView : UserControl
{
    bool disableScrolling = false;
    double lockedY = 0;
    FlattenedNoteViewModel? MobileDraggedFlattenedNote = null;

    private void DragButton_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Properties.IsLeftButtonPressed && sender is Button senderButton)
        {
            var model = DataContext as MainViewModel;
            Debug.WriteLine($"DragButton_PointerMoved owo! LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure}");

            if (senderButton.Parent?.Parent is ContentPresenter contentPresenter)
            {
                if (contentPresenter.Content is FlattenedNoteViewModel draggedViewModel)
                {
                    if (model != null)
                        model.AddDebugText($"Drag Start {draggedViewModel}");

                    var result = DragDrop.DoDragDrop(e, new CustomDragData(draggedViewModel), DragDropEffects.Move);
                    Task.Run(() => Debug.WriteLine($"DragButton_DragDrop.DoDragDrop done! Result={result.Result}"));

                    if (!Globals.IsDesktop)
                    {
                        MobileDraggedFlattenedNote = draggedViewModel;
                        disableScrolling = true;
                        lockedY = scrollViewer?.Offset.Y ?? 0;
                    }
                }
            }
        }
    }

    private void NoteContainer_OnDragOver(object? sender, DragEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"NoteContainer_OnDragOver: {e}");

        Debug.WriteLine($"NoteContainer_OnDragOver: {e}");
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void NoteContainer_OnDrop(object? sender, DragEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"NoteContainer_OnDrop: {e}");

        if (e.Data.Contains(CustomDragData.Format))
        {
            FlattenedNoteViewModel? draggedFlattenedNote = e.Data.Get(CustomDragData.Format) as FlattenedNoteViewModel;
            Debug.WriteLine($"NoteContainer_OnDrop: {draggedFlattenedNote?.FlattenedNote.OriginalNote.Text}");

            if (draggedFlattenedNote != null)
            {
                var flattenedNotes = model!.FlattenedNotes;

                var presenterElem = sender as Grid;
                var draggedToFlattenedNote = presenterElem?.DataContext as FlattenedNoteViewModel;

                if (draggedToFlattenedNote != null)
                    MoveNoteFromTo(draggedFlattenedNote, draggedToFlattenedNote);
            }
        }
    }

    void MoveNoteFromTo(FlattenedNoteViewModel draggedFlattenedNote, FlattenedNoteViewModel draggedToFlattenedNote)
    {
        var model = DataContext as MainViewModel;
        var flattenedNotes = model!.FlattenedNotes;

        var ogDraggedNote = draggedFlattenedNote.FlattenedNote.OriginalNote;
        var ogDraggedNoteParent = draggedFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        var ogDraggedToNote = draggedToFlattenedNote!.FlattenedNote.OriginalNote;
        var ogDraggedToNoteParent = draggedToFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        var ogDraggedToNoteParentIndex = ogDraggedToNoteParent.SubNotes.IndexOf(ogDraggedToNote);
        var draggedToNoteFlattenedIndex = draggedToFlattenedNote == null ? -1 : flattenedNotes.IndexOf(draggedToFlattenedNote);

        if (ogDraggedNote.RecursiveSubnotes().FirstOrDefault(n => n == ogDraggedToNote) != null)
            return; // Can't move a note into one of its own subnotes

        Debug.WriteLine($"NoteContainer_OnDrop reorder! ogDraggedNote{ogDraggedNote} ogDraggedNoteParent{ogDraggedNoteParent} ogDraggedToNote{ogDraggedToNote} ogDraggedToNote{ogDraggedToNote} ogDraggedToNoteParent{ogDraggedToNoteParent} ogDraggedToNoteParentIndex{ogDraggedToNoteParentIndex} draggedToNoteFlattenedIndex{draggedToNoteFlattenedIndex}");

        // Removal
        flattenedNotes.Remove(draggedFlattenedNote);
        ogDraggedNoteParent.SubNotes.Remove(ogDraggedNote);

        // Add
        flattenedNotes.Insert(draggedToNoteFlattenedIndex, draggedFlattenedNote);
        ogDraggedToNoteParent.SubNotes.Insert(ogDraggedToNoteParentIndex, ogDraggedNote);

        // Reflatten
        model.ReFlatten();
        unsavedChanges = true;
        // TODO: Update Server?
    }

    private void Handle_Reordering_On_MainView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        disableScrolling = false;

        if (!Globals.IsDesktop && MobileDraggedFlattenedNote != null)
        {
            var pos = e.GetPosition(this);
            var elems = this.GetInputElementsAt(pos, false);
            if (model != null)
                model.AddDebugText($"Main_Reordering 0 {elems.Select(e => e.GetType().Name).Aggregate((x, y) => x + " " + y) ?? "null"}");
            var elem = elems.Where(x => x.GetType() != typeof(ScrollViewer)).First() as StyledElement;
            Debug.WriteLine($"Main_Reordering 1 {elem?.ToString() ?? "null"}");
            if (model != null)
                model.AddDebugText($"Main_Reordering 1 {elem?.ToString() ?? "null"}");
            if (elem == null)
                return;
            while (elem != null && (elem is not ContentPresenter || (elem is ContentPresenter presenterCandidate && presenterCandidate.DataContext?.GetType() != typeof(FlattenedNoteViewModel))))
            {
                if (elem is ContentPresenter presenterCandidatee)
                {
                    Debug.WriteLine(presenterCandidatee.DataContext?.GetType());
                    Debug.WriteLine(presenterCandidatee.DataContext?.GetType() == typeof(FlattenedNoteViewModel));
                }
                Debug.WriteLine($"Main_Reordering 2 {elem} {elem?.GetType()}");
                // if (model != null)
                //     model.AddDebugText($"Main_Reordering 2 {elem} {elem?.GetType()}");

                elem = elem?.Parent;
            }
            var presenterElem = elem as ContentPresenter;
            var draggedToFlattenedNote = presenterElem?.Content as FlattenedNoteViewModel;

            if (draggedToFlattenedNote != null)
            {
                if (model != null)
                    model.AddDebugText($"MobileDraggedFlattenedNote {MobileDraggedFlattenedNote.Text} draggedToFlattenedNote {draggedToFlattenedNote.Text}");
                MoveNoteFromTo(MobileDraggedFlattenedNote, draggedToFlattenedNote);
            }
        }
    }
}
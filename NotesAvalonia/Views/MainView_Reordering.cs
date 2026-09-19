using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public static class DragDropFormats
{
    public static readonly DataFormat<FlattenedNoteViewModel> FlattenedNoteRef =
        DataFormat.CreateInProcessFormat<FlattenedNoteViewModel>("FlattenedNoteRef");
}

// Reordering notes by dragging the handle on the left of a row.
//
// Desktop uses the platform drag & drop: the handle's pointer move starts a drag, and the row under the
// pointer is a drop target (see DragButton_PointerMoved / NoteContainer_OnDrop).
//
// Android has neither a drag source nor a working DragDrop.DoDragDropAsync (it never completes there),
// so mobile drags are tracked by the view itself:
//
//   * a press on the handle arms the drag (see OnDragReordering_PointerPressed). This has to happen on
//     the view and not through an attribute on the handle: Button marks the press handled in its own
//     class handler, so a XAML handler on that same element never runs;
//   * while armed, the view owns the touch - it tracks the pointer and swallows the moves, and the
//     list's scroll gesture recognizers are put aside for the duration (see SuspendListScrollGestures).
//     Without that the recognizer claims any movement along the scroll axis, which both ends the drag
//     early and scrolls the list out from under it;
//   * the drag ends on pointer release, or - since Android can cancel a touch instead of releasing it -
//     on a lost pointer capture or on the next press.
public partial class MainView : UserControl
{
    const string DragHandleName = "DragHandle";

    // Backstop for a scroll that was already running when the drag started: suspending the recognizers
    // stops new scroll gestures, but not an inertia animation that is already in flight.
    bool disableScrolling = false;
    double lockedY = 0;

    FlattenedNoteViewModel? mobileDraggedNote = null;
    Button? mobileDragHandle;
    Point mobileDragLastPos;
    readonly List<(InputElement Host, ScrollGestureRecognizer Recognizer)> suspendedScrollGestures = new();

    bool dragInProgress = false;

    void InitDragReordering()
    {
        this.AddHandler(InputElement.PointerPressedEvent, OnDragReordering_PointerPressed, RoutingStrategies.Tunnel);
        this.AddHandler(InputElement.PointerMovedEvent, OnDragReordering_PointerMoved, RoutingStrategies.Tunnel);
    }

    // Arms a mobile drag as soon as the handle is touched. The desktop drag still starts from the
    // handle's pointer move (DragButton_PointerMoved).
    private void OnDragReordering_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Any press means the previous gesture is over, even if it never reported a release.
        FinishStaleMobileDrag();

        if (Globals.IsDesktop || !e.Properties.IsLeftButtonPressed)
            return;

        var dragHandle = FindDragHandleButton(e.Source);
        if (dragHandle == null || !TryGetNoteOfDragHandle(dragHandle, out var draggedViewModel))
            return;

        mobileDraggedNote = draggedViewModel;
        mobileDragHandle = dragHandle;
        mobileDragLastPos = e.GetPosition(this);
        disableScrolling = true;
        lockedY = scrollViewer?.Offset.Y ?? 0;

        SuspendListScrollGestures();

        // PointerCaptureLost is a direct event, so it has to be observed on the element holding the
        // capture rather than on the view.
        dragHandle.AddHandler(InputElement.PointerCaptureLostEvent, OnDragHandleCaptureLost);
    }

    static Button? FindDragHandleButton(object? source) =>
        source is Visual visual
            ? visual.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault(b => b.Name == DragHandleName)
            : null;

    static bool TryGetNoteOfDragHandle(Button dragHandle, out FlattenedNoteViewModel? draggedViewModel)
    {
        draggedViewModel = (dragHandle.Parent?.Parent as ContentPresenter)?.Content as FlattenedNoteViewModel;
        return draggedViewModel != null;
    }

    // While a drag is armed the pointer belongs to it: the position is tracked here (the events are
    // swallowed in the tunnel phase, so the handle itself never sees them) and the movement is marked
    // handled so nothing else acts on it.
    private void OnDragReordering_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (mobileDraggedNote == null)
            return;

        mobileDragLastPos = e.GetPosition(this);
        e.Handled = true;
    }

    // Android can cancel a touch instead of releasing it, so a lost capture ends the drag too.
    private void OnDragHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (mobileDraggedNote == null)
            return;

        FinishMobileDrag(mobileDragLastPos);
    }

    // A press while a drag is still armed means the previous gesture ended without a release or a
    // capture loss: drop where the finger last was instead of leaving the drag (and the list's scroll
    // gestures) in limbo.
    void FinishStaleMobileDrag()
    {
        if (mobileDraggedNote == null)
            return;

        FinishMobileDrag(mobileDragLastPos);
    }

    private void Handle_Reordering_On_MainView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        dragInProgress = false;

        if (Globals.IsDesktop || mobileDraggedNote == null)
            return;

        // Not marked handled: the handle still needs the release to clear its pressed state. The drag
        // state is cleared here, so the second (bubble) pass of this handler is a no-op.
        FinishMobileDrag(e.GetPosition(this));
    }

    // Drops the dragged note onto whatever row sits under `pos` and clears the drag state. Every way a
    // mobile drag can end funnels through here.
    void FinishMobileDrag(Point pos)
    {
        var draggedNote = mobileDraggedNote;
        mobileDraggedNote = null;
        disableScrolling = false;
        UnhookDragHandleCapture();
        RestoreListScrollGestures();
        if (draggedNote == null)
            return;

        var draggedToNote = NoteUnder(pos);

        // Dropping a note onto its own row is a no-op; without this a plain tap on the handle would
        // enqueue a pointless delete+add pair.
        if (draggedToNote == null || draggedToNote.EffectiveNote.Id == draggedNote.EffectiveNote.Id)
            return;

        MoveNoteFromTo(draggedNote, draggedToNote);
    }

    // The note row under a point, or null when the pointer is somewhere else (e.g. the empty space
    // below the last row). Every element inside a row inherits that row's view model - including
    // template internals such as the drag handle's presenter or the expand button's - so reading the
    // first one found is enough, and is independent of which part of the row was hit.
    FlattenedNoteViewModel? NoteUnder(Point pos) =>
        this.GetInputElementsAt(pos, false)
            .OfType<StyledElement>()
            .Select(element => element.DataContext)
            .OfType<FlattenedNoteViewModel>()
            .FirstOrDefault();

    void UnhookDragHandleCapture()
    {
        if (mobileDragHandle == null)
            return;

        mobileDragHandle.RemoveHandler(InputElement.PointerCaptureLostEvent, OnDragHandleCaptureLost);
        mobileDragHandle = null;
    }

    // Takes the notes list's scroll gesture recognizers out of play for the duration of a drag.
    //
    // They are not on the ScrollViewer itself but on the ScrollContentPresenter in its template, and a
    // recognizer only claims movement along its own axis - which is why a vertical drag used to lose
    // the touch (and scroll the list) while a horizontal one kept it.
    void SuspendListScrollGestures()
    {
        foreach (var host in ScrollGestureHosts())
        {
            foreach (var recognizer in host.GestureRecognizers.OfType<ScrollGestureRecognizer>().ToList())
            {
                host.GestureRecognizers.Remove(recognizer);
                suspendedScrollGestures.Add((host, recognizer));
            }
        }
    }

    IEnumerable<InputElement> ScrollGestureHosts()
    {
        if (scrollViewer == null)
            yield break;

        yield return scrollViewer;

        foreach (var presenter in scrollViewer.GetVisualDescendants().OfType<ScrollContentPresenter>())
            yield return presenter;
    }

    void RestoreListScrollGestures()
    {
        foreach (var (host, recognizer) in suspendedScrollGestures)
        {
            if (!host.GestureRecognizers.Contains(recognizer))
                host.GestureRecognizers.Add(recognizer);
        }

        suspendedScrollGestures.Clear();
    }

    // Desktop: starts a platform drag when the handle is dragged. Mobile never comes here.
    private async void DragButton_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!Globals.IsDesktop || dragInProgress || mobileDraggedNote != null)
            return; // never start a second drag

        if (!e.Properties.IsLeftButtonPressed || sender is not Button dragHandle)
            return;

        if (!TryGetNoteOfDragHandle(dragHandle, out var draggedViewModel))
            return;

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.Create(DragDropFormats.FlattenedNoteRef, draggedViewModel));

        dragInProgress = true;
        try
        {
            await DragDrop.DoDragDropAsync(
                new PointerPressedEventArgs(sender, e.Pointer, this, new Point(), (ulong)DateTime.Now.ToBinary(), new PointerPointProperties(), KeyModifiers.None),
                dataTransfer,
                DragDropEffects.Move);
        }
        finally
        {
            dragInProgress = false;
        }
    }

    private void NoteContainer_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void NoteContainer_OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DragDropFormats.FlattenedNoteRef))
            return;

        var draggedFlattenedNote = e.DataTransfer.TryGetValue(DragDropFormats.FlattenedNoteRef);
        if (draggedFlattenedNote == null)
            return;

        var draggedToFlattenedNote = (sender as Grid)?.DataContext as FlattenedNoteViewModel;
        if (draggedToFlattenedNote == null)
            return;

        // Ctrl + drop creates a symlink to the dragged note before the drop target instead of moving
        // it (plain drag & drop needs no modifier keys).
        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            viewModel?.CreateLinkTo(draggedFlattenedNote.EffectiveNote, draggedToFlattenedNote, asChild: false, insertBefore: true);
        else
            MoveNoteFromTo(draggedFlattenedNote, draggedToFlattenedNote);
    }

    // Moves the dragged note in the payload tree - the flattened list is rebuilt from it - and queues
    // the matching server changes.
    void MoveNoteFromTo(FlattenedNoteViewModel draggedFlattenedNote, FlattenedNoteViewModel draggedToFlattenedNote)
    {
        var ogDraggedNote = draggedFlattenedNote.FlattenedNote.OriginalNote;
        var ogDraggedNoteParent = draggedFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        var ogDraggedToNote = draggedToFlattenedNote.FlattenedNote.OriginalNote;
        var ogDraggedToNoteParent = draggedToFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        var ogDraggedToNoteParentIndex = ogDraggedToNoteParent.SubNotes.IndexOf(ogDraggedToNote);

        if (ogDraggedNote.RecursiveSubNotes().Any(n => n.Note == ogDraggedToNote))
            return; // a note cannot be moved into one of its own subnotes

        ogDraggedNoteParent.SubNotes.Remove(ogDraggedNote);
        ogDraggedToNoteParent.SubNotes.Insert(ogDraggedToNoteParentIndex, ogDraggedNote);
        viewModel?.ReFlatten();

        // One Move change instead of delete + add: a delete removes the whole subtree (and keeps it
        // as trash), so re-adding only the dragged note itself would leave its subnotes deleted.
        Config.Data.AddNoteChange(new NoteChange()
        {
            Type = NoteChangeType.Move,
            NoteId = ogDraggedNote.Id,
            ParentId = viewModel!.ServerParentIdOf(ogDraggedToNoteParent),
            ChildInsertionIndex = ogDraggedToNoteParentIndex,
        });
    }
}

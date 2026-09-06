using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    Window? window => TopLevel.GetTopLevel(this) as Window;
    ScrollViewer? scrollViewer;
    MainViewModel? viewModel => DataContext as MainViewModel;
    Helper.Popup? popupManager;

    public MainView()
    {
        InitializeComponent();
        Loaded += MainView_Loaded;

        try
        {
            InitCommunicatorBasedOnConfig();
        }
        catch (Exception ex)
        {
            Notes.Interface.Logger.WriteLine($"Failed to initialize communicator: {ex.ToString()}");
        }

        // Set platform ui scale
        var layoutTransformControl = this.GetLogicalDescendants()
            .OfType<LayoutTransformControl>()
            .FirstOrDefault();
        if (layoutTransformControl != null)
            layoutTransformControl.LayoutTransform = new Avalonia.Media.ScaleTransform(Globals.LayoutScale, Globals.LayoutScale);

        // Mobile: don't show the window border's context menu (Close) on long-press
        if (!Globals.IsDesktop)
        {
            var windowBorder = this.GetLogicalDescendants()
                .OfType<Border>()
                .FirstOrDefault(x => x.Name == "WindowBorder");
            if (windowBorder != null)
                windowBorder.ContextMenu = null;
        }
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        Debug.WriteLine("MainView loaded!");
        popupManager = new(ex =>
        {
            if (DataContext is MainViewModel model)
                model.AddDebugText($"Failed to show popup: {ex} {ex.StackTrace}");
        }, window, this.FindControl<Border>("WindowBorder"));
#if DEBUG
        window?.AttachDevTools();
#endif
        Handle_Communicator_On_MainView_Loaded(sender, e);

        // Handler
        this.AddHandler(
            InputElement.PointerPressedEvent,
            MainView_PointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        this.AddHandler(
            InputElement.PointerReleasedEvent,
            MainView_PointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        this.AddHandler(
            InputElement.KeyDownEvent,
            MainView_KeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );

        // Remember what was last written when a note stops being edited (all platforms): a deferred
        // payload can lag the last keystrokes, and must not regress that content.
        this.AddHandler(InputElement.LostFocusEvent, OnNoteLostFocus, RoutingStrategies.Bubble);

        // Mobile: keep the view locked while a note row is dragged (row reorder) so the list
        // cannot scroll out from under the drag.
        if (!Globals.IsDesktop)
        {
            scrollViewer = this.GetLogicalDescendants()
                .OfType<ScrollViewer>()
                .First();

            // Mobile: while a note is being edited, freeze the TextBox's height so a soft line wrap
            // (or deleting one) cannot change the row's height and relayout the list - that
            // relayout made Android drop the active input connection (IME/focus loss). The height
            // is restored to auto when focus leaves the note.
            this.AddHandler(InputElement.GotFocusEvent, OnMobileNoteGotFocus, RoutingStrategies.Bubble);

            scrollViewer.PropertyChanged += (s, e) =>
            {
                if (e.Property == ScrollViewer.OffsetProperty && disableScrolling)
                    scrollViewer.Offset = new Avalonia.Vector(0, lockedY);
            };
        }
    }

    void OnMobileNoteGotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is TextBox { DataContext: FlattenedNoteViewModel } textBox && textBox.Bounds.Height > 0)
        {
            textBox.Tag = textBox.Bounds.Height;
            textBox.Height = textBox.Bounds.Height; // freeze at the current (content) height
        }
    }

    void OnNoteLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is not TextBox { DataContext: FlattenedNoteViewModel } textBox)
            return;

        // Restore auto-sizing (mobile freeze from E3).
        if (textBox.Tag is double frozenHeight)
        {
            textBox.Height = double.NaN;
            textBox.Tag = null;
        }

        // Capture the exact content/revision at the moment the user stopped editing so an older
        // deferred payload cannot regress it (see ApplyReceivedPayload).
        if (textBox.DataContext is FlattenedNoteViewModel nvm)
        {
            lastEditedNoteId = nvm.EffectiveNote.Id;
            lastEditedNoteData = CloneNoteData(nvm.EffectiveNote.Data);
        }
    }

    private void MainView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"MainView_PointerReleased: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");

        Handle_Reordering_On_MainView_PointerReleased(sender, e);
    }

    private void MainView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var model = DataContext as MainViewModel;
        if (model != null)
            model.AddDebugText($"MainView_PointerPressed: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");
        Debug.WriteLine($"MainView_PointerPressed: LeftButtonPressed={e.Properties.IsLeftButtonPressed}, Pressure={e.Properties.Pressure} {e.GetPosition(sender as ItemsControl)}");

        foreach (var fnvm in viewModel?.FlattenedNoteVMs ?? [])
        {
            fnvm.NotTemporarilyUnHidden = true;
        }
    }

    private void Border_ContextMenu_Close_Click(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
        window?.Close();
    }

    private void Note_Spoiler_Rectangle_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var rect = sender as Rectangle;
        var nvm = rect!.DataContext as FlattenedNoteViewModel;
        nvm!.NotTemporarilyUnHidden = false;
    }
}
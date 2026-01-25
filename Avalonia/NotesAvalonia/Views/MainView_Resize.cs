using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace NotesAvalonia.Views;

enum DragType
{
    Normal,
    ResizeBottomRight,
    ResizeBottomLeft,
    ResizeTopRight,
    ResizeTopLeft,
}

public partial class MainView : UserControl
{
    Point dragPointerSauce, dragGlobalPointerSauce, dragWindowSizeSauce;
    PixelPoint dragWindowPosSauce;
    DragType dragType = DragType.Normal;
    bool isChangingSizeOrPos = false;

    private void WindowBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Globals.IsDesktop || !e.Properties.IsLeftButtonPressed)
            return;
        //Debug.WriteLine($"WindowBorder_PointerPressed {e.GetPosition(this)} {e.Source} {e.Pointer.Type} {e.Properties.IsLeftButtonPressed}");

        //Debug.WriteLine($"{this.Parent?.GetType()}");
        var window = (Parent as Window)!;

        dragPointerSauce = e.GetPosition(window);
        dragGlobalPointerSauce = e.GetPosition(window) + window.Position.ToPoint(1);
        dragWindowPosSauce = window.Position;
        dragWindowSizeSauce = new Point(window.Width, window.Height);

        if (dragPointerSauce.X >= window.Width - 16 && dragPointerSauce.Y >= window.Height - 16)
            dragType = DragType.ResizeBottomRight;
        else if (dragPointerSauce.X <= 16 && dragPointerSauce.Y >= window.Height - 16)
            dragType = DragType.ResizeBottomLeft;
        else if (dragPointerSauce.X >= window.Width - 16 && dragPointerSauce.Y <= 16)
            dragType = DragType.ResizeTopRight;
        else if (dragPointerSauce.X <= 16 && dragPointerSauce.Y <= 16)
            dragType = DragType.ResizeTopLeft;
        else
            dragType = DragType.Normal;

        isChangingSizeOrPos = true;
    }

    private void WindowBorder_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        //Debug.WriteLine($"WindowBorder_PointerReleased {e.GetPosition(this)} {e.Source} {e.Pointer.Type} {e.Properties.IsLeftButtonPressed}");

        isChangingSizeOrPos = false;
    }

    private void Border_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!Globals.IsDesktop)
            return;
        if (e.Properties.IsLeftButtonPressed == false)
            isChangingSizeOrPos = false;
        if (!isChangingSizeOrPos)
            return;

        var window = (this.Parent as Window)!;
        var newPos = e.GetPosition(window) + window.Position.ToPoint(1);
        var deltaX = dragGlobalPointerSauce.X - newPos.X;
        var deltaY = dragGlobalPointerSauce.Y - newPos.Y;
        //Debug.WriteLine($"Border_PointerMoved? {dragWindowPosSauce} / {dragGlobalPointerSauce} / {newPos} / {deltaX} / {deltaY} / {dragType} / {window.RenderScaling}");

        double width = window.Width, height = window.Height;

        if (dragType == DragType.ResizeBottomRight)
        {
            width = Math.Max(Globals.MinWindowSize.X, dragWindowSizeSauce.X - deltaX);
            height = Math.Max(Globals.MinWindowSize.Y, dragWindowSizeSauce.Y - deltaY);
            window.Width = width;
            window.Height = height;
        }
        else if (dragType == DragType.ResizeBottomLeft)
        {
            width = Math.Max(Globals.MinWindowSize.X, dragWindowSizeSauce.X + deltaX / window.RenderScaling);
            height = Math.Max(Globals.MinWindowSize.Y, dragWindowSizeSauce.Y - deltaY);
            window.Width = width;
            window.Height = height;
            window.Position = new PixelPoint((int)(-deltaX), (int)(0)) + dragWindowPosSauce;
        }
        else if (dragType == DragType.ResizeTopLeft)
        {
            width = Math.Max(Globals.MinWindowSize.X, dragWindowSizeSauce.X + deltaX / window.RenderScaling);
            height = Math.Max(Globals.MinWindowSize.Y, dragWindowSizeSauce.Y + deltaY / window.RenderScaling);
            window.Width = width;
            window.Height = height;
            window.Position = new PixelPoint((int)(-deltaX), (int)(-deltaY)) + dragWindowPosSauce;
        }
        else if (dragType == DragType.ResizeTopRight)
        {
            width = Math.Max(Globals.MinWindowSize.X, dragWindowSizeSauce.X - deltaX);
            height = Math.Max(Globals.MinWindowSize.Y, dragWindowSizeSauce.Y + deltaY / window.RenderScaling);
            window.Width = width;
            window.Height = height;
            window.Position = new PixelPoint((int)(0), (int)(-deltaY)) + dragWindowPosSauce;
        }
        else if (dragType == DragType.Normal)
            window.Position = new PixelPoint((int)(-deltaX), (int)(-deltaY)) + dragWindowPosSauce;

        UpdateClip(window, width, height);
    }

    private void UpdateClip(Window window, double width, double height)
    {
        window.Clip = new Avalonia.Media.RectangleGeometry
        {
            Rect = new Avalonia.Rect(0, 0, width, height),
            RadiusX = Globals.WindowBorderRadius,
            RadiusY = Globals.WindowBorderRadius
        };
    }
}
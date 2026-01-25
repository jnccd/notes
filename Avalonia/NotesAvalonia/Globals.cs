using System;
using Avalonia;

public static class Globals
{
    public static readonly Point InitialWindowSize = new(400, 300);
    public static readonly Point MinWindowSize = new(300, 150);
    public static readonly double WindowBorderRadius = 10;
    public static readonly bool IsDesktop = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public static readonly double LayoutScale = IsDesktop ? 0.90 : 1.25;
#if DEBUG
    public static readonly string RunConfig = "Debug";
#else
    public static readonly string RunConfig = "Release";
#endif
}
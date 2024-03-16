﻿// Source: https://github.com/christianrondeau/GoToWindow/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CycleThroughAppWindows;

public static class WindowEntryFactory
{
    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsIconic(IntPtr hWnd);

    public static WindowEntry Create(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint processId);

        return Create(hWnd, processId);
    }

    public static WindowEntry Create(IntPtr hWnd, uint processId)
    {
        var windowTitle = GetWindowTitle(hWnd);

        var isVisible = !IsIconic(hWnd);

        return new WindowEntry
        {
            HWnd = hWnd,
            Title = windowTitle,
            ProcessId = processId,
            IsVisible = isVisible
        };
    }

    static string GetWindowTitle(IntPtr hWnd)
    {
        var lLength = GetWindowTextLength(hWnd);
        if (lLength == 0)
            return null;

        var builder = new StringBuilder(lLength);
        GetWindowText(hWnd, builder, lLength + 1);
        return builder.ToString();
    }
}
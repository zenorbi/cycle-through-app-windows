using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CycleThroughAppWindows;

public static class Logic
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
		
    [DllImport("user32.dll", SetLastError=true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    
    [DllImport("User32.dll")]
    static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    public static void SwitchToNextWindow()
    {
        var foregroundWindow = GetForegroundWindow();
        GetWindowThreadProcessId(foregroundWindow, out var processId);
        var windowEntries = WindowsListFactory.Load();
        var foregroundWindowProcessName = GetProcessName(processId);
        windowEntries.RemoveAll(windowEntry => windowEntry.ProcessName != foregroundWindowProcessName);
        if (windowEntries.Count == 0) return;
        windowEntries.Reverse();
        var index = windowEntries.FindIndex(windowEntry => windowEntry.HWnd == foregroundWindow);
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            ++index;
        }

        index %= windowEntries.Count;
        SetForegroundWindow(windowEntries[index].HWnd);
    }

    static string GetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int) pid).ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
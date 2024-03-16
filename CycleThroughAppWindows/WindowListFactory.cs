﻿// Source: https://github.com/christianrondeau/GoToWindow/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CycleThroughAppWindows;

/// <remarks>
/// Thanks to Tommy Carlier for how to get the list of windows: http://blog.tcx.be/2006/05/getting-list-of-all-open-windows.html
/// Thanks to taby for window eligibility: http://stackoverflow.com/questions/210504/enumerate-windows-like-alt-tab-does
/// Thanks to Hans Passant & Tim Beaudet for Windows 10 apps process name: http://stackoverflow.com/a/32513438/154480
/// Thanks to vhanla for hiding closed Windows 10 apps: https://github.com/christianrondeau/GoToWindow/pull/55
/// </remarks>
public static class WindowsListFactory
{
    const int MaxLastActivePopupIterations = 50;

    delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

    public enum GetAncestorFlags
    {
        GetParent = 1,
        GetRoot = 2,
        GetRootOwner = 3
    }

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern IntPtr GetLastActivePopup(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public static List<IWindowEntry> Load()
    {
        var lShellWindow = GetShellWindow();
        var windows = new List<IWindowEntry>();
        var currentProcessId = Process.GetCurrentProcess().Id;

        EnumWindows((hWnd, lParam) =>
        {
            InspectPotentialWindow(hWnd, lShellWindow, currentProcessId, windows);
            return true;
        }, 0);

        return windows;
    }

    static void InspectPotentialWindow(IntPtr hWnd, IntPtr lShellWindow, int currentProcessId, ICollection<IWindowEntry> windows)
    {
        if (!HWndEligibleForActivation(hWnd, lShellWindow))
            return;

        var className = GetClassName(hWnd);

        if (className == "ApplicationFrameWindow")
            InspectWindows10AppWindow(hWnd, windows, className);
        else
            InspectNormalWindow(hWnd, currentProcessId, windows, className);
    }

    static void InspectNormalWindow(IntPtr hWnd, int currentProcessId, ICollection<IWindowEntry> windows, string className)
    {
        if (!ClassEligibleForActivation(className))
            return;

        var window = WindowEntryFactory.Create(hWnd);

        if (IsKnownException(window))
            return;

        if (window.ProcessId == currentProcessId || window.Title == null)
            return;

        UpdateProcessName(window);
        windows.Add(window);
    }

    static void InspectWindows10AppWindow(IntPtr hWnd, ICollection<IWindowEntry> windows, string className)
    {
        // check if windows is not cloaked
        const int DWMWA_CLOAKED = 14;
        DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
        if (cloaked != 0) return;

        var foundChildren = false;
        GetWindowThreadProcessId(hWnd, out uint processId);

        EnumChildWindows(hWnd, (childHWnd, lparam) =>
        {
            GetWindowThreadProcessId(childHWnd, out uint childProcessId);
            if (processId != childProcessId)
            {
                var childClassName = GetClassName(hWnd);

                var window = WindowEntryFactory.Create(childHWnd, childProcessId);
                if (window.Title != null)
                {
                    UpdateProcessName(window);

                    if (IsKnownWindows10Exception(window)) return true;

                    //TODO: Windows 10 App Icons
                    // 1. Get the window.ProcessFileName
                    // 2. Look in the folder for AppManifest.xml
                    // 3. Look for Package/Properties/Logo
                    // 4. Load that file (should be a PNG)

                    windows.Add(window);
                    foundChildren = true;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (!foundChildren)
        {
            var window = WindowEntryFactory.Create(hWnd, processId);
            if (window.Title != null)
            {
                window.ProcessName = "Windows 10 App";
                windows.Add(window);
            }
        }
    }

    static bool IsKnownWindows10Exception(WindowEntry window)
    {
        if (window.ProcessName == "MicrosoftEdge")
            return true;

        if (window.ProcessName == "MicrosoftEdgeCP")
        {
            if (window.Title == "CoreInput")
                return true;

            if (window.Title == "about:tabs")
                return true;
        }

        return false;
    }

    static bool ClassEligibleForActivation(string className)
    {
        if (Array.IndexOf(WindowsClassNamesToSkip, className) > -1)
            return false;

        if (className.StartsWith("WMP9MediaBarFlyout")) //WMP's "now playing" taskbar-toolbar
            return false;

        return true;
    }

    static string GetClassName(IntPtr hWnd)
    {
        var classNameStringBuilder = new StringBuilder(256);
        var length = GetClassName(hWnd, classNameStringBuilder, classNameStringBuilder.Capacity);
        return length == 0 ? null : classNameStringBuilder.ToString();
    }

    static void UpdateProcessName(IWindowEntry window)
    {
        using var process = Process.GetProcessById((int) window.ProcessId);
        window.ProcessName = process.ProcessName;
    }

    static bool IsKnownException(IWindowEntry window)
    {
        if (window.ProcessName == "Fiddler" && window.Title == "SSFiddlerMsgWin")
            return true;

        return false;
    }

    static readonly string[] WindowsClassNamesToSkip =
    {
        "Shell_TrayWnd", // Task Bar
        "DV2ControlHost", // Start Menu
        "MsgrIMEWindowClass", // Messenger
        "SysShadow", // Messenger
        "Button", // UI component, e.g. Start Menu button
        "Windows.UI.Core.CoreWindow", // Windows 10 Store Apps
        "Frame Alternate Owner", // Edge
        "MultitaskingViewFrame", // The original Win + Tab view
    };

    static bool HWndEligibleForActivation(IntPtr hWnd, IntPtr lShellWindow)
    {
        if (hWnd == lShellWindow)
            return false;

        var root = GetAncestor(hWnd, GetAncestorFlags.GetRootOwner);

        if (GetLastVisibleActivePopUpOfWindow(root) != hWnd)
            return false;

        return true;
    }

    static IntPtr GetLastVisibleActivePopUpOfWindow(IntPtr window)
    {
        var level = MaxLastActivePopupIterations;
        var currentWindow = window;
        while (level-- > 0)
        {
            var lastPopUp = GetLastActivePopup(currentWindow);

            if (IsWindowVisible(lastPopUp))
                return lastPopUp;

            if (lastPopUp == currentWindow)
                return IntPtr.Zero;

            currentWindow = lastPopUp;
        }

        Console.WriteLine($"Could not find last active popup for window {window} after {MaxLastActivePopupIterations} iterations");
        return IntPtr.Zero;
    }
}
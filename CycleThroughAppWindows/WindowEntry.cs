using System;

namespace CycleThroughAppWindows;

public class WindowEntry : IWindowEntry
{
    public IntPtr HWnd { get; set; }
    public uint ProcessId { get; set; }
    public string Title { get; set; }
    public bool IsVisible { get; set; }
    public string ProcessName { get; set; }

    public bool IsSameWindow(IWindowEntry other)
    {
        if (other == null)
            return false;

        return ProcessId == other.ProcessId && HWnd == other.HWnd;
    }

    public override string ToString()
    {
        return $"{ProcessName} ({ProcessId}): \"{Title}\"";
    }
}
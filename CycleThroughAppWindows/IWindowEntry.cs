using System;

namespace CycleThroughAppWindows;

public interface IWindowEntry
{
    IntPtr HWnd { get; set; }
    uint ProcessId { get; set; }
    string ProcessName { get; set; }
    string Title { get; set; }
    bool IsVisible { get; set; }
}
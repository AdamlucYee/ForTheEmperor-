using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace ForTheEmperor;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("shell32.dll")] internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    [DllImport("shell32.dll", EntryPoint = "SHChangeNotify", CharSet = CharSet.Unicode)]
    private static extern void NotifyPath(uint eventId, uint flags, string item1, IntPtr item2);

    internal static void NotifyRecycled(string path)
    {
        // IFileOperation on a short-lived STA can leave Explorer's desktop view stale.
        // PATHW | FLUSH sends the event and waits for Shell consumers to receive it.
        // Never send a delete notification unless the source actually disappeared.
        if (File.Exists(path)) return;
        NotifyPath(0x00000004, 0x0005 | 0x1000, path, IntPtr.Zero); // SHCNE_DELETE
        string? parent = Path.GetDirectoryName(path);
        if (parent != null) NotifyPath(0x00001000, 0x0005 | 0x1000, parent, IntPtr.Zero); // SHCNE_UPDATEDIR
    }
    internal static void RefreshDesktop()
    {
        foreach (var folder in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path)) NotifyPath(0x00001000, 0x0005 | 0x1000, path, IntPtr.Zero);
        }
    }

    private static IntPtr DesktopList()
    {
        IntPtr view = FindWindowEx(FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
        if (view == IntPtr.Zero) EnumWindows((h, _) => { view = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null); return view == IntPtr.Zero; }, IntPtr.Zero);
        return view == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
    }

    internal static bool? DesktopIconExists(string path)
    {
        try
        {
            var list = DesktopList();
            if (list == IntPtr.Zero) return null;
            var root = AutomationElement.FromHandle(list);
            var condition = new OrCondition(new PropertyCondition(AutomationElement.NameProperty, Path.GetFileName(path)), new PropertyCondition(AutomationElement.NameProperty, Path.GetFileNameWithoutExtension(path)));
            return root.FindFirst(TreeScope.Children, condition) != null;
        }
        catch (Exception ex) { App.Log(L.T("桌面视图检查：") + ex.Message); return null; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION info);
    [StructLayout(LayoutKind.Sequential)] private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IdHigh, IdLow;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SHFILEINFO
    {
        public IntPtr Icon; public int IconIndex; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attrs, out SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

    internal static BitmapSource? FileIcon(string path)
    {
        if (SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
        try { var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image; }
        finally { DestroyIcon(info.Icon); }
    }

    public static (uint Volume, ulong Id) FileIdentity(string path)
    {
        using var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) throw new IOException(L.T("无法验证文件身份。"), Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        return (info.Volume, ((ulong)info.IdHigh << 32) | info.IdLow);
    }
    public static void ToolWindow(Window w, bool clickThrough = false)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x80 | 0x08000000 | (clickThrough ? 0x20 : 0));
    }
    public static Point Cursor() { GetCursorPos(out var p); return new(p.X, p.Y); }
    public static Point Foot(Window w)
    {
        GetWindowRect(new WindowInteropHelper(w).Handle, out var r);
        return new((r.Left + r.Right) / 2.0, r.Bottom - 22 * Dpi(w));
    }
    public static double Dpi(Window w) => Math.Max(96, GetDpiForWindow(new WindowInteropHelper(w).Handle)) / 96.0;
    public static void MoveFoot(Window w, Point p)
    {
        var h = new WindowInteropHelper(w).Handle;
        GetWindowRect(h, out var r);
        SetWindowPos(h, IntPtr.Zero, (int)(p.X - (r.Right - r.Left) / 2.0), (int)(p.Y - (r.Bottom - r.Top) + 22 * Dpi(w)), 0, 0, 0x15);
    }
    // UI Automation provides screen coordinates without reading Explorer process memory.
    public static Point LocateDesktopIcon(string path, Point fallback)
    {
        try
        {
            string display = Path.GetFileName(path);
            if (SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x200) != IntPtr.Zero) display = info.DisplayName;
            var list = DesktopList();
            if (list == IntPtr.Zero) return fallback;
            var root = AutomationElement.FromHandle(list);
            var matches = root.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, display));
            // Ambiguous display names (hidden extensions) must never resolve to an arbitrary icon.
            if (matches.Count != 1) return fallback;
            Rect r = matches[0].Current.BoundingRectangle;
            if (!r.IsEmpty && r.Width > 0 && r.Height > 0) return new(r.Left + r.Width / 2, r.Top + Math.Min(r.Height / 2, 32));
        }
        catch (Exception ex) { App.Log(L.T("图标定位回退至菜单位置：") + ex.Message); }
        return fallback;
    }
}

// COM declarations follow the native vtable order. Recycle-on-delete is mandatory.
internal static class Recycle
{
    public static void File(string path, Action? verify = null)
    {
        var op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
        IShellItem? item = null;
        var guard = new RecycleGuard { Verify = verify };
        try
        {
            Guid iid = typeof(IShellItem).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item));
            // Silent, no permanent-delete confirmation accepted, abort on errors, require recycle.
            op.SetOperationFlags(0x0004 | 0x0010 | 0x0040 | 0x0400 | 0x2000 | 0x4000 | 0x00080000 | 0x00100000 | 0x20000000);
            op.DeleteItem(item, guard);
            op.PerformOperations();
            op.GetAnyOperationsAborted(out bool aborted);
            if (aborted || !guard.Success || System.IO.File.Exists(path)) throw new IOException(L.T("未移入回收站，处决已取消。") + (guard.Error < 0 ? L.T(" 错误：0x") + guard.Error.ToString("X8") : ""));
            Native.NotifyRecycled(path);
        }
        finally { if (item != null) Marshal.ReleaseComObject(item); Marshal.ReleaseComObject(op); }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHCreateItemFromParsingName(string path, IntPtr bind, ref Guid iid, out IShellItem item);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem parent);
    void GetDisplayName(uint sigdn, out IntPtr name);
    void GetAttributes(uint mask, out uint attrs);
    void Compare(IShellItem other, uint hint, out int order);
}

[ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    void Advise(IFileOperationProgressSink sink, out uint cookie);
    void Unadvise(uint cookie);
    void SetOperationFlags(uint flags);
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
    void SetProgressDialog(IntPtr dialog);
    void SetProperties(IntPtr properties);
    void SetOwnerWindow(uint hwnd);
    void ApplyPropertiesToItem(IShellItem item);
    void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object items);
    void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink? sink);
    void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string name);
    void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink? sink);
    void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
    void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink? sink);
    void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
    void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
    void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);
    void NewItem(IShellItem destination, uint attrs, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IFileOperationProgressSink? sink);
    void PerformOperations();
    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
}

[ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileOperationProgressSink
{
    [PreserveSig] int StartOperations();
    [PreserveSig] int FinishOperations(int result);
    [PreserveSig] int PreRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr newItem);
    [PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr newItem);
    [PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr newItem);
    [PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
    [PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr newItem);
    [PreserveSig] int PreNewItem(uint flags, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostNewItem(uint flags, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attrs, int result, IntPtr newItem);
    [PreserveSig] int UpdateProgress(uint total, uint done);
    [PreserveSig] int ResetTimer();
    [PreserveSig] int PauseTimer();
    [PreserveSig] int ResumeTimer();
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RecycleGuard : IFileOperationProgressSink
{
    public bool Success; public int Error;
    internal Action? Verify;
    public int StartOperations() => 0;
    public int FinishOperations(int result) { Error = result; return 0; }
    // TSF_DELETE_RECYCLE_IF_POSSIBLE must be present. Refuse a nuke operation.
    public int PreDeleteItem(uint flags, IntPtr item)
    {
        if ((flags & 0x80) == 0) return unchecked((int)0x80004004);
        try { Verify?.Invoke(); return 0; }
        catch { return unchecked((int)0x80004004); }
    }
    public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr newItem) { Error = result; Success = result >= 0 && newItem != IntPtr.Zero; return 0; }
    public int PreRenameItem(uint f, IntPtr i, string n) => 0;
    public int PostRenameItem(uint f, IntPtr i, string n, int r, IntPtr ni) => 0;
    public int PreMoveItem(uint f, IntPtr i, IntPtr d, string n) => 0;
    public int PostMoveItem(uint f, IntPtr i, IntPtr d, string n, int r, IntPtr ni) => 0;
    public int PreCopyItem(uint f, IntPtr i, IntPtr d, string n) => 0;
    public int PostCopyItem(uint f, IntPtr i, IntPtr d, string n, int r, IntPtr ni) => 0;
    public int PreNewItem(uint f, IntPtr d, string n) => 0;
    public int PostNewItem(uint f, IntPtr d, string n, string t, uint a, int r, IntPtr ni) => 0;
    public int UpdateProgress(uint t, uint d) => 0;
    public int ResetTimer() => 0;
    public int PauseTimer() => 0;
    public int ResumeTimer() => 0;
}

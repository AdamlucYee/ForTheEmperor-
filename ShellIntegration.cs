using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ForTheEmperor;

internal static class ShellMenu
{
    internal const string KeyPath = @"Software\Classes\*\shell\ForTheEmperor.Execute";
    public static void Register(string owner)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("", L.T("以帝皇之名处决"));
        key.SetValue("Icon", "\"" + Environment.ProcessPath + "\",0");
        key.SetValue("MultiSelectModel", "Single");
        key.SetValue("FTAOwner", owner);
        string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        key.SetValue("AppliesTo", string.Join(" OR ", roots.Where(x => x.Length > 0).Select(x => "System.ItemFolderPathDisplay:=\"" + x.Replace("\"", "\"\"") + "\"")));
        using var command = key.CreateSubKey("command");
        command.SetValue("", "\"" + Environment.ProcessPath + "\" --execute \"%1\"");
        Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static void Remove(string? owner = null)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
        {
            if (key == null || (owner != null && !Equals(key.GetValue("FTAOwner"), owner))) return;
        }
        Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
        Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static void Watch(int pid, long start, string owner)
    {
        try { using var p = Process.GetProcessById(pid); if (p.StartTime.ToUniversalTime().Ticks == start) p.WaitForExit(); }
        catch (ArgumentException) { }
        try { Remove(owner); } catch { }
    }
}

internal sealed record Command(string Action, string? Path = null, double X = 0, double Y = 0);

internal static class Bridge
{
    public static string Id => "ForTheEmperor." + WindowsIdentity.GetCurrent().User!.Value + "." + Process.GetCurrentProcess().SessionId;
    public static async Task<string> Send(Command command)
    {
        using var timeout = new CancellationTokenSource(4000);
        using var pipe = new NamedPipeClientStream(".", Id, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);
        await Write(pipe, JsonSerializer.Serialize(command), timeout.Token);
        return await Read(pipe, timeout.Token);
    }
    public static async Task Listen(Func<Command, Task<string>> receive, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(Id, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
                deadline.CancelAfter(3000);
                var command = JsonSerializer.Deserialize<Command>(await Read(pipe, deadline.Token)) ?? throw new IOException(L.T("无效指令。"));
                string reply = await receive(command);
                await Write(pipe, reply, deadline.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { App.Log("IPC：" + ex.Message); }
        }
    }
    private static async Task Write(Stream stream, string value, CancellationToken ct)
    {
        byte[] data = Encoding.UTF8.GetBytes(value);
        if (data.Length > 32768) throw new IOException(L.T("指令过长。"));
        await stream.WriteAsync(BitConverter.GetBytes(data.Length), ct);
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }
    private static async Task<string> Read(Stream stream, CancellationToken ct)
    {
        byte[] header = new byte[4]; await stream.ReadExactlyAsync(header, ct);
        int length = BitConverter.ToInt32(header);
        if (length < 1 || length > 32768) throw new IOException(L.T("无效指令长度。"));
        byte[] data = new byte[length]; await stream.ReadExactlyAsync(data, ct);
        return Encoding.UTF8.GetString(data);
    }
}

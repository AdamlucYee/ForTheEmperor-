using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ForTheEmperor;

public sealed record Chapter(string Name, string English, string Color, string Trim, string Mark, string Weapon, string Quote)
{
    public static readonly Chapter[] All = {
        new("极限战士", "ULTRAMARINES", "#245BB4", "#D7B772", "Ω", "爆弹枪精准射击", "Courage and honour."),
        new("帝国之拳", "IMPERIAL FISTS", "#E5B630", "#292C32", "✊", "重拳砸碎", "Primarch-Progenitor, to your glory!"),
        new("太空野狼", "SPACE WOLVES", "#7997A6", "#D6B264", "ᛉ", "链锯剑冲锋", "For Russ and the Allfather!"),
        new("圣血天使", "BLOOD ANGELS", "#BB3444", "#E2BB73", "♦", "跳跃劈砍", "For Sanguinius!"),
        new("黑色圣堂", "BLACK TEMPLARS", "#343940", "#DFDDD1", "✠", "链锯剑处决", "No pity. No remorse. No fear."),
        new("火蜥蜴", "SALAMANDERS", "#328A57", "#E1B95D", "♨", "火焰喷射", "Into the fires of battle!"),
        new("暗鸦守卫", "RAVEN GUARD", "#363F4F", "#D8E1E8", "◆", "快速突袭", "Victorus aut Mortis."),
        new("钢铁之手", "IRON HANDS", "#4C5663", "#B5C9CF", "⚙", "机械扫描后处决", "The flesh is weak.")
    };
    public Brush Armor => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));
    public Brush Gold => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Trim));
}

public enum Phase { Idle, Alert, Turn, Run, Arrive, Attack, Execution, Recover, Return }

// The only commit point is in Execution, after the attack has landed.
public sealed class Mission
{
    public Phase Phase { get; private set; } = Phase.Idle;
    public double Time { get; private set; }
    public double Progress => Math.Clamp(Time / Duration(Phase), 0, 1);
    public bool Busy => Phase != Phase.Idle;
    public bool Committed { get; private set; }
    public bool Cancelled { get; private set; }
    public double RunDuration { get; set; } = 1.5;
    public int ChapterIndex { get; set; }
    public double ImpactAt => CombatStyle.All[Math.Clamp(ChapterIndex, 0, 7)].ImpactAt;
    public event Action<Phase>? Changed;
    public event Action? Impact;
    public void Start()
    {
        if (Busy) throw new InvalidOperationException(L.T("战士正在执行任务。"));
        Committed = false; Cancelled = false; Set(Phase.Alert);
    }
    public bool Cancel()
    {
        if (!Busy || Committed || Phase == Phase.Return) return false;
        Cancelled = true; Set(Phase.Return); return true;
    }
    public void Tick(double seconds)
    {
        if (!Busy) return;
        Time += Math.Clamp(seconds, 0, .1);
        if (Phase == Phase.Execution && Time >= ImpactAt && !Committed)
        {
            Committed = true;
            Impact?.Invoke();
        }
        if (Time >= Duration(Phase)) Set(Phase == Phase.Return ? Phase.Idle : Phase + 1);
    }
    public double Duration(Phase phase) => phase switch {
        Phase.Alert => .55, Phase.Turn => .25, Phase.Run => RunDuration,
        Phase.Arrive => .25, Phase.Attack => CombatStyle.All[Math.Clamp(ChapterIndex, 0, 7)].Windup, Phase.Execution => CombatStyle.All[Math.Clamp(ChapterIndex, 0, 7)].Execution,
        Phase.Recover => .85, Phase.Return => RunDuration, _ => 1
    };
    private void Set(Phase phase) { Phase = phase; Time = 0; Changed?.Invoke(phase); }
}

public sealed class Preferences
{
    public string? Language { get; set; }
    public int Chapter { get; set; }
    public bool Sound { get; set; }
    public double Scale { get; set; } = 1;
    public bool MenuEnabled { get; set; } = true;
    private static string FileName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForTheEmperor", "settings.json");
    public static Preferences Load()
    {
        try { return Parse(File.ReadAllText(FileName)); }
        catch { return new(); }
    }
    internal static Preferences Parse(string json)
    {
        var p = JsonSerializer.Deserialize<Preferences>(json) ?? new();
        p.Chapter = Math.Clamp(p.Chapter, 0, 7);
        p.Scale = double.IsFinite(p.Scale) ? Math.Clamp(p.Scale, .7, 1.5) : 1;
        if (!L.Supported(p.Language)) p.Language = null;
        return p;
    }
    public void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(FileName)!); File.WriteAllText(FileName + ".tmp", JsonSerializer.Serialize(this)); File.Move(FileName + ".tmp", FileName, true); }
        catch (Exception ex) { App.Log(L.T("保存设置失败：") + ex.Message); }
    }
}

public sealed record FileStamp(string Path, long Length, long WriteTime, long CreationTime, uint Volume, ulong FileId);

public sealed class FilePolicy
{
    private readonly string[] roots;
    public FilePolicy(params string[] allowedRoots) { roots = allowedRoots.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Path.GetFullPath).ToArray(); }
    public static FilePolicy Desktop() => new(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
    public FileStamp Capture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || path.StartsWith(@"\\?\") || path.IndexOf(':', 2) >= 0) throw new IOException(L.T("仅支持本机桌面上的普通文件。"));
        string full = Path.GetFullPath(path);
        if (!roots.Any(root => string.Equals(Path.GetDirectoryName(full)?.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) throw new IOException(L.T("只能处决桌面上的文件，不包括子文件夹内的文件。"));
        var info = new FileInfo(full);
        if (!info.Exists || (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0) throw new IOException(L.T("文件不存在，或是系统文件、目录、重解析点；任务已取消。"));
        if (string.Equals(full, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("战士不能处决自己。"));
        var id = Native.FileIdentity(full);
        return new(full, info.Length, info.LastWriteTimeUtc.Ticks, info.CreationTimeUtc.Ticks, id.Volume, id.Id);
    }
    public void Validate(FileStamp stamp)
    {
        if (Capture(stamp.Path) != stamp) throw new IOException(L.T("动画期间文件发生变化；已取消删除。"));
    }
}

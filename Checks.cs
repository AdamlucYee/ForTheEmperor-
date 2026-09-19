using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ForTheEmperor;

internal static class Checks
{
    internal static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Test(string name, Action test)
        {
            try { test(); results.Add("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name + ": " + ex); }
        }
        void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
        Test("Language preferences migrate old settings and roundtrip without losing pet options", () => {
            var old = Preferences.Parse("{\"Chapter\":5,\"Scale\":1.3,\"Sound\":true,\"MenuEnabled\":false}");
            Assert(old.Language == null && old.Chapter == 5 && old.Scale == 1.3 && old.Sound && !old.MenuEnabled);
            old.Language = "en";
            var saved = Preferences.Parse(System.Text.Json.JsonSerializer.Serialize(old));
            Assert(saved.Language == "en" && saved.Chapter == 5 && saved.Scale == 1.3 && !saved.MenuEnabled);
            Assert(Preferences.Parse("{\"Language\":\"unknown\"}").Language == null);
        });
        Test("English covers all chapters, combat details, errors and dynamic reports", () => {
            string before = L.Language;
            try {
                L.SetLanguage("en");
                bool HasChinese(string s) => s.Any(c => c >= '\u4e00' && c <= '\u9fff');
                foreach (var entry in L.Entries) Assert(!string.IsNullOrWhiteSpace(entry.Value) && !HasChinese(L.T(entry.Key)), entry.Key);
                foreach (var c in Chapter.All) Assert(!HasChinese(L.T(c.Name)) && !HasChinese(L.T(c.Weapon)));
                foreach (var c in CombatStyle.All) Assert(!HasChinese(L.T(c.Detail)));
                Assert(L.T("已移入回收站：中文文档.txt") == "Moved to Recycle Bin: 中文文档.txt");
                Assert(L.T("处决未完成：无法验证文件身份。") == "Execution failed: Could not verify the file's identity.");
                try { new FilePolicy(output).Capture("relative.txt"); throw new Exception("Expected rejection"); }
                catch (IOException ex) { Assert(!HasChinese(ex.Message)); }
                L.SetLanguage("zh-CN"); Assert(L.T("以帝皇之名处决") == "以帝皇之名处决");
            } finally { L.SetLanguage(before); }
        });
        Test("Command panel switches languages live and renders both layouts", () => {
            string before = L.Language;
            var window = new CommandWindow(null) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -2500, Top = -2500, ShowActivated = false };
            try {
                window.Show();
                var combo = (System.Windows.Controls.ComboBox)window.FindName("LanguageCombo");
                foreach (string language in new[] { "en", "zh-CN", "en" }) {
                    combo.SelectedIndex = language == "en" ? 1 : 0;
                    window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    window.UpdateLayout();
                    Assert(L.Language == language);
                    Assert(window.Title == L.T("FOR THE EMPEROR · 桌面战士"));
                    Assert((string)((System.Windows.Controls.Button)window.FindName("DemoButton")).Content == L.T("▶   演练处决动画"));
                    Assert(((System.Windows.Controls.TextBlock)window.FindName("ChapterName")).Text == L.T("极限战士"));
                    var rootView = (FrameworkElement)window.Content;
                    Save(rootView, (int)rootView.ActualWidth, (int)rootView.ActualHeight, Path.Combine(output, "panel-" + language + ".png"));
                }
                var picker = new LanguageWindow("en") { WindowStartupLocation = WindowStartupLocation.Manual, Left = -2500, Top = -2500, ShowActivated = false };
                picker.Show(); picker.UpdateLayout();
                var content = (FrameworkElement)picker.Content;
                Save(content, (int)content.ActualWidth, (int)content.ActualHeight, Path.Combine(output, "language-picker.png"));
                picker.Close();
            } finally { window.Close(); L.SetLanguage(before); }
        });
        Test("Full sequence commits once, after attack, returns to idle", () => {
            var mission = new Mission(); var phases = new List<Phase>(); int impacts = 0;
            mission.Changed += p => phases.Add(p);
            mission.Impact += () => { Assert(mission.Phase == Phase.Execution); Assert(mission.Time >= mission.ImpactAt); impacts++; };
            mission.Start(); for (int i = 0; i < 1600; i++) mission.Tick(.02);
            Assert(!mission.Busy && impacts == 1);
            Assert(phases.SequenceEqual(new[] { Phase.Alert, Phase.Turn, Phase.Run, Phase.Arrive, Phase.Attack, Phase.Execution, Phase.Recover, Phase.Return, Phase.Idle }));
        });
        Test("Cancellation in every pre-impact phase prevents deletion", () => {
            foreach (Phase phase in new[] { Phase.Alert, Phase.Turn, Phase.Run, Phase.Arrive, Phase.Attack, Phase.Execution })
            {
                var m = new Mission(); int count = 0; m.Impact += () => count++; m.Start();
                for (int i = 0; i < 1500 && m.Phase != phase; i++) m.Tick(.02);
                Assert(m.Phase == phase); Assert(m.Cancel());
                for (int i = 0; i < 1500; i++) m.Tick(.02);
                Assert(!m.Busy && count == 0 && m.Cancelled, phase.ToString());
            }
        });
        Test("Busy mission rejects a second request; late cancel cannot pretend to undo", () => {
            var m = new Mission(); m.Start(); bool rejected = false;
            try { m.Start(); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected);
            for (int i = 0; i < 1500 && !m.Committed; i++) m.Tick(.02);
            Assert(m.Committed && !m.Cancel());
        });
        string root = Path.GetFullPath(Path.Combine(output, "disposable-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Test("File policy allows only direct files and detects changed/replaced targets", () => {
            var policy = new FilePolicy(root); string path = Path.Combine(root, "毕业论文 test.txt");
            File.WriteAllText(path, "disposable fixture"); var stamp = policy.Capture(path); policy.Validate(stamp);
            File.AppendAllText(path, " changed");
            bool rejected = false; try { policy.Validate(stamp); } catch (IOException) { rejected = true; } Assert(rejected);
            File.Delete(path); File.WriteAllText(path, "new file");
            rejected = false; try { policy.Validate(stamp); } catch (IOException) { rejected = true; } Assert(rejected);
            foreach (string invalid in new[] { root, Path.Combine(root, "child", "file.txt"), "relative.txt", @"\\server\share\file.txt", path + ":stream", Environment.ProcessPath! })
            {
                rejected = false; try { policy.Capture(invalid); } catch (IOException) { rejected = true; }
                Assert(rejected, "Not rejected: " + invalid);
            }
            File.Delete(path);
        });
        Test("Recycle guard refuses permanent-delete callbacks", () => {
            var guard = new RecycleGuard(); Assert(guard.PreDeleteItem(0, IntPtr.Zero) < 0); Assert(guard.PreDeleteItem(0x80, IntPtr.Zero) == 0);
            guard.PostDeleteItem(0x80, IntPtr.Zero, 0, IntPtr.Zero); Assert(!guard.Success);
            guard.Verify = () => throw new IOException("Changed file"); Assert(guard.PreDeleteItem(0x80, IntPtr.Zero) < 0);
        });
        Test("Native IFileOperation recycles only a generated disposable fixture", () => {
            string path = Path.Combine(root, "ForTheEmperor-recycle-test-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, "Disposable test fixture generated by ForTheEmperor --self-test. Safe to remove from Recycle Bin.");
            var policy = new FilePolicy(root); var stamp = policy.Capture(path); policy.Validate(stamp);
            Recycle.File(path); Assert(!File.Exists(path));
        });
        Test("Current-user named pipe roundtrip preserves Unicode and spaces", () => {
            using var stop = new CancellationTokenSource();
            Command? received = null;
            var server = Task.Run(() => Bridge.Listen(c => { received = c; return Task.FromResult("OK"); }, stop.Token));
            var command = new Command("execute", @"C:\桌面\毕业 论文.txt", -512.5, 350);
            try { Assert(Bridge.Send(command).GetAwaiter().GetResult() == "OK"); Assert(received == command); }
            finally { stop.Cancel(); server.GetAwaiter().GetResult(); }
        });
        Test("All eight chapter attack variants render", () => {
            foreach (int i in Enumerable.Range(0, 8)) foreach (var phase in new[] { Phase.Idle, Phase.Run, Phase.Attack, Phase.Execution })
            {
                var view = new MarineView { Width = 240, Height = 260, ChapterIndex = i, Phase = phase, Clock = 2.7, Progress = .4, StateTime = .75 };
                view.Measure(new(240, 260)); view.Arrange(new(0, 0, 240, 260));
                var bmp = new RenderTargetBitmap(240, 260, 96, 96, PixelFormats.Pbgra32); bmp.Render(view);
            }
        });
        Test("All chapter impact times are distinct, cancelable, and commit exactly once", () => {
            for (int chapter = 0; chapter < 8; chapter++)
            {
                var m = new Mission { ChapterIndex = chapter }; int impacts = 0; m.Impact += () => { Assert(m.Time >= m.ImpactAt); impacts++; };
                m.Start(); for (int n = 0; n < 2500; n++) m.Tick(.02);
                Assert(impacts == 1 && !m.Busy);
                var cancelled = new Mission { ChapterIndex = chapter }; cancelled.Impact += () => throw new Exception("Cancelled mission committed");
                cancelled.Start(); while (cancelled.Phase != Phase.Execution) cancelled.Tick(.01);
                while (cancelled.Time < cancelled.ImpactAt - .025) cancelled.Tick(.01);
                Assert(cancelled.Cancel()); for (int n = 0; n < 800; n++) cancelled.Tick(.02);
                Assert(!cancelled.Committed && !cancelled.Busy);
            }
        });
        Test("Every sprite atlas contains sixteen nonempty transparent frames", () => {
            for (int chapter = 0; chapter < 8; chapter++) for (int frame = 0; frame < 16; frame++)
            {
                var bmp = SpriteAtlas.Frame(chapter, frame); byte[] data = new byte[bmp.PixelWidth * bmp.PixelHeight * 4]; bmp.CopyPixels(data, bmp.PixelWidth * 4, 0);
                int opaque = 0; for (int i = 3; i < data.Length; i += 4) if (data[i] > 0) opaque++;
                Assert(opaque > 600 && opaque < bmp.PixelWidth * bmp.PixelHeight * .9, $"Invalid alpha in {chapter}/{frame}");
            }
        });
        Test("Distinct particle effects render through windup, impact and recovery", () => {
            for (int chapter = 0; chapter < 8; chapter++) foreach (double time in new[] { -.4, .05, .2, .45, .9, 1.5, 2.2 })
            {
                var e = new EffectView { Width = 520, Height = 440, ChapterIndex = chapter, Phase = time < 0 ? Phase.Attack : Phase.Execution, Progress = .6, Time = time, Clock = 2, Demo = true, Impact = time > CombatStyle.All[chapter].ImpactAt, TargetRemoved = time > CombatStyle.All[chapter].ImpactAt };
                e.Measure(new(520, 440)); e.Arrange(new(0, 0, 520, 440));
                var bmp = new RenderTargetBitmap(520, 440, 96, 96, PixelFormats.Pbgra32); bmp.Render(e);
            }
        });
        File.WriteAllLines(Path.Combine(output, "test-results.txt"), results);
        return results.Any(x => x.StartsWith("FAIL")) ? 1 : 0;
    }
    internal static int ProbeDesktop(string path, bool expected)
    {
        var end = DateTime.UtcNow.AddSeconds(5); bool? actual;
        do { actual = Native.DesktopIconExists(path); if (actual == expected) return 0; Thread.Sleep(100); } while (DateTime.UtcNow < end);
        return actual == null ? 2 : 1;
    }
    internal static int RenderAnimation(string output)
    {
        Directory.CreateDirectory(output);
        var encoder = new GifBitmapEncoder();
        for (int f = 0; f < 86; f++)
        {
            var visual = CombatFrame(f / 15.0, false);
            var bitmap = new RenderTargetBitmap(1440, 720, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var meta = new BitmapMetadata("gif");
            meta.SetQuery("/grctlext/Delay", (ushort)7);
            meta.SetQuery("/grctlext/Disposal", (byte)2);
            if (f == 0)
            {
                meta.SetQuery("/appext/application", System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                meta.SetQuery("/appext/data", new byte[] { 3, 1, 0, 0, 0 });
            }
            encoder.Frames.Add(BitmapFrame.Create(bitmap, null, meta, null));
        }
        using (var file = new MemoryStream()) { encoder.Save(file); File.WriteAllBytes(Path.Combine(output, "combat-preview.gif"), GifTiming.Set(file.ToArray(), 7)); }
        Save(CombatFrame(0, true), 1440, 720, Path.Combine(output, "combat-preview.png"));
        return 0;
    }
    private static DrawingVisual CombatFrame(double clock, bool contactSheet)
    {
        var scene = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(scene, BitmapScalingMode.NearestNeighbor);
        using var dc = scene.RenderOpen();
        dc.DrawRectangle(MarineView.B("#111A22"), null, new(0, 0, 1440, 720));
        for (int i = 0; i < 8; i++)
        {
            double x = i % 4 * 360, y = i / 4 * 360;
            var style = CombatStyle.All[i];
            double t = contactSheet ? 1.05 + style.Windup + style.ImpactAt + .13 : clock;
            Phase phase; double stateTime, duration;
            if (t < .35) { phase = Phase.Idle; stateTime = t; duration = .35; }
            else if (t < 1.05) { phase = Phase.Run; stateTime = t - .35; duration = .7; }
            else if (t < 1.05 + style.Windup) { phase = Phase.Attack; stateTime = t - 1.05; duration = style.Windup; }
            else if (t < 1.05 + style.Windup + style.Execution) { phase = Phase.Execution; stateTime = t - 1.05 - style.Windup; duration = style.Execution; }
            else { phase = Phase.Recover; stateTime = t - 1.05 - style.Windup - style.Execution; duration = 1; }
            double executionTime = t - 1.05 - style.Windup;
            var marine = new MarineView { Width = 370, Height = 260, ChapterIndex = i, Phase = phase, StateTime = stateTime, Progress = Math.Clamp(stateTime / duration, 0, 1), Clock = t };
            marine.Measure(new(370, 260)); marine.Arrange(new(0, 0, 370, 260));
            var effect = new EffectView { Width = 520, Height = 440, ChapterIndex = i, Phase = phase, Time = executionTime, Progress = marine.Progress, Clock = t, Demo = true, Impact = executionTime >= style.ImpactAt, TargetRemoved = executionTime >= style.ImpactAt, Source = new(90 - style.StandOff, style.FootOffset - style.MuzzleHeight) };
            effect.Measure(new(520, 440)); effect.Arrange(new(0, 0, 520, 440));
            dc.PushClip(new RectangleGeometry(new(x + 2, y + 2, 356, 356)));
            dc.DrawRectangle(MarineView.B("#18242D"), new Pen(MarineView.B("#35434C"), 1), new(x + 6, y + 6, 348, 348));
            MarineView.Text(dc, Chapter.All[i].Name, new(x + 18, y + 15), 17, "#E0CEA1", true);
            MarineView.Text(dc, Chapter.All[i].English, new(x + 18, y + 42), 9, "#7F98A8");
            dc.DrawImage(Render(marine, 370, 260), new(x + 280 - style.StandOff - 185, y + 52, 370, 260));
            dc.DrawImage(Render(effect, 520, 440), new(x + 20, y + 70 - style.FootOffset, 520, 440));
            MarineView.Text(dc, style.Detail, new(x + 18, y + 322), 10, "#9CAEB5");
            dc.Pop();
        }
        return scene;
    }
    private static RenderTargetBitmap Render(Visual view, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); return bitmap;
    }
    internal static int Render(string output)
    {
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new CommandWindow(null) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
        window.Show(); window.UpdateLayout();
        var root = (FrameworkElement)window.Content;
        Save(root, (int)root.ActualWidth, (int)root.ActualHeight, Path.Combine(output, "command-panel.png"));
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(MarineView.B("#151E24"), null, new(0, 0, 1040, 640));
            for (int i = 0; i < 8; i++)
            {
                double x = 20 + i % 4 * 255, y = 15 + i / 4 * 310;
                var v = new MarineView { Width = 240, Height = 260, ChapterIndex = i };
                v.Measure(new(240, 260)); v.Arrange(new(0, 0, 240, 260));
                dc.DrawRectangle(new VisualBrush(v), null, new(x, y, 240, 260));
                MarineView.Text(dc, Chapter.All[i].Name, new(x + 80, y + 262), 15, "#DFCC9F", true);
                MarineView.Text(dc, Chapter.All[i].Weapon, new(x + 60, y + 286), 11, "#95A5AA");
            }
        }
        Save(drawing, 1040, 640, Path.Combine(output, "chapters.png"));
        window.Close(); app.Shutdown(); return 0;
    }
    private static void Save(Visual view, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}

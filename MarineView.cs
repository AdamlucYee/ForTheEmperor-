using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ForTheEmperor;

public sealed class MarineView : FrameworkElement
{
    public int ChapterIndex { get; set; }
    public Phase Phase { get; set; }
    public double Clock { get; set; }
    public double Progress { get; set; }
    public double StateTime { get; set; }
    public double VerticalReserve { get; set; }
    public int Facing { get; set; } = 1;
    public string? Bubble { get; set; }
    private static readonly Dictionary<string, Brush> Brushes = new();
    public MarineView() { RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); SnapsToDevicePixels = true; }
    internal static Brush B(string hex)
    {
        if (!Brushes.TryGetValue(hex, out var b)) { b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); Brushes.Add(hex, b); }
        return b;
    }
    internal static void Text(DrawingContext dc, string value, Point p, double size, string color, bool bold = false)
    {
        var t = new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, B(color), 1.0);
        dc.DrawText(t, p);
    }
    internal int FrameIndex()
    {
        var style = CombatStyle.All[ChapterIndex];
        if (Phase is Phase.Run or Phase.Return) return 2 + (int)(Clock * style.RunFps) % 8;
        if (Phase is Phase.Alert or Phase.Turn) return 1;
        if (Phase == Phase.Arrive) return 10;
        if (Phase == Phase.Attack) return Progress < .52 ? 10 : 11;
        if (Phase == Phase.Execution)
        {
            if (StateTime < style.ImpactAt) return ChapterIndex is 5 or 7 ? 12 : 11;
            double hit = StateTime - style.ImpactAt;
            return ChapterIndex switch {
                0 => new[] { 12, 13, 14, 13, 12, 13, 14, 15 }[Math.Min(7, (int)(hit * 8))],
                1 => hit < .15 ? 12 : hit < .65 ? 13 : 14,
                2 => hit < .16 ? 12 : hit < .46 ? 13 : 14,
                3 => hit < .1 ? 12 : hit < .6 ? 13 : 14,
                4 => hit < .12 ? 12 : hit < .9 ? 13 : 14,
                5 => hit < .5 ? 13 : 14,
                6 => hit < .12 ? 12 : hit < .28 ? 13 : 14,
                _ => hit < .32 ? 13 : 14
            };
        }
        if (Phase == Phase.Recover) return 15;
        return Clock % 9 > 7.8 ? 1 : 0;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = ActualHeight / (260 + VerticalReserve);
        dc.PushTransform(new TranslateTransform((ActualWidth - 240 * scale) / 2, VerticalReserve * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        bool run = Phase is Phase.Run or Phase.Return;
        var sprite = SpriteAtlas.Frame(ChapterIndex, FrameIndex());
        double p = Progress, hit = StateTime - CombatStyle.All[ChapterIndex].ImpactAt;
        double bob = run ? -Math.Round(Math.Abs(Math.Sin(Clock * CombatStyle.All[ChapterIndex].RunFps * Math.PI / 4)) * 2) : Math.Round(Math.Sin(Clock * 2) * .7);
        double offset = 0;
        if (Phase == Phase.Attack)
        {
            if (ChapterIndex == 3) bob -= Math.Round(Math.Sin(p * Math.PI / 2) * 36);
            if (ChapterIndex is 1 or 2 or 6) offset = -Math.Round(p * 9);
        }
        if (Phase == Phase.Execution)
        {
            if (ChapterIndex == 3) bob -= Math.Round(Math.Max(0, 1 - StateTime / .3) * 36);
            if (ChapterIndex is 1 or 2 or 6 && hit >= 0) offset = Math.Round(Math.Sin(Math.Min(1, hit / .65) * Math.PI) * (ChapterIndex == 6 ? 25 : 13));
            if (ChapterIndex == 0 && hit >= 0) offset -= Math.Round((1 - (hit * 8 % 1)) * 4);
            if (ChapterIndex is 4 or 5 && hit >= 0) offset += Math.Sin(Clock * 65) > 0 ? 1 : -1;
        }
        Pixel.Rect(dc, 84, 235, 72, 5, "#32090D13"); Pixel.Rect(dc, 96, 240, 48, 2, "#21090D13");
        dc.PushTransform(new ScaleTransform(Facing, 1, 120, 0));
        if (ChapterIndex == 6 && (run || Phase == Phase.Execution && hit is >= 0 and < .35))
        {
            for (int i = 3; i > 0; i--) { dc.PushOpacity(.09 + .04 * (3 - i)); dc.DrawImage(sprite, new(-63.75 - i * 11 + offset, -18 + bob, 367.5, 300)); dc.Pop(); }
        }
        if (run)
        {
            for (int i = 0; i < 5; i++) { double age = (Clock * 2 + i * .19) % 1; dc.PushOpacity((1 - age) * .3); Pixel.Rect(dc, 70 - age * 40, 233 - age * 10, 3 + age * 5, 2 + age * 2, "#9F9276"); dc.Pop(); }
        }
        dc.PushTransform(new TranslateTransform(offset, bob));
        dc.DrawImage(sprite, new Rect(-63.75, -18, 367.5, 300));
        if (ChapterIndex == 3 && (Phase == Phase.Attack && p > .45 || Phase == Phase.Execution && StateTime < .25))
        {
            for (int i = 0; i < 14; i++)
            {
                double u = Pixel.Noise(i * 7 + (int)(Clock * 16)), length = 8 + u * 27;
                Pixel.Rect(dc, 72 + (i % 3) * 4, 102 + i * 2, 4, length / 3, i % 3 == 0 ? "#FFFFDB" : i % 2 == 0 ? "#FFA34F" : "#DE5F37");
            }
        }
        dc.Pop(); dc.Pop();
        if (!string.IsNullOrEmpty(Bubble))
        {
            dc.DrawRoundedRectangle(B("#F01B252D"), new Pen(B("#B09A70"), 1), new(4, 0, 232, 35), 3, 3);
            var ft = new FormattedText(Bubble, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), Bubble.Length > 30 ? 9 : 11, MarineView.B("#F0E9D8"), 1) { MaxTextWidth = 218, MaxTextHeight = 29, TextAlignment = TextAlignment.Center, Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(ft, new(11, 5));
        }
        dc.Pop(); dc.Pop();
    }
    internal static void Skull(DrawingContext dc, double x, double y, double r, string color)
    {
        Pixel.Rect(dc, x - r, y - r * .6, r * 2, r * 1.3, color);
        Pixel.Rect(dc, x - r * .5, y, r, r, color);
        Pixel.Rect(dc, x - r * .65, y - 1, r * .45, r * .4, "#182029"); Pixel.Rect(dc, x + r * .2, y - 1, r * .45, r * .4, "#182029");
    }
}

internal static class Pixel
{
    internal static double Noise(int seed) { uint x = unchecked((uint)seed * 747796405u + 2891336453u); x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u; return ((x >> 22) ^ x) / (double)uint.MaxValue; }
    internal static void Rect(DrawingContext dc, double x, double y, double w, double h, string color) => dc.DrawRectangle(MarineView.B(color), null, new(Math.Round(x / 2) * 2, Math.Round(y / 2) * 2, Math.Max(2, Math.Round(w / 2) * 2), Math.Max(2, Math.Round(h / 2) * 2)));
    internal static void Line(DrawingContext dc, Point a, Point b, string color, double size = 2)
    {
        int n = Math.Max(1, (int)((b - a).Length / 2));
        for (int i = 0; i <= n; i++) { Point p = a + (b - a) * (i / (double)n); Rect(dc, p.X - size / 2, p.Y - size / 2, size, size, color); }
    }
    internal static void Ring(DrawingContext dc, double radius, double squash, string color, double size = 2)
    {
        for (int i = 0; i < 48; i++) { double a = i * Math.PI / 24; Rect(dc, Math.Cos(a) * radius, Math.Sin(a) * radius * squash, size, size, color); }
    }
}

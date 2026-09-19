using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ForTheEmperor;

internal static class SpriteAtlas
{
    internal static readonly string[] Names = { "ultramarines", "imperial-fists", "space-wolves", "blood-angels", "black-templars", "salamanders", "raven-guard", "iron-hands" };
    private static readonly Dictionary<int, BitmapSource[]> Cache = new();
    internal const int Resolution = 128;
    internal const int CanvasWidth = 196;
    internal const int CanvasHeight = 160;
    internal static BitmapSource Frame(int chapter, int frame)
    {
        if (!Cache.TryGetValue(chapter, out var frames))
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ForTheEmperor.assets.sprites." + Names[chapter] + ".png") ?? throw new InvalidOperationException(L.T("找不到战团精灵图：") + Names[chapter]);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var sheet = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            int width = sheet.PixelWidth, height = sheet.PixelHeight;
            byte[] pixels = new byte[width * height * 4]; sheet.CopyPixels(pixels, width * 4, 0);
            // Identify each connected character instead of clipping at a rigid grid line:
            // lunging blades can extend into the transparent margin of the next cell.
            int[] labels = new int[width * height], queue = new int[width * height];
            int[] selected = new int[16], areas = new int[16]; int label = 0;
            for (int start = 0; start < labels.Length; start++)
            {
                if (labels[start] != 0 || pixels[start * 4 + 3] < 100) continue;
                label++; int read = 0, count = 1; long sumX = 0, sumY = 0;
                queue[0] = start; labels[start] = label;
                while (read < count)
                {
                    int pos = queue[read++], x = pos % width, y = pos / width;
                    sumX += x; sumY += y;
                    void Visit(int next)
                    {
                        if (labels[next] == 0 && pixels[next * 4 + 3] >= 100) { labels[next] = label; queue[count++] = next; }
                    }
                    if (x > 0) Visit(pos - 1); if (x + 1 < width) Visit(pos + 1);
                    if (y > 0) Visit(pos - width); if (y + 1 < height) Visit(pos + width);
                }
                int cell = Math.Clamp((int)(sumY / (double)count * 4 / height), 0, 3) * 4 + Math.Clamp((int)(sumX / (double)count * 4 / width), 0, 3);
                if (count > areas[cell]) { areas[cell] = count; selected[cell] = label; }
            }
            frames = new BitmapSource[16];
            for (int f = 0; f < 16; f++)
            {
                // Sample onto a deliberate logical pixel grid once, retaining source alpha.
                int x0 = (int)Math.Round(f % 4 * width / 4.0), x1 = (int)Math.Round((f % 4 + 1) * width / 4.0);
                int y0 = (int)Math.Round(f / 4 * height / 4.0), y1 = (int)Math.Round((f / 4 + 1) * height / 4.0);
                if (areas[f] < 1000) throw new InvalidOperationException(L.T("精灵图中的角色不完整：") + Names[chapter] + "/" + f);
                byte[] cell = new byte[CanvasWidth * CanvasHeight * 4];
                for (int y = 0; y < CanvasHeight; y++) for (int x = 0; x < CanvasWidth; x++)
                {
                    int localX = chapter == 6 && f is 4 or 8 or 9 ? CanvasWidth - 1 - x : x;
                    int sx = x0 + (int)Math.Floor((localX - (CanvasWidth - Resolution) / 2.0 + .5) * (x1 - x0) / Resolution);
                    int sy = y0 + (int)Math.Floor((y - (CanvasHeight - Resolution) / 2.0 + .5) * (y1 - y0) / Resolution);
                    if (sx < 0 || sx >= width || sy < 0 || sy >= height || labels[sy * width + sx] != selected[f]) continue;
                    int src = (sy * width + sx) * 4, dst = (y * CanvasWidth + x) * 4;
                    cell[dst] = pixels[src]; cell[dst + 1] = pixels[src + 1]; cell[dst + 2] = pixels[src + 2]; cell[dst + 3] = 255;
                }
                frames[f] = BitmapSource.Create(CanvasWidth, CanvasHeight, 96, 96, PixelFormats.Pbgra32, null, cell, CanvasWidth * 4);
                frames[f].Freeze();
            }
            Cache.Add(chapter, frames);
        }
        return frames[Math.Clamp(frame, 0, 15)];
    }
}

public sealed record CombatStyle(double Windup, double Execution, double ImpactAt, double StandOff, double RunFps, double FootOffset, double MuzzleHeight, string Detail)
{
    public static readonly CombatStyle[] All = {
        new(.8, 1.35, .18, 155, 12, 155, 155, "稳姿瞄准 / 三连爆弹 / 弹壳抛射"),
        new(1.05, 1.3, .22, 88, 10, 115, 115, "动力拳蓄力 / 近身重击 / 地裂碎石"),
        new(.7, 1.2, .17, 105, 15, 100, 110, "低姿冲锋 / 横向锯切 / 连续磨削火星"),
        new(1.0, 1.4, .3, 98, 13, 15, 110, "跳包升空 / 俯冲劈砍 / 着地尘环"),
        new(1.2, 1.7, .34, 110, 11, 20, 110, "双手举剑 / 重斩停顿 / 持续链锯切割"),
        new(.7, 2.0, .8, 175, 11, 125, 125, "点火预热 / 锥形焰流 / 余烬与黑烟"),
        new(.85, 1.05, .14, 120, 18, 65, 110, "隐匿蓄势 / 残影突刺 / 交叉爪痕"),
        new(1.1, 1.7, .65, 165, 11, 155, 155, "扫描锁定 / 线圈充能 / 能量束分解")
    };
}

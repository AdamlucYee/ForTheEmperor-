using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ForTheEmperor;

public sealed class EffectView : FrameworkElement
{
    public int ChapterIndex;
    public double Progress;
    public double Time;
    public double Clock;
    public bool Demo;
    public bool Impact;
    public bool TargetRemoved;
    public Phase Phase;
    public Point Source = new(-95, -38);
    public BitmapSource? FileIcon;
    public EffectView() { RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); RenderOptions.SetEdgeMode(this, EdgeMode.Aliased); }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2));
        double hit = Time - CombatStyle.All[ChapterIndex].ImpactAt;
        bool attacking = Phase is Phase.Attack or Phase.Execution;
        if (Demo && !TargetRemoved)
        {
            Pixel.Rect(dc, -19, -25, 38, 48, "#D6D6C5"); Pixel.Rect(dc, -15, -21, 30, 40, "#354452");
            MarineView.Skull(dc, 0, -5, 10, "#E1C791");
            MarineView.Text(dc, "演练目标", new(-26, 32), 11, "#DDCBA5");
        }
        if (!Impact)
        {
            double r = 33 + Math.Sin(Clock * 6) * 2;
            dc.PushOpacity(.7);
            for (int i = 0; i < 4; i++)
            {
                dc.PushTransform(new RotateTransform(i * 90));
                Pixel.Line(dc, new(-r, -r + 11), new(-r, -r), "#CCB484"); Pixel.Line(dc, new(-r, -r), new(-r + 11, -r), "#CCB484");
                dc.Pop();
            }
            dc.Pop();
        }
        if (Phase == Phase.Attack)
        {
            if (ChapterIndex == 7) Scan(dc, Progress);
            if (ChapterIndex == 1) Charge(dc, Source, Progress, "#EFBE5D");
            if (ChapterIndex == 5) Flame(dc, Progress * .3, .22);
            if (ChapterIndex == 3 && Progress > .5) Dust(dc, (Progress - .5) * .7, "#8F8880", 16);
        }
        if (Phase >= Phase.Execution && Time < 3.6)
        {
            switch (ChapterIndex)
            {
                case 0: Bolter(dc, hit); break;
                case 1: Fist(dc, hit); break;
                case 2: Chainsaw(dc, hit, false); break;
                case 3: Angel(dc, hit); break;
                case 4: Chainsaw(dc, hit, true); break;
                case 5: Flame(dc, Time, 1); if (hit > 0) { Embers(dc, hit); Smoke(dc, hit - .3, "#403F3D", 35); } break;
                case 6: Raven(dc, hit); break;
                case 7: Iron(dc, hit); break;
            }
            if (TargetRemoved && hit >= 0) Fragments(dc, hit);
        }
        dc.Pop();
    }
    private static void Charge(DrawingContext dc, Point p, double t, string color)
    {
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6 + t * 6, r = 20 * (1 - t) + 4;
            Pixel.Rect(dc, p.X + Math.Cos(a) * r, p.Y + Math.Sin(a) * r, 2, 2, color);
        }
    }
    private void Bolter(DrawingContext dc, double t)
    {
        for (int shot = 0; shot < 3; shot++)
        {
            double age = t - shot * .25;
            if (age < 0) continue;
            if (age < .095)
            {
                Pixel.Line(dc, Source, new(0, 0), "#F6AD4A", 5);
                Pixel.Line(dc, Source, new(0, 0), "#FFFFD5", 2);
                dc.PushTransform(new TranslateTransform(Source.X, Source.Y)); Flash(dc, 1 - age / .095, "#FFD577"); dc.Pop();
            }
            if (age < .7)
            {
                Burst(dc, age, 28, 105, "#FFA752", 100);
                // Brass casings arc back toward the marine, then drop under gravity.
                double side = Source.X < 0 ? -1 : 1;
                Point casing = new(Source.X + side * age * 42, Source.Y - 40 * age + 125 * age * age);
                dc.PushTransform(new RotateTransform((int)(age * 700 / 30) * 30, casing.X, casing.Y));
                Pixel.Rect(dc, casing.X, casing.Y, 6, 3, "#BC8D42"); Pixel.Rect(dc, casing.X, casing.Y, 4, 2, "#F7D28A"); dc.Pop();
            }
            Smoke(dc, age - .06, "#817567", 10);
        }
    }
    private static void Fist(DrawingContext dc, double t)
    {
        if (t < 0) return;
        if (t < .2) Flash(dc, 1 - t / .2, "#FFE4A0");
        if (t < .8)
        {
            dc.PushOpacity(1 - t / .8); Pixel.Ring(dc, 16 + t * 145, .35, "#DDBD6D", 4); dc.Pop();
            for (int i = 0; i < 7; i++)
            {
                double a = i * .9, r = Math.Min(1, t * 6) * (40 + Pixel.Noise(i) * 35);
                Point p = new(Math.Cos(a) * r, Math.Sin(a) * r * .5);
                Pixel.Line(dc, new(), new(p.X * .55, p.Y * .4), "#5A4A38", 4);
                Pixel.Line(dc, new(p.X * .55, p.Y * .4), p, "#B59C69", 2);
            }
        }
        Burst(dc, t, 45, 125, "#C2AB79", 190); Dust(dc, t, "#877D67", 34);
    }
    private static void Chainsaw(DrawingContext dc, double t, bool templar)
    {
        if (t < 0) return;
        if (t < (templar ? .45 : .28)) Slash(dc, t / (templar ? .45 : .28), templar ? -1.5 : -.7, templar ? "#E9DBC0" : "#B7DEEC", templar ? 92 : 70);
        double length = templar ? 1.03 : .65;
        if (t < length)
        {
            // Continuous emissions along the cut, independent of frame rate.
            int emissions = (int)(t * 70);
            for (int i = Math.Max(0, emissions - 35); i <= emissions; i++)
            {
                double age = t - i / 70.0, n = Pixel.Noise(i + 80), vx = (n - .2) * 200;
                double x = (templar ? 8 : -18) + vx * age, y = 8 - (50 + Pixel.Noise(i + 13) * 140) * age + age * age * 180;
                dc.PushOpacity(Math.Clamp(1 - age / .65, 0, 1));
                Pixel.Line(dc, new(x, y), new(x - vx * .035, y + 4), i % 4 == 0 ? "#FFF6CE" : "#FFB754", 2);
                dc.Pop();
            }
        }
        if (templar && t > .38 && t < .75) { dc.PushOpacity(.55); Slash(dc, (t - .38) / .37, -.95, "#D9BC83", 62); dc.Pop(); }
        Dust(dc, t + .05, "#7E7C71", 20);
    }
    private static void Angel(DrawingContext dc, double t)
    {
        if (t < 0) return;
        if (t < .38) Slash(dc, t / .38, -1.0, "#F98070", 110);
        if (t < .13) Flash(dc, 1 - t / .13, "#FFF4CC");
        if (t < .85) { dc.PushOpacity(1 - t / .85); Pixel.Ring(dc, 12 + t * 135, .42, "#F5C7A6", 3); dc.Pop(); }
        Burst(dc, t, 55, 135, "#EABD9A", 190); Dust(dc, t, "#9A877A", 30);
    }
    private void Flame(DrawingContext dc, double t, double strength)
    {
        double end = Phase == Phase.Attack ? .5 : 1.65;
        int born = Math.Min(95, (int)(t * 65));
        Vector direction = new(-Source.X, -Source.Y); Vector normal = new(-direction.Y, direction.X); if (normal.Length > 0) normal.Normalize();
        for (int i = Math.Max(0, born - 50); i <= born; i++)
        {
            double age = t - i / 65.0;
            if (age < 0 || age > .75 || i / 65.0 > end) continue;
            double u = age / .55, spread = (Pixel.Noise(i * 9) - .5) * (10 + u * 68) * strength;
            Point point = Source + direction * u + normal * spread;
            point.Y -= age * age * 40;
            double size = (5 + Pixel.Noise(i * 11) * 10 + u * 8) * strength;
            dc.PushOpacity(Math.Clamp((1 - age / .75) * strength, 0, 1));
            Pixel.Rect(dc, point.X - size / 2, point.Y - size / 2, size, size, u > .8 ? "#C85132" : "#ED7937");
            Pixel.Rect(dc, point.X - size / 3, point.Y - size / 3, size * .65, size * .65, u > .65 ? "#FFB349" : "#FFDD8B");
            if (u < .55) Pixel.Rect(dc, point.X, point.Y, size * .25, size * .25, "#FFF6CC");
            dc.Pop();
        }
    }
    private static void Embers(DrawingContext dc, double t)
    {
        for (int i = 0; i < 65; i++)
        {
            double age = t - Pixel.Noise(i * 3) * .8;
            if (age < 0 || age > 1.2) continue;
            double x = (Pixel.Noise(i * 5) - .5) * 70 + Math.Sin(age * 5 + i) * 12;
            double y = 18 - age * (40 + Pixel.Noise(i * 7) * 70);
            dc.PushOpacity(1 - age / 1.2); Pixel.Rect(dc, x, y, 2 + (i % 3), 3, i % 4 == 0 ? "#FFF5AD" : "#FD944B"); dc.Pop();
        }
    }
    private static void Raven(DrawingContext dc, double t)
    {
        if (t < 0) return;
        for (int i = 0; i < 3; i++)
        {
            double a = t - i * .07;
            if (a is >= 0 and < .28) { dc.PushTransform(new TranslateTransform(i * 8 - 8, 0)); Slash(dc, a / .28, -.65, "#C8E8F4", 83); dc.Pop(); }
        }
        if (t is > .22 and < .48) Slash(dc, (t - .22) / .26, .8, "#9BAFD2", 72);
        for (int i = 0; i < 24; i++)
        {
            double age = t - i * .008;
            if (age < 0 || age > .8) continue;
            double angle = Pixel.Noise(i + 32) * Math.PI * 2;
            dc.PushOpacity(1 - age / .8);
            Pixel.Rect(dc, Math.Cos(angle) * age * 135, Math.Sin(angle) * age * 65 + age * 16, 3, i % 2 == 0 ? 8 : 3, i % 4 == 0 ? "#B2C2D9" : "#495368");
            dc.Pop();
        }
    }
    private static void Scan(DrawingContext dc, double t)
    {
        double y = -33 + (t % 1) * 66;
        dc.PushOpacity(.8); Pixel.Line(dc, new(-32, y), new(32, y), "#73E4DC", 2); dc.Pop();
        dc.PushOpacity(.23); Pixel.Rect(dc, -32, y - 7, 64, 6, "#67DAD6"); dc.Pop();
        for (int i = 0; i < 3; i++) { Pixel.Rect(dc, 39, -20 + i * 10, 4, 4, t > i * .3 ? "#A7F9DB" : "#455961"); }
    }
    private void Iron(DrawingContext dc, double t)
    {
        if (t < 0) { Scan(dc, Time / CombatStyle.All[7].ImpactAt); Charge(dc, Source, Time / CombatStyle.All[7].ImpactAt, "#90F0F3"); return; }
        if (t < .4)
        {
            Pixel.Line(dc, Source, new(), "#245B79", 15);
            Pixel.Line(dc, Source, new(), "#5BDFEE", 8);
            Pixel.Line(dc, Source, new(), "#E8FFFF", 3);
            for (int i = 0; i < 7; i++) { double u = i / 6.0; Point p = Source + (new Point() - Source) * u; Pixel.Rect(dc, p.X, p.Y + Math.Sin(t * 40 + i) * 9, 3, 3, "#83E4E9"); }
        }
        if (t < .8) { dc.PushOpacity(1 - t / .8); Pixel.Ring(dc, 16 + t * 90, 1, "#91ECF0"); dc.Pop(); }
        for (int i = 0; i < 48; i++)
        {
            double age = t - Pixel.Noise(i) * .15;
            if (age < 0 || age > 1) continue;
            double x = i % 8 * 7 - 25 + (Pixel.Noise(i * 7) - .5) * age * 85, y = i / 8 * 8 - 24 - age * (30 + Pixel.Noise(i * 11) * 70);
            dc.PushOpacity(1 - age); Pixel.Rect(dc, x, y, 3, 3, i % 4 == 0 ? "#E7FFFF" : "#63C5D9"); dc.Pop();
        }
        Smoke(dc, t, "#586D79", 14);
    }
    private static void Slash(DrawingContext dc, double t, double angle, string color, double radius)
    {
        dc.PushOpacity(Math.Clamp(1 - t, 0, 1));
        for (int i = 0; i < 34; i++)
        {
            double a = angle - .9 + i / 33.0 * (1.3 + t), r = radius * (.7 + t * .3);
            Pixel.Rect(dc, Math.Cos(a) * r - r * .55, Math.Sin(a) * r, 4, 4, color);
            if (i > 7) Pixel.Rect(dc, Math.Cos(a) * (r - 5) - r * .55, Math.Sin(a) * (r - 5), 2, 2, "#FFFFE6");
        }
        dc.Pop();
    }
    private static void Flash(DrawingContext dc, double power, string color)
    {
        double r = 26 * Math.Clamp(power, 0, 1);
        for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; Pixel.Line(dc, new(), new(Math.Cos(a) * r, Math.Sin(a) * r), color, 4); }
        Pixel.Rect(dc, -6 * power, -6 * power, 12 * power, 12 * power, "#FFFFDD");
    }
    private static void Burst(DrawingContext dc, double t, int count, double speed, string color, double gravity)
    {
        if (t < 0) return;
        for (int i = 0; i < count; i++)
        {
            double life = .28 + Pixel.Noise(i + 87) * .6;
            if (t > life) continue;
            double angle = Pixel.Noise(i + 12) * Math.PI * 2, v = speed * (.35 + Pixel.Noise(i + 36));
            double x = Math.Cos(angle) * v * t, y = Math.Sin(angle) * v * t + gravity * t * t;
            dc.PushOpacity(1 - t / life);
            Pixel.Rect(dc, x, y, i % 4 == 0 ? 5 : 2, i % 4 == 0 ? 4 : 2, i % 5 == 0 ? "#FFF1CB" : color); dc.Pop();
        }
    }
    private static void Dust(DrawingContext dc, double t, string color, int count)
    {
        if (t < 0) return;
        for (int i = 0; i < count; i++)
        {
            double life = .45 + Pixel.Noise(i * 3) * .45;
            if (t > life) continue;
            double a = Pixel.Noise(i) * Math.PI * 2, r = t * (30 + Pixel.Noise(i * 7) * 100);
            dc.PushOpacity((1 - t / life) * .65);
            Pixel.Rect(dc, Math.Cos(a) * r, 18 + Math.Sin(a) * r * .25 - t * 9, 6 + t * 12, 4 + t * 4, color); dc.Pop();
        }
    }
    private static void Smoke(DrawingContext dc, double t, string color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double age = t - Pixel.Noise(i * 7) * .45;
            if (age < 0 || age > 1.15) continue;
            double size = 6 + age * 19, x = (Pixel.Noise(i * 13) - .5) * (25 + age * 60), y = -age * (20 + Pixel.Noise(i * 31) * 35);
            dc.PushOpacity((1 - age / 1.15) * .38);
            Pixel.Rect(dc, x - size / 2, y - size / 3, size, size * .66, color); Pixel.Rect(dc, x - size / 3, y - size / 2, size * .66, size, color); dc.Pop();
        }
    }
    private void Fragments(DrawingContext dc, double t)
    {
        if (t > 1.05) return;
        for (int i = 0; i < 16; i++)
        {
            double a = Pixel.Noise(i * 13) * Math.PI * 2, v = 25 + Pixel.Noise(i * 5) * 75;
            double x = i % 4 * 8 - 16 + Math.Cos(a) * v * t, y = i / 4 * 8 - 16 + Math.Sin(a) * v * t + (ChapterIndex == 7 ? -80 : 75) * t * t;
            dc.PushOpacity(Math.Clamp(1 - t / 1.05, 0, 1));
            if (FileIcon != null)
            {
                int w = FileIcon.PixelWidth / 4, h = FileIcon.PixelHeight / 4;
                var piece = new CroppedBitmap(FileIcon, new(i % 4 * w, i / 4 * h, w, h));
                dc.DrawImage(piece, new(Math.Round(x), Math.Round(y), 7, 7));
            }
            else Pixel.Rect(dc, x, y, 4, 5, i % 3 == 0 ? "#D6C693" : "#899DA8");
            dc.Pop();
        }
    }
}

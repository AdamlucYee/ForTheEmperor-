using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ForTheEmperor;

public partial class CommandWindow : Window
{
    private readonly App? app;
    private bool updating;
    private readonly DispatcherTimer animation;
    internal CommandWindow(App? app)
    {
        this.app = app;
        InitializeComponent();
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(700, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        CombatDetail.HorizontalAlignment = HorizontalAlignment.Left;
        for (int i = 0; i < Chapter.All.Length; i++)
        {
            int index = i; var ch = Chapter.All[i];
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = ch.Mark, Foreground = MarineView.B(i == 1 ? "#E5B630" : ch.Trim), FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = L.T(ch.Name), FontSize = 11, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var button = new Button { Content = stack, Tag = index, Margin = new Thickness(4), Padding = new Thickness(3, 5, 3, 5), ToolTip = L.T(ch.Weapon) };
            button.Click += (_, _) => this.app?.SelectChapter(index); ChapterGrid.Children.Add(button);
        }
        animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        animation.Tick += (_, _) => { if (IsVisible) { Hero.Clock += .045; Hero.InvalidateVisual(); } };
        IsVisibleChanged += (_, _) => { if (IsVisible) animation.Start(); else animation.Stop(); };
        if (app != null) app.Pet.StatusChanged += Refresh;
        Closing += HideOnClose;
        Refresh();
    }
    private void HideOnClose(object? sender, CancelEventArgs e) { if (app != null && !Dispatcher.HasShutdownStarted) { e.Cancel = true; Hide(); } }
    internal void Refresh()
    {
        updating = true;
        int index = app?.Settings.Chapter ?? 0; var chapter = Chapter.All[index];
        ChapterName.FontSize = L.English ? 27 : 35;
        ChapterName.Text = L.T(chapter.Name); ChapterEnglish.Text = chapter.English; WeaponName.Text = L.T(chapter.Weapon);
        CombatDetail.Text = L.T(CombatStyle.All[index].Detail);
        LanguageCombo.SelectedIndex = L.English ? 1 : 0;
        Hero.ChapterIndex = index; Hero.InvalidateVisual();
        bool busy = app?.Pet.Engine.Busy ?? false;
        foreach (Button button in ChapterGrid.Children)
        {
            var ch = Chapter.All[(int)button.Tag];
            ((TextBlock)((StackPanel)button.Content).Children[1]).Text = L.T(ch.Name);
            button.ToolTip = L.T(ch.Weapon);
            bool selected = (int)button.Tag == index;
            button.Background = MarineView.B(selected ? "#283945" : "#1B252A");
            button.BorderBrush = MarineView.B(selected ? "#CEB17C" : "#354044");
            button.IsEnabled = !busy;
        }
        DemoButton.IsEnabled = !busy; CancelButton.IsEnabled = busy; SizeSlider.IsEnabled = !busy;
        MenuCheck.IsChecked = app?.Settings.MenuEnabled ?? true;
        MenuCheck.IsEnabled = app?.Preview != true;
        SoundCheck.IsChecked = app?.Settings.Sound ?? false; SizeSlider.Value = app?.Settings.Scale ?? 1;
        MenuNote.Text = app?.Preview == true ? L.T("预览模式 · 未修改系统右键菜单") : app?.MenuStatus ?? L.T("Windows 11：右键 → 显示更多选项");
        StatusText.Text = app?.Pet.Status ?? L.T("等待命令"); ReportText.Text = app?.Pet.LastReport ?? L.T("战士已就位。为了帝皇。");
        ReportText.ToolTip = ReportText.Text;
        updating = false;
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || !IsLoaded || LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        string language = (string)item.Tag;
        if (app != null) app.ChangeLanguage(language);
        else { L.SetLanguage(language); Refresh(); }
    }
    private void DemoClick(object sender, RoutedEventArgs e) => app?.Pet.Demo();
    private void RecallClick(object sender, RoutedEventArgs e) => app?.Pet.Recall();
    private void CancelClick(object sender, RoutedEventArgs e) => app?.Pet.CancelMission();
    private void MenuClick(object sender, RoutedEventArgs e) { if (!updating) app?.SetMenu(MenuCheck.IsChecked == true); }
    private void SoundClick(object sender, RoutedEventArgs e) { if (app != null && !updating) { app.Settings.Sound = SoundCheck.IsChecked == true; if (!app.Preview) app.Settings.Save(); } }
    private void PetSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (app != null && !updating && IsLoaded) { app.Settings.Scale = e.NewValue; app.Pet.SetScale(e.NewValue); if (!app.Preview) app.Settings.Save(); } }
}

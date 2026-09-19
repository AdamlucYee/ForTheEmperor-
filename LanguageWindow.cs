using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForTheEmperor;

internal sealed class LanguageWindow : Window
{
    internal string SelectedLanguage { get; private set; }
    internal LanguageWindow(string initial)
    {
        SelectedLanguage = initial;
        Title = "FOR THE EMPEROR · Language / 语言";
        Width = 500; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = MarineView.B("#11181C"); Foreground = MarineView.B("#E8E7DF");
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"); FontSize = 14;
        var panel = new StackPanel { Margin = new Thickness(30) };
        Content = new Border { Child = panel, Background = Background };
        panel.Children.Add(new TextBlock { Text = "FOR THE EMPEROR", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = MarineView.B("#CEB17C") });
        panel.Children.Add(new TextBlock { Text = "Choose your language / 选择语言", FontSize = 19, Margin = new Thickness(0, 18, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "You can change this later in the command panel.\n之后可在指挥面板中随时更改。", TextWrapping = TextWrapping.Wrap, Foreground = MarineView.B("#AAB6B3"), Margin = new Thickness(0, 0, 0, 20) });
        var chinese = new RadioButton { Content = "简体中文", IsChecked = initial == "zh-CN", Margin = new Thickness(0, 8, 0, 8), Foreground = Foreground, FontSize = 17, GroupName = "Language" };
        var english = new RadioButton { Content = "English", IsChecked = initial == "en", Margin = new Thickness(0, 8, 0, 22), Foreground = Foreground, FontSize = 17, GroupName = "Language" };
        panel.Children.Add(chinese); panel.Children.Add(english);
        var start = new Button { Content = "Continue / 进入桌面", IsDefault = true, Padding = new Thickness(12), Background = MarineView.B("#CEB17C"), Foreground = MarineView.B("#172027"), FontWeight = FontWeights.SemiBold };
        start.Click += (_, _) => { SelectedLanguage = english.IsChecked == true ? "en" : "zh-CN"; DialogResult = true; };
        panel.Children.Add(start);
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;

namespace ForTheEmperor;

public sealed class L : INotifyPropertyChanged
{
    public static L Current { get; } = new();
    private static readonly Dictionary<string, string> EnglishText = Load();
    public static string Language { get; private set; } = "zh-CN";
    public static bool English => Language == "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(English ? "en-US" : "zh-CN");
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => T(key);
    public static bool Supported(string? language) => language is "zh-CN" or "en";
    public static void SetLanguage(string language)
    {
        if (!Supported(language)) throw new ArgumentException("Unsupported language", nameof(language));
        Language = language;
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs("Item[]"));
    }
    public static string T(string text)
    {
        if (!English) return text;
        if (EnglishText.TryGetValue(text, out var value)) return value;
        // Reports retain their original key so a language switch can refresh history too.
        foreach (var pair in EnglishText)
            if (pair.Key.EndsWith('：') && text.StartsWith(pair.Key, StringComparison.Ordinal))
                return pair.Value + T(text[pair.Key.Length..]);
        return text;
    }
    internal static IReadOnlyDictionary<string, string> Entries => EnglishText;
    private static Dictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ForTheEmperor.assets.localization.json")
            ?? throw new InvalidOperationException("Missing embedded language resources.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}

public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding("[" + Key + "]") { Source = L.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

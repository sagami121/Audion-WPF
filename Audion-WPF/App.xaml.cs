using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Audion_WPF
{
    public partial class App : Application
    {
        public static string ThemeOverride { get; set; }

        public static bool IsLightThemePreferred()
        {
            const string personalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            using (var key = Registry.CurrentUser.OpenSubKey(personalizePath))
            {
                var value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                if (value is int intValue)
                {
                    return intValue != 0;
                }
            }

            return false;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ApplyTheme(ThemeOverride);
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnExit(e);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Dispatcher.Invoke(() => ApplyTheme(ThemeOverride));
        }

        public void ApplyTheme(string theme)
        {
            var effectiveTheme = theme;
            if (string.IsNullOrWhiteSpace(effectiveTheme) || effectiveTheme == "system")
            {
                effectiveTheme = IsLightThemePreferred() ? "light" : "dark";
            }

            ThemeOverride = theme;

            if (effectiveTheme == "light")
            {
                SetBrush("MainBg", "#F8FAFC");
                SetBrush("SidebarBg", "#FFFFFF");
                SetBrush("PanelAltBg", "#F1F5F9");
                SetBrush("BorderBrush", "#E2E8F0");
                SetBrush("BorderHoverBrush", "#CBD5E1");
                SetBrush("TextMain", "#0F172A");
                SetBrush("TextDim", "#475569");
                SetBrush("TextFaint", "#94A3B8");
            }
            else
            {
                SetBrush("MainBg", "#1A1A1A");
                SetBrush("SidebarBg", "#222222");
                SetBrush("PanelAltBg", "#2A2A2A");
                SetBrush("BorderBrush", "#333333");
                SetBrush("BorderHoverBrush", "#444444");
                SetBrush("TextMain", "#E0E0E0");
                SetBrush("TextDim", "#999999");
                SetBrush("TextFaint", "#666666");
            }

            SetBrush("Accent", "#A78BFA");
            SetBrush("Glow", "#38BDF8");
            SetAccentGradient("#A78BFA", "#38BDF8");
        }

        private void SetBrush(string resourceKey, string colorValue)
        {
            Resources[resourceKey] = new SolidColorBrush(ParseColor(colorValue));
        }

        private void SetAccentGradient(string startColor, string endColor)
        {
            Resources["AccentGradient"] = new LinearGradientBrush(
                ParseColor(startColor),
                ParseColor(endColor),
                45);
        }

        private static Color ParseColor(string colorValue)
        {
            return (Color)ColorConverter.ConvertFromString(colorValue);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Audion_WPF
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();

            checkAlwaysOnTop.IsChecked = settings.AlwaysOnTop;
            checkRestoreSession.IsChecked = settings.RestoreSession;
            checkShowLyrics.IsChecked = settings.ShowLyrics;

            btnThemeDark.IsChecked = settings.Theme != "light";
            btnThemeLight.IsChecked = settings.Theme == "light";

            SelectComboValue(comboLanguage, settings.Language ?? "ja");
            ApplyTranslations(settings.Language ?? "ja");
            txtVersion.Text = "Ver " + VersionInfo.DisplayVersion;
        }

        public AppSettings ResultSettings { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BeginOpenAnimation();
        }

        private void ApplyTranslations(string language)
        {
            txtDialogTitle.Text = LocalizationService.Translate(language, "settings");
            checkAlwaysOnTopLabel.Text = LocalizationService.Translate(language, "always_on_top");
            checkRestoreSessionLabel.Text = LocalizationService.Translate(language, "restore_session");
            checkShowLyricsLabel.Text = LocalizationService.Translate(language, "show_lyrics");
            txtThemeLabel.Text = LocalizationService.Translate(language, "theme");
            txtLanguageLabel.Text = LocalizationService.Translate(language, "language");

            btnThemeDark.Content = LocalizationService.Translate(language, "theme_dark");
            btnThemeLight.Content = LocalizationService.Translate(language, "theme_light");
            SetComboItemText(comboLanguage, 0, "日本語");
            SetComboItemText(comboLanguage, 1, "English");
            btnSave.Content = LocalizationService.Translate(language, "save");
            txtFeedbackButton.Text = "フィードバックを送る";
        }

        private static void SelectComboValue(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if ((string)item.Tag == tag)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static void SetComboItemText(ComboBox comboBox, int index, string text)
        {
            if (index >= 0 && index < comboBox.Items.Count)
            {
                var item = comboBox.Items[index] as ComboBoxItem;
                if (item != null)
                {
                    item.Content = text;
                }
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            if (clicked == null)
            {
                return;
            }

            btnThemeDark.IsChecked = clicked == btnThemeDark;
            btnThemeLight.IsChecked = clicked == btnThemeLight;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            ResultSettings = new AppSettings
            {
                AlwaysOnTop = checkAlwaysOnTop.IsChecked == true,
                RestoreSession = checkRestoreSession.IsChecked == true,
                ShowLyrics = checkShowLyrics.IsChecked == true,
                Theme = btnThemeLight.IsChecked == true ? "light" : "dark",
                Language = ((ComboBoxItem)comboLanguage.SelectedItem).Tag.ToString()
            };

            DialogResult = true;
        }

        private void btnFeedback_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Feedback UI is not implemented yet.", "Audion");
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DialogResult = false;
            }
        }

        private void CardBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
            e.Handled = true;
        }

        private void BeginOpenAnimation()
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            CardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(220)));
            CardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(220)));
        }
    }
}

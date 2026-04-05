using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IOPath = System.IO.Path;
using Microsoft.Win32;
using NAudio.Wave;

namespace Audion_WPF
{
    public partial class MainWindow : Window
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".m4a", ".wma", ".ogg"
        };

        private readonly ObservableCollection<AudioTrack> _playlist = new ObservableCollection<AudioTrack>();
        private readonly DispatcherTimer _timer;
        private readonly DoubleAnimation _albumSpinAnimation;
        private readonly Random _random = new Random();
        private readonly string _appDataDir;
        private readonly string _settingsPath;
        private readonly string _sessionPlaylistPath;
        private IWavePlayer _outputDevice;
        private AudioFileReader _audioFile;
        private bool _isInternalSelectionChange;
        private bool _isRestoringSession;
        private string _currentFilePath;
        private AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _appDataDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Audion-WPF");
            _settingsPath = IOPath.Combine(_appDataDir, "settings.json");
            _sessionPlaylistPath = IOPath.Combine(_appDataDir, "session-playlist.json");

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;

            playlistList.ItemsSource = _playlist;
            CollectionViewSource.GetDefaultView(playlistList.ItemsSource).Filter = FilterPlaylist;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += Timer_Tick;
            _albumSpinAnimation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(12)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            LoadSettings();
            ApplySettingsToUi();
            ApplyTranslations();
            UpdatePlaylistUi();
            UpdatePlaybackButtons();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            ApplyWindowChromeTheme();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowChromeTheme();
            if (_settings.RestoreSession)
            {
                _isRestoringSession = true;
                LoadPlaylistFromPath(_sessionPlaylistPath, false);
                _isRestoringSession = false;
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_settings.Theme == "system")
                {
                    ((App)Application.Current).ApplyTheme(_settings.Theme);
                    ApplyWindowChromeTheme();
                }
            });
        }

        private void ApplyWindowChromeTheme()
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero)
            {
                return;
            }

            var useDarkMode = App.IsLightThemePreferred() ? 0 : 1;
            if (_settings != null && _settings.Theme == "light")
            {
                useDarkMode = 0;
            }
            else if (_settings != null && _settings.Theme == "dark")
            {
                useDarkMode = 1;
            }
            var attributeSize = Marshal.SizeOf(typeof(int));
            var result = DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, attributeSize);
            if (result != 0)
            {
                DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref useDarkMode, attributeSize);
            }
        }

        private void LoadSettings()
        {
            _settings = AppSettings.Load(_settingsPath);
        }

        private void ApplySettingsToUi()
        {
            ((App)Application.Current).ApplyTheme(_settings.Theme);
            Topmost = _settings.AlwaysOnTop;
            sidebarColumn.Width = new GridLength(_settings.SidebarWidth);
            volumeSlider.Value = _settings.Volume;
            speedSlider.Value = _settings.Speed;
            UpdateMuteUi();
            UpdateRepeatUi();
            UpdateShuffleUi();
            ApplyWindowChromeTheme();
            ResetNowPlayingText();
        }

        private void ApplyTranslations()
        {
            txtAddFiles.Text = T("add_file");
            txtAddFolder.Text = T("add_folder");
            txtSearchPlaceholder.Text = T("search_placeholder");
            txtPlaylistHeader.Text = T("playlist");
            txtDropHintLine1.Text = T("drop_hint_line1");
            txtDropHintLine2.Text = T("drop_hint_line2");
            txtSpeedLabel.Text = T("speed");
            btnSavePlaylist.ToolTip = T("save_playlist");
            btnLoadPlaylist.ToolTip = T("load_playlist");
            btnClearPlaylist.ToolTip = T("clear_all");
            btnShuffle.ToolTip = T("shuffle");
            btnRepeat.ToolTip = T("repeat");
            btnMute.ToolTip = T("mute");
            btnSettings.ToolTip = T("settings");
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                ResetNowPlayingText();
            }
            UpdatePlaylistUi();
        }

        private string T(string key)
        {
            return LocalizationService.Translate(_settings.Language, key);
        }

        private bool FilterPlaylist(object item)
        {
            var track = item as AudioTrack;
            if (track == null)
            {
                return false;
            }

            var query = searchBox != null ? searchBox.Text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return track.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   track.DisplaySubtitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void btnFileSelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio files|*.mp3;*.wav;*.flac;*.aac;*.m4a;*.wma;*.ogg|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                AddTracks(dialog.FileNames, true);
            }
        }

        private void btnFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                        .Where(IsSupportedAudioFile)
                        .ToArray();
                    AddTracks(files, true);
                }
            }
        }

        private void btnSavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON playlist|*.json",
                DefaultExt = "json",
                AddExtension = true,
                FileName = "playlist.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            SavePlaylistToPath(dialog.FileName);
        }

        private void btnLoadPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON playlist|*.json|All files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            LoadPlaylistFromPath(dialog.FileName, true);
        }

        private void btnRemoveSelectedTrack_Click(object sender, RoutedEventArgs e)
        {
            RemoveTrack(playlistList.SelectedItem as AudioTrack);
        }

        private void btnClearPlaylist_Click(object sender, RoutedEventArgs e)
        {
            ClearPlaylistInternal();
        }

        private void btnShuffle_Click(object sender, RoutedEventArgs e)
        {
            _settings.Shuffle = !_settings.Shuffle;
            UpdateShuffleUi();
            PersistRuntimeState();
        }

        private void btnRepeat_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Repeat == "none")
            {
                _settings.Repeat = "all";
            }
            else if (_settings.Repeat == "all")
            {
                _settings.Repeat = "one";
            }
            else
            {
                _settings.Repeat = "none";
            }

            UpdateRepeatUi();
            PersistRuntimeState();
        }

        private void btnMute_Click(object sender, RoutedEventArgs e)
        {
            _settings.Muted = !_settings.Muted;
            ApplyMuteState();
            PersistRuntimeState();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var originalEffect = RootLayout.Effect;
            RootLayout.Effect = new BlurEffect { Radius = 12 };

            var dialog = new SettingsWindow(_settings) { Owner = this };
            var result = dialog.ShowDialog();

            RootLayout.Effect = originalEffect;

            if (result == true && dialog.ResultSettings != null)
            {
                _settings.AlwaysOnTop = dialog.ResultSettings.AlwaysOnTop;
                _settings.RestoreSession = dialog.ResultSettings.RestoreSession;
                _settings.ShowLyrics = dialog.ResultSettings.ShowLyrics;
                _settings.Language = dialog.ResultSettings.Language;
                _settings.Theme = dialog.ResultSettings.Theme;
                ApplySettingsToUi();
                ApplyTranslations();
                PersistRuntimeState();
            }
        }

        private void PlaylistItemDelete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                RemoveTrack(button.Tag as AudioTrack);
            }
        }

        private void AddTracks(IEnumerable<string> files, bool playFirstAdded)
        {
            AudioTrack firstNewTrack = null;

            foreach (var file in files.Where(IsSupportedAudioFile))
            {
                if (_playlist.Any(existingTrack => string.Equals(existingTrack.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var newTrack = new AudioTrack(file);
                TryPopulateTrackDetails(newTrack);
                _playlist.Add(newTrack);
                if (firstNewTrack == null)
                {
                    firstNewTrack = newTrack;
                }
            }

            CollectionViewSource.GetDefaultView(playlistList.ItemsSource).Refresh();
            UpdatePlaylistUi();

            if (playFirstAdded && firstNewTrack != null)
            {
                _isInternalSelectionChange = true;
                playlistList.SelectedItem = firstNewTrack;
                playlistList.ScrollIntoView(firstNewTrack);
                _isInternalSelectionChange = false;
                LoadAudio(firstNewTrack);
            }

            PersistRuntimeState();
        }

        private void TryPopulateTrackDetails(AudioTrack track)
        {
            try
            {
                using (var reader = new AudioFileReader(track.FilePath))
                {
                    track.DurationSeconds = reader.TotalTime.TotalSeconds;
                }
            }
            catch
            {
                track.DurationSeconds = 0;
            }
        }

        private void RemoveTrack(AudioTrack track)
        {
            if (track == null)
            {
                return;
            }

            var wasCurrentTrack = string.Equals(_currentFilePath, track.FilePath, StringComparison.OrdinalIgnoreCase);
            var selectedIndex = playlistList.SelectedIndex;

            _playlist.Remove(track);
            CollectionViewSource.GetDefaultView(playlistList.ItemsSource).Refresh();

            if (wasCurrentTrack)
            {
                StopAudio(true);
                ResetNowPlayingText();
                ResetAlbumArt();
            }

            if (_playlist.Count > 0)
            {
                _isInternalSelectionChange = true;
                playlistList.SelectedIndex = Math.Min(selectedIndex, _playlist.Count - 1);
                _isInternalSelectionChange = false;
            }
            else
            {
                playlistList.SelectedItem = null;
            }

            UpdatePlaylistUi();
            PersistRuntimeState();
        }

        private void ClearPlaylistInternal()
        {
            StopAudio(true);
            _playlist.Clear();
            CollectionViewSource.GetDefaultView(playlistList.ItemsSource).Refresh();
            playlistList.SelectedItem = null;
            ResetNowPlayingText();
            ResetAlbumArt();
            UpdatePlaylistUi();
            PersistRuntimeState();
        }

        private void SavePlaylistToPath(string path)
        {
            var data = new PlaylistFile
            {
                Tracks = _playlist.Select(track => new PlaylistTrack { FilePath = track.FilePath }).ToList()
            };

            var dir = IOPath.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var stream = File.Create(path))
            {
                var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(PlaylistFile));
                serializer.WriteObject(stream, data);
            }
        }

        private void LoadPlaylistFromPath(string path, bool autoPlayFirstTrack)
        {
            if (!File.Exists(path))
            {
                return;
            }

            PlaylistFile data = null;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(PlaylistFile));
                    data = serializer.ReadObject(stream) as PlaylistFile;
                }
            }
            catch
            {
                return;
            }

            var files = data != null && data.Tracks != null
                ? data.Tracks.Where(track => track != null && !string.IsNullOrWhiteSpace(track.FilePath))
                    .Select(track => track.FilePath)
                    .Where(IsSupportedAudioFile)
                    .ToArray()
                : new string[0];

            StopAudio(true);
            _playlist.Clear();
            AddTracks(files, autoPlayFirstTrack);

            if (!autoPlayFirstTrack && _playlist.Count > 0)
            {
                _isInternalSelectionChange = true;
                playlistList.SelectedIndex = 0;
                _isInternalSelectionChange = false;
                UpdateSelectedTrackPreview();
            }
        }

        private void PersistRuntimeState()
        {
            if (_settings == null)
            {
                return;
            }

            Directory.CreateDirectory(_appDataDir);
            _settings.SidebarWidth = sidebarColumn.Width.Value;
            _settings.Save(_settingsPath);

            if (_settings.RestoreSession || _isRestoringSession)
            {
                SavePlaylistToPath(_sessionPlaylistPath);
            }
        }

        private static bool IsSupportedAudioFile(string path)
        {
            return File.Exists(path) && SupportedExtensions.Contains(IOPath.GetExtension(path));
        }

        private void LoadAudio(AudioTrack track)
        {
            if (track == null)
            {
                return;
            }

            StopAudio(false);

            _audioFile = new AudioFileReader(track.FilePath);
            _audioFile.Volume = _settings.Muted ? 0f : (float)(volumeSlider.Value / 100d);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_audioFile);
            _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            _currentFilePath = track.FilePath;

            txtTrackName.Text = track.Title;
            txtArtistName.Text = track.DisplaySubtitle;
            txtTotalTime.Text = _audioFile.TotalTime.ToString(@"m\:ss");
            seekSlider.Maximum = Math.Max(1, _audioFile.TotalTime.TotalSeconds);
            seekSlider.Value = 0;
            ApplyAlbumArt(track);
            PlayAudio();
        }

        private void ApplyAlbumArt(AudioTrack track)
        {
            if (!string.IsNullOrWhiteSpace(track.CoverPath) && File.Exists(track.CoverPath))
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(track.CoverPath, UriKind.Absolute);
                    image.EndInit();
                    albumArtEllipse.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                    albumArtEllipse.Visibility = Visibility.Visible;
                    fallbackDisc.Visibility = Visibility.Collapsed;
                    return;
                }
                catch
                {
                }
            }

            ResetAlbumArt();
        }

        private void ResetAlbumArt()
        {
            albumArtEllipse.Fill = null;
            albumArtEllipse.Visibility = Visibility.Collapsed;
            fallbackDisc.Visibility = Visibility.Visible;
        }

        private void PlayAudio()
        {
            if (_outputDevice == null)
            {
                return;
            }

            _outputDevice.Play();
            SetPlayGlyph("||");
            _timer.Start();
            AlbumArtRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, _albumSpinAnimation);
            UpdatePlaybackButtons();
        }

        private void PauseAudio()
        {
            if (_outputDevice == null)
            {
                return;
            }

            _outputDevice.Pause();
            SetPlayGlyph("\u25B6");
            _timer.Stop();
            AlbumArtRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            UpdatePlaybackButtons();
        }

        private void StopAudio(bool resetPosition)
        {
            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            _currentFilePath = null;
            _timer.Stop();
            AlbumArtRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            SetPlayGlyph("\u25B6");

            if (resetPosition)
            {
                seekSlider.Value = 0;
                txtCurrentTime.Text = "0:00";
                txtTotalTime.Text = "0:00";
            }

            UpdatePlaybackButtons();
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_audioFile == null)
                {
                    return;
                }

                var endedNaturally = _audioFile.Position >= _audioFile.Length;
                if (!endedNaturally)
                {
                    return;
                }

                if (_settings.Repeat == "one")
                {
                    _audioFile.Position = 0;
                    PlayAudio();
                    return;
                }

                PlayAdjacentTrack(1, true);
            });
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_outputDevice == null)
            {
                if (_playlist.Count > 0)
                {
                    playlistList.SelectedIndex = Math.Max(playlistList.SelectedIndex, 0);
                    LoadAudio(playlistList.SelectedItem as AudioTrack);
                }

                return;
            }

            if (_outputDevice.PlaybackState == PlaybackState.Playing)
            {
                PauseAudio();
            }
            else
            {
                PlayAudio();
            }
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            PlayAdjacentTrack(-1, false);
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            PlayAdjacentTrack(1, false);
        }

        private void PlayAdjacentTrack(int direction, bool naturalAdvance)
        {
            if (_playlist.Count == 0)
            {
                return;
            }

            var currentIndex = playlistList.SelectedIndex;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = GetNextTrackIndex(currentIndex, direction, naturalAdvance);
            if (nextIndex < 0 || nextIndex >= _playlist.Count)
            {
                PauseAudio();
                seekSlider.Value = 0;
                if (_audioFile != null)
                {
                    _audioFile.CurrentTime = TimeSpan.Zero;
                }
                return;
            }

            _isInternalSelectionChange = true;
            playlistList.SelectedIndex = nextIndex;
            playlistList.ScrollIntoView(playlistList.SelectedItem);
            _isInternalSelectionChange = false;
            LoadAudio(playlistList.SelectedItem as AudioTrack);
        }

        private int GetNextTrackIndex(int currentIndex, int direction, bool naturalAdvance)
        {
            if (_settings.Shuffle && direction > 0 && _playlist.Count > 1)
            {
                var candidates = Enumerable.Range(0, _playlist.Count).Where(index => index != currentIndex).ToArray();
                return candidates[_random.Next(candidates.Length)];
            }

            var nextIndex = currentIndex + direction;
            if (nextIndex >= _playlist.Count)
            {
                return _settings.Repeat == "all" && naturalAdvance ? 0 : -1;
            }

            if (nextIndex < 0)
            {
                return _settings.Repeat == "all" ? _playlist.Count - 1 : -1;
            }

            return nextIndex;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_audioFile == null)
            {
                return;
            }

            seekSlider.Value = _audioFile.CurrentTime.TotalSeconds;
            txtCurrentTime.Text = _audioFile.CurrentTime.ToString(@"m\:ss");
            txtTotalTime.Text = _audioFile.TotalTime.ToString(@"m\:ss");
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtVolume != null)
            {
                txtVolume.Text = ((int)Math.Round(e.NewValue)).ToString() + "%";
            }

            if (_settings == null)
            {
                return;
            }

            _settings.Volume = e.NewValue;
            ApplyMuteState();
            PersistRuntimeState();
        }

        private void speedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtSpeed != null)
            {
                txtSpeed.Text = e.NewValue.ToString("0.0") + "x";
            }

            if (_settings == null)
            {
                return;
            }

            _settings.Speed = e.NewValue;
            PersistRuntimeState();
        }

        private void seekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_audioFile != null)
            {
                _audioFile.CurrentTime = TimeSpan.FromSeconds(seekSlider.Value);
                txtCurrentTime.Text = _audioFile.CurrentTime.ToString(@"m\:ss");
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                return;
            }

            var expandedFiles = new List<string>();
            foreach (var path in files)
            {
                if (Directory.Exists(path))
                {
                    expandedFiles.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(IsSupportedAudioFile));
                }
                else if (IsSupportedAudioFile(path))
                {
                    expandedFiles.Add(path);
                }
            }

            AddTracks(expandedFiles, true);
        }

        private void searchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(playlistList.ItemsSource).Refresh();
        }

        private void playlistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlaylistUi();

            if (_isInternalSelectionChange)
            {
                return;
            }

            UpdateSelectedTrackPreview();
        }

        private void playlistList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedTrack = playlistList.SelectedItem as AudioTrack;
            if (selectedTrack != null)
            {
                LoadAudio(selectedTrack);
            }
        }

        private void UpdateSelectedTrackPreview()
        {
            var selectedTrack = playlistList.SelectedItem as AudioTrack;
            if (selectedTrack != null && _audioFile == null)
            {
                txtTrackName.Text = selectedTrack.Title;
                txtArtistName.Text = selectedTrack.DisplaySubtitle;
                ApplyAlbumArt(selectedTrack);
            }
        }

        private void UpdatePlaylistUi()
        {
            txtPlaylistCount.Text = _playlist.Count.ToString() + " 曲";
            txtPlaylistCount.Text = _playlist.Count.ToString() + T("tracks_suffix");
            dropHint.Visibility = _playlist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetNowPlayingText()
        {
            txtTrackName.Text = T("select_track");
            txtArtistName.Text = T("welcome");
        }

        private void UpdateMuteUi()
        {
            SetIconGlyph(btnMute, _settings.Muted ? "\uE198" : "\uE15D");
        }

        private void UpdateRepeatUi()
        {
            btnRepeat.ToolTip = T("repeat");
            btnRepeat.Background = _settings.Repeat == "none"
                ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                : (Brush)Application.Current.Resources["Accent"];
        }

        private void UpdateShuffleUi()
        {
            btnShuffle.Background = _settings.Shuffle
                ? (Brush)Application.Current.Resources["Accent"]
                : new SolidColorBrush(Color.FromRgb(70, 70, 70));
        }

        private void UpdatePlaybackButtons()
        {
            btnPlay.Background = _outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Playing
                ? (Brush)Application.Current.Resources["Accent"]
                : new SolidColorBrush(Color.FromRgb(80, 80, 80));
        }

        private void ApplyMuteState()
        {
            if (_audioFile != null)
            {
                _audioFile.Volume = _settings.Muted ? 0f : (float)(volumeSlider.Value / 100d);
            }

            UpdateMuteUi();
        }

        private static void SetIconGlyph(Button button, string glyph)
        {
            var textBlock = button.Content as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = glyph;
            }
        }

        private void SetPlayGlyph(string glyph)
        {
            var textBlock = btnPlay.Content as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = glyph;
                textBlock.Margin = glyph == "||" ? new Thickness(0) : new Thickness(4, 0, 0, 0);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            PersistRuntimeState();
            StopAudio(false);
            base.OnClosing(e);
        }
    }
}

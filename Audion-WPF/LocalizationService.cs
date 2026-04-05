using System.Collections.Generic;

namespace Audion_WPF
{
    public static class LocalizationService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Strings =
            new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "ja",
                    new Dictionary<string, string>
                    {
                        { "add_file", "ファイル" },
                        { "add_folder", "フォルダ" },
                        { "search_placeholder", "曲名、アーティスト名で検索..." },
                        { "playlist", "プレイリスト" },
                        { "tracks_suffix", " 曲" },
                        { "drop_hint_line1", "ここに音楽ファイルを" },
                        { "drop_hint_line2", "ドラッグ&ドロップ" },
                        { "clear_all", "全削除" },
                        { "select_track", "曲を選択してください" },
                        { "welcome", "Audion へようこそ" },
                        { "speed", "速度" },
                        { "save_playlist", "プレイリストを保存" },
                        { "load_playlist", "プレイリストを読み込み" },
                        { "remove_track", "選択した曲を削除" },
                        { "shuffle", "シャッフル" },
                        { "repeat", "リピート" },
                        { "mute", "ミュート" },
                        { "settings", "設定" },
                        { "theme", "テーマ" },
                        { "theme_system", "システム" },
                        { "theme_dark", "ダーク" },
                        { "theme_light", "ライト" },
                        { "language", "言語" },
                        { "always_on_top", "常に最前面に表示" },
                        { "restore_session", "起動時に前回の状態を復元" },
                        { "show_lyrics", "歌詞を表示する" },
                        { "save", "保存" },
                        { "cancel", "キャンセル" },
                        { "repeat_none", "リピートなし" },
                        { "repeat_all", "全曲リピート" },
                        { "repeat_one", "1曲リピート" }
                    }
                },
                {
                    "en",
                    new Dictionary<string, string>
                    {
                        { "add_file", "Files" },
                        { "add_folder", "Folder" },
                        { "search_placeholder", "Search title, artist..." },
                        { "playlist", "Playlist" },
                        { "tracks_suffix", " tracks" },
                        { "drop_hint_line1", "Drop audio files here" },
                        { "drop_hint_line2", "Drag and drop" },
                        { "clear_all", "Clear All" },
                        { "select_track", "Choose a track" },
                        { "welcome", "Welcome to Audion" },
                        { "speed", "Speed" },
                        { "save_playlist", "Save playlist" },
                        { "load_playlist", "Load playlist" },
                        { "remove_track", "Remove selected track" },
                        { "shuffle", "Shuffle" },
                        { "repeat", "Repeat" },
                        { "mute", "Mute" },
                        { "settings", "Settings" },
                        { "theme", "Theme" },
                        { "theme_system", "System" },
                        { "theme_dark", "Dark" },
                        { "theme_light", "Light" },
                        { "language", "Language" },
                        { "always_on_top", "Always on top" },
                        { "restore_session", "Restore previous session on startup" },
                        { "show_lyrics", "Show lyrics" },
                        { "save", "Save" },
                        { "cancel", "Cancel" },
                        { "repeat_none", "No Repeat" },
                        { "repeat_all", "Repeat All" },
                        { "repeat_one", "Repeat One" }
                    }
                }
            };

        public static string Translate(string language, string key)
        {
            Dictionary<string, string> dict;
            if (!Strings.TryGetValue(language ?? "ja", out dict))
            {
                dict = Strings["ja"];
            }

            string value;
            if (dict.TryGetValue(key, out value))
            {
                return value;
            }

            return Strings["en"].ContainsKey(key) ? Strings["en"][key] : key;
        }
    }
}

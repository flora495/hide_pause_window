using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HidePauseWindow
{
    public sealed class Settings
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// ウィンドウ表示/非表示を切り替えるキーボードのキー(<see cref="Microsoft.Xna.Framework.Input.Keys"/>の値)。
        /// 未設定の間は空配列。
        /// </summary>
        public int[] KeyboardToggleKeys { get; set; } = new int[0];

        /// <summary>
        /// ウィンドウ表示/非表示を切り替えるコントローラーのボタン(デバイス固有のボタン番号)。
        /// 未設定の間は空配列。
        /// </summary>
        public int[] PadToggleKeys { get; set; } = new int[0];

        public static Settings Load(string path)
        {
            if (!File.Exists(path))
            {
                return new Settings();
            }
            XElement root = XDocument.Load(path).Root;
            return new Settings
            {
                IsEnabled = (bool?)root.Element("IsEnabled") ?? true,
                KeyboardToggleKeys = ParseKeys((string)root.Element("KeyboardToggleKeys")),
                PadToggleKeys = ParseKeys((string)root.Element("PadToggleKeys")),
            };
        }

        public void Save(string path)
        {
            new XDocument(new XElement("Settings",
                new XElement("IsEnabled", IsEnabled),
                new XElement("KeyboardToggleKeys", FormatKeys(KeyboardToggleKeys)),
                new XElement("PadToggleKeys", FormatKeys(PadToggleKeys)))).Save(path);
        }

        private static string FormatKeys(int[] keys)
        {
            return string.Join(",", keys ?? new int[0]);
        }

        private static int[] ParseKeys(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new int[0];
            }
            return value.Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Select(s => int.Parse(s))
                .ToArray();
        }
    }
}

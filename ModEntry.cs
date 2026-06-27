using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace HidePauseWindow
{
    [JumpKingMod("F.HidePauseWindow")]
    public static class ModEntry
    {
        private const string SettingsFileName = "F.HidePauseWindow.Settings.xml";

        private static readonly string SettingsPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SettingsFileName);

        private static Settings s_settings;

        /// <summary>
        /// 遅延ロードする。メインメニューの設定一覧はBeforeLevelLoadより先に触れる可能性があるため
        /// (keep_babe_skinで確認済みの落とし穴、NOTES.md参照)。
        /// </summary>
        public static Settings Settings => s_settings ?? (s_settings = Settings.Load(SettingsPath));

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            var harmony = new Harmony("F.HidePauseWindow.Harmony");
            PauseManagerPatches.Apply(harmony);
        }

        public static void SaveSettings()
        {
            Settings.Save(SettingsPath);
        }

        [MainMenuItemSetting]
        public static EnabledToggle MainEnabledSetting(object factory, GuiFormat format)
        {
            return new EnabledToggle();
        }

        [PauseMenuItemSetting]
        public static EnabledToggle PauseEnabledSetting(object factory, GuiFormat format)
        {
            return new EnabledToggle();
        }

        [MainMenuItemSetting]
        public static TextButton MainHideWindowKeySetting(object factory, GuiFormat format)
        {
            return HideWindowKeyMenu.Create(factory, format);
        }

        [PauseMenuItemSetting]
        public static TextButton PauseHideWindowKeySetting(object factory, GuiFormat format)
        {
            return HideWindowKeyMenu.Create(factory, format);
        }
    }
}

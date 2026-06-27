using HarmonyLib;
using JumpKing.Controller;

namespace HidePauseWindow
{
    /// <summary>
    /// <c>JumpKing.PauseMenu.PauseManager</c>(internalクラス)へのHarmonyパッチ群。
    /// <c>PauseManager</c>はinternalだが、対象メソッドはいずれも<c>public</c>のため
    /// 既存mod(area_info_display_on_pauseの<c>SetPause</c>パッチ)と同じ
    /// <c>AccessTools.TypeByName</c>/<c>AccessTools.Method</c>方式でパッチできる。
    /// </summary>
    internal static class PauseManagerPatches
    {
        private const string KeyboardSaveIdentifier = "pc_keyboard_jump_king";

        private static bool s_isPaused;
        private static bool s_hidden;
        private static bool s_lastPressed;

        public static void Apply(Harmony harmony)
        {
            var pauseManagerType = AccessTools.TypeByName("JumpKing.PauseMenu.PauseManager");

            harmony.Patch(AccessTools.Method(pauseManagerType, "SetPause"),
                postfix: new HarmonyMethod(typeof(PauseManagerPatches), nameof(SetPausePostfix)));

            harmony.Patch(AccessTools.Method(pauseManagerType, "PauseUpdate"),
                prefix: new HarmonyMethod(typeof(PauseManagerPatches), nameof(PauseUpdatePrefix)));

            harmony.Patch(AccessTools.Method(pauseManagerType, "Draw"),
                prefix: new HarmonyMethod(typeof(PauseManagerPatches), nameof(DrawPrefix)));
        }

        /// <summary>
        /// ポーズが解除された瞬間(Esc・Resume・Give Up等、どの経路で解除されても呼ばれる)に、
        /// 非表示状態を必ずリセットする。次にポーズを開いた時は常にウィンドウが見える状態から始まる。
        /// </summary>
        private static void SetPausePostfix(bool p_pause)
        {
            s_isPaused = p_pause;
            if (!p_pause)
            {
                s_hidden = false;
                s_lastPressed = false;
            }
        }

        /// <summary>
        /// <c>PauseManager.PauseUpdate</c>は一時停止中・非停止中を問わず毎フレーム呼ばれる
        /// (非停止中はポーズキー入力の監視のためだけに呼ばれている)。
        /// このprefixはオリジナルの処理(メニューのビヘイビアツリーのTick)が走る前に実行されるため、
        /// 非表示中に<c>ConsumePadPresses</c>でこのフレームのメニュー入力を握りつぶしておけば、
        /// 直後にオリジナル側が読む上下/確認/キャンセル/pauseの状態は安全に「何も押されていない」になる。
        /// </summary>
        private static void PauseUpdatePrefix()
        {
            if (!ModEntry.Settings.IsEnabled)
            {
                s_hidden = false;
                s_lastPressed = false;
                return;
            }
            if (!s_isPaused)
            {
                s_lastPressed = false;
                return;
            }

            bool pressed = IsToggleKeyPressed();
            if (pressed && !s_lastPressed)
            {
                s_hidden = !s_hidden;
            }
            s_lastPressed = pressed;

            if (s_hidden)
            {
                ControllerManager.instance.MenuController.ConsumePadPresses();
            }
        }

        /// <summary>
        /// 非表示中はポーズ画面の描画そのものをスキップする。ゲーム側に背景の暗転処理は無いため、
        /// これだけでフリーズした背景画面がそのまま見える状態になる(<c>IsPaused</c>自体は変更しないため、
        /// 一時停止状態は維持される)。
        /// </summary>
        private static bool DrawPrefix()
        {
            return !s_hidden;
        }

        private static bool IsToggleKeyPressed()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected)
            {
                return false;
            }

            bool isKeyboard = main.GetPad().GetSaveIdentifier() == KeyboardSaveIdentifier;
            int[] bind = isKeyboard ? ModEntry.Settings.KeyboardToggleKeys : ModEntry.Settings.PadToggleKeys;
            if (bind == null || bind.Length == 0)
            {
                return false;
            }

            int[] pressed = main.GetPad().GetPressedButtons();
            for (int i = 0; i < pressed.Length; i++)
            {
                for (int j = 0; j < bind.Length; j++)
                {
                    if (pressed[i] == bind[j])
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

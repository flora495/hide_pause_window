using System.Reflection;
using BehaviorTree;
using HarmonyLib;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions.BindController;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HidePauseWindow
{
    /// <summary>
    /// メイン/ポーズ両メニューの設定一覧に表示する「Hide Window Key」項目を組み立てる。
    /// ゲーム本体の操作設定画面(<c>MenuFactory.CreateBindControls</c>)、および参考にしたWorkshop mod
    /// (<c>Phoenixx19.SaveStates</c>、Bind Keys画面)と同じ<see cref="BindButtonFrame"/>を使った
    /// 「ボタンを押して登録する」UIを、トグル用の1ボタンだけに絞って簡略化したもの。
    /// 「変更を保持しますか？」の確認・取り消しタイマー(ゲーム本体の操作再設定にあるもの)は、
    /// このmodの設定が誤って致命的な操作不能を起こす類のものではないため省略し、即時反映する。
    /// </summary>
    internal static class HideWindowKeyMenu
    {
        private static readonly GuiFormat PopupFormat = new GuiFormat
        {
            anchor_bounds = new Rectangle(0, 0, 480, 360),
            anchor = new Vector2(0.5f, 0.5f),
            all_margin = 16,
            element_margin = 8,
            all_padding = 16,
        };

        private static MethodInfo s_addDrawableMethod;

        public static TextButton Create(object factory, GuiFormat format)
        {
            if (s_addDrawableMethod == null)
            {
                s_addDrawableMethod = AccessTools.Method(factory.GetType(), "AddDrawable");
            }

            CaptureToggleKeyNode captureNode = new CaptureToggleKeyNode();
            BTsequencor captureFlow = new BTsequencor();
            captureFlow.AddChild(new WaitUntilNoRawInputNode());
            captureFlow.AddChild(captureNode);

            BindButtonFrame frame = new BindButtonFrame(PopupFormat, captureFlow);
            frame.AddChild(new TextButton("Press a key or button to bind...", captureFlow));
            frame.Initialize();
            s_addDrawableMethod.Invoke(factory, new object[] { frame });

            TextButton button = new TextButton(BuildLabel(), captureFlow);
            captureNode.DisplayButton = button;
            return button;
        }

        public static string BuildLabel()
        {
            return "Hide Window Key (KB: " + Describe(ModEntry.Settings.KeyboardToggleKeys, isKeyboard: true)
                + " / Pad: " + Describe(ModEntry.Settings.PadToggleKeys, isKeyboard: false) + ")";
        }

        private static string Describe(int[] keys, bool isKeyboard)
        {
            if (keys == null || keys.Length == 0)
            {
                return "-";
            }
            if (isKeyboard)
            {
                return ((Keys)keys[0]).ToString();
            }
            return "Button " + keys[0];
        }
    }
}

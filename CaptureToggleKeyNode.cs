using BehaviorTree;
using JumpKing.Controller;
using JumpKing.PauseMenu.BT;

namespace HidePauseWindow
{
    /// <summary>
    /// 「直近入力があったデバイス」が次に押した1ボタンを、ウィンドウ非表示トグル用のキー/ボタンとして
    /// 設定に保存するノード。キーボードとそれ以外(コントローラー類)は<see cref="JumpKing.Controller.IPad.GetSaveIdentifier"/>
    /// で区別し、別々のスロット(<see cref="Settings.KeyboardToggleKeys"/>/<see cref="Settings.PadToggleKeys"/>)に保存する
    /// (ボタン番号の意味はデバイスごとに異なるため、デバイスをまたいで同じ配列で共有すると誤判定になる)。
    /// </summary>
    internal sealed class CaptureToggleKeyNode : IBTnode
    {
        private const string KeyboardSaveIdentifier = "pc_keyboard_jump_king";

        /// <summary>
        /// キャプチャ成功時にラベルを更新する対象の表示ボタン。<see cref="HideWindowKeyMenu.Create"/>から設定される。
        /// </summary>
        public TextButton DisplayButton { get; set; }

        protected override BTresult MyRun(TickData p_data)
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected)
            {
                return BTresult.Failure;
            }
            int[] pressed = main.GetPad().GetPressedButtons();
            if (pressed.Length == 0)
            {
                return BTresult.Running;
            }

            int code = pressed[0];
            if (main.GetPad().GetSaveIdentifier() == KeyboardSaveIdentifier)
            {
                ModEntry.Settings.KeyboardToggleKeys = new[] { code };
            }
            else
            {
                ModEntry.Settings.PadToggleKeys = new[] { code };
            }
            ModEntry.SaveSettings();
            // 今押されているこのボタンが、登録直後にそのままトグル発火の「新規押下」として
            // 誤検出されないようにする(PauseManagerPatches.NOTES.md「不具合修正」参照)。
            PauseManagerPatches.SuppressCurrentToggleEdge();

            if (DisplayButton != null)
            {
                DisplayButton.Text = HideWindowKeyMenu.BuildLabel();
            }
            return BTresult.Success;
        }
    }
}

using BehaviorTree;
using JumpKing.Controller;

namespace HidePauseWindow
{
    /// <summary>
    /// キー登録フローの開始直前に挟むノード。「Hide Window Key」項目をConfirmで選んだ際の
    /// そのキー押下がまだ物理的に押されたままの間は待機し、何も押されていない状態になってから
    /// 次のキャプチャに進む。これが無いと、選択に使ったキー自体が新しいバインドとして
    /// 即座に登録されてしまう。ゲーム本体のバニラ操作設定画面の<c>WaitUntilNoInput</c>と同じ役割だが、
    /// デバイス選択(BBKEY_PAD)を経由しないぶん簡略化し、現在の「直近入力があったデバイス」を直接見る。
    /// </summary>
    internal sealed class WaitUntilNoRawInputNode : IBTnode
    {
        protected override BTresult MyRun(TickData p_data)
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected)
            {
                return BTresult.Failure;
            }
            if (main.GetPad().GetPressedButtons().Length != 0)
            {
                return BTresult.Running;
            }
            return BTresult.Success;
        }
    }
}

# Hide Pause Window 調査メモ

逆コンパイル（ilspycmd、実機インストール済みの`JumpKing.exe`、および参考modとして指定されたWorkshopアイテム`3161216998`(`Phoenixx19.SaveStates`)の`JumpKingSaveStates.dll`）による調査の経緯と、確定した仕様をまとめたもの。

**実装状況**: 本ドキュメント作成時点では未実装（仕様確定・実現可能性調査のみ完了）。

## 【重要・厳守】実装時の制約（他modから引き継ぎ）

このmodもSteam Workshop上での公開を想定する。**Jump King本体のプログラムやファイル（`C:\Program Files (x86)\Steam\steamapps\common\Jump King\`配下など）は一切変更・上書き・削除してはならない。** 実装は必ず`hide_pause_window/`フォルダの下だけで行うこと（ビルド参照のための読み取り専用パス指定を除く）。本体への変更が必要に見える場合は、Harmonyによる実行時パッチなど、本体ファイルを書き換えない手段で実現する（[keep_babe_skin](../keep_babe_skin/NOTES.md)・[confirm_count_control](../confirm_count_control/MenuFactoryPatches.cs)・[area_info_display_on_pause](../area_info_display_on_pause/NOTES.md)と同じ方針）。

Steam Workshop公開を前提とするため、プロジェクトのフォルダ構成は[keep_babe_skin](../keep_babe_skin/)・[confirm_count_control](../confirm_count_control/)・[area_info_display_on_pause](../area_info_display_on_pause/)と同じ形にする（csproj・Properties/AssemblyInfo.cs・lib/0Harmony.dll・各.csファイルをリポジトリのルート直下に直接配置し、サブフォルダにネストしない）。

## やりたいこと（仕様）

ポーズ中に、ユーザーが設定したボタンを押すと、ポーズ画面に表示されているウィンドウ（ステータス枠・メインメニュー枠・Objective欄など）をすべて消し、背景のゲーム画面を全て見ることができるようにする。バニラではポーズ中にこのウィンドウを消す手段が無い。

## 調査結果サマリ（結論: 十分に実現可能）

| 必要な機能 | ゲーム側にあるか | 実現方法 |
|---|---|---|
| ポーズ画面の描画を1箇所でまとめて止める | **ある** | `JumpKing.PauseMenu.PauseManager.Draw()`をHarmonyでprefixパッチし、非表示フラグが立っていれば`return false` |
| 背景に暗転オーバーレイが別途存在しないか | **無い（確認済み）** | バニラのポーズ画面のスクリーンショットを確認した限り、背景はフルカラーのまま。`PauseManager.Draw()`を丸ごとスキップするだけで、フリーズした背景が綺麗に見える |
| ゲーム独自のボタン体系に「任意のMOD用ボタン」を追加する空きスロット | **無い** | `JumpKing.Controller.PadBinding`は up/down/left/right/jump/pause/confirm/cancel/boots/snake/restart の固定11種類のみ。mod独自にキー/ボタンを保持・判定する必要がある |
| 非表示中の誤操作（上下キー移動・誤確定）を防ぐ手段 | **ある（公開API）** | `JumpKing.Controller.MenuController.ConsumePadPresses()`を毎フレーム呼べば、その瞬間のメニュー入力（上下/確認/キャンセル/pause）を握りつぶせる |
| 「ボタンを押して登録する」キーバインドUIを自前で作る前例 | **ある（Workshop実例で確認済み）** | `Phoenixx19.SaveStates`（Workshop ID `3161216998`）が、ゲーム本体内蔵の`BindButtonFrame`等を流用して、独自のキーバインド画面を実装している |

技術的な障壁は無い。以下、各調査項目の詳細。

## 詳細調査

### 1. ポーズ画面の描画フロー（`JumpKing.PauseMenu.PauseManager`）

`PauseManager`（`internal class`）の`Draw()`がポーズ画面描画の唯一の入口になっている。

```csharp
public override void Draw()
{
    if (IsPaused)
    {
        if (m_pause.IsRunning())
        {
            m_main_menu.DrawBgOnly();
        }
        m_factory.Draw(m_bt_manager.GetRaw());  // ステータス枠・メインメニュー枠・Objective欄など全部
    }
}
```

`m_factory.Draw(...)`は、`PauseManager`のビヘイビアツリー（`m_bt_manager`）にこれまで追加された全ての描画可能要素（`AddDrawable`で登録されたもの）を描画する。これは他mod（`area_info_display_on_pause`の追加表示など）が同じパイプラインに乗せている要素も含むため、**この1箇所をスキップするだけで、バニラ・他mod追加分を問わずポーズ画面の表示物すべてが消える**。

`IsPaused`自体（ゲームの一時停止状態）はこの`Draw()`とは独立した別のフラグであり、`Draw()`をスキップしてもポーズ状態そのものには影響しない。つまり背景は静止画のまま、ウィンドウだけが消える、という求めている挙動がそのまま実現できる。

`PauseManager`は`internal`クラスだが、`area_info_display_on_pause`の`MenuFactoryPatches.cs`が`PauseManager.SetPause`に対し`AccessTools.TypeByName`/`AccessTools.Method`経由でHarmonyパッチを当てている前例があり、同じ手法が`Draw`/`PauseUpdate`にもそのまま使える。

### 2. 暗転オーバーレイの有無

ユーザー提供のスクリーンショット（バニラのポーズ画面）を確認した結果、背景は通常のゲーム画面と同じ明るさ・色味のまま表示されており、画面全体を覆う暗転・フィルター等は確認できなかった。`PauseManager.Draw()`の実装にも、ウィンドウ群の描画以外に背景を加工するような処理は無い。よって「ウィンドウの描画を止めるだけ」で目的の見た目（綺麗な背景がそのまま見える）が達成できると判断できる。

### 3. 入力システムの構造

`JumpKing.Controller.PadInstance.GetPadState()`が、`m_binding`（`PadBinding`、up/down/left/right/jump/pause/confirm/cancel/boots/snake/restartの11種の固定アクション）に対して、現在押されているキー/ボタンの番号（`IPad.GetPressedButtons()`が返す`int[]`、キーボードなら`Keys`の値、コントローラーならボタン番号）が含まれるかどうかを判定する。

```csharp
private bool IsPressed(int[] pressed, int[] bind)
{
    foreach (int num in pressed)
        foreach (int num2 in bind)
            if (num == num2) return true;
    return false;
}
```

このアクション一覧に新しいアクション（このmod用のトグルボタン）を追加する余地はゲーム側に存在しない。そのため、**mod独自に「キー/ボタン番号の配列」を1つ保持し、同じ`IsPressed`相当の判定を自前で行う**方式にする。

`JumpKing.Controller.ControllerManager.instance.GetMain()`は「直近で何らかの入力があったデバイス」の`PadInstance`を返し、`PadInstance.GetPad().GetPressedButtons()`はそのデバイス（キーボード/Xboxパッド/その他パッド）が今押している全ボタンの番号を、デバイスの種類を問わず同じ`int[]`形式で返す。つまり**キーボードとコントローラーを区別せず同じロジックで扱える**ため、両対応は追加の分岐なしに実現できる。

### 4. メニュー入力（上下/確認/キャンセル/pause）の処理場所

`JumpKing.PauseMenu.BT.MenuSelector.MyRun()`が、描画とは独立して**毎フレーム**メニューの入力を処理している。

```csharp
protected override BTresult MyRun(TickData p_data)
{
    ...
    PadState padState = ControllerManager.instance.MenuController.GetPadState();
    ...
    if (AllowEscape)
    {
        if (padState.cancel) { ControllerManager.instance.MenuController.ConsumePadPresses(); return BTresult.Failure; }
        if (padState.pause) { PauseManager.SetPause(p_pause: false); return BTresult.Failure; }
    }
    if (padState.down) Index++;
    if (padState.up) Index--;
    ...
}
```

重要な発見: **ポーズ解除（Escキー）はここで直接処理されており、`PauseManager.Draw()`の描画スキップとは無関係に動作する。** つまり、Harmonyで`Draw`だけをスキップする実装にしても、Escキーによる通常のポーズ解除は影響を受けない。

一方で、この`MyRun`は描画の有無に関わらず毎フレーム呼ばれ続けるため、**何の対策もしないと、ウィンドウを非表示にしている間も上下キーでカーソルが動いたり、見えない状態でConfirmを押してメニュー項目（Give Up等）を実行してしまう事故が起こり得る**。

これを防ぐ手段として、`JumpKing.Controller.MenuController`に以下の公開メソッドがある。

```csharp
public void ConsumePadPresses()
{
    _menu_state = default(PadState);
}
```

非表示フラグが立っている間、毎フレームこれを呼んでおけば（こちらのトグル判定自体は`MenuController`を経由しない独自ポーリングのため影響を受けない）、`MenuSelector.MyRun`を含むメニュー側の入力処理はその瞬間「何も押されていない」状態として扱われ、安全に無効化できる。

### 5. キー登録UIの前例（`Phoenixx19.SaveStates`、Workshop ID `3161216998`）

ユーザーが希望するUXの参考として指定された上記Workshopアイテムがローカルにダウンロードされていたため、`JumpKingSaveStates.dll`を逆コンパイルして実装を確認した。

このmodは独自の3アクション（`SavePos`/`LoadPos`/`DeletePos`）について、ゲーム本体の設定画面と同様の「ボタンを押して登録する」UIをポーズメニュー内に実装している。要点:

- 独自の`EBinding`列挙型と、`Dictionary<EBinding, int[]>`によるキー/ボタン保持（複数同時バインドにも対応した可変長配列）。XMLで永続化（`Preferences`クラス、`INotifyPropertyChanged`で変更時に自動保存）
- UI構築は、ゲーム本体の`MenuFactory`が実際のコントロール設定画面で使っている部品（`JumpKing.PauseMenu.BT.Actions.BindController`名前空間の`BindButtonFrame`・`MenuSelectorBack`・`TimerAction`・`WaitUntilNoInput`/`WaitUntilNoMenuInput`等）をそのまま流用して構築している
  - 「ボタンを押してください」という表示（`BindButtonFrame`）→ 押されたボタンを検出（独自`CustomBindButton`、`MyRun`内で`ControllerManager.instance.GetMain().GetPad().GetPressedButtons()`を読む）→ 反映
  - 登録後は「変更を保持しますか？5秒で元に戻ります」という確認（`TimerAction`、ゲーム本体のコントロール設定画面と同じUX）を経て確定する
  - `MenuFactory`の`internal`フィールド（`m_drawables`・`CONTROLS_GUI_FORMAT_LEFT/RIGHT`等）へのアクセスはHarmonyの`Traverse`（`Traverse.Create(__instance).Field(...)`）で行っている
- 実際の押下判定（ゲームプレイ中の効果発動）は`ControllerManager.instance.GetMain().GetPad().GetPressedButtons()`を直接読む自前の`CustomPadInstance`が行い、前回フレームとの比較でエッジ検出（「今押された瞬間」の判定）をしている。これはゲーム本体の`PadInstance.GetPressed()`と同じ考え方

→ **「ボタンを押して登録するUI」は前例があり、内蔵部品の流用で実現可能**と判断できる。本modでは1ボタン（トグル用）だけなので、上記のうち`MakeBindController`の1アクション分（`SavePos`等を1つだけ扱う形）に相当する規模で十分。

## 確定した仕様（ユーザー回答済み）

| 論点 | 決定 |
|---|---|
| 非表示中のメニュー入力（上下/確認/キャンセル） | **完全に無効化する**。`ConsumePadPresses()`を非表示中は毎フレーム呼び、誤操作を防ぐ。Escによるポーズ解除も非表示中は同様に効かなくなる（再表示してから操作する） |
| ボタン設定UI | `Phoenixx19.SaveStates`（Workshop ID `3161216998`）の Bind Keys 画面と同様、**ボタンを押して登録する方式**。ゲーム本体・SaveStatesが使っている`BindButtonFrame`等の部品を流用する |
| 対応デバイス | **キーボード・コントローラーの両対応**。`ControllerManager.instance.GetMain().GetPad().GetPressedButtons()`がデバイスを問わず同じ形式で値を返すため、追加の分岐は不要 |

## 実装方針（案、未実装）

- `Settings.cs` — トグル用のキー/ボタン番号配列（`int[]`）をXML保存。既存mod群と同じ`Settings.Load`/`Save`パターン
- `HideToggleState.cs` — 押下のエッジ検出（前回フレームとの比較）と、現在の非表示フラグを保持
- `PauseManagerPatches.cs`
  - `PauseManager.PauseUpdate`へのHarmonyパッチ（prefixまたはpostfix）→ 毎フレーム独自ボタンのエッジ検出、非表示フラグの反転。非表示中は`ControllerManager.instance.MenuController.ConsumePadPresses()`
  - `PauseManager.Draw`へのprefix → 非表示フラグが立っていれば`return false`
- `MenuFactoryPatches.cs` — `[PauseMenuItemSetting]`に「ウィンドウ非表示ボタンの設定」項目を追加し、`Phoenixx19.SaveStates`の`MakeBindController`/`MakeBindButtonMenu`パターンを1ボタン分流用して「ボタンを押してください」UIを構築

## Harmonyの同梱方針

`keep_babe_skin`の調査で判明した通り、Jump KingのMod LoaderはWorkshopの「Required Items」経由の共有ライブラリ読み込みを安定して扱えないため、`0Harmony.dll`は本modにも直接同梱する方針とする（`confirm_count_control`/`keep_babe_skin`/`area_info_display_on_pause`と同じ）。

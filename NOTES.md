# Hide Pause Window 調査メモ

逆コンパイル（ilspycmd、実機インストール済みの`JumpKing.exe`、および参考modとして指定されたWorkshopアイテム`3161216998`(`Phoenixx19.SaveStates`)の`JumpKingSaveStates.dll`）による調査の経緯と、確定した仕様をまとめたもの。

**実装状況**: 実装・ビルド確認済み（リポジトリルートの各`.cs`ファイル）。実機テストもほぼ完了し、テスト中に見つかった不具合（後述）はいずれも修正済み。

**実装後の補足**:
- 「キー登録UI」は、調査時点で想定していたゲーム本体内蔵の`BindCatchSave`/`SetBBKeyNode`/`TimerAction`による「変更を保持しますか？5秒で元に戻ります」の確認フローは実装せず、押された瞬間に即時保存する方式に簡略化した。movement/jump等のゲーム進行に必須なキーと違い、このmodのトグルキーを誤って思わぬ値に設定してしまっても、いつでもこの同じ画面を開いて上書きできるだけで実害が無いため
- キーボードとコントローラーは判定がデバイス依存（ボタン番号の意味が異なる）のため、`Settings.KeyboardToggleKeys`/`Settings.PadToggleKeys`の2つに分けて保持する。デバイスの判別は`IPad.GetSaveIdentifier()`（キーボードは`"pc_keyboard_jump_king"`固定）で行い、`ControllerManager.GetMain()`（直近で入力があったデバイス）を介して両方を同じロジックで扱う
- キー登録時の押下検出は`ControllerManager.instance.GetMain().GetPad().GetPressedButtons()`を直接読む方式（`Phoenixx19.SaveStates`の`CustomBindButton`と同じ考え方）。ゲーム本体の`SelectDevice`/`BBKEY_PAD`によるデバイス選択UIは使っていない（トグル用の1ボタンだけなので不要と判断し、簡略化した）
- ポーズ中のトグルキー判定(`IsToggleKeyPressed`)は、当初は登録時と同じく`GetMain()`の1台だけを見ていたが（`Phoenixx19.SaveStates`の`CustomPadInstance`と同じ）、後述の不具合により接続中の全デバイスを見る方式に変更した

## 不具合修正: 「Hide Window Key」選択時に画面が固まる（実機テストで発覚・修正済み）

実機テストで、ポーズメニューから「Hide Window Key」をConfirmすると、ポップアップが一切表示されず、上下キー・キャンセル・Escも反応しない「フリーズ」状態になる不具合が報告された。

**原因**: `HideWindowKeyMenu.Create`で、外側の`TextButton`の子ノードに`captureFlow`（`WaitUntilNoRawInputNode`→`CaptureToggleKeyNode`の`BTsequencor`）を直接渡しており、それをラップしているはずの`frame`（`BindButtonFrame`、「Press a key...」の表示と10秒タイムアウトを持つ）が一度も`Run()`されない構造になっていた。

これにより:
- `BindButtonFrame.Draw()`は`base.last_result == BTresult.Running`の時しか描画しないが、`frame`自体がTickされないため`last_result`が更新されず、**ポップアップが永久に表示されない**
- `JumpKing.PauseMenu.BT.MenuSelector.MyRun()`は、現在選択中の項目が`Running`を返している間は上下/キャンセル/pauseの入力処理を一切行わない（`bool flag = m_last_child_result != BTresult.Running; if (flag) { ...入力処理... }`）。`captureFlow`自体は正常にRunning中だったため、メニュー全体が「見えないまま入力を受け付けない」状態に陥った
- `BindButtonFrame`が内蔵する10秒の自動キャンセルタイマー(`MyRun`内の`m_timer.Update(...)`)も、`frame`自体がTickされないため動作せず、唯一の自動回復手段も機能しなかった

**修正**: 外側`TextButton`の子ノードを`captureFlow`ではなく`frame`（`BindButtonFrame`自身）に変更した。`BindButtonFrame.MyRun()`が内部で`m_main_node.Run(p_data)`（＝`captureFlow.Run(...)`）を呼ぶため、キャプチャの動作自体は変わらず、`frame`自身が正しくTickされることで描画・10秒タイムアウトの両方が機能するようになった。

**既知の限度（軽微、対応不要と判断）**: トグルキーをゲーム本体の「pause」キー（デフォルトEsc）と同じボタンに登録した場合、非表示→再表示の際の押下が`ConsumePadPresses()`で握りつぶされず素通りし、再表示と同時にポーズ自体も解除される。トグル専用に別のキーを選べば発生しない、利用者が意図的に重複させた場合のみの軽微な仕様上の癖。

## 実機テストでの追加修正（3点）

実機での再テストで3つの修正依頼があり、いずれも対応済み。

1. **ラベル変更**: 「Hide Window Key」を、参考にした`Phoenixx19.SaveStates`の表記に合わせて「Bind Keys」に変更（`HideWindowKeyMenu.BuildLabel`）。

2. **不具合: キー登録時、登録に使った押下自体が即座にトグルとして誤検出される**
   - 例: トグルキーを`T`に設定すると、`T`を押して登録した瞬間、その同じ押下が「トグルキーが押された」と判定され、登録直後にウィンドウが非表示になってしまっていた。
   - 原因: `CaptureToggleKeyNode`が新しいバインドを保存した時点では、そのキーはまだ物理的に押されたままの状態。次のフレームで`PauseManagerPatches.IsToggleKeyPressed()`がそれを「押されている」と検出するが、エッジ検出用の`s_lastPressed`はまだ`false`のままなので「新規の押下」と判定され、即座にトグルが発生していた。
   - 修正: `PauseManagerPatches`に`SuppressCurrentToggleEdge()`を追加し、`CaptureToggleKeyNode`が保存した直後に呼び出すことで`s_lastPressed`を強制的に`true`にする。これにより、登録に使ったキーが実際に一度離されるまでは「新規の押下」として扱われなくなり、登録直後の誤発火が起きなくなった。

3. **要望: 設定キーに関わらずESCキーは「非表示→表示」のみを行う（逆方向は不要）**
   - 当初、`PauseUpdatePrefix`に「非表示中は物理的なESCキー(`Keyboard.GetState().IsKeyDown(Keys.Escape)`)が押されていれば`s_hidden`を強制的に`false`にする」処理を追加した。
   - 表示中にESCを押しても何もしない（要望通り、逆方向は実装していない）。
   - **その後、「ESCに限らず非表示中は任意のキー/ボタンで表示に戻るようにしてほしい」と仕様変更があったため、ESC固有のチェックを`IsAnyInputPressed()`（キーボードの`GetPressedKeys()`、および`ControllerManager.GetConnectedPads()`で取得できる接続中の全パッドの`GetPressedButtons()`のいずれかが1つでも押されていればtrue）に置き換えた。** ESCだけの特別扱いは無くなり、トグルキー自身を含むあらゆる入力で非表示→表示に戻る。
   - `ConsumePadPresses()`は`wasHidden || s_hidden`の判定のまま変更していない。これにより、ウィンドウを再表示させたその押下が、`MenuSelector.MyRun`側の上下/確認/キャンセル/pause入力として読まれて意図せずメニューが動いたりポーズ自体が解除されたりすることを防いでいる（要望は「表示が戻る」だけで、それ以外の副作用は意図していないため）。
   - 表示中→非表示への遷移（トグルキーの新規押下判定）と、非表示中→表示への遷移（任意の入力判定）を`if`/`else if`で排他的に処理しているため、同一フレーム内で両方が同時に評価されて二重トグルすることはない。

## 不具合修正: トグルキーを押すと一瞬非表示になるがすぐ再表示されてしまう（実機テストで発覚・修正済み）

上記3.の変更直後の実機テストで、「トグルキーを押すと一瞬非表示になるが、何も押さなくてもすぐに表示に戻ってしまう」不具合が報告された。

**原因**: `IsAnyInputPressed()`は「現在押されているキー/ボタンが1つでもあるか」を毎フレーム見るだけの判定だった。トグルキーを押して非表示にした瞬間、そのキー自体は通常数フレーム（人間の反応速度では最低でも数十ms＝数フレーム）押されたままになる。非表示になった次のフレームでも`wasHidden`が`true`になり、かつそのトグルキー自身がまだ押されたままなので`IsAnyInputPressed()`が`true`を返し、「ユーザーが新しく何かを押した」と誤判定して即座に再表示していた。

**修正**: 「一度すべてのキー/ボタンが離されてから、次に何か押されたら再表示する」という2段階の状態(`s_armedToShow`)を追加した。

- 非表示になった直後は`s_armedToShow = false`から始まり、`IsAnyInputPressed()`が`false`になる（＝何も押されていない瞬間が一度でも来る）までは再表示のトリガーを一切受け付けない
- `IsAnyInputPressed()`が`false`になった時点で`s_armedToShow = true`になり、以降は次に何か押された瞬間に再表示される
- 表示→非表示へ切り替わった時(トグルキーの新規押下を検出した時)は`s_armedToShow`を`false`にリセットする（次に非表示になった時も同じ手順を踏む）

これにより、トグルキーを押した時の物理的な押下継続が「次の押下」と誤認されることがなくなった。挙動としては「トグルキーで非表示にした後、一度キーを離してから何か押せば再表示される」という形になる（押しっぱなしのままでは再表示されない）。

## 仕様変更: 「任意のキー」の判定範囲をゲーム本体が認識する操作に限定（スクリーンショット対応）

ユーザーから、「非表示中にスクリーンショットを撮りたいが、スクリーンショットのショートカット（Steamの`F12`等）を押すと、それ自体が『何か押された』と判定されて表示状態に戻ってしまい撮影できない」という報告があった。

**原因**: それまでの`IsAnyInputPressed()`は`Keyboard.GetState().GetPressedKeys()`（キーボードの物理キーを無差別に全部見る）と、接続中の全`PadInstance`の`GetPressedButtons()`を見ていた。これらはOS/Steamオーバーレイが横取りするスクリーンショット用のキー（`F12`・`PrintScreen`等）が物理的に押されている間も「押されている」と認識してしまい、ゲーム側がそのキーを一切使っていなくても再表示のトリガーになってしまっていた。

**修正**: `IsAnyInputPressed()`を、素のキーボード全キー走査から、**ゲーム本体が実際に認識する操作だけ**を見る方式に変更した。

```csharp
private static bool IsAnyInputPressed()
{
    if (IsToggleKeyPressed())
    {
        return true;
    }
    bool[] state = ControllerManager.instance.GetPadState().ToArray();
    for (int i = 0; i < state.Length; i++)
    {
        if (state[i]) { return true; }
    }
    return false;
}
```

`JumpKing.Controller.ControllerManager.GetPadState()`は、接続中の全デバイス(キーボード+各コントローラー)を合算した`PadState`（`public struct`、`up`/`down`/`left`/`right`/`jump`/`confirm`/`cancel`/`pause`/`boots`/`snake`/`restart`の11個の`bool`フィールドを持つ）を返す。`PadState.ToArray()`（`public`）でこれを`bool[]`に変換し、1つでも`true`があれば「ゲームにとって意味のある操作が行われた」と判定する。これに、このmod自身が設定しているトグルキー(`IsToggleKeyPressed()`)も合わせて見ることで、トグルキー自身を押しても引き続き再表示されるようにしている。

この変更により、`F12`/`PrintScreen`のようにゲーム本体のどの操作にも割り当てられていない物理キーは判定対象から外れ、スクリーンショット操作だけでは非表示状態が解除されなくなった。一方で、矢印キー・Space/Enter・Backspace・Escape等、ゲーム本体の何らかの操作に紐づくキーは引き続き「任意のキー」として機能する（実用上、誤って思い出せないトグルキーの代わりに表示を戻す手段としては十分な範囲）。

## 不具合修正: 一部のコントローラーで、ポーズ直後にトグルキーが効かない（実機テストで発覚・修正済み）

サードパーティ製のXbox 360互換USBパッド（`VID_045E&PID_028E`）を使っていると、ポーズを開いた直後にトグルボタンを押してもウィンドウが消えず、十字キーの上下などを押した後でないと効かない、という不具合が報告された。キーボードでは発生しない。

**原因**:
- ゲーム本体の`ControllerManager.Update()`は、ゲーム操作（up/down/jump/pause等の11種）の新規押下があったデバイスだけを`_current_main`（`GetMain()`の戻り値）にする。どの操作にも割り当てられていないトグルキーを押しても、メインは切り替わらない
- このパッドは、XInput（`XboxPad`、ゲーム内表示「xbox 360 controller 1」）とは別に、DirectInput（`LegacyPad`）としても認識されていた。ゲームはDirectInputデバイスの製品名に`"XINPUT"`（大文字小文字を区別）・`"360 controller"`・`"XBOX"`のいずれかが含まれる場合に重複とみなして除外する（`SlimJoystickManager.GetDevices()`）。しかしこのパッドのDirectInput名（レジストリ`HKCU\...\Joystick\OEM\VID_045E&PID_028E`の`OEMName`）は`Controller (XInput STANDARD GAMEPAD)`で、大文字小文字の違いにより除外をすり抜けていた
- 1回のボタン操作でXInput側とDirectInput側の両方に押下が届くため、タイミングや割り当て次第でメインがDirectInput側になる。その状態で`IsToggleKeyPressed()`が`GetMain()`だけを見ると、DirectInput側のボタン番号と、XInput形式で登録されたトグルキー（Xボタン=`16384`）を比べることになり、一致しない。上下を押してメインがXInput側に戻ると効くようになっていた

**修正**: `IsToggleKeyPressed()`を、`GetMain()`の1台ではなく`ControllerManager.GetConnectedPads()`の全デバイスを走査し、キーボードには`KeyboardToggleKeys`、それ以外には`PadToggleKeys`を照合する方式に変更した。ゲーム本体もプレイ操作やメニュー操作（ポーズキー含む）の判定は全デバイス合算（`GetPadState()`/`GetPressedPadState()`）で行っており、`GetMain()`はボタン名表示やキー登録など1台に絞る必要がある場面でだけ使っているため、それに揃えた形。デバイスをまたぐ操作（コントローラーでポーズを開いてキーボードのトグルキーを押す等）でも即座に効くようになった。

キー登録側（`CaptureToggleKeyNode`/`WaitUntilNoRawInputNode`）は`GetMain()`のまま変更していない。登録時にメインがDirectInput側でその番号が保存されても、判定側が全デバイスを見るため検出できる。登録側まで全デバイス走査にすると、1回の押下でXInput側とDirectInput側の両方から番号が来て、どちらを保存するか決める必要が生じるため。

**既知の限度（軽微、対応不要と判断）**: キーボード以外のデバイスは`PadToggleKeys`を共有しているため、XInputとDirectInputでたまたま同じ値になるボタン（XInputの十字キー・Start・Backの`1, 2, 4, 8, 16, 32`と、DirectInputのボタン1・2・4・8・16・32）をトグルキーにした場合は、両方のデバイスで反応する。

## 実機テストの完了状況

[TEST_CASES.md](TEST_CASES.md)の全11セクションのうち、10セクションをチェック済み。残る1項目（セクション1「`crashlog.log`に新規のクラッシュが追加されていないか」）は、テスト中に`MoreSaves`由来の無関係な例外（セーブフォルダへのアクセス拒否）が追記されていたため未チェックのまま残している（`F.HidePauseWindow`/`PauseManager`への言及は無く、本mod自体が原因のクラッシュではないと判断している）。

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

## 実装方針（実装済み。実際のファイル構成は[README.md](README.md)の「プロジェクト構成」を参照）

調査直後に立てた当初案では`HideToggleState.cs`/`MenuFactoryPatches.cs`という名前を想定していたが、実装時に以下の構成に整理した。

- `Settings.cs` — トグル用のキー/ボタン番号配列（`int[]`、キーボード/コントローラー別）をXML保存。既存mod群と同じ`Settings.Load`/`Save`パターン
- `PauseManagerPatches.cs` — 当初案の`HideToggleState.cs`の役割（エッジ検出・非表示フラグ）も統合
  - `PauseManager.PauseUpdate`へのHarmonyパッチ（prefix）→ 毎フレーム独自ボタンのエッジ検出、非表示フラグの反転。非表示中は`ControllerManager.instance.MenuController.ConsumePadPresses()`
  - `PauseManager.Draw`へのprefix → 非表示フラグが立っていれば`return false`
  - `PauseManager.SetPause`へのpostfix → ポーズ解除時に非表示状態をリセット
- `HideWindowKeyMenu.cs`/`CaptureToggleKeyNode.cs`/`WaitUntilNoRawInputNode.cs` — 当初案の`MenuFactoryPatches.cs`の役割。`[PauseMenuItemSetting]`/`[MainMenuItemSetting]`に「Bind Keys」項目を追加し、`Phoenixx19.SaveStates`の`MakeBindController`/`MakeBindButtonMenu`パターンを1ボタン分流用して「ボタンを押してください」UIを構築

## Harmonyの同梱方針

`keep_babe_skin`の調査で判明した通り、Jump KingのMod LoaderはWorkshopの「Required Items」経由の共有ライブラリ読み込みを安定して扱えないため、`0Harmony.dll`は本modにも直接同梱する方針とする（`confirm_count_control`/`keep_babe_skin`/`area_info_display_on_pause`と同じ）。

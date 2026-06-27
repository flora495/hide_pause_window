# Hide Pause Window

Jump Kingのmodです。ポーズ中に設定したボタンを押すと、ポーズ画面のウィンドウ（ステータス枠・メインメニュー枠・Objective欄など）をすべて非表示にし、フリーズした背景のゲーム画面をそのまま見ることができます。もう一度同じボタンを押すと元のポーズ画面に戻ります。

## 現在の状態

**実装・ビルド確認済み。実機での動作確認はまだ行っていません。** 調査の詳細・確定した仕様は[NOTES.md](NOTES.md)を、実機テストのチェックリストは[TEST_CASES.md](TEST_CASES.md)を参照してください。

## 使い方

メインメニュー（ホーム画面）→ **Mods** → **HidePauseWindow**、またはゲーム中の**ポーズメニュー → Mods → HidePauseWindow**から、以下の設定を変更できます。

1. **Enabled** - mod全体の有効/無効
2. **Bind Keys** - 選択して`Confirm`すると「ボタンを押して登録する」画面が開く。キーボードのキーを押すとキーボード用、コントローラーのボタンを押すとコントローラー用のバインドとして、それぞれ別に登録される（10秒間何も押さなければ自動的にキャンセルされる）。登録に使った押下自体が誤ってトグルとして反応することはない

ゲーム中に該当キー/ボタンを押すと、ポーズが解除されることなく、ウィンドウが非表示になります。非表示の間は上下キー・確認・キャンセルなどのメニュー操作は無効化されており、**キーボード・コントローラーを問わず何らかのキー/ボタンを押すと、それが何であっても表示が戻り**、通常の操作に戻ります（表示中は、設定した特定のキー/ボタンを新たに押した時だけ非表示になります。ポーズ自体の解除はこれらの押下では発生しません）。

設定はmod自身のdllと同じフォルダに`F.HidePauseWindow.Settings.xml`として保存されます。デフォルトはキーボード・コントローラーともに未設定（Unbound）です。

## 技術的な実現方針（概要）

- `JumpKing.PauseMenu.PauseManager.Draw()`をHarmonyでパッチし、非表示フラグが立っている間は描画自体をスキップする（ゲーム側に暗転オーバーレイ等は存在しないため、これだけで背景全体がそのまま見える見た目になる）
- `JumpKing.PauseMenu.PauseManager.PauseUpdate()`をHarmonyでパッチし、毎フレーム独自のボタン押下を検出（ゲーム本体のボタン体系には空きスロットが無いため、mod独自にキー/ボタンを保持・判定する）。非表示中は`ControllerManager.instance.MenuController.ConsumePadPresses()`でメニュー入力を握りつぶす
- キー登録UIは、ゲーム本体が内蔵する`BindButtonFrame`等のコントロール割り当て部品を、[Save States](https://steamcommunity.com/sharedfiles/filedetails/?id=3161216998) modと同じ手法で流用する

詳しい調査経緯・各クラスの実装根拠は[NOTES.md](NOTES.md)を参照してください。

## 必須環境

Harmonyを使用していますが、`0Harmony.dll`はこのmodに同梱されているため、別途サブスクライブする必要はありません。

## プロジェクト構成

| ファイル | 役割 |
|---|---|
| `ModEntry.cs` | modのエントリーポイント（`[JumpKingMod]`）。Harmonyパッチの適用と、メイン・ポーズ両メニューへの設定項目登録を行う |
| `Settings.cs` | 有効/無効フラグと、キーボード/コントローラー別のトグル用ボタンを保持する設定データ。XMLでの読み込み/保存 |
| `EnabledToggle.cs` | mod自体の有効/無効を切り替えるチェックボックス部品 |
| `PauseManagerPatches.cs` | `PauseManager.Draw`/`PauseManager.PauseUpdate`/`PauseManager.SetPause`へのHarmonyパッチ。描画スキップ、トグルキーのエッジ検出、非表示中のメニュー入力無効化、ポーズ解除時の状態リセットを行う |
| `HideWindowKeyMenu.cs` | 設定一覧に表示する「Bind Keys」項目と、その先の「ボタンを押して登録する」UI（`BindButtonFrame`を使用）を組み立てる |
| `CaptureToggleKeyNode.cs` | 次に押されたボタンを検出し、キーボード/コントローラー別に設定へ保存するノード |
| `WaitUntilNoRawInputNode.cs` | キー登録を開始する前に、選択操作に使ったボタン自体が押されたままになっていないかを待つノード |

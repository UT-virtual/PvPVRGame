# Desktop / VR 共通起動

## 使うシーン

`Assets/Scenes/GameScene.unity` をWindows・Quest・Editorで共通に使う。既存MainSceneを複製し、その時点の地形・RoundManager・XR Origin・UIを引き継いでいる。旧MainSceneとForPCMainSceneは検証・比較用として残す。共通構成へ移行した端末と旧ビルドを同じ部屋で混在させない。

Build Settingsの有効シーンはGameSceneのみ（Build Index 0）。Unity 6のBuild Profileで独自のScene Listを使用している場合は、その一覧もGameSceneに揃える。WindowsとAndroidで同じシーン構成とPrefabを使って再ビルドする。

## 起動モード

- Editor：GameSceneのNetworkLauncherに付くLocalPlayModeControllerのEditor Modeで選ぶ。共通シーンの保存値はVirtual Reality。
- Windows exe：引数なしでDesktop。`--vr`を付けた場合だけPCVR。
- Quest Androidビルド：引数なしでVR。
- `--desktop`はDesktopを明示する。両引数を同時に付けると起動準備を止めてエラーを表示する。
- Editor専用のSimulate Vr In Editorは、HMDなしでVR分岐を調べる場合だけ使用する。実機テストではOFF。

起動後にHMDを接続しても、選択済みのDesktopモードはVRへ変わらない。モードを変更する場合はアプリまたはPlayモードを再起動する。

## XR設定

Windows / AndroidともInitialize XR on StartupをOFFにする。OpenXRのLoader・各プラットフォームの必要なFeatureは有効にしておく。LocalPlayModeControllerがVRの場合だけ手動でXRを初期化する。初期化は最初のStart処理後に行い、XR Displayの起動確認後に通信へ進む。失敗時は接続を始めず、画面とログへ理由を表示する。

DesktopではXR Originを非アクティブにし、PC用Camera・Cinemachine・Audio Listenerを有効にする。VRではVR用CameraとListenerを使い、PC用を無効にする。Cameraの有効性を確認せず唯一のCameraをOFFにすることを避ける。

## 通信とプレイヤー

2026-10-30更新：待機中・切断後はR／ゲームパッドStart／VR両手グリップで再接続できる。旧Runner終了後にGameSceneを再読み込みしてシーン内のNetworkObjectと参照を復元する。詳細と検証範囲は[再接続・VR観戦の修正](RecoveryChanges.ja.md)を参照。

NetworkLauncherはプロセス内の単一インスタンスとして常駐する。読み込まれたシーンにもLauncherがある場合は、そのシーンのUI・Camera・XR参照を既存Launcherへ渡し、新しいLauncherオブジェクトは破棄する。追加の自動接続は行わない。

NetworkSessionControllerにも接続所有者のチェックを設け、開始中の二重要求を拒否する。再接続では旧RunnerのShutdownを待つ。終了後の非同期処理が別の接続へ干渉しないよう、操作番号と破棄状態を確認する。Launcher終了時にはRunnerも終了させる。

Hostは全員にGamePlayer.prefabを生成する。GamePlayerはPlayer.prefabを複製した共通Prefabであり、PC用部品とVR用頭・手同期を含む。

- 自分の入力とCameraはローカルの起動モードを使う。
- Hostは各プレイヤーの入力IsVRを使って移動・射撃を分ける。
- PC射撃は従来のPCPlayerと同様に視線・目の位置から発射する。
- VR射撃は割り当てられた銃口を使う。
- VRBodyTargetSyncは入力のモードを同期し、VRプレイヤーにだけVRIKと頭・手姿勢を適用する。PCプレイヤーはアニメーションとCameraTargetに取り付ける武器表示を使う。
- 他人のCameraTarget更新を、見ている端末のモードで止めない。対象プレイヤーのモードで判断する。

スキル選択のVR操作やワールド空間UIも同じローカルモードに従う。

## テストする順番

1. GameSceneを開きEditor ModeをDesktopにしてPlay。PC Cameraだけで表示されることを確認。
2. WindowsへGameSceneをビルド。引数なしで起動し、HMD接続後もDesktopのままか確認。
3. Editor ModeをVirtual Realityへ変更し、Link / Air LinkでPlay。Windows exeとEditorの2人でReadyから開始。
4. Windows exeを先に起動する場合と、Editorを先に起動する場合の両方を試す。
5. QuestへAndroidビルドを入れ、EditorのVR＋Quest単体のVRでも確認する。

Photon App ID、App Version、リージョン、ルーム名を揃える。特に同じ名前のルームでもリージョンが違えば合流できない。

確認項目：実人数と待機人数が一致する、各プロセスにRunnerが1つ、全員Readyで開始する、移動・頭・手・銃口・射撃方向が正しい、次ラウンドでも入力とカメラが保たれる。PCでの武器表示位置はGamePlayerのVRWeaponVisualSettingsのDesktop Weapon Local Positionで調整する。

## 診断ログと自動確認

- `[PlayMode]`：選ばれたモードとXRの準備状況。
- `[NetworkSessionController]`：接続開始、シーン名・Build Index・Owner ID・Runner ID、切断。
- `[NetworkLauncher]`：シーン読み込み、参照の引き継ぎ、重複Launcherの抑止、Camera設定。
- `[NetworkPlayerSpawner]`：参加者生成と重複参加コールバックの抑止。

`Tools > PvPVRGame > Validate Shared Game Setup`で起動モード選択、ビルドシーン、XRの自動起動OFF、必要なシーン参照と共通Prefabを確認できる。この操作はGameSceneを開くため、作業中シーンを保存してから実行する。

バッチ確認の入口はGameSetupValidation.Run。DesktopのPlayモード起動と重複Launcher抑止をネットワーク接続なしで確認する入口はGameSetupValidation.RunDesktopSmoke。実際のPhoton通信とHMDのトラッキング・操作・画質は実機テストが必要。

## 今回の検証（2026-10-23）

- Unity同梱のC#コンパイラと既存のUnityビルド引数を使用し、Editor向け（検証メニューを含む）とWindowsプレイヤー向けのコンパイルが成功。FusionのIL後処理を含むUnityの完全ビルドではない。
- コンパイル済みプレイヤーコードに対し、Windows/Quest/Editorの既定モード、明示引数、大文字小文字、競合引数の8ケースが成功。
- GameSceneとGamePlayerのYAMLに重複ID・欠落したローカル参照がないこと、共通Prefabの指定・有効シーン・XR自動起動OFFを確認。
- Unityバッチ検証は、このプロジェクトが既存Editorで開かれているため起動を拒否された。既存Editorは閉じていない。Playモードのカメラ・単一Launcher確認、実際のPhoton接続、HMDでの操作、Androidビルドは未実行。

# PvPVRGame 現行実装仕様書

版：1.1 / 更新日：2026-10-30（全体調査：2026-10-24） / 対象：Desktop・VR共通化後の作業ツリー

この文書は「今あるコードがどのように組み合わさり、いつ動くか」を説明する実装仕様書です。理想の完成仕様ではありません。ゲームのルール、実際の保存設定、ソフトウェアの責務、未検証事項を分けて記載します。

**最初に読む順番：1章の全体像 → 4章の試合の流れ → 5章の一連の実行 → 17章の修正場所。細部が必要になったら、18章の全スクリプト索引へ進んでください。**

2026-10-30の復旧修正・検証とレビュー訂正は[再接続・VR観戦の修正](RecoveryChanges.ja.md)を参照してください。

## 目次

1. [ゲームとシステムの全体像](#sec-01)
2. [文書の対象と確認状態](#sec-02)
3. [Scene・Prefab・配置する部品](#sec-03)
4. [ゲームルールと試合の状態](#sec-04)
5. [起動から次の試合までの実行シナリオ](#sec-05)
6. [Desktop／VRの起動・Camera・身体](#sec-06)
7. [ネットワークの責務とライフサイクル](#sec-07)
8. [入力仕様と処理タイミング](#sec-08)
9. [移動・HP・射撃・アイテム](#sec-09)
10. [スキル仕様](#sec-10)
11. [UI・音・観戦](#sec-11)
12. [現在の設定値と変更場所](#sec-12)
13. [ビルドと複数端末テスト](#sec-13)
14. [検証ツールと受け入れ条件](#sec-14)
15. [現行の制限・未決定事項](#sec-15)
16. [不具合調査の入口](#sec-16)
17. [何を変えるとき、どこを読むか](#sec-17)
18. [全ゲームスクリプト索引](#sec-18)
19. [更新方法・根拠ファイル](#sec-19)

<a id="sec-01"></a>

## 1. ゲームとシステムの全体像

惑星表面を移動し、射撃とラウンドごとに選ぶ2つのスキルを使って戦うネットワーク対戦ゲームです。WindowsのDesktop、PC接続のVR、Quest単体のVRを、共通のゲームシーンで動かす構成です。現時点で全環境の実機動作が確認済みという意味ではありません。

中心になるのは、次の3つです。

- **NetworkLauncher：接続するまでと、自分の操作環境を準備する入口。**
- **RoundManager：待機、選択、戦闘、結果を切り替える試合の司令塔。**
- **PlayerController：1人分の入力を移動・射撃・スキルへ振り分ける実行窓口。**

```text
各端末の準備                         全員で共有するゲーム
LocalPlayModeController             RoundManager
  ├─ Desktop：PC入力・PC Camera       ├─ Ready → 選択 → 開始待ち
  └─ VR：XR・HMD・左右の手            └─ 戦闘 → 結果 → 次ラウンド
          ↓                                  ↓
NetworkLauncher → NetworkRunner → GamePlayerを人数分生成
                                        ↓
                              PlayerController
                              ├─ Move / Look / Camera
                              ├─ Weapon → Projectile → Health
                              └─ Skill / 頭と手 / 表示・音
```

**自分の端末で変わるもの**は、入力装置、Camera、Audio Listener、XRの利用、ローカルUIです。**参加者で共有するもの**は、ゲームシーン、生成するプレイヤー、位置・HP・弾数・スキル状態、試合のphaseです。

<a id="sec-02"></a>

## 2. 文書の対象と確認状態

対象はAssets/Scripts配下のゲームスクリプト、GameScene、GamePlayer、Editorの検証ツール、ビルド・XR設定です。Photon、XRI、FinalIKなどのライブラリ内部全体は対象外ですが、ゲーム側から使う役割は説明します。

- **実装確認**：2026-10-24時点のコード・Scene・Prefabの保存内容を確認。
- **保存値**：Inspectorで保存された値。C#フィールドの初期値より優先されます。
- **前回の機械検証**：2026-10-23にEditor／Windows向けC#コンパイル、起動モード8ケース、Scene／Prefabのローカル参照を確認。
- **未検証**：Unityでの完全なビルド・FusionのIL後処理・Play動作・実際のPhoton通信・HMD操作・Androidビルド。前回のUnityバッチ起動は既存Editorがプロジェクトを開いていたため拒否されました。

この文書を作るためにゲームの処理は変更していません。過去の実行ログだけで、現行改修の不具合修正が実証されたとは扱いません。

### 2.1 主な依存関係

| 項目 | 版 | 用途 |
| --- | --- | --- |
| Unity Editor | 6000.3.17f1 | ProjectVersionに保存されたEditor版 |
| Photon Fusion | 2.0.12 | 接続・NetworkObject・入力・状態同期 |
| FinalIK / VRIK | 導入済み（版未確定） | VRの頭・手ターゲットから身体姿勢を作る |
| URP | 17.3.0 | 描画 |
| Cinemachine | 3.1.7 | PC・観戦Camera |
| Input System | 1.19.0 | Input Actionsと入力取得 |
| XR Interaction Toolkit | 3.3.2 | XR Origin・コントローラー構成 |
| OpenXR | 1.16.1 | XRデバイスとの接続 |
| Meta XR SDK | 203.0.0 | Quest関連機能 |

Photon Fusion 2.0.12はAssets/Photon以下の導入物とFUSION_2_0_12定義を確認しています。FinalIKはAssets/Plugins/RootMotion以下にあり、パッケージマニフェストだけでは版を確定できないため、正確な版は記載しません。

<a id="sec-03"></a>

## 3. Scene・Prefab・配置する部品

### 3.1 正式な共通構成

- **GameScene.unity**：共通ゲームシーン。MainSceneを複製し、管理系・地形・UI・XR Originを引き継いでいます。
- **GamePlayer.prefab**：Hostが全員に生成する共通プレイヤー。Player.prefabを複製し、PC表示とVR身体同期を両方持たせています。
- **MainScene / ForPCMainScene**：既存の比較・検証用シーン。現在の標準起動先ではありません。
- **Player / PCPlayer**：既存Prefab。GameSceneの標準生成先ではありません。

EditorBuildSettingsではGameSceneだけが有効で、Build Indexは0です。WindowsとAndroidで同じ一覧を使います。個別Build ProfileのScene Listが有効なら、そちらも確認してください。同じIndexに別のゲームシーンを割り当てる構成は採用しません。

### 3.2 Scene側とPlayer側を分ける

Scene側にはNetworkLauncher、LocalPlayModeController、RoundManager、HealthItemSpawner、SpectatorCameraController、各種UI、BgmManager、PC用Camera／Cinemachine、XR Originを置きます。

Player側にはNetworkObject、NetworkTransform、NetworkPlayer、PlayerController、CharacterController、PlayerMove、PlayerLook、PlayerCamera、PlayerWeapon、PlayerHealth、PlayerSkillController、PlayerIdentity、VRBodyTargetSync、VRWeaponVisualSettings、PlayerSound、XRayVisionControllerなどを置きます。子オブジェクトには見た目、武器、CameraTarget、頭・手のターゲット、頭上アイコンや一部HUDがあります。

SceneのXR Originは端末ごとに存在し、自分のプレイヤーへだけ取り付けます。相手のHMDをそのまま生成するのではなく、相手の頭・手の位置をネットワークで受け取ってアバターへ反映します。

### 3.3 Inspectorに付けないクラス

NetworkSessionController、NetworkPlayerSpawner、NetworkInputCollector、NetworkConnectionUIController、LocalSkillSelectionInputControllerはNetworkLauncherがnewで作ります。Round系の補助Controller、Registry、CalculatorなどはRoundManagerが作ります。

これらは通常のC#クラスです。UnityがUpdateを自動呼び出しする部品ではなく、所有者のメソッドから呼ばれて動きます。時間待ちが必要な場合は、所有者のMonoBehaviourを通じてCoroutineを開始します。

<a id="sec-04"></a>

## 4. ゲームルールと試合の状態

RoundManager.phaseはNetworked状態です。Host側が状態を変え、各端末のRoundManager.Renderが変化を検出してUIへ反映します。

```text
WaitingForReady
    ↓ 必要人数を満たし、登録プレイヤー全員がReady
SkillSelecting
    ↓ 全員が2つ選択、または締切で不足分を自動選択
RoundStarting
    ↓ スキル適用・開始待ち
RoundPlaying
    ↓ 死亡通知を受け、生存者が1人以下
RoundEnding
    ├─ 残りラウンドあり → SkillSelecting
    └─ 最終ラウンド終了 → MatchFinished → WaitingForReady
```

### 4.1 各状態の操作と仕事

| 状態 | 主な仕事 | 移動 | 武器・スキル使用 | 次へ進む条件 |
| --- | --- | --- | --- | --- |
| WaitingForReady | 登録、Ready、待機一覧 | 可 | 不可 | 人数条件＋全員Ready |
| SkillSelecting | 候補配布、選択、再配置 | 不可 | 通常戦闘操作は不可 | 全員完了または締切 |
| RoundStarting | 選択スキル適用、開始演出 | 不可 | 不可 | 開始待ち終了 |
| RoundPlaying | 戦闘、HP、弾、アイテム | 可 | 可 | 死亡時に生存1人以下 |
| RoundEnding | 得点、ラウンド結果 | 不可 | 不可 | 結果表示待ち終了 |
| MatchFinished | 最終結果、状態リセット | 不可 | 不可 | 待機へ戻る時間経過 |

待機・戦闘・結果は同じゲームシーン内の状態です。通常のラウンド進行で別Sceneへ順に移動する構造ではありません。通信に参加するときのFusionシーン同期とは区別してください。

### 4.2 人数、色、勝敗

接続要求の上限はNetworkSessionController.StartGameのPlayerCount=8。開始条件の最少人数はGameSceneでは1で、開発用に1人でもReadyから開始できます。複数人の本来の対戦条件に変更する場合はRoundManagerのminPlayersToStartを変更します。

参加者には登録順でRed、Blue、Green、Yellowを循環して割り当てます。5人以上では同じ色が再利用されます。

ラウンド終了は**生存チーム数ではなく生存プレイヤー数**で判定します。最後の1人に1点、0人なら勝者なし。スコアの内部保存はPlayerHealthごとですが、結果UIの最終勝者マスクは色ごとの合計得点で決定し、同点の色は複数表示できます。個人単位のログ集計と色単位の結果表示があるため、5人以上の遊び方は別途仕様を整理する余地があります。

戦闘を時間切れで終わらせる処理は確認できません。1人戦や、誰も死亡しない場合に自動終了するとは限りません。

<a id="sec-05"></a>

## 5. 起動から次の試合までの実行シナリオ

### 5.1 起動・準備

1. NetworkLauncher.Awakeがプロセス内の既存Launcherを確認。なければ自分を常駐させ、補助クラスと入力・接続UIを準備します。
2. LocalPlayModeController.SelectModeがDesktop／VRを決めます。起動後のHMD接続状況でモードを自動変更しません。
3. NetworkLauncher.StartのCoroutineが初期化を待ちます。VRは最初のStart段階後にXR Loaderを初期化し、XR Displayの稼働を待ちます。
4. Camera、Audio Listener、XR Origin、UI追従先を設定します。準備失敗時は理由を表示し、接続しません。

### 5.2 接続・生成

1. Auto Start On LaunchがONならAutoHostOrClientでTestRoomへ接続します。OFFならHost／ClientのUIを使う設計ですが、現行GameSceneの接続ボタン参照は未設定です。
2. NetworkSessionControllerがPrefabと現在のSceneのBuild Indexを確認し、NetworkRunnerとNetworkSceneManagerDefaultを作ります。
3. Runner.StartGameへGameMode、SessionName、SceneRef、SceneManager、PlayerCountを渡します。
4. 参加イベントOnPlayerJoinedをNetworkPlayerSpawnerへ渡し、HostだけがGamePlayerを生成します。参加者をInput Authorityとして割り当てます。
5. PlayerIdentityへ番号・表示色インデックスを設定。PlayerHealth.SpawnedはHost側でHP・生死・Readyなどを初期化し、RoundManagerへ登録します。
6. RoundPlayerRegistryが参加者と死亡通知の購読を管理し、Ready・スコア・スキル選択にも登録します。入力所有者のないデバッグダミーは通常の試合登録対象から除外されます。

### 5.3 自分のプレイヤーの準備

NetworkPlayer.SpawnedはHasInputAuthorityを使って自分か他人かを判定します。自分のプレイヤーへ入力を登録し、VRならXR Originを子として取り付け、頭・手の参照を渡します。1フレーム後にローカルCameraを設定し、さらに1フレーム後に待機室へ生成完了を通知します。

PlayerHealth、PlayerController、NetworkPlayerなど複数コンポーネントのSpawnedが関係します。この説明は責務と呼び出し経路であり、それら全部の厳密な呼び出し順を保証するものではありません。

### 5.4 Ready・選択・開始

Ready入力は入力パケットを通ってHostのPlayerControllerへ届き、RoundManager.SetPlayerReadyを呼びます。全員ReadyならRoundSkillSelectionControllerが候補を作り、弾と回復アイテムを消し、SpawnPointsへ全員を再配置します。

1回目・2回目それぞれ4候補をHostが作って全員へ通知します。LocalSkillSelectionInputControllerがカーソルと確定を読み、RPC_RequestSelectSkillBySlotでHostへ要求。Hostは選択回数などを確認し、進捗を返します。締切では不足する選択を自動補完します。

RoundStartControllerが2つのスキルを各Playerへ適用し、RoundStartingへ移行。開始演出と開始待ちを経てRoundPlayingへ進み、回復アイテムを配置します。

### 5.5 命中・死亡・結果

```text
Projectile.OnTriggerEnter
  → 相手のPlayerHealthを探す
  → TakeDamage：軽減・蓄積を考慮
  → ApplyDamageImmediately：HPを更新
  → HPが0ならDie
  → OnDiedを発行
  → RoundPlayerRegistryが登録した通知先
  → RoundManager.HandlePlayerDied
  → RoundFlowController：生存者を数える
  → 必要なら得点加算・結果UI・次ラウンド
```

死亡そのものとラウンド終了は別の判断です。弾はHPや試合を直接管理せず、HP担当へダメージを渡します。HP担当は死亡を通知し、試合担当が終了条件を判断します。

最終ラウンド後は得点・Ready・選択・スキル履歴をリセットし、全員を再配置して待機へ戻ります。通常の次試合のために全員が再接続する必要はありません。

<a id="sec-06"></a>

## 6. Desktop／VRの起動・Camera・身体

### 6.1 モード選択

| 実行環境 | 引数なしの動作 | 明示指定 |
| --- | --- | --- |
| Unity Editor | LocalPlayModeControllerのEditor Mode | --desktop／--vrがあればそちらを優先 |
| Windowsビルド | Desktop | --vrでPCVR、--desktopでDesktop |
| Androidビルド | VR | --desktopで上書きも可能だが、主用途はQuest VR |

両方の引数を指定した場合はエラーです。大文字小文字は区別しません。GameSceneのEditor Modeの保存値はVirtualRealityで、XR準備の待ち時間上限は15秒です。HMDなしで通常プレイする場合はEditor ModeをDesktopに変えます。

XRの自動起動はWindows／AndroidともOFF。OpenXR Loaderは残し、VR時だけ手動で起動します。Desktop起動ではXR Originを非アクティブにするため、後からQuestを接続しても起動モードをVRへ変更しません。

### 6.2 ローカル表示

- Desktop：PC Camera、Cinemachine Brain、CinemachineCamera、PC Audio Listenerを有効にします。CameraTargetを視線・目の位置に更新し、Cinemachineが追従します。
- VR：XR Origin内のCameraとListenerを使い、PC用を停止します。XR Originを自分のPlayerの子にし、余分なCharacterControllerを停止。HMDと手の姿勢を入力・身体同期へ渡します。
- 起動設定やCamera参照が不足していれば、接続前に準備を中断します。PlayerCamera側にもVR Cameraの有効性確認があります。

### 6.3 相手の表示と武器

VRBodyTargetSyncは、Hostで入力のIsVRと頭・手をNetworked状態へ保存します。自分はLateUpdateで実機の姿勢を反映し、他人はRenderで同期姿勢を反映します。VRの身体はFinalIKのVRIKを使用。DesktopプレイヤーにはVRIKを適用せず、通常のアニメーションと視点に取り付ける武器を使います。

Desktop射撃は視線と目の位置から、VR射撃は銃口から発射します。VR射撃直前にはHost上でもその入力刻みの手の姿勢を適用し、Render待ちの古い銃口を使わないようにしています。他人のDesktop CameraTargetを更新する際も、見る側のHMD状態ではなく対象プレイヤーのモード・同期視点を使います。

Simulate Vr In EditorはXR起動確認を省略する開発オプションです。実機の頭・手のトラッキングを生成する機能ではありません。旧Force Vr Simulation In Editor／Keyboard HMD Simulationは別の入力補助フラグとして残っており、現在のモードを選ぶスイッチではありません。

<a id="sec-07"></a>

## 7. ネットワークの責務とライフサイクル

### 7.1 誰が決めるか

**Input Authority**はそのPlayerの入力元、**State Authority**は確定状態を更新する側です。現在のHost／Client方式では、各端末が自分の入力を送り、Hostが移動・弾・HPなどを処理します。

PlayerController.FixedUpdateNetworkはGetInput後にState Authorityを確認し、持たない側では実際の移動・射撃へ進みません。したがって、現行コードの移動を「クライアントでも予測実行する」とは説明しません。自分の視線はUpdate時にローカルにも即時反映するため、視線と移動は経路が異なります。

### 7.2 3種類の連絡手段

| 仕組み | 用途 | 具体例 |
| --- | --- | --- |
| Networked | 継続的な状態の同期 | HP、弾数、phase、頭・手、スキルとTickTimer |
| RPC | 端末間の要求・通知 | 選択要求、選択候補・進捗、結果表示、復活通知 |
| C# event / Action | 同じプロセス内の通知 | OnDied、OnHealthChanged、OnAmmoChanged、OnShot |

C# eventを呼んだだけでは、ネットワーク上の全員に同じイベントが届くわけではありません。音・演出の全端末再生は、どこで発火しているかを個別に確認します。

### 7.3 接続を増殖させないための管理

NetworkLauncherは常駐する単一インスタンスです。読み込まれたSceneにもLauncherがある場合、Scene内のUI・XR・Camera参照を既存のものへ引き継ぎ、新しいLauncherオブジェクトを破棄します。Sceneの再読込で自動接続を増やさない構成です。

NetworkSessionControllerにもプロセス内の接続所有者を確認するチェックがあります。Runner作成・接続開始・失敗時の後始末・再接続・終了を所有します。再接続は旧RunnerのShutdownを待ちます。非同期処理には操作番号と破棄状態の確認があり、終了後の古い処理が新しい接続へ干渉しないようにしています。

NetworkPlayerSpawnerはHost側でPlayerRefごとに1体だけ生成し、同じ参加者への重複した参加通知を拒否します。Runnerが切り替わったときは生成管理をリセットします。

### 7.4 Scene同期・再接続・退出

StartGameへ現在のSceneRefとNetworkSceneManagerDefaultを渡すため、端末ごとに別のゲームシーンを独立維持する構成ではありません。Host側のScene情報と対応できる共通シーン一覧を使います。

自動再試行はClientモードでGameNotFoundになった場合だけで、間隔は1秒、回数上限0は無制限を意味します。AutoHostOrClientであらゆる失敗を無制限再試行する実装ではありません。

待機中または切断後は、Rキー／ゲームパッドStart／VR両手グリップでClientとして再接続できます。旧Runnerの終了後にGameSceneを再読み込みし、RoundManager・UI・Camera・入力参照を復元します。退出時は対応PlayerをDespawnし、各登録から除外します。Host移行コールバックの処理は空であり、Host終了後の対戦継続は保証しません。

### 7.5 主な同期データ

| 担当 | 主なフィールド（コードから抽出） |
| --- | --- |
| [PlayerNetworkInput](../Assets/Scripts/Network/PlayerNetworkInput.cs) | 入力パケット：`Vector2 MoveInput`、`Vector2 LookInput`、`NetworkButtons Buttons`、`Vector3 AimForward`、`Vector3 ViewForward`、`byte HasLookDirection`、`NetworkBool IsVR`、`Quaternion HMDRotation`、`Vector3 HMDPosition`、`Vector3 LeftHandPosition`、`Quaternion LeftHandRotation`、`byte HasLeftHand`、`Vector3 RightHandPosition`、`Quaternion RightHandRotation`、`byte HasRightHand` |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | `GamePhase phase` |
| [PlayerController](../Assets/Scripts/Player/PlayerController.cs) | `NetworkButtons PreviousButtons`、`NetworkBool NetworkedIsRunning`、`float NetworkedMoveX`、`float NetworkedMoveY`、`NetworkBool IsCurrentInputVR`、`Vector3 NetworkedSpectatorCameraPosition`、`Vector3 NetworkedSpectatorViewForward`、`Vector3 NetworkedSpectatorViewUp` |
| [PlayerHealth](../Assets/Scripts/Player/PlayerHealth.cs) | `float NetworkedCurrentHealth`、`NetworkBool NetworkedIsDead`、`NetworkBool NetworkedIsReady`、`int NetworkedTeamIndex` |
| [PlayerWeapon](../Assets/Scripts/Player/PlayerWeapon.cs) | `int NetworkedCurrentAmmo`、`NetworkBool NetworkedIsReloading` |
| [PlayerSkillController](../Assets/Scripts/Player/PlayerSkillController.cs) | `PlayerSkillType CurrentRoundSkill`、`PlayerSkillType CurrentRoundSecondSkill`、`PlayerSkillType LastRoundSkill`、`PlayerSkillType LastRoundSecondSkill`、`PlayerSkillType ActiveSkill`、`TickTimer SkillActiveTimer`、`TickTimer CooldownTimer`、`PlayerSkillType ActiveSecondSkill`、`TickTimer SecondSkillActiveTimer`、`TickTimer SecondCooldownTimer`、`int CurrentSelectedSkillIndex` |
| [VRBodyTargetSync](../Assets/Scripts/Player/VRBodyTargetSync.cs) | `NetworkBool NetworkedIsVr`、`NetworkBool NetworkedTargetsInitialized`、`Vector3 NetworkedHeadPosition`、`Quaternion NetworkedHeadRotation`、`Vector3 NetworkedLeftHandPosition`、`Quaternion NetworkedLeftHandRotation`、`Vector3 NetworkedRightHandPosition`、`Quaternion NetworkedRightHandRotation` |
| [PlayerIdentity](../Assets/Scripts/Player/PlayerIdentity.cs) | `int PlayerNumber`、`int ColorIndex` |

| 担当 | RPC | 送受信指定 |
| --- | --- | --- |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_RequestSelectSkillBySlot | RpcSources.All, RpcTargets.StateAuthority |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_NotifySkillSelectionProgress | RpcSources.StateAuthority, RpcTargets.All |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_SetSkillOptions | RpcSources.StateAuthority, RpcTargets.All |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_ShowRoundEndUI | RpcSources.StateAuthority, RpcTargets.All |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_HideRoundEndUI | RpcSources.StateAuthority, RpcTargets.All |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_ShowFinalResultUI | RpcSources.StateAuthority, RpcTargets.All |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs) | RPC_HideFinalResultUI | RpcSources.StateAuthority, RpcTargets.All |
| [PlayerHealth](../Assets/Scripts/Player/PlayerHealth.cs) | RPC_AfterRespawn | RpcSources.StateAuthority, RpcTargets.All |
| [PlayerHealth](../Assets/Scripts/Player/PlayerHealth.cs) | RPC_SetAliveState | RpcSources.StateAuthority, RpcTargets.All |

CurrentRoundは通常のC#プロパティであり、このNetworked一覧には含まれません。結果UIにはRPCでラウンド番号が渡されます。各UIのラウンド表記がHostと一致するかは通信テストで確認してください。

<a id="sec-08"></a>

## 8. 入力仕様と処理タイミング

### 8.1 毎フレームとネットワーク刻み

NetworkLauncher.Update → NetworkInputCollectorで視線・移動・ボタンを読み、短いボタン操作をキューへ保存します。FusionのOnInputでPlayerNetworkInputへ詰め、HostのPlayerController.FixedUpdateNetworkが実行します。ボタンの押し始めはPreviousButtonsとの差で判定し、射撃は押されている間の状態を使います。

SkillSelecting中は通常のボタン蓄積より選択入力を優先します。移動・武器などもphaseで制限されます。Input Actionsの定義があるだけで、その操作がゲームで使われるとは限りません。

### 8.2 現在の主な操作

下表のInput Action割り当てはPlayerControls.inputactionsの定義、コード固定の割り当てはNetworkInputCollector／LocalSkillSelectionInputControllerから確認しています。SceneのInputActionReferenceはInspectorでも確認してください。

| 操作 | Desktop・ゲームパッド | VR | 備考 |
| --- | --- | --- | --- |
| 移動 | WASD・矢印、左スティック | 左スティック | Player/Move。惑星表面で移動 |
| 視点 | マウス、右スティック | HMD回転＋右スティック旋回 | Player/Lookが割り当て済みならそちらが先 |
| ジャンプ | Space、ゲームパッド南ボタン。東ボタンの固定フォールバックもある | 右コントローラーprimaryButton | Action成功後または固定入力を判定 |
| 射撃 | Player/Attackにマウス左、Enter、ゲームパッド西ボタン等 | 右トリガー | fireActionがあればそちらを使う。Jキー等は未設定時のフォールバック |
| リロード | K、ゲームパッド西ボタン | 右コントローラーsecondaryButton | Player/Reload。Attackとの西ボタン重複に注意 |
| Ready | Enter。ゲームパッド左トリガー | 左右トリガー同時押し | VR時は左右同時押しの経路を使う |
| 選択カーソル | WASD、移動Action、左スティック | 左スティック | 2×2の4枠、倒しっぱなしの連続移動を抑制 |
| 選択確定 | Enter、ゲームパッド南ボタン | 左右いずれかのトリガー | HostへRPCで選択要求 |
| スキル切り替え／発動 | Q／U、右肩／左肩ボタン | 左手を下向きで左トリガー＝枠1、上向きで＝枠2 | 上下判定はHMDのupとの内積±0.5 |
| 待機中・切断後の再接続 | R | 両手グリップ | Clientとして接続し直す |
| 観戦対象変更 | A／D、左スティック左右 | 左スティック左右 | 戦闘が続いている場合のみ |

PlayerControlsにはCrouch、Sprint、Interactなども定義されていますが、現行の主要ゲーム入力パケットに専用の処理はありません。PlayerInput.csはPrefabに存在するものの、ReadInputを呼ぶ主要経路は確認できず、現在の入口はNetworkInputCollectorです。

### 8.3 Unity／Fusionの呼び出し場所

| タイミング | このゲームでの主な用途 |
| --- | --- |
| Awake | 参照取得、管理者の所有確認、補助クラスの生成 |
| OnEnable / OnDisable | Input Actions、UI検索・イベント購読の開始と停止 |
| Start | モード準備の待機、自動接続など |
| Spawned | Fusionに接続されたオブジェクトの初期化・試合登録 |
| Update | ローカル入力、UIの表示判定、透視、BGM |
| OnInput | その端末のPlayerNetworkInputをFusionへ渡す |
| FixedUpdateNetwork | Hostの操作・弾移動・スキル時間更新 |
| Render | アニメーション、phase表示、他人の身体姿勢・スキル効果反映 |
| LateUpdate | CameraTarget、自分の頭・手、ワールド空間UIの追従 |
| OnTriggerEnter | 弾の命中、回復アイテムの取得 |
| Coroutine | 選択締切、開始待ち、結果表示、アイテム再出現 |
| Despawned / OnDestroy | 登録解除、入力・UI・通信・XRの片付け |

UpdateとFixedUpdateNetworkの回数は1対1ではありません。別コンポーネント同士の厳密な実行順は、表の上から順という意味ではありません。

<a id="sec-09"></a>

## 9. 移動・HP・射撃・アイテム

### 9.1 移動・重力

PlayerMoveはCharacterControllerで移動します。SphereCastで地面を探し、接地している場所の法線を上方向として、表面に沿った前・右方向を作ります。地面が見つからないときはplanetCenterからの方向を利用する経路があります。姿勢合わせ、接地への補正、重力、ジャンプ、スキルの倍率を組み合わせます。

DesktopのMoveOnSurfaceとVRのMoveOnSurfaceVRを入力のIsVRで分けます。スキル選択・開始待ち・結果中は通常移動を停止。死亡時もPlayerControllerの操作処理を止めます。

### 9.2 武器と弾

PlayerWeapon.TryFireProjectileは状態権限、リロード中、発射間隔、弾数を検査します。発射できた場合にRunner.Spawnで弾を生成し、弾数とタイマーを更新します。弾数0の通知はOnDryFireです。

ProjectileはHost側のFixedUpdateNetworkで位置と寿命を進め、OnTriggerEnterで命中を処理します。所有者自身への接触を除外し、対象へダメージを渡してDespawnします。重力爆発弾ではOverlapSphereで範囲内を調べ、同じPlayerHealthへ重複して爆発ダメージを与えないようにしています。通常弾はTrigger接触方式であり、Hitscanや過去位置を使う命中補正を実装しているとは説明しません。

弾にはTrigger Colliderと、重力OFF・KinematicのRigidbodyを設定する処理があります。高速弾のすり抜けや各VR環境での命中精度は実機確認項目です。

### 9.3 リロードと特殊弾

ReloadAmmoがリロード状態と時間を設定し、PlayerControllerから呼ぶWeapon.Tickが時間を減らします。完了で弾数を補充します。PlayerWeaponに独立したUpdateでリロード時間を進める処理はありません。

InstantReload有効中は通常リロードを即時補充にします。特殊弾装填・次弾強化は通常のスキル効果時間とは別に武器内部へ状態を保存し、弾の消費・リロード等で処理します。

### 9.4 HP・死亡・復活

PlayerHealthはHP、生死、Ready、色をNetworkedで保持します。TakeDamageで死亡済み・非正ダメージ・軽減率を検査し、遅延ダメージ中なら蓄積します。通常はHPを減らして0未満を避け、0でDieを呼びます。

死亡時には生死状態を反映するRPCとOnDied通知があり、身体・武器・アイコン・Colliderなどの状態を更新します。RespawnForRoundではHP、生死、位置を戻し、武器を即時補充。RPC_AfterRespawnで各端末の見た目・移動基準・ローカルCameraTargetなどを復元します。

### 9.5 回復アイテム

RoundStartController → RoundHealthItemController → HealthItemSpawnerの順で配置します。配置数は整数除算の参加人数/2、最低1、出現地点数が上限です。例えば2～3人で1個、4～5人で2個。地点がなければ配置しません。

HealthItemはHost側で接触相手を回復し、実際にHPが増えた場合だけ取得扱いにします。取得後はDespawnし、Spawnerが再出現を予約。ラウンド終了では既存アイテムと再出現待ちを片付けます。

<a id="sec-10"></a>

## 10. スキル仕様

### 10.1 選択と発動

1ラウンド2枠。1回目の4候補を選んだ後、1回目の候補群に含まれない種類から2回目の4候補を作るため、両候補群は原則として重複しません。候補はHostが作って全員へ通知する共通候補です。

PlayerSkillTypeはNoneを除き13種類。GameSceneのselectableRoundSkillsには12種類が保存され、GravityBurstReloadは現行候補から外れています。実装があることと、現在の試合で選べることは区別します。

PlayerSkillControllerが2枠の選択・有効時間・クールダウンを管理し、PlayerSkillActivatorが種類別に発動。PlayerSkillEffectApplierが移動・武器・HPへ効果を反映します。HostでTickTimerを更新し、Renderでも必要な効果を反映します。

クールダウンは各枠の効果終了後に20秒。前ラウンドと同じスキルの連続選択は、CanSelectSkillが現在常にtrueを返すため禁止されていません。設定名だけを見て連続使用禁止と判断しないでください。

### 10.2 GamePlayerに保存された効果値

| 種類 | 内容 | 保存された有効時間 | 主な効果 | 現行候補 |
| --- | --- | --- | --- | --- |
| DoubleJump | 二段ジャンプ | 20秒 | 空中追加ジャンプ1回 | 選択可 |
| RapidFire | 連射強化 | 15秒 | 発射間隔×0.4 | 選択可 |
| BulletSpeedUp | 弾速強化 | 15秒 | 弾速×1.8 | 選択可 |
| DamageReduction | 被ダメージ軽減 | 10秒 | 被ダメージ×0.5 | 選択可 |
| XRayVision | 透視 | 5秒 | 自分の端末で透視表示 | 選択可 |
| MoveSpeedUp | 移動強化 | 10秒 | 移動速度×2 | 選択可 |
| SlowFall | 落下緩和 | 20秒 | 落下重力×0.1 | 選択可 |
| Shrink | 縮小 | 15秒 | 身体×0.5、与ダメージ×0.5 | 選択可 |
| DelayedDamageInvincible | ダメージ蓄積 | 5秒 | 受けたダメージを蓄積し解除後に適用 | 選択可 |
| InstantReload | 即時リロード | 10秒 | 通常リロードを即時化 | 選択可 |
| GravityBurstReload | 重力爆発弾装填 | 0.1秒 | 20発装填、重力・爆発あり | 候補から除外 |
| HeavyBulletReload | 重弾装填 | 0.1秒 | 5発装填。速度・威力・発射間隔を変更 | 選択可 |
| NextShotDamageBoost | 次弾強化 | 0.1秒 | 次弾威力×1.5 | 選択可 |

短時間の装填・次弾強化スキルの0.1秒は発動状態の保持時間です。特殊弾の残弾や次弾への効果が必ず0.1秒で消えるという意味ではありません。遅延ダメージも恒久無敵ではなく、解除後に蓄積分を受ける実装です。

<a id="sec-11"></a>

## 11. UI・音・観戦

WaitingRoomUIは待機phase、自分の生成状態、接続に属するPlayerのReadyを表示します。人数は現在のRunnerに属し入力所有者があるPlayerHealthをPlayerRefでまとめて数え、ダミーや重複表示を除外します。

SkillSelectUIは選択phaseを監視し、候補、カーソル、1回目／2回目、確定状況を表示。表示用残り時間は各端末のTime.deltaTimeで進みます。実際の締切はHostのCoroutineなので、完全に同期した残り時間表示ではありません。

BattleStartUIはRoundStartingへの変化で開始演出を行います。ファイル名はBattleStartPanel.csです。BattleHudManagerはRoundPlaying中のローカルHUDを表示し、BattleHudBinderが表示対象を自分／観戦相手のHP・武器・スキルへ結び付けます。

HealthBarはHP変更通知と補間・揺れ表示、AmmoBarは武器の弾数通知、SkillHudUIはスキル状態を表示。PlayerHeadIconは識別情報を基に頭上表示を行い、VRWorldSpaceUIFollowerはCameraに追従する空間UIを配置します。RoundEndUI、FinalResultUIはRPCで渡された結果を表示します。

SpectatorCameraControllerは自分の死亡後、戦闘が続いて対象があれば待ち時間後にCinemachineの追従先を観戦視点へ変更し、HUDの対象も切り替えます。2人戦で死亡直後にラウンドが終われば観戦に入らない場合があります。PCはCinemachine、VRはXR Originを観戦相手の目の位置と身体の向きへ合わせます。観戦者の頭の追跡を維持し、対象変更・復活・ラウンド終了・切断で元へ戻します。

BgmManagerは待機と戦闘のphaseで曲を切り替えます。他のphase専用の停止・切替分岐はなく、直前の曲が続く場合があります。PlayerSoundはジャンプ、射撃、リロード、HP変化、死亡のC# eventを購読します。全端末で同じ効果音が鳴ることまでは、eventの購読だけでは保証しません。

<a id="sec-12"></a>

## 12. 現在の設定値と変更場所

下表はGameSceneまたはGamePlayerに実際に保存された値です。ソースの初期値とは異なるものがあります。

| 保存先 | フィールド | 内容 | 値 | 単位・補足 |
| --- | --- | --- | --- | --- |
| GameScene / RoundManager | minPlayersToStart | 開始最少人数 | 1 | 人 |
| GameScene / RoundManager | skillSelectionDuration | 選択締切 | 30 | 秒 |
| GameScene / RoundManager | startAfterAllSkillsSelectedDelay | 開始待ち | 3 | 秒 |
| GameScene / RoundManager | maxRoundCount | ラウンド数 | 5 | 回 |
| GameScene / RoundManager | nextRoundDelay | ラウンド結果待ち | 5 | 秒 |
| GameScene / RoundManager | returnToWaitingDelay | 最終結果待ち | 5 | 秒 |
| NetworkSessionController | StartGameArgs.PlayerCount | 接続上限 | 8 | 人（コード固定） |
| GameScene / NetworkLauncher | roomName | ルーム名 | TestRoom | 文字列 |
| GameScene / NetworkLauncher | autoStartOnLaunch | 自動接続 | 1 | 1＝ON |
| GameScene / LocalPlayModeController | editorMode | Editor起動モード | VirtualReality | enum値1 |
| GameScene / LocalPlayModeController | xrStartupTimeout | XR準備上限 | 15 | 秒 |
| GamePlayer / PlayerHealth | maxHealth | 最大HP | 10 | HP |
| GamePlayer / PlayerMove | moveSpeed | 移動速度 | 10 | 座標単位/秒 |
| GamePlayer / PlayerMove | gravity | 重力加速度 | 6 | 座標単位/秒² |
| GamePlayer / PlayerMove | jumpSpeed | ジャンプ初速 | 12 | 座標単位/秒 |
| GamePlayer / PlayerMove | groundSnapDistance | 接地補正距離 | 0.1 | 座標単位 |
| GamePlayer / PlayerWeapon | projectileSpeed | 通常弾速度 | 40 | 座標単位/秒 |
| GamePlayer / PlayerWeapon | projectileLifeTime | 弾の寿命 | 3 | 秒 |
| GamePlayer / PlayerWeapon | projectileDamage | 基本威力 | 1 | HP |
| GamePlayer / PlayerWeapon | maxAmmo | 装弾数 | 20 | 発 |
| GamePlayer / PlayerWeapon | fireInterval | 発射間隔 | 0.15 | 秒 |
| GamePlayer / PlayerWeapon | reloadDuration | 通常リロード | 2 | 秒 |
| GamePlayer / PlayerWeapon | heavyBulletSpeedMultiplier | 重弾速度倍率 | 3 | 倍 |
| GamePlayer / PlayerWeapon | heavyBulletDamageMultiplier | 重弾威力倍率 | 2 | 倍 |
| GamePlayer / PlayerWeapon | heavyBulletFireIntervalMultiplier | 重弾発射間隔倍率 | 6 | 倍 |
| GameScene / HealthItemSpawner | healAmount | 回復量 | 5 | HP |
| GameScene / HealthItemSpawner | respawnDelay | 再出現待ち | 60 | 秒 |
| GameScene / SpectatorCameraController | spectatorDelay | 観戦へ移る待ち | 3 | 秒 |

RoundManagerのhealthItemSpawnerは未割り当てですが、RoundHealthItemControllerにScene内検索のフォールバックがあります。未割り当て参照がすべて即エラーになるわけではありません。

PlayerMoveのplanetCenterはGamePlayerにPrefabアセットへの参照として保存されています。地面未検出時に使われるため、実際のステージ中心と一致するか確認が必要です。地面LayerやColliderの設定も移動結果へ影響します。

<a id="sec-13"></a>

## 13. ビルドと複数端末テスト

### 13.1 基本の手順

1. GameSceneを開く。Desktopで確認するならEditor ModeをDesktop、実機VRならVirtualRealityへ設定。
2. XRのInitialize XR on StartupはWindows／AndroidともOFFのまま、必要なOpenXR Loader・Featureは有効にする。
3. Windows／Android双方のScene一覧をGameSceneに揃え、同じコードとPrefabで再ビルド。
4. Photon App ID、App Version、リージョン、ルーム名を揃える。設定値そのものはPhotonAppSettingsで管理し、識別情報をこの文書へ複製しない。
5. 先に起動した側の接続完了を待ってから、2台目を起動する。

### 13.2 テスト構成

| 組み合わせ | 端末A | 端末B | 確認の狙い |
| --- | --- | --- | --- |
| PC×PC | Editor Desktop | Windows exe引数なし | 接続、人数、Ready、通常戦闘 |
| VR×PC | Editor VR＋Quest Link | Windows exe引数なし | XRをEditorだけが使う、モード混在 |
| VR×VR | Editor VR＋HMD1 | Quest2台目のAndroidアプリ | 2人の実機姿勢、PCとAndroidの混在 |
| Quest×Quest | Androidアプリ1 | Androidアプリ2 | Quest単体での処理負荷・操作・通信 |

VR×VRの実機操作にはHMDが2台必要です。1台のHMDを同じPC上のEditorとexeで取り合う構成を標準テストにしません。HostがPCの場合とVRの場合の両方を試します。

共通構成の詳細な準備は[共通起動ガイド](SharedGameSetup.ja.md)にも記載しています。旧exeと改修後のEditorは混在させず、ネットワーク状態の構造が揃うよう再ビルドしてください。

<a id="sec-14"></a>

## 14. 検証ツールと受け入れ条件

GameRecoveryValidation.RunではローカルFusionの復旧とVR観戦位置の自動検査を実行できます（実行終了時にEditorも終了するバッチ専用）。

Assets/Editor/GameSetupValidation.csには、設定チェックとDesktopの起動検証の入口があります。

- Tools > PvPVRGame > Validate Shared Game Setup：起動モードの分岐、共通Scene、XR自動起動OFF、Scene参照、共通Playerを検査。未保存Sceneがある場合は中断します。
- GameSetupValidation.Run：バッチ用の設定検証。
- GameSetupValidation.RunDesktopSmoke：接続せずDesktopをPlayし、XR Originが非アクティブ、描画CameraとListenerが1つ、重複Launcherを作っても既存所有者が維持されることを確認する入口。

検証入口が実装されていることと、そのテストが実行済みであることは別です。前回の確認範囲は2章を参照してください。

| ID | 実機・Playで確認する条件 | 現時点 |
| --- | --- | --- |
| T01 | PC×PCで人数が2人から増えず、全員Readyで開始 | 未検証 |
| T02 | PC exe起動後にQuest接続してもDesktopのまま | 分岐コード確認済み、実機未検証 |
| T03 | VR×PCで両者の移動・手・銃口・HPが正しい | 未検証 |
| T04 | Hostを入れ替えても同じように開始・進行する | 未検証 |
| T05 | 次ラウンドでHP・弾数・位置・スキルが復元 | 実装確認済み、通信未検証 |
| T06 | 5ラウンド後に結果表示・Readyリセット・待機復帰 | 実装確認済み、通信未検証 |
| T07 | 退出・再接続でRunnerが重複せず残留しない | 実装確認済み、通信未検証 |
| T08 | VR×VR・Quest単体ビルドで起動・表示・入力が動く | 未検証 |

<a id="sec-15"></a>

## 15. 現行の制限・未決定事項

以下は、仕様の理解と今後の確認に関係する箇所です。この文書の作成中にゲームコードを修正したものではありません。

1. **実機検証前の共通化**：共有Scene・Prefab・接続管理は実装済みですが、Unityでのコンパイル・FusionのIL後処理・ローカル接続と復旧を検証済み。プレイヤーの完全ビルド、HMD、複数端末のPhoton通信は未検証です。
2. **開始人数1**：開発設定。通常対戦で必要な人数は運用前に決めます。
3. **8人と4色の関係**：生存判定は個人、最終UIは色合計。5人以上の競技ルールは整理が必要です。
4. **退出・途中参加**：参加者登録と生成はありますが、途中参加の可否・ラウンド途中の待機扱いを明示した仕組みは確認できません。RoundPlayingでの退出をきっかけに終了を再判定する分岐も確認できません。
5. **Host終了**：Host移行処理は空。継続や再参加の挙動は別途確認します。
6. **VR観戦**：XR Originによる観戦と復帰を実装。位置・姿勢の復元をローカル検証し、Questでの操作・見やすさは実機確認が必要です。
7. **入力の重複**：PlayerControlsではAttackとReloadがゲームパッド西ボタンへ重複割当。Sprint／Crouch等のAction定義も、ゲーム側の実装済み機能とは限りません。
8. **選択時間の表示**：UIのローカルカウントダウンとHostの締切は別。途中参加・遅延時の一致は保証しません。
9. **空間参照とリソース**：planetCenterの参照、スポーン地点数、地面Layer、Collider、VRアバターと武器の位置は実シーンで確認が必要です。
10. **音の同期**：C# event中心。PlayerSoundのDespawned内にはOnReloadCanceledへ再度+=する箇所もあり、解除処理の点検余地があります。
11. **スキル要求の検証**：RPCで受け取ったPlayerRefから登録Playerを探す実装です。通信上の送信者と要求対象の一致検証は、対外提供前の確認事項です。
12. **手動接続UI**：GameSceneのHost／Clientボタンと接続メニューRootは未割り当て。自動接続をOFFにするだけで完全な手動接続画面が使えるとは限りません。

<a id="sec-16"></a>

## 16. 不具合調査の入口

| 症状 | まず見る場所 | 確認する情報 |
| --- | --- | --- |
| 接続前に止まる | LocalPlayModeController、NetworkLauncher.Start | 選択モード、XR Error、Camera参照 |
| 接続できない | NetworkSessionController | Build Index、共通Scene、Photon設定、失敗理由 |
| 人数が増える | Launcherの所有者、Runner、Spawner | Owner ID、Runner ID、Scene読込、参加PlayerRef |
| Readyから進まない | RoundReadyController、RoundManager | 登録人数、全員Ready、phase |
| 選択が確定しない | LocalSkillSelectionInputController → 選択RPC | 対象PlayerRef、スロット、Hostの確認通知 |
| 自分だけ動かない | NetworkPlayer → OnInput → PlayerController | Input Authority、State Authority、生死、phase |
| 惑星から落ちる・姿勢がおかしい | PlayerMove | SphereCast、Layer、Collider、中心・接地距離 |
| VRの表示・手がおかしい | LocalPlayModeController、VRBodyTargetSync | Loader、XR Display、HMD・手の参照、対象のモード |
| 撃てない・当たらない | PlayerWeapon → Projectile → PlayerHealth | 弾数、リロード、銃口、Trigger、権限 |
| 次ラウンドへ進まない | OnDied → Registry → RoundFlowController | 生存人数、phase、結果Coroutine |
| HUDが別人を示す | BattleHudBinder、SpectatorCameraController | ローカル所有者、観戦対象、購読先 |

ログの接頭辞は[PlayMode]、[NetworkLauncher]、[NetworkSessionController]、[NetworkPlayerSpawner]、[RoundManager]などです。調査時は「何人起動したか」「各端末のモード」「Hostはどちらか」「Scene名」「Runnerが何個か」を合わせて記録します。

<a id="sec-17"></a>

## 17. 何を変えるとき、どこを読むか

- **プレイ人数・待ち時間・ラウンド数**：GameSceneのRoundManagerの保存設定。遷移ロジックはRoundReadyController／RoundStartController／RoundFlowController。
- **操作方法**：PlayerControls.inputactionsとNetworkInputCollector。選択操作はLocalSkillSelectionInputController。
- **VR／Desktopの起動条件**：LocalPlayModeController。CameraとXR参照の結び付けはNetworkLauncher。
- **移動・ジャンプ・惑星への追従**：GamePlayerのPlayerMove。入力を実行する条件はPlayerController。
- **威力・弾数・連射・リロード**：GamePlayerのPlayerWeapon。弾の飛び方・接触はProjectile。
- **HP・軽減・死亡・復活**：PlayerHealth。勝敗への接続はOnDied → RoundPlayerRegistry → RoundFlowController。
- **スキルを追加**：PlayerSkillType → PlayerSkillControllerの設定／状態 → Settings → Activator／EffectApplier → 候補リスト・表示。種類だけ増やしても選択・発動・UI全部が自動対応するとは限りません。
- **武器の見た目や握り位置**：VRWeaponVisualSettingsとVRBodyTargetSync。発射判定の銃口とは分けて確認。
- **UIの見た目**：該当Scene／PrefabとUIコンポーネント。HPや弾数のルール自体はUIへ移さない。
- **接続の安定性・再接続**：NetworkSessionController。単一所有とScene参照引き継ぎはNetworkLauncher。

読む順番は「変更したいプレイ上の動作 → 入口クラス → 呼ばれる担当クラス → 保存値 → 実行条件」です。全ファイルを先に暗記する必要はありません。

<a id="sec-18"></a>

## 18. 全ゲームスクリプト索引

ソースのファイル名、実際の型、配置と主な呼び出し契機を列挙します。コードが存在すること、Scene／Prefabに付くこと、その条件を満たして実行されることは、それぞれ別です。

### Network

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [LocalPlayModeController](../Assets/Scripts/Network/LocalPlayModeController.cs)<br>`class LocalPlayModeController : MonoBehaviour` | Scene | 起動モード決定、手動XR初期化、稼働確認、終了。 | LauncherからSelectMode / Initialize、OnDestroy |
| [LocalSkillSelectionInputController](../Assets/Scripts/Network/LocalSkillSelectionInputController.cs)<br>`class LocalSkillSelectionInputController` | コードから使用 | 4枠カーソル、選択確定、RPC要求、確認通知。 | SkillSelecting中のUpdate |
| [NetworkConnectionUIController](../Assets/Scripts/Network/NetworkConnectionUIController.cs)<br>`class NetworkConnectionUIController` | コードから使用 | 接続メニュー、ボタン、状態テキスト。 | 接続準備・結果・ボタン操作 |
| [NetworkInputCollector](../Assets/Scripts/Network/NetworkInputCollector.cs)<br>`class NetworkInputCollector` | コードから使用 | 入力蓄積、ローカル視線、入力パケット生成。 | Updateから呼ぶ / OnInput |
| [NetworkLauncher](../Assets/Scripts/Network/NetworkLauncher.cs)<br>`class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks` | Scene | 起動、接続の入口、単一所有、Scene参照引き継ぎ、Camera/XR設定。 | Awake / Start / Update / Fusion callbacks |
| [NetworkPlayer](../Assets/Scripts/Network/NetworkPlayer.cs)<br>`class NetworkPlayer : NetworkBehaviour` | Player | 自分と他人のローカル部品・入力・Camera・XRを設定。 | Spawned / Despawned、生成後Coroutine |
| [NetworkPlayerSpawner](../Assets/Scripts/Network/NetworkPlayerSpawner.cs)<br>`class NetworkPlayerSpawner` | コードから使用 | HostでのPlayer生成・削除、重複参加の防止、番号付与。 | OnPlayerJoined / OnPlayerLeft |
| [NetworkSessionController](../Assets/Scripts/Network/NetworkSessionController.cs)<br>`class NetworkSessionController` | コードから使用 | Runner作成・接続・再試行・再接続・Shutdownの所有。 | Launcherが生成してStartGame等を呼ぶ |
| [PlayerNetworkInput](../Assets/Scripts/Network/PlayerNetworkInput.cs)<br>`struct PlayerNetworkInput : INetworkInput` | コードから使用 | 通常操作と頭・手の姿勢を運ぶ構造体。同ファイルにPlayerInputButton列挙型。 | CollectInput → Fusion → GetInput |

### Round

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [RoundDebugLogger](../Assets/Scripts/Round/RoundDebugLogger.cs)<br>`class RoundDebugLogger` | コードから使用 | Ready、候補、SpawnPointsなどの診断。 | 登録・設定・選択の確認時 |
| [RoundFlowController](../Assets/Scripts/Round/RoundFlowController.cs)<br>`class RoundFlowController` | コードから使用 | 生存者判定、得点・結果、次ラウンド・試合リセット。 | 死亡通知 / 結果Coroutine |
| [RoundHealthItemController](../Assets/Scripts/Round/RoundHealthItemController.cs)<br>`class RoundHealthItemController` | コードから使用 | Round処理から回復Spawnerへの仲介と参照補完。 | 戦闘開始 / 選択開始 / 終了 |
| [RoundManager](../Assets/Scripts/Round/RoundManager.cs)<br>`class RoundManager : NetworkBehaviour` | Scene | 試合phase・公開窓口・補助クラス構成・RPC。 | Awake / Spawned / Render / Ready・死亡・選択 |
| [RoundPlayerRegistry](../Assets/Scripts/Round/RoundPlayerRegistry.cs)<br>`class RoundPlayerRegistry` | コードから使用 | 参加者一覧、色付与、OnDiedの購読・解除。 | 登録 / 退出 / 死亡通知 |
| [RoundProjectileCleaner](../Assets/Scripts/Round/RoundProjectileCleaner.cs)<br>`class RoundProjectileCleaner` | コードから使用 | 残ったネットワーク弾を片付ける。 | 選択開始 / ラウンド・試合終了 |
| [RoundReadyController](../Assets/Scripts/Round/RoundReadyController.cs)<br>`class RoundReadyController` | コードから使用 | 参加者のReadyと全員Ready判定。 | 登録 / Ready / 試合リセット |
| [RoundScoreCalculator](../Assets/Scripts/Round/RoundScoreCalculator.cs)<br>`class RoundScoreCalculator` | コードから使用 | 個人の点数保存、色合計、結果マスク、ログ。 | 参加登録 / 勝利 / 結果表示 |
| [RoundSkillOptionBuilder](../Assets/Scripts/Round/RoundSkillOptionBuilder.cs)<br>`class RoundSkillOptionBuilder` | コードから使用 | 候補の重複排除、シャッフル、2回分の候補群生成。 | ラウンドの選択開始 |
| [RoundSkillSelectionController](../Assets/Scripts/Round/RoundSkillSelectionController.cs)<br>`class RoundSkillSelectionController` | コードから使用 | 候補配布、2回選択、締切、選択開始時の再配置。 | 選択開始 / RPC / 締切Coroutine |
| [RoundSpawnController](../Assets/Scripts/Round/RoundSpawnController.cs)<br>`class RoundSpawnController` | コードから使用 | SpawnPointsの収集・割り当て・シャッフル・再配置。 | 選択開始 / 試合リセット |
| [RoundStartController](../Assets/Scripts/Round/RoundStartController.cs)<br>`class RoundStartController` | コードから使用 | スキル適用、開始待ち、戦闘開始、回復配置。 | 全員選択完了後のCoroutine |
| [RoundUIController](../Assets/Scripts/Round/RoundUIController.cs)<br>`class RoundUIController` | コードから使用 | 待機UI、開始演出、ラウンド・最終結果の表示窓口。 | phase変更 / 結果RPC |
| [TeamColor](../Assets/Scripts/Round/TeamColor.cs)<br>`enum TeamColor` | コードから使用 | Red / Blue / Green / Yellowの識別。 | 登録・スコア・表示 |

### Player

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [PlayerController](../Assets/Scripts/Player/PlayerController.cs)<br>`class PlayerController : NetworkBehaviour` | Player | 入力をphase・権限で検査し移動、武器、スキルへ振り分け。 | FixedUpdateNetwork / Render / LateUpdate |
| [PlayerHealth](../Assets/Scripts/Player/PlayerHealth.cs)<br>`class PlayerHealth : NetworkBehaviour` | Player | HP、生死、Ready、色、軽減・回復、死亡通知、復活。 | Spawned / TakeDamage / Heal / Respawn / RPC |
| [PlayerIdentity](../Assets/Scripts/Player/PlayerIdentity.cs)<br>`class PlayerIdentity : NetworkBehaviour` | Player | 番号と色インデックスを同期。 | Hostの生成時にSetIdentity |
| [PlayerInput](../Assets/Scripts/Player/PlayerInput.cs)<br>`class PlayerInput : MonoBehaviour` | Player | 旧形式の入力取得部品。現在の主要経路からReadInput呼び出しなし。 | 明示的にReadInputされた場合。現行はCollector |
| [PlayerLook](../Assets/Scripts/Player/PlayerLook.cs)<br>`class PlayerLook : MonoBehaviour` | Player | 通常入力・HMDから視線方向を計算。 | Awake、ApplyLocalLookやHostの操作処理 |
| [PlayerMove](../Assets/Scripts/Player/PlayerMove.cs)<br>`class PlayerMove : MonoBehaviour` | Player | CharacterController、接地、表面基準、移動、姿勢、重力・ジャンプ。 | Awake、PlayerControllerから呼ぶ |
| [PlayerSkillActivator](../Assets/Scripts/Player/PlayerSkillActivator.cs)<br>`class PlayerSkillActivator` | コードから使用 | 種類別の発動開始、特殊弾装填、次弾強化。 | TryActivateSkill系から呼ぶ |
| [PlayerSkillController](../Assets/Scripts/Player/PlayerSkillController.cs)<br>`class PlayerSkillController : NetworkBehaviour` | Player | 2枠の選択・発動状態と効果・クールダウン管理。 | Awake / FixedUpdateNetwork / Render / 発動要求 |
| [PlayerSkillEffectApplier](../Assets/Scripts/Player/PlayerSkillEffectApplier.cs)<br>`class PlayerSkillEffectApplier` | コードから使用 | 移動・武器・HPへ現在のスキル効果を反映。 | スキル更新・Renderから呼ぶ |
| [PlayerSkillSettings](../Assets/Scripts/Player/PlayerSkillSettings.cs)<br>`class PlayerSkillSettings` | コードから使用 | スキルの設定値を補助クラスへ渡す読み取り用データ。 | PlayerSkillControllerの初期化 |
| [PlayerSkillType](../Assets/Scripts/Player/PlayerSkillType.cs)<br>`enum PlayerSkillType` | コードから使用 | Noneと13種類のスキル識別子。 | 候補・選択・発動・UIで参照 |
| [PlayerWeapon](../Assets/Scripts/Player/PlayerWeapon.cs)<br>`class PlayerWeapon : NetworkBehaviour` | Player | 弾数、発射間隔、弾生成、リロード、特殊弾。 | Spawned、PlayerControllerからTick/射撃等 |
| [VRBodyTargetSync](../Assets/Scripts/Player/VRBodyTargetSync.cs)<br>`class VRBodyTargetSync : NetworkBehaviour` | Player | 各Playerのモード、頭・手、IK、武器の持ち方。射撃前の姿勢適用。 | Spawned / FixedUpdateNetwork / LateUpdate / Render |
| [VRWeaponVisualSettings](../Assets/Scripts/Player/VRWeaponVisualSettings.cs)<br>`class VRWeaponVisualSettings : MonoBehaviour` | Player | 武器、握り親、VRオフセット、PC表示位置、大きさ。 | VRBodyTargetSyncが参照 |

### Camera

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [PlayerCamera](../Assets/Scripts/Camera/PlayerCamera.cs)<br>`class PlayerCamera : MonoBehaviour` | Player | ローカルCamera切り替え、CameraTarget、他人のPC視点反映。 | 生成後のSetupLocalCamera、LateUpdateから呼ぶ |
| [SpectatorCameraController](../Assets/Scripts/Camera/SpectatorCameraController.cs)<br>`class SpectatorCameraController : MonoBehaviour` | Scene | 死亡待ち、対象選択、同期観戦視点、HUD切り替え。 | Awake / Update / LateUpdate |

### Projectile

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [Projectile](../Assets/Scripts/Projectile/Projectile.cs)<br>`class Projectile : NetworkBehaviour` | コードから使用 | 弾移動、寿命、Trigger命中、自分への命中除外、範囲爆発。 | Spawned / FixedUpdateNetwork / OnTriggerEnter |

### Item

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [HealthItem](../Assets/Scripts/Item/HealthItem.cs)<br>`class HealthItem : NetworkBehaviour` | コードから使用 | Hostで回復対象を判定し、回復成功時だけ取得。 | Awake / Initialize / OnTriggerEnter |
| [HealthItemSpawner](../Assets/Scripts/Item/HealthItemSpawner.cs)<br>`class HealthItemSpawner : MonoBehaviour` | Scene | 人数別配置、取得後の再出現、配置物とCoroutineの掃除。 | 戦闘開始 / 取得通知 / 終了 |

### UI

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [AmmoBar](../Assets/Scripts/UI/BattleHUD/AmmoBar.cs)<br>`class AmmoBar : MonoBehaviour` | Scene／Player | ローカル武器の検索と弾数セル表示。 | OnEnable / SetupWeapon / 弾数イベント |
| [BattleHudBinder](../Assets/Scripts/UI/BattleHUD/BattleHudBinder.cs)<br>`class BattleHudBinder : MonoBehaviour` | Scene | 自分または観戦相手のHP・武器・スキルをHUDへ接続。 | OnEnableの検索Coroutine / 対象変更 |
| [BattleHudManager](../Assets/Scripts/UI/BattleHUD/BattleHudManager.cs)<br>`class BattleHudManager : MonoBehaviour` | Scene／Player | 戦闘phaseのローカルHUD表示。 | Awake / Start / Update |
| [BattleStartPanel](../Assets/Scripts/UI/BattleHUD/BattleStartPanel.cs)<br>`class BattleStartUI : MonoBehaviour` | Scene | 実際の型名はBattleStartUI。開始演出を再生。 | Start / PlaySequence Coroutine |
| [HealthBar](../Assets/Scripts/UI/BattleHUD/HealthBar.cs)<br>`class HealthBar : MonoBehaviour` | Scene／Player | HP通知、表示値の補間・揺れ、対象の設定。 | SetupPlayerHealth / HPイベント / Update |
| [PlayerHeadIcon](../Assets/Scripts/UI/BattleHUD/PlayerHeadIcon.cs)<br>`class PlayerHeadIcon : NetworkBehaviour` | Player | 識別情報の頭上表示とCamera向きへの追従。 | Awake / Spawned / LateUpdate |
| [SkillHudUI](../Assets/Scripts/UI/BattleHUD/SkillHudUI.cs)<br>`class SkillHudUI : MonoBehaviour` | Scene | スキル2枠、発動・クールダウン等の表示。 | Awake / Update / 対象設定 |
| [SpectatorStatusUI](../Assets/Scripts/UI/BattleHUD/SpectatorStatusUI.cs)<br>`class SpectatorStatusUI : MonoBehaviour` | Scene | 観戦状態と対象の表示。 | Awake / Update |
| [FinalResultUI](../Assets/Scripts/UI/FinalResult/FinalResultUI.cs)<br>`class FinalResultUI : MonoBehaviour` | Scene | 最終勝者一覧を表示。 | Awake / 最終結果RPC |
| [FinalResultWinnerEntryUI](../Assets/Scripts/UI/FinalResult/FinalResultWinnerEntryUI.cs)<br>`class FinalResultWinnerEntryUI : MonoBehaviour` | コードから使用 | 最終勝者ごとの表示行。 | FinalResultUIから設定 |
| [RoundEndScoreEntryUI](../Assets/Scripts/UI/RoundEnd/RoundEndScoreEntryUI.cs)<br>`class RoundEndScoreEntryUI : MonoBehaviour` | コードから使用 | ラウンド結果の各得点行。 | RoundEndUIから設定 |
| [RoundEndUI](../Assets/Scripts/UI/RoundEnd/RoundEndUI.cs)<br>`class RoundEndUI : MonoBehaviour` | Scene | ラウンド勝者と色ごとの得点一覧。 | Awake / 結果RPCから呼ぶ |
| [SkillCardUI](../Assets/Scripts/UI/SkillSelectUI/SkillCardUI.cs)<br>`class SkillCardUI : MonoBehaviour` | Scene | 個別スキルカードの表示と選択状態。 | SkillSelectUIの更新から呼ぶ |
| [SkillSelectUI](../Assets/Scripts/UI/SkillSelectUI/SkillSelectUI.cs)<br>`class SkillSelectUI : MonoBehaviour` | Scene | 候補カード、選択段階、確定、残り時間の表示。 | Awake / Update |
| [VRWorldSpaceUIFollower](../Assets/Scripts/UI/VRWorldSpaceUIFollower.cs)<br>`class VRWorldSpaceUIFollower : MonoBehaviour` | Scene／Player | 選択モードのCameraを使う空間UIの追従。 | LateUpdate / Camera参照設定 |
| [WaitingRoomUI](../Assets/Scripts/UI/WaitingRoomUI/WaitingRoomUI.cs)<br>`class WaitingRoomUI : MonoBehaviour` | Scene | 待機phase、自分の生成、現在の接続の人数・Ready一覧。 | Awake / Update / 生成・表示通知 |

### Sound

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [BgmManager](../Assets/Scripts/Sound/BgmManager.cs)<br>`class BgmManager : MonoBehaviour` | Scene | 待機・戦闘phaseに応じたBGM切り替え。 | Update |
| [PlayerSound](../Assets/Scripts/Sound/PlayerSound.cs)<br>`class PlayerSound : NetworkBehaviour` | Player | 操作とHP・死亡イベントを購読しSEを再生。 | Awake / Spawned / Despawned / 各イベント |

### Effect

| ファイル・型 | 配置／生成 | 役割 | 主な呼び出し |
| --- | --- | --- | --- |
| [XRayVisionController](../Assets/Scripts/Effect/XRayVisionController.cs)<br>`class XRayVisionController : NetworkBehaviour` | Player | 自分の透視スキルに応じて相手の表示を変更・復元。 | Awake / Update / OnDisable |

### 18.1 Editor用の補助コード

[GameSetupValidation.cs](../Assets/Editor/GameSetupValidation.cs)はUnityEditor専用です。ゲーム実行用の61ファイルとは別に、設定検証・起動モード検証・DesktopのPlayスモーク検証の入口を持ちます。

<a id="sec-19"></a>

## 19. 更新方法・根拠ファイル

主な根拠は、GameScene、GamePlayer、Assets/Scripts以下、Assets/Editor/GameSetupValidation、Packages/manifest.json、ProjectSettings/ProjectVersion、EditorBuildSettings、Assets/XR/XRGeneralSettingsPerBuildTarget、PlayerControls.inputactionsです。本文の各索引からソースへ移動できます。

この仕様書の数値はスナップショットです。Inspectorやコードを変更したら、該当のルール・保存値・確認状態も更新します。特にScene／Prefabを保存せずにPlayで試した値は、この仕様書の保存値には含めません。

更新時の確認事項：起動モードとScene一覧、生成Prefab、人数・phase条件、入力割り当て、武器・スキルの保存値、Networkedデータ、RPC、実機テスト結果。未検証項目は、再現手順と実行環境を記録してから確認済みに変更してください。

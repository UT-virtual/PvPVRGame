# Cosmic Sphere Arena

## 概要

本作は、UnityとPhoton Fusionを使用して制作した、複数人で対戦するVR対応オンラインPvPシューティングゲームです。

プレイヤーは球体惑星上を移動しながら、射撃、ふわりとしたジャンプ、リロード、スキルを使って戦います。ゲームの流れとしては、待機画面 → 全員準備完了 → スキル選択 → ラウンド開始 → 武器やスキルを使って戦う → 数ラウンド終了後に勝者表示 → 待機画面という構成になっています。

本作品はチーム制作です。私はチームリーダーとして、担当範囲の整理、進捗確認、タスク調整を行いました。実装面では、主にプレイヤー制御、射撃・弾数・リロード、スキル、ラウンド進行など、対戦ゲームとして成立するための中心的な処理の実装・整理を担当しました。

VR対応、サウンド実装、UI実装は、それぞれチームメンバーと分担しました。

この README では、実行方法と、ソースコード上で特に見ていただきたい実装箇所を案内します。

## デモ・資料

* プレイ映像: [https://youtu.be/cO3BRzCSSEk](https://youtu.be/cO3BRzCSSEk)
* リポジトリ: [https://github.com/UT-virtual/PvPVRGame/tree/feature/kusaka0914](https://github.com/UT-virtual/PvPVRGame/tree/feature/kusaka0914)

## 実行方法

Unityで本プロジェクトを開いて実行します。

使用しているUnityバージョンは以下です。

```txt
Unity 6000.3.17f1
```

実行時は、以下のシーンを使用します。

```txt
Assets/Scenes/ForPCMainScene.unity
```

Photon Fusionを使用しているため、ネットワーク機能の確認には複数クライアントでの実行が必要です。複数ビルドして起動することで確認が可能です。

## 開発環境・使用技術

* Unity 6000.3.17f1
* C#
* Photon Fusion
* Unity Input System
* Cinemachine
* Universal Render Pipeline
* Meta XR SDK
* OpenXR
* XR Interaction Toolkit

## チーム制作での担当範囲

### 自分の担当

* チームリーダー
  * メンバーごとの担当範囲の整理
  * 実装の優先順位づけ
  * 締切の設定
  * 進捗確認
  * タスク調整
* プレイヤー制御・射撃・弾数管理・スキル
  * [Assets/Scripts/Player/](Assets/Scripts/Player/)
* カメラ制御
  * [Assets/Scripts/Camera/](Assets/Scripts/Camera/)
* エフェクト効果
  * [Assets/Scripts/Effect/](Assets/Scripts/Effect/)
* アイテム効果
  * [Assets/Scripts/Item/](Assets/Scripts/Item/)
* Photon Fusionを用いたプレイヤー状態・ラウンド状態の同期処理
  * [Assets/Scripts/Network/](Assets/Scripts/Network/)
* 銃弾
  * [Assets/Scripts/Projectile/](Assets/Scripts/Projectile/)
* ラウンド進行
  * [Assets/Scripts/Round/](Assets/Scripts/Round/)

### メンバーと分担した範囲（私の担当外）

* VR対応
  * PlayerプレハブのFinal IKなど
* サウンド実装
  * [Assets/Scripts/Sound/](Assets/Scripts/Sound/)
* UI実装
  * [Assets/Scripts/UI/](Assets/Scripts/UI/)

## ディレクトリ構成

```txt
Assets/
├─ Scenes/                         ゲームで使用するシーン、各メンバーの作業用シーン
├─ Scripts/
│  ├─ Player/                      プレイヤー制御、移動、武器、スキルなど
│  ├─ Round/                       ラウンド進行、スキル選択、勝敗処理
│  ├─ Network/                     Photon Fusionの起動、入力収集、プレイヤー生成
│  ├─ Camera/                      プレイヤーカメラ関連
│  ├─ Effect/                      透視などエフェクト関連
│  ├─ Projectile/                  銃弾
│  ├─ Item/                        回復アイテムなど
│  ├─ Sound/                       サウンド関連
│  └─ UI/                          UI関連
└─ Prefabs/                        プレイヤー、弾、アイテムなどのPrefab
```

## 特に見ていただきたい実装

### 1. Photon Fusionを前提としたプレイヤー制御

主に見ていただきたい箇所:

* [PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)
* [PlayerMove.cs](Assets/Scripts/Player/PlayerMove.cs)

`PlayerController` は、プレイヤーまわりの処理の入口として、移動、武器、スキル、ラウンド状態に応じた入力処理を呼び分ける役割を持たせています。

ローカルだけで動く処理ではなく、Photon Fusion上で複数プレイヤーが参加することを前提に、StateAuthority側で処理するものと、各クライアントに反映するものを意識して実装しました。

見るポイント:

* `NetworkBehaviour` を用いたプレイヤー制御
* `GetInput` を使ったネットワーク入力の取得
* ラウンド状態に応じて、操作可能か、射撃可能かを切り替えている点
* `PlayerController` に処理を集めすぎず、個別クラスに委譲している点

### 2. ラウンド進行管理

主に見ていただきたい箇所:

* [RoundManager.cs](Assets/Scripts/Round/RoundManager.cs)
* [Roundフォルダ](Assets/Scripts/Round/)

ラウンド進行では、待機画面、スキル選択、ラウンド開始、ラウンド中、ラウンド終了、試合終了といったフェーズを管理しています。

対戦ゲームでは、現在のフェーズによって、プレイヤーが操作できるか、射撃できるか、UIに何を表示するかが変わります。そのため、ラウンド状態を明確に分け、各機能が現在のフェーズを参照して動けるようにしました。

見るポイント:

* ゲーム全体の進行状態をフェーズとして管理している点
* プレイヤー制御や武器使用可否とラウンド状態を連携している点

### 3. 武器・射撃・弾数・リロード処理

主に見ていただきたい箇所:

* [PlayerWeapon.cs](Assets/Scripts/Player/Weapon/PlayerWeapon.cs)
* [ProjectileSpawner.cs](Assets/Scripts/Player/Weapon/ProjectileSpawner.cs)

対戦ゲームとして成立するように、射撃間隔、弾数、リロード、弾速、ダメージ、特殊弾などを管理しています。

弾の生成処理は `ProjectileSpawner` に分け、`PlayerWeapon` が武器の状態管理を担当し、弾の生成は専用クラスに委譲する構成にしています。

見るポイント:

* 弾数とリロード状態をネットワーク上で共有している点
* 射撃間隔やリロード中の制御を行っている点
* 武器仕様を変更する際に、見るべき処理を追いやすくしている点

### 4. スキル処理

主に見ていただきたい箇所:

* [PlayerSkillController.cs](Assets/Scripts/Player/Skill/PlayerSkillController.cs)
* [PlayerSkillActivator.cs](Assets/Scripts/Player/Skill/PlayerSkillActivator.cs)
* [PlayerSkillEffectApplier.cs](Assets/Scripts/Player/Skill/PlayerSkillEffectApplier.cs)
* [PlayerSkillSettings.cs](Assets/Scripts/Player/Skill/PlayerSkillSettings.cs)

プレイヤーがラウンド中に使用できるスキルを実装しています。スキルには、移動速度上昇、連射、弾速上昇、即時リロード、特殊弾など、プレイヤーの行動に影響するものがあります。

スキルの選択、発動、効果時間、クールダウンを管理し、ラウンドごとに戦い方を変えられるようにしました。

見るポイント:

* スキルの発動状態、効果時間、クールダウンを管理している点
* 武器や移動など、他のプレイヤー機能と連携している点

### 5. プレイヤー状態管理・死亡・リスポーン関連

主に見ていただきたい箇所:

* [PlayerHealth.cs](Assets/Scripts/Player/Health/PlayerHealth.cs)
* [PlayerRespawnController.cs](Assets/Scripts/Player/Health/PlayerRespawnController.cs)

HP、死亡、リスポーンに関する処理です。プレイヤーの状態がネットワーク上で共有されるため、StateAuthority側での処理と、各クライアントへの反映を分ける必要があります。

見るポイント:

* HPや死亡状態をネットワーク上で共有している点
* StateAuthority側でHP変更や死亡判定を行っている点

## その他可能であれば見ていただきたい実装

### 1. 回復アイテム

主に見ていただきたい箇所:

* [HealthItem.cs](Assets/Scripts/Item/HealthItem.cs)

フィールド上の回復アイテムに触れることで、プレイヤーのHPを回復できるようにしています。

見るポイント:

* `NetworkBehaviour` を用いている点
* StateAuthority側で取得判定を行っている点
* 取得後にネットワーク上からアイテムを削除している点

### 2. ネットワーク起動・入力収集

主に見ていただきたい箇所:

* [NetworkLauncher.cs](Assets/Scripts/Network/NetworkLauncher.cs)
* [NetworkSessionController.cs](Assets/Scripts/Network/NetworkSessionController.cs)
* [NetworkInputCollector.cs](Assets/Scripts/Network/NetworkInputCollector.cs)
* [PlayerNetworkInput.cs](Assets/Scripts/Network/PlayerNetworkInput.cs)

Photon Fusionでのセッション開始、プレイヤー生成、ネットワーク入力の収集を行う部分です。

見るポイント:

* `NetworkRunner` を用いたHost / Client起動
* `INetworkRunnerCallbacks` を用いた参加・退出処理
* `PlayerNetworkInput` に入力情報をまとめている点

## 設計で意識したこと

* ローカルで動くだけでなく、ネットワーク同期を前提に処理を考える
* StateAuthority側で処理するものと、各クライアントに反映するものを分ける
* 機能追加や仕様変更時に、見るべきファイルを追いやすくする
* クラス名・関数名から役割が分かるように命名する

## リファクタリングで改善したこと

以前は、プレイヤーまわりやラウンドまわりの処理が複数機能にまたがり、どこに責務を持たせるべきか整理が難しい部分がありました。現在は、以下のように役割ごとに分割しています。

```txt
Player
├─ PlayerController        プレイヤー処理の入口
├─ PlayerMove              移動処理
├─ PlayerLook              視点関連
├─ PlayerCamera            カメラ関連
├─ PlayerWeapon            武器関連
├─ ProjectileSpawner       弾の生成
├─ PlayerSkillController   スキル関連
└─ PlayerHealth            HP、死亡、リスポーン関連

Round
├─ RoundManager            ラウンド全体の管理
├─ RoundPlayerRegistry     プレイヤー登録
├─ RoundReadyController    準備完了管理
├─ RoundSkillSelection     スキル選択
├─ RoundStartController    ラウンド開始処理
├─ RoundFlowController     ラウンド中・終了処理
└─ RoundUIController       UI表示との連携
```

この分割により、移動、射撃、リロード、スキル、ラウンド進行などを、変更理由ごとに追いやすくしました。

## 今後の改善点

* ネットワーク環境での長時間プレイテストを増やし、同期ずれや例外的な状態遷移をさらに確認する
* スキルの種類を増やし、スキルごとの処理をより追加しやすい構成にする
* ラウンド中の状況がより分かりやすくなるよう、UIや演出との連携を強める
* 対戦バランスを調整し、武器・スキル・移動速度の関係を改善する

# Cosmic Sphere Arena

Unity / Photon Fusionで開発した、球体フィールドを舞台とする最大4人対戦のオンラインPvPシューティングゲームです。

プレイヤーは惑星の表面に沿って移動し、射撃やラウンドごとに選択するスキルを使って戦います。PC・VRの入力に対応するほか、対戦に参加しない端末からプレイヤー視点で観戦できる仕組みも実装しています。

大学サークルでのチーム制作作品です。私はチームリーダーとして進捗管理・担当調整を行いながら、主にプレイヤー制御、戦闘、スキル、ラウンド進行、ネットワーク機能などを担当しました。

### 制作情報

| 項目 | 内容 |
| --- | --- |
| 制作形態 | チーム制作 |
| 役割 | チームリーダー / ゲームプログラマー |
| 開発環境 | Unity 6000.3.17f1 |
| 言語 | C# |
| 主な技術 | Photon Fusion / Unity Input System / OpenXR / Cinemachine |
| 対戦人数 | 最大4人 |
| 接続人数 | 最大5人（観戦者を含む） |
| 主な担当 | プレイヤー制御 / 戦闘 / スキル / ラウンド進行 / ネットワーク関連機能 / 観戦機能 |

- **プレイ映像:** https://youtu.be/cO3BRzCSSEk
- **GitHub:** https://github.com/UT-virtual/PvPVRGame/tree/feature/kusaka0914
- **ポートフォリオサイト（他の制作物も紹介しています）:** https://kusaka0914.github.io

<!-- ここにゲーム画面のスクリーンショットを追加 -->

---

## チーム制作での担当範囲

本作は大学サークルでのチーム制作です。私はチームリーダーとして開発全体の進行管理を担当し、プログラム面ではプレイヤー制御・戦闘・ネットワーク処理・ラウンド進行など、ゲームプレイの中心となる処理を主に実装しました。

### 自分の担当

**チームリーダー**

- メンバーごとの担当範囲の整理
- 実装の優先順位づけ・締切の設定
- 進捗確認・タスク調整

**プログラム実装**

- プレイヤー制御・移動・射撃・弾薬管理・スキル
- カメラ制御・観戦機能
- エフェクト効果・アイテム効果
- Photon Fusionによる通信・入力収集・状態同期・参加者管理
- 弾の移動・衝突・ダメージ判定
- ラウンド進行・スキル選択・得点・勝敗判定

### 他メンバーの担当

- **VR対応**
   - PlayerプレハブへのFinal IKの導入・設定など
- **グラフィックプログラミング**
   - シェーダーなどの描画表現に関する実装
- **サウンド実装**
   - 射撃・ジャンプ・リロード・死亡など、ゲーム内の動作に応じた効果音の再生制御
   - ゲームロジックとの連携処理を含む、サウンド機能全般の実装
- **UI実装**
   - HP・弾薬・スキル・待機画面・ラウンド結果などのUI
   - ゲーム状態に応じた表示更新や、ゲームロジックとの連携処理を含むUI機能全般の実装

### チームでの機能連携

ゲームプレイの処理と、それに伴うUI・サウンド・描画表現などを分担しました。

例えば、私が射撃・弾薬消費・リロードなどのゲームロジックを実装し、それに合わせて他メンバーが射撃音・リロード音の再生や、弾薬表示の更新処理を実装しました。

HP・スキル・ラウンド進行についても、私が担当するゲーム状態の管理に対して、他メンバーがUIや演出を連携させる形で開発しました。

担当範囲はディレクトリ単位ではなく機能単位で分けており、同じスクリプト内にも複数メンバーの実装が含まれる場合があります。

---

## 制作コンセプト

VRならではの没入感と、周囲の人も一緒に楽しめる対戦体験を目指して開発しています。

通常の平面ステージとは異なり、球体の表面を移動できるフィールドを採用しました。プレイヤーごとに上方向が変化するため、惑星の反対側や側面からも移動・攻撃できることが特徴です。

また、対戦を繰り返しても展開が単調にならないよう、ラウンドごとに2種類のスキルを選択する仕組みを設けています。

展示での利用も想定し、ゲームに参加していない人がプレイヤーの視点を切り替えながら観戦できる機能を実装しています。

---

## ゲームの特徴

- **球体地形での移動・戦闘** — 惑星の表面に沿って移動し、さまざまな方向から攻撃できる
- **最大4人のオンラインPvP** — Photon Fusionを利用したHost / Client方式の対戦
- **ラウンドごとのスキル選択** — 候補から2種類のスキルを選び、戦闘中に使用できる
- **特殊弾を含む射撃システム** — 弾薬・リロード・特殊弾・範囲攻撃などに対応
- **ラウンド制の試合進行** — 準備、スキル選択、戦闘、結果表示を管理
- **PC・VR入力への対応** — キーボード・ゲームパッド・VRコントローラーの入力を扱う
- **観戦者モード** — 対戦に参加せず、生存プレイヤーの視点を切り替えて観戦できる

<!-- ここにプレイ中の別スクリーンショットを追加 -->

---

## コードを見る方へ ― まずこの3箇所

短時間でコードを確認いただく場合は、以下の3つをご覧ください。

1. **球体地形上での移動・姿勢制御** — 3D数学・物理判定・キャラクター制御
2. **Photon Fusionによるオンライン対戦の同期設計** — Authority・入力処理・状態同期
3. **ラウンド進行とスキルシステムの設計** — 状態遷移・責務分離・複数機能の連携

### 1. 球体地形上での移動・姿勢制御

#### まず見るコード

[PlayerMove.cs](Assets/Scripts/Player/PlayerMove.cs)

`PlayerMove.ProbeGround()`

`PlayerMove.UpdateAimBasis()`

`PlayerMove.MoveOnSurface()`

#### 課題

一般的な3Dゲームでは、ワールド座標のY軸を上方向として移動や重力を処理できます。

しかし球体地形では、プレイヤーの位置によって地面の上方向が変化します。

ワールドの上方向を固定すると、惑星の側面や裏側で移動方向・姿勢・ジャンプが不自然になります。

さらに、地面の傾斜や凹凸に対しても、接地面に沿った移動方向を計算する必要があります。

#### 実装

`PlayerMove`では、地表法線を基準とする移動座標系を構築しています。

まず、`SphereCast`を使って足元の地面を検出し、接地位置と法線を取得します。

```text

接地判定

↓

地表法線を取得

↓

現在の上方向を決定

↓

移動方向を地表の接平面へ投影

↓

移動・姿勢・重力・ジャンプへ反映

```

取得した法線を`surfaceUp`として保持し、`Vector3.ProjectOnPlane`によって移動方向を地表の接平面へ投影します。

その方向から前方・右方向の直交基底を構築し、入力に応じた移動量を計算しています。

姿勢には`Quaternion.LookRotation`と`Quaternion.Slerp`を用い、地表の上方向に合わせてプレイヤーを回転させています。

また、地表を検出できない場合は惑星中心からプレイヤーへの方向を補助的に使用し、空中でも上方向を求められるようにしています。

ジャンプや落下についても同じ地表基準の上方向を使用し、通常の移動処理と一貫した座標系で扱っています。

#### 関連コード

- [PlayerMove.cs](Assets/Scripts/Player/PlayerMove.cs)
   - `ProbeGround`
   - `UpdateAimBasis`
   - `MoveOnSurface`
   - `MoveOnSurfaceVR`
   - `AlignToSurface`
   - `ApplyGravityAndJump`
- [PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)
   - `ApplyMovementInput`
- [PlayerLook.cs](Assets/Scripts/Player/PlayerLook.cs)
- [PlayerCamera.cs](Assets/Scripts/Camera/PlayerCamera.cs)

地形に依存する移動・姿勢・重力の処理を`PlayerMove`へ集約し、ネットワーク入力の取得やAuthority判定は`PlayerController`側に分離しています。

### 2. Photon Fusionによるオンライン対戦の同期設計

#### まず見るコード

[PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)

`PlayerController.FixedUpdateNetwork()`

`PlayerController.SimulatePlayer()`

#### 課題

オンライン対戦では、各端末が独立してHP減少・弾薬消費・スキル発動などを確定すると、二重処理や状態の不一致が発生する可能性があります。

一方、入力から描画までをすべて通信結果に依存させると、操作への反応が遅く感じられる原因になります。

そのため、プレイヤーの入力取得、ゲーム状態の確定、描画処理を分ける必要があります。

#### 実装

Photon FusionのHost方式を利用し、主要なゲーム状態をStateAuthority側で確定する構成にしています。

```text

InputAuthority側

   入力を収集

        ↓

   PlayerNetworkInput

        ↓

   Fusion経由で入力を渡す

        ↓

StateAuthority側

   FixedUpdateNetwork

        ↓

   移動・射撃・スキル処理

        ↓

   HP・弾薬・ゲーム状態を更新

        ↓

Networkedプロパティ / RPC

        ↓

各端末

   描画・UI・効果音などへ反映

```

`NetworkInputCollector`では、Unity Input Systemから取得した操作を`PlayerNetworkInput`へまとめます。

入力を収集するUnityのフレームと、Fusionが入力を取得するタイミングは必ずしも一致しません。そのため、ジャンプなどの単発操作は一時的に保持し、Fusionの入力取得時に渡す方式にしています。

`PlayerController.FixedUpdateNetwork()`ではStateAuthorityを確認してから入力を解釈し、移動・武器・スキルの各コンポーネントへ処理を振り分けています。

また、ローカルの視点更新は入力端末側でも行い、ゲーム状態の確定処理と視覚的な操作応答を分離しています。

HP・弾薬・スキル状態などの継続的な状態は同期プロパティを使用し、効果音のような単発イベントはRPCによって通知しています。

#### 関連コード

- [NetworkInputCollector.cs](Assets/Scripts/Network/NetworkInputCollector.cs)
   - `UpdateHmdAndLook`
   - `QueueGameplayButtons`
   - `CollectInput`
- [PlayerNetworkInput.cs](Assets/Scripts/Network/PlayerNetworkInput.cs)
- [PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)
   - `FixedUpdateNetwork`
   - `SimulatePlayer`
- [PlayerHealth.cs](Assets/Scripts/Player/PlayerHealth.cs)
- [PlayerWeapon.cs](Assets/Scripts/Player/PlayerWeapon.cs)
- [NetworkSessionController.cs](Assets/Scripts/Network/NetworkSessionController.cs)

ネットワーク接続・入力収集・プレイヤー生成・ゲームプレイ処理を別クラスに分離し、それぞれの変更が他の処理へ広がりにくい構成にしています。

### 3. ラウンド進行とスキルシステムの設計

#### まず見るコード

[RoundManager.cs](Assets/Scripts/Round/RoundManager.cs)

[RoundFlowController.cs](Assets/Scripts/Round/RoundFlowController.cs)

[PlayerSkillController.cs](Assets/Scripts/Player/PlayerSkillController.cs)

#### 課題

ラウンド制のオンライン対戦では、準備完了、スキル選択、戦闘、死亡、結果表示、次ラウンドへの移行といった複数の状態を管理する必要があります。

さらに、途中退出や通信の遅延によって、同じ処理が複数回実行されたり、古いスキル選択要求が届いたりする可能性があります。

これらを1つのクラスへ集中させると、機能追加や不具合修正の影響範囲が大きくなります。

#### 実装

試合の進行状態を明確に分け、`RoundManager`を各機能の窓口としています。

```text

WaitingForReady

     ↓

SkillSelecting

     ↓

RoundStarting

     ↓

RoundPlaying

     ↓

RoundEnding

     ↓

次ラウンド または MatchFinished

     ↓

WaitingForReady

```

具体的な処理は、次のクラスへ分担しています。

| クラス | 責務 |
| --- | --- |
| `RoundReadyController` | 準備完了状態の管理 |
| `RoundSkillSelectionController` | スキル選択の進行・確定 |
| `RoundSkillOptionBuilder` | 選択候補の抽選 |
| `RoundStartController` | 戦闘開始までの待機・スキル適用 |
| `RoundFlowController` | ラウンド終了・次ラウンド・待機復帰 |
| `RoundScoreCalculator` | 得点・勝敗判定 |
| `RoundSpawnController` | プレイヤーのリスポーン位置決定 |

スキル選択では、候補から2種類を選びます。

選択要求はStateAuthority側で処理し、送信者、選択フェーズを識別する番号、現在の選択段階を確認しています。

これにより、古いラウンドの要求や、応答待ちの再送による重複選択を抑える設計です。

また、戦闘開始時刻にはFusionの`TickTimer`を使用し、各端末が同期された残り時間を参照してカウントダウンを表示します。

#### スキル処理の分離

プレイヤー側のスキル機能についても、次のように責務を分けています。

- `PlayerSkillController`：2つのスキル枠、発動状態、効果時間、クールダウン
- `PlayerSkillActivator`：スキル発動時の一度限りの処理
- `PlayerSkillEffectApplier`：発動中の移動・武器・HPなどへの継続効果

継続効果は現在有効なスキルの組み合わせからパラメータを再計算し、効果終了後は通常の値へ戻す構成です。

特殊弾装填や次弾強化など、発動時に一度だけ実行する処理は`PlayerSkillActivator`側に分けています。

#### 関連コード

- [RoundManager.cs](Assets/Scripts/Round/RoundManager.cs)
- [RoundFlowController.cs](Assets/Scripts/Round/RoundFlowController.cs)
   - `CheckRoundEnd`
   - `AbortMatchToWaiting`
- [RoundSkillSelectionController.cs](Assets/Scripts/Round/RoundSkillSelectionController.cs)
   - `HandleRequestSelectSkillBySlot`
   - `TryFinishSkillSelection`
- [RoundSkillOptionBuilder.cs](Assets/Scripts/Round/RoundSkillOptionBuilder.cs)
- [PlayerSkillController.cs](Assets/Scripts/Player/PlayerSkillController.cs)
- [PlayerSkillActivator.cs](Assets/Scripts/Player/PlayerSkillActivator.cs)
- [PlayerSkillEffectApplier.cs](Assets/Scripts/Player/PlayerSkillEffectApplier.cs)

試合中に人数が不足した場合には、進行中の処理を停止し、残った参加者の得点・準備・スキル状態を初期化して待機状態へ戻す処理も設けています。

---

## 設計について

チーム開発で機能追加や調整を進めやすくするため、**変更理由の異なる処理を1つのクラスへ集中させないこと**を意識しています。

### プレイヤー機能の分離

`PlayerController`を操作処理の窓口とし、各機能を専用コンポーネントへ分けています。

- `PlayerMove`：移動・接地・重力・ジャンプ
- `PlayerLook`：視点方向
- `PlayerCamera`：カメラ制御
- `PlayerWeapon`：射撃・弾薬・リロード
- `PlayerHealth`：HP・死亡・リスポーン
- `PlayerSkillController`：スキル状態管理

### ネットワーク機能の分離

ネットワークについても、

- セッションの開始・終了・再接続
- PC・VR入力の収集
- プレイヤーの生成・破棄
- 対戦者・観戦者の役割管理
- ゲーム状態の確定・同期

を分けています。

`NetworkLauncher`は、これらのControllerとFusionのコールバックを接続する窓口として機能します。

### データと状態管理

ゲームの進行状態や各プレイヤーの状態は、Photon Fusionの同期機能を利用して管理しています。

一方、エフェクト・UI表示・効果音などの処理では、同期された状態や必要なイベント通知を受けて表示を更新する構成です。

また、UnityのInspectorから調整するパラメータは`SerializeField`で管理し、コードを変更せず調整できるようにしています。

---

## その他の実装

以下では、自身の担当機能に加え、チームメンバーが担当した関連機能も紹介しています。各機能の担当範囲は「チーム制作での担当範囲」をご参照ください。

<details>

<summary><strong>対戦者と観戦者を分離した参加者管理</strong></summary>

### 主なコード

- [NetworkParticipantRegistry.cs](Assets/Scripts/Network/NetworkParticipantRegistry.cs)
- [NetworkPlayerSpawner.cs](Assets/Scripts/Network/NetworkPlayerSpawner.cs)
- [LocalParticipantRoleController.cs](Assets/Scripts/Network/LocalParticipantRoleController.cs)

接続している端末を、対戦者と観戦者に分けて管理しています。

観戦者にもネットワーク上の接続者IDは存在しますが、プレイヤーキャラクターは生成しません。

そのため、観戦者はHP・スキル・得点・勝敗判定に含まれません。

役割変更はサーバー側で検証し、対戦人数上限や待機フェーズであるかを確認したうえで、キャラクターの生成・破棄を行います。

同じ役割要求が再送されても、生成・破棄を繰り返さないようにしています。

</details>

<details>

<summary><strong>死亡後・観戦専用端末で共通利用するカメラ</strong></summary>

### 主なコード

- [SpectatorCameraController.cs](Assets/Scripts/Camera/SpectatorCameraController.cs)
- [PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)
   - `UpdateNetworkedSpectatorView`
- [BattleHudBinder.cs](Assets/Scripts/UI/BattleHUD/BattleHudBinder.cs)

死亡したプレイヤーと観戦専用端末で、プレイヤー視点の観戦処理を共通利用しています。

- 生存プレイヤーを観戦対象として列挙
- コントローラーやキーボードで対象を切り替え
- 同期されたカメラ位置・向きへ追従
- 観戦対象のモデルや頭上アイコンをローカル表示から一時的に除外
- 観戦対象に応じたHUDへ切り替え
- 復活・観戦終了時にカメラと表示状態を復元

プレイヤーの観戦用視点は、StateAuthority側で同期する位置・方向を参照しています。

</details>

<details>

<summary><strong>射撃・弾薬・リロード・特殊弾</strong></summary>

### 主なコード

- [PlayerWeapon.cs](Assets/Scripts/Player/PlayerWeapon.cs)
- [Projectile.cs](Assets/Scripts/Projectile/Projectile.cs)

射撃機能では、射撃間隔・残弾・リロード状態を管理しています。

弾の生成に成功した場合だけ弾薬を消費し、StateAuthority側で命中・ダメージ処理を行います。

通常弾に加え、重力の影響を受ける特殊弾や、着弾時に周囲へダメージを与える弾を実装しています。

範囲ダメージでは、同じプレイヤーの複数Colliderによってダメージが重複しないよう、処理済みの対象を管理しています。

</details>

<details>

<summary><strong>PC・VR入力の共通処理</strong></summary>

### 主なコード

- [NetworkInputCollector.cs](Assets/Scripts/Network/NetworkInputCollector.cs)
- [PlayerNetworkInput.cs](Assets/Scripts/Network/PlayerNetworkInput.cs)
- [VRBodyTargetSync.cs](Assets/Scripts/Player/VRBodyTargetSync.cs)

キーボード・ゲームパッド・VRコントローラーから取得した入力を、共通のネットワーク入力データへまとめています。

VRでは、HMDや左右コントローラーの位置・回転を入力データとして扱い、身体の表示制御に利用しています。

Editor上でVR入力を確認するためのシミュレーション処理も実装しています。

</details>

<details>

<summary><strong>HP・死亡・ラウンドごとのリスポーン</strong></summary>

### 主なコード

- [PlayerHealth.cs](Assets/Scripts/Player/PlayerHealth.cs)
- [RoundSpawnController.cs](Assets/Scripts/Round/RoundSpawnController.cs)

HPの減少・回復・死亡をStateAuthority側で処理しています。

死亡後はプレイヤー操作・表示状態を切り替え、次ラウンドではHP・移動状態・武器状態などを初期化してリスポーンさせます。

リスポーン位置は複数の候補から選択し、同じラウンドでプレイヤー同士が重なりにくいように配置しています。

</details>

<details>

<summary><strong>透視スキルとプレイヤー描画の制御</strong></summary>

### 主なコード

- [XRayVisionController.cs](Assets/Scripts/Effect/XRayVisionController.cs)
- [PlayerSkillController.cs](Assets/Scripts/Player/PlayerSkillController.cs)

透視スキルの発動状態に応じて、他プレイヤーの表示方法を変更します。

表示を変更する対象のRendererと元のMaterial情報を管理し、効果終了時に元の表示へ戻す処理を実装しています。

</details>

---

## 使用技術

| 分野 | 使用技術 |
| --- | --- |
| Game Engine | Unity 6000.3.17f1 |
| Language | C# |
| Networking | Photon Fusion |
| Input | Unity Input System |
| VR | OpenXR / XR Interaction Toolkit / Meta XR SDK |
| Camera | Cinemachine |
| Rendering | Universal Render Pipeline（URP） |
| UI | Unity UI / TextMeshPro |

### 主な技術選定理由

<details>

<summary><strong>詳細を見る</strong></summary>

**Unity / C#**

チーム制作において、プレイヤー・UI・演出などをコンポーネント単位で分担し、Inspectorから動作を調整しやすい環境として利用しています。

**Photon Fusion**

複数人でのオンライン対戦に必要なネットワーク入力と状態同期を扱うために採用しています。

本作ではHost方式を用い、状態の確定処理をStateAuthority側に集約しています。

**Unity Input System**

PC・ゲームパッド・VRコントローラーといった複数の入力方式を扱うために利用しています。

**Cinemachine**

通常のプレイヤーカメラと観戦時のカメラ切り替え・追従に利用しています。

</details>

---

## コード構成

主要なゲームプログラムは`Assets/Scripts/`以下に配置しています。

```text

Assets/Scripts/

├─ Camera/

│  ├─ PlayerCamera.cs

│  └─ SpectatorCameraController.cs

│

├─ Network/

│  ├─ NetworkLauncher.cs

│  ├─ NetworkSessionController.cs

│  ├─ NetworkInputCollector.cs

│  ├─ NetworkPlayerSpawner.cs

│  ├─ NetworkParticipantRegistry.cs

│  ├─ LocalParticipantRoleController.cs

│  └─ LocalSkillSelectionInputController.cs

│

├─ Player/

│  ├─ PlayerController.cs

│  ├─ PlayerMove.cs

│  ├─ PlayerLook.cs

│  ├─ PlayerHealth.cs

│  ├─ PlayerWeapon.cs

│  ├─ PlayerSkillController.cs

│  ├─ PlayerSkillActivator.cs

│  ├─ PlayerSkillEffectApplier.cs

│  └─ VRBodyTargetSync.cs

│

├─ Projectile/     弾の移動・衝突・ダメージ

├─ Item/           回復アイテム

├─ Effect/         表示効果

├─ Round/          ラウンド進行・得点・選択・復活

├─ Sound/          BGM・効果音

└─ UI/             HUD・待機画面・スキル選択・結果表示

```

各機能の内部処理を専用クラスに分離し、`NetworkLauncher`、`PlayerController`、`RoundManager`などの窓口から連携させています。

---

## 実行方法

### 必要環境

- Unity 6000.3.17f1
- Photon Fusionの利用に必要な設定
- PCで実行する場合は対応する入力デバイス

### PC向けシーン

`Assets/Scenes/ForPCMainScene.unity`

### 起動手順

1. Unity Hubからプロジェクトを開く
2. `ForPCMainScene`を開く
3. Photon Fusionの必要な設定を確認する
4. Unity EditorでPlayする
5. Host / Clientとして対戦ルームに接続する

`NetworkLauncher`では自動接続や手動接続を設定できます。

複数端末で対戦する場合は、同じルームへ接続して参加します。

観戦専用端末も同じルームへ接続し、対戦者とは別の役割で参加できます。

※本READMEでは、リポジトリ上で確認できるPC向けシーンを案内しています。VR機器別のセットアップ・ビルド手順は、使用環境での確認が必要です。

## 補足

本リポジトリにはUnityプロジェクトに必要な外部ライブラリやアセットも含まれています。

プログラムの設計・実装を評価いただく場合は、主に[Assets/Scripts](Assets/Scripts)をご確認ください。

特に重点的に取り組んだ実装については、上記の「コードを見る方へ ― まずこの3箇所」にまとめています。

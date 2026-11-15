# 再接続・VR観戦の修正（2026-10-30）

## 変更した動作

- 再接続・接続失敗後の再試行では、旧RunnerのShutdown後にGameSceneを再読み込みする。破棄されたRoundManagerなどのシーン内NetworkObjectを復元してから新しいRunnerを作る。
- LauncherとLocalPlayModeControllerは引き続き常駐する。新しいシーンのLauncherは参照を引き継いで破棄されるため、起動モードを変更せず、追加の自動接続もしない。
- シーン復元後は入力Action、UI、Camera、XR参照を再設定し、古いプレイヤーと保留入力をクリアする。
- 切断後はRoundManagerが存在しなくても再接続操作を受け付ける。PCはR、ゲームパッドはStart、VRは両手グリップ。待機中も同じ操作でClientとして接続し直せる。
- 自動再試行は従来どおりClientのGameNotFoundだけ。Hostの退出後に対戦の状態を引き継ぐHost Migrationは実装していない。
- VR観戦では、観戦相手の目の位置と身体の向きにXR Originを合わせる。相手の頭の回転を強制せず、観戦者自身の頭の追跡を維持する。対象変更、復活、ラウンド終了、切断で元のプレイヤーの位置・回転へ戻す。
- VRの観戦対象は左スティック左右でも切り替えられる。PCのCinemachine観戦は従来の方式を使う。

## 前回レビューの訂正

GamePlayerの`hideLocalVisual: 0`だけを根拠に「PCで身体が表示される」とした指摘は不十分だった。`PlayerHealth.ApplyAliveState`が自分の身体を非表示にし、武器は別途表示している。表示処理は維持し、スポーン・復活後の回帰検査を追加した。

## 検証方法

`GameRecoveryValidation.Run`はUnityのバッチモード用。終了時にEditorも終了するため、通常の作業中のEditorでは呼ばない。

この検証は外部Photon接続を使わず`GameMode.Single`で実行する。接続中の連続再接続要求と、Runner終了後の再接続をそれぞれ1回ずつ実行する。同じ再接続処理へSingleを渡して2回復旧する。毎回シーンとRoundManagerが新規生成され、Launcher、Runner、Camera、AudioListenerが1つであること、入力ActionとローカルPlayer参照が復元すること、重複Joinを無視すること、自分の身体がスポーン・復活後に非表示であることを検査する。

VR部分はHMDなしでXR Originの配置、身体の向き、物理的な頭の移動に相当する位置差分、終了時の原状復帰を検査する。実際のXR初期化、Questの入力・表示、Photonの複数クライアント通信はこのテストの対象外。

実行結果は`Logs/RecoveryValidation*.log`で確認する。完了判定は`[GameRecoveryValidation] PASS`と終了コード0の両方を使う。

2026-10-30の実行結果：Unity 6000.3.17f1のバッチ・描画なしモードで最終テスト成功、終了コード0。ログは`Logs/RecoveryValidationFinal.log`。UnityのコンパイルとFusion IL後処理も成功。独立したレビュー担当エージェントによる静的レビューでは追加の確実な不具合は見つからなかった。以前追加した構成検証スクリプトのmeta GUIDが33桁でインポートされない問題も修正した。

## 実機で残る確認

1. PC＋PC、PC＋VR、VR＋VRで参加・Ready・試合開始。
2. 接続中に切断してから、各端末の再接続操作で復帰できること。
3. 3人以上など死亡後も戦闘が続く条件で、VR観戦開始、対象変更、復活時に頭の追跡と視点の復帰が正しいこと。
4. 観戦中の移動や向きの追従について、VRでの見やすさを確認すること。

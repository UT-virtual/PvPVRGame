using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Meta XRの後処理が上書きなしでコピーする生成ファイルを、その直前に片付ける。
/// PackageCacheを書き換えず、再ビルド時の同名ファイル衝突を防ぐ。
/// </summary>
public sealed class MetaXrRuntimeBindingsBuildCleanup : IPostprocessBuildWithReport
{
    private const string FileName = "RuntimeActionBindings.json";

    // OVRDumpBuildInfo.callbackOrder (0) より先に実行する。
    public int callbackOrder => -100;

    public void OnPostprocessBuild(BuildReport report)
    {
        // AndroidではSDKがこのコピーを行わない。
        if (report.summary.platformGroup != BuildTargetGroup.Standalone) return;

        string source = Path.Combine(Application.dataPath, "StreamingAssets", FileName);
        try
        {
            if (RemovePreviousGeneratedBindings(source, report.summary.outputPath))
                Debug.Log("[Meta XR] Removed previous generated runtime bindings before SDK copy.");
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new BuildFailedException("Meta XRの生成済み入力設定を更新できません。出力先の権限とファイルの使用状況を確認してください。\n" + exception.Message);
        }
    }

    internal static bool RemovePreviousGeneratedBindings(string source, string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath) || !File.Exists(source)) return false;

        // SDKと同じパス計算にし、Tempに限らず実際のビルド出力先へ対応する。
        string destination = Path.GetFullPath(outputPath + "/../" + FileName);
        string sourcePath = Path.GetFullPath(source);
        if (string.Equals(sourcePath, destination, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Build output overlaps the StreamingAssets source directory.");

        if (!File.Exists(destination)) return false;
        File.Delete(destination);
        return true;
    }
}

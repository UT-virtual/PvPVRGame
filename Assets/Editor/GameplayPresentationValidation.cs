using System;
using System.Reflection;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>地表に対する復活位置と、同率勝利の表示をEditorで検証する。</summary>
public static class GameplayPresentationValidation
{
    public static void Run()
    {
        try
        {
            GameSetupValidation.Validate();
            ValidateGroundedRespawn();
            ValidateSharedWinners();
            Debug.Log("[GameplayPresentationValidation] PASS: grounded respawn, shared winners, centered text, shared game setup.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateGroundedRespawn()
    {
        GameObject ground = new("ValidationPlanet", typeof(SphereCollider));
        GameObject player = new("ValidationPlayer", typeof(CharacterController), typeof(PlayerMove));
        try
        {
            ground.layer = 29;
            ground.transform.position = new Vector3(20000f, 0f, 0f);
            ground.GetComponent<SphereCollider>().radius = 10f;
            CharacterController capsule = player.GetComponent<CharacterController>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            capsule.skinWidth = 0.08f;
            PlayerMove move = player.GetComponent<PlayerMove>();
            SetField(move, "controller", capsule);
            SetField(move, "planetCenter", ground.transform);
            SetField(move, "planetLayer", (LayerMask)(1 << 29));
            Physics.SyncTransforms();
            foreach (Vector3 up in new[] { Vector3.up, Vector3.down, Vector3.right, new Vector3(1f, 1f, 1f).normalized })
            {
                foreach (float centerY in new[] { 0f, 0.5f })
                {
                    capsule.center = new Vector3(0f, centerY, 0f);
                    // 足元が地面に重なるスポーンでも、移動せずに立てる位置へ補正する。
                    Vector3 position = ground.transform.position + up * 10f;
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up);
                    move.ResolveGroundedRespawnPose(ref position, ref rotation);
                    Vector3 capsuleBottom = position + rotation * Vector3.up * (centerY - capsule.height * 0.5f);
                    float clearance = Vector3.Distance(capsuleBottom, ground.transform.position) - 10f;
                    Require(clearance >= 0.03f && clearance < 0.15f, $"Respawn intersects planet or floats: {clearance}");
                    Require(Vector3.Dot(rotation * Vector3.up, up) > 0.99f, "Respawn orientation does not follow planet surface.");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(ground);
        }
    }

    private static void ValidateSharedWinners()
    {
        GameObject root = new("ValidationResult", typeof(RectTransform));
        GameObject label = new("ValidationWinner", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            label.transform.SetParent(root.transform, false);
            TMP_Text text = label.GetComponent<TMP_Text>();
            FinalResultUI ui = root.AddComponent<FinalResultUI>();
            SetField(ui, "winnerText", text);
            MethodInfo show = typeof(FinalResultUI).GetMethod("ShowWinnerText", BindingFlags.Instance | BindingFlags.NonPublic);
            show.Invoke(ui, new object[] { (1 << (int)TeamColor.Red) | (1 << (int)TeamColor.Blue) });
            Require(text.text == "<space=1.2em><space=1.2em>の勝利！" && text.GetComponentsInChildren<Image>().Length == 2,
                "Tied leaders were not both announced as winners.");
            Require(text.alignment == TextAlignmentOptions.Center, "Winner announcement is not centered.");
            show.Invoke(ui, new object[] { 1 << (int)TeamColor.Red });
            Require(text.text == "<space=1.2em>の勝利！" && text.GetComponentsInChildren<Image>().Count(icon => icon.gameObject.activeSelf) == 1,
                "Single winner announcement regressed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

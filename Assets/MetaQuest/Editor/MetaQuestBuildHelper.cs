// MetaQuestBuildHelper.cs
// Meta Quest向けビルド設定を一括適用するエディターツール

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class MetaQuestBuildHelper : EditorWindow
{
    [MenuItem("Tools/Meta Quest/Setup Build Settings")]
    static void ShowWindow()
    {
        GetWindow<MetaQuestBuildHelper>("Meta Quest Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Meta Quest ビルド設定", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("1. Androidプラットフォームに切替", GUILayout.Height(30)))
        {
            SwitchToAndroid();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("2. XR Plugin設定を適用", GUILayout.Height(30)))
        {
            ApplyXRSettings();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("3. Player Settings 最適化", GUILayout.Height(30)))
        {
            OptimizePlayerSettings();
        }

        GUILayout.Space(20);
        GUILayout.Label("手動設定が必要な項目:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "以下はUnity Editorで手動設定してください:\n\n" +
            "1. Edit > Project Settings > XR Plug-in Management\n" +
            "   - Androidタブで 'OpenXR' にチェック\n" +
            "   - OpenXR > Features で 'Meta Quest Feature Group' を有効化\n\n" +
            "2. Edit > Project Settings > XR Plug-in Management > OpenXR\n" +
            "   - Interaction Profiles に 'Meta Quest Touch Pro Controller' を追加\n\n" +
            "3. File > Build Settings > Android\n" +
            "   - Texture Compression: ASTC\n" +
            "   - Run Device: Quest デバイスを選択",
            MessageType.Info);
    }

    static void SwitchToAndroid()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[MetaQuest] Androidプラットフォームに切替完了");
        }
        else
        {
            Debug.Log("[MetaQuest] すでにAndroidプラットフォームです");
        }
    }

    static void ApplyXRSettings()
    {
        // Graphics API を OpenGLES3 / Vulkan に設定
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });

        Debug.Log("[MetaQuest] Graphics API: Vulkan に設定");
        Debug.Log("[MetaQuest] XR Plugin Management は手動で OpenXR を有効化してください");
    }

    static void OptimizePlayerSettings()
    {
        // Android 固有設定
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Quest 2 minimum
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;

        // IL2CPP（Quest必須）
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        // ARM64（Quest必須）
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // 画面設定
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // パフォーマンス
        PlayerSettings.gpuSkinning = true;
        QualitySettings.vSyncCount = 0; // VRではVSyncをOFF

        Debug.Log("[MetaQuest] Player Settings 最適化完了");
        Debug.Log("[MetaQuest] Min SDK: API 29 / Target: API 32 / IL2CPP / ARM64");
    }
}
#endif

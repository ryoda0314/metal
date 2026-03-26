// PassthroughBlendFeatureEnabler.cs
// Editor script that automatically enables the PassthroughBlendFeature in OpenXR settings for Android

using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

[InitializeOnLoad]
public static class PassthroughBlendFeatureEnabler
{
    static PassthroughBlendFeatureEnabler()
    {
        // Delay to ensure OpenXR settings are loaded
        EditorApplication.delayCall += EnableFeature;
    }

    static void EnableFeature()
    {
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null) return;

        var feature = settings.GetFeature<PassthroughBlendFeature>();
        if (feature == null)
        {
            Debug.LogWarning("[PassthroughBlendFeatureEnabler] PassthroughBlendFeature not found in OpenXR Android settings. Reimport may be needed.");
            return;
        }

        if (!feature.enabled)
        {
            feature.enabled = true;
            EditorUtility.SetDirty(settings);
            Debug.Log("[PassthroughBlendFeatureEnabler] PassthroughBlendFeature を自動有効化しました");
        }
    }
}

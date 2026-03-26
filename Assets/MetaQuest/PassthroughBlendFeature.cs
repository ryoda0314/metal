// PassthroughBlendFeature.cs
// OpenXR Feature that sets Environment Blend Mode to AlphaBlend for Meta Quest passthrough MR
// This must be enabled in Project Settings → XR Plug-in Management → OpenXR → Android tab

using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;

[OpenXRFeature(
    UiName = "Passthrough Blend Mode (AlphaBlend)",
    BuildTargetGroups = new[] { BuildTargetGroup.Android },
    FeatureId = PassthroughBlendFeature.featureId,
    OpenxrExtensionStrings = "",
    Company = "Custom",
    Version = "1.0.0"
)]
#endif
public class PassthroughBlendFeature : OpenXRFeature
{
    public const string featureId = "com.custom.passthrough-blend";

    protected override void OnSessionCreate(ulong xrSession)
    {
        SetEnvironmentBlendMode(XrEnvironmentBlendMode.AlphaBlend);
        Debug.Log("[PassthroughBlend] Environment Blend Mode → AlphaBlend (パススルー有効)");
    }
}

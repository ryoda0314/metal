// VRPlatformManager.cs
// VRプラットフォーム検出と統一入力インターフェース
// SteamVR / Meta Quest (OpenXR) の両対応

using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class VRPlatformManager : MonoBehaviour
{
    public enum VRPlatform { Unknown, SteamVR, MetaQuest, OpenXR_Generic }

    public static VRPlatformManager Instance { get; private set; }
    public VRPlatform CurrentPlatform { get; private set; } = VRPlatform.Unknown;

    [Header("Debug")]
    public bool logPlatformInfo = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        DetectPlatform();
    }

    void DetectPlatform()
    {
        var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(xrDisplaySubsystems);

        string loadedDeviceName = "";
        if (xrDisplaySubsystems.Count > 0)
        {
            loadedDeviceName = xrDisplaySubsystems[0].SubsystemDescriptor.id;
        }

        // XRSettings からもデバイス名を取得
        var inputDevices = new List<InputDevice>();
        InputDevices.GetDevices(inputDevices);

        bool isQuest = false;
        bool isSteamVR = false;

        foreach (var device in inputDevices)
        {
            string deviceName = device.name.ToLower();
            if (deviceName.Contains("quest") || deviceName.Contains("oculus") || deviceName.Contains("meta"))
                isQuest = true;
            if (deviceName.Contains("vive") || deviceName.Contains("index") || deviceName.Contains("steamvr"))
                isSteamVR = true;
        }

        // OpenXR runtime名でも判定
        if (loadedDeviceName.ToLower().Contains("oculus") || loadedDeviceName.ToLower().Contains("meta"))
            isQuest = true;
        if (loadedDeviceName.ToLower().Contains("steamvr") || loadedDeviceName.ToLower().Contains("openvr"))
            isSteamVR = true;

#if UNITY_ANDROID
        // Android = Meta Quest (スタンドアロン)
        isQuest = true;
#endif

        if (isQuest)
            CurrentPlatform = VRPlatform.MetaQuest;
        else if (isSteamVR)
            CurrentPlatform = VRPlatform.SteamVR;
        else
            CurrentPlatform = VRPlatform.OpenXR_Generic;

        if (logPlatformInfo)
        {
            Debug.Log($"[VRPlatform] Detected: {CurrentPlatform} | XR Subsystem: {loadedDeviceName}");
        }
    }

    /// <summary>
    /// コントローラーの速度を取得（プラットフォーム非依存）
    /// </summary>
    public static bool TryGetControllerVelocity(InputDevice device, out Vector3 velocity)
    {
        return device.TryGetFeatureValue(CommonUsages.deviceVelocity, out velocity);
    }

    /// <summary>
    /// コントローラーの角速度を取得
    /// </summary>
    public static bool TryGetControllerAngularVelocity(InputDevice device, out Vector3 angVelocity)
    {
        return device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out angVelocity);
    }

    /// <summary>
    /// グリップの押し込み量を取得 (0-1)
    /// </summary>
    public static float GetGripValue(InputDevice device)
    {
        if (device.TryGetFeatureValue(CommonUsages.grip, out float value))
            return value;
        return 0f;
    }

    /// <summary>
    /// トリガーの押し込み量を取得 (0-1)
    /// </summary>
    public static float GetTriggerValue(InputDevice device)
    {
        if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
            return value;
        return 0f;
    }

    /// <summary>
    /// ハプティクス（振動）を送信
    /// </summary>
    public static void SendHaptics(InputDevice device, float amplitude, float duration)
    {
        device.SendHapticImpulse(0, amplitude, duration);
    }
}

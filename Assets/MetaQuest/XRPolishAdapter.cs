// XRPolishAdapter.cs
// MetalSwirlPolisherをXR Interaction Toolkit（Meta Quest）で使えるようにするアダプター
// 既存のMetalSwirlPolisherコンポーネントと併用する

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

/// <summary>
/// MetalSwirlPolisherにXRI互換の入力を提供するアダプター。
/// MetalSwirlPolisherと同じGameObjectにアタッチして使用。
/// SteamVRのHand依存を置き換え、XRコントローラーの速度・位置を供給する。
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class XRPolishAdapter : MonoBehaviour
{
    [Header("XR Input")]
    [Tooltip("左右どちらのコントローラーを優先するか")]
    public XRNode preferredHand = XRNode.RightHand;

    [Header("Polish Feedback")]
    [Tooltip("磨き時のハプティクス強度")]
    [Range(0f, 1f)]
    public float hapticAmplitude = 0.3f;
    [Tooltip("ハプティクス持続時間")]
    public float hapticDuration = 0.05f;

    private XRGrabInteractable grabInteractable;
    private InputDevice activeController;
    private bool isGrabbed = false;
    private Transform interactorTransform;

    // 速度追跡（MetalSwirlPolisherが参照）
    public Vector3 ControllerVelocity { get; private set; }
    public Vector3 ControllerPosition { get; private set; }
    public bool IsActive => isGrabbed;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void Start()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);

        // コントローラーデバイスを取得
        RefreshController();
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    void RefreshController()
    {
        activeController = InputDevices.GetDeviceAtXRNode(preferredHand);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        interactorTransform = args.interactorObject.GetAttachTransform(grabInteractable);
        RefreshController();
        Debug.Log("[XRPolishAdapter] Polisher grabbed via XRI");
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        interactorTransform = null;
        Debug.Log("[XRPolishAdapter] Polisher released");
    }

    void Update()
    {
        if (!activeController.isValid)
        {
            RefreshController();
        }

        // コントローラーの速度を更新
        if (activeController.isValid)
        {
            if (activeController.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 vel))
                ControllerVelocity = vel;

            if (activeController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                ControllerPosition = pos;
        }
        else if (interactorTransform != null)
        {
            // フォールバック: Transformの差分から速度を推定
            Vector3 newPos = interactorTransform.position;
            ControllerVelocity = (newPos - ControllerPosition) / Time.deltaTime;
            ControllerPosition = newPos;
        }
    }

    /// <summary>
    /// 磨き中のハプティクスフィードバック
    /// </summary>
    public void SendPolishHaptics()
    {
        if (!isGrabbed || !activeController.isValid) return;
        activeController.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
    }

    /// <summary>
    /// 強い衝突時のハプティクス
    /// </summary>
    public void SendImpactHaptics(float intensity)
    {
        if (!activeController.isValid) return;
        float amp = Mathf.Clamp01(intensity);
        activeController.SendHapticImpulse(0, amp, 0.1f);
    }
}

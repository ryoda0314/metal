// PolisherControllerMount.cs
// 研磨機をコントローラーに固定するスクリプト（Mode B）
// MetalSwirlPolisher の Mode A (グラブ) とは完全に独立
// このコンポーネントが付いていれば研磨機はコントローラーに追随する

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
#if UNITY_ANDROID
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#else
using Valve.VR.InteractionSystem;
#endif

[DefaultExecutionOrder(50)] // MetalSwirlPolisher(100) より先に実行
public class PolisherControllerMount : MonoBehaviour
{
    public enum TargetHand { Left, Right }

    [Header("固定先")]
    public TargetHand hand = TargetHand.Right;

    [Header("オフセット")]
    [Tooltip("コントローラーからの位置オフセット")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("コントローラーからの回転オフセット（度）")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("トリガー")]
    [Tooltip("トリガーを押している間だけ研磨する（OFFなら常時研磨）")]
    public bool requireTrigger = true;

    // === 外部から参照（MetalSwirlPolisher が読む） ===
    [HideInInspector] public bool isMounted = false;
    [HideInInspector] public bool isTriggerActive = false;

    private Transform controllerTransform;
    private InputAction triggerAction;

#if UNITY_ANDROID
    private XRBaseController xrController;
#else
    private Hand steamVRHand;
#endif

    void Awake()
    {
        // グラブ系コンポーネントを無効化（Aの掴む動作を排除）
        DisableGrabComponents();
    }

    void Start()
    {
        StartCoroutine(FindAndMount());
    }

    void DisableGrabComponents()
    {
#if UNITY_ANDROID
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = false;
            Debug.Log("[Mount] XRGrabInteractable を無効化");
        }
#else
        var inter = GetComponent<Interactable>();
        if (inter != null) { inter.enabled = false; }
        var throwable = GetComponent<Throwable>();
        if (throwable != null) { throwable.enabled = false; }
        Debug.Log("[Mount] SteamVR Interactable/Throwable を無効化");
#endif

        // Rigidbody をキネマティックに
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    IEnumerator FindAndMount()
    {
        // XRRigSetup.Start() でコントローラーが生成されるのを待つ
        yield return null;
        yield return null; // 2フレーム待機

#if UNITY_ANDROID
        var controllers = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(FindObjectsSortMode.None);
        UnityEngine.XR.Interaction.Toolkit.ActionBasedController targetCtrl = null;
        string handName = hand == TargetHand.Left ? "Left" : "Right";

        foreach (var c in controllers)
        {
            if (c.gameObject.name.Contains(handName))
            {
                targetCtrl = c;
                break;
            }
        }

        if (targetCtrl == null)
        {
            Debug.LogError($"[Mount] {handName} Controller が見つかりません！");
            yield break;
        }

        controllerTransform = targetCtrl.transform;
        xrController = targetCtrl;

        // トリガー入力
        string devicePath = hand == TargetHand.Left
            ? "<XRController>{LeftHand}"
            : "<XRController>{RightHand}";
        triggerAction = new InputAction("MountTrigger", InputActionType.Value, devicePath + "/trigger");
        triggerAction.Enable();

        isMounted = true;
        Debug.Log($"[Mount] {handName} Controller に固定完了");

#else
        var hands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
        Hand targetHand = null;
        string steamHandName = hand == TargetHand.Left ? "left" : "right";

        foreach (var h in hands)
        {
            if (h.gameObject.name.ToLower().Contains(steamHandName))
            {
                targetHand = h;
                break;
            }
        }

        if (targetHand == null)
        {
            Debug.LogError($"[Mount] {steamHandName} Hand が見つかりません！");
            yield break;
        }

        controllerTransform = targetHand.transform;
        steamVRHand = targetHand;
        isMounted = true;
        Debug.Log($"[Mount] {steamHandName} Hand に固定完了");
#endif
    }

    void LateUpdate()
    {
        if (!isMounted || controllerTransform == null) return;

        // コントローラーに追随（毎フレーム位置・回転を強制上書き）
        transform.position = controllerTransform.TransformPoint(positionOffset);
        transform.rotation = controllerTransform.rotation * Quaternion.Euler(rotationOffset);

        // トリガー状態を更新
        if (!requireTrigger)
        {
            isTriggerActive = true;
        }
        else if (triggerAction != null)
        {
            isTriggerActive = triggerAction.ReadValue<float>() > 0.1f;
        }
        else
        {
            isTriggerActive = false;
        }
    }

    /// <summary>振動フィードバックを送信</summary>
    public void SendHaptic(float amplitude, float duration)
    {
#if UNITY_ANDROID
        if (xrController != null)
            xrController.SendHapticImpulse(amplitude, duration);
#else
        if (steamVRHand != null)
            steamVRHand.TriggerHapticPulse((ushort)(duration * 1000000f), 100f, amplitude);
#endif
    }
}

using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#else
using Valve.VR.InteractionSystem;
#endif

#if !UNITY_ANDROID
[RequireComponent(typeof(Interactable))]
#endif
public class CubeController : MonoBehaviour
{
#if UNITY_ANDROID
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        // Rigidbody を kinematic にして掴む前に落下しないようにする
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 精密つかみ: つかんだ場所を維持（中心にスナップしない）
        grabInteractable.useDynamicAttach = true;

        // 即座に手に追従
        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;

        // 投げ無効（手を離したらその場に留まる）
        grabInteractable.throwOnDetach = false;

        // 片手のみ（もう片方の手でつかむと元の手から離れる）
        grabInteractable.selectMode = InteractableSelectMode.Single;
    }
#else
    // 精密つかみ用：つかんだ時のオフセットを保存
    private Vector3 grabOffsetLocal;      // 手のローカル座標系でのオフセット
    private Quaternion grabRotationLocal; // 手のローカル座標系での回転
    private Hand attachedHand;            // 現在つかんでいる手
    private bool isGrabbed = false;

    // 動的アタッチメントポイント（手のモデルが配置される場所）
    private GameObject dynamicAttachPoint;

    private Hand.AttachmentFlags attachmentFlags =
        Hand.AttachmentFlags.DetachFromOtherHand |
        Hand.AttachmentFlags.TurnOnKinematic |
        Hand.AttachmentFlags.DetachOthers;

    private Interactable interactable;

    void Awake()
    {
        interactable = this.GetComponent<Interactable>();
        if (interactable != null)
        {
            interactable.hideHandOnAttach = false;
            interactable.hideSkeletonOnAttach = false;
            interactable.highlightOnHover = false;
            interactable.skeletonPoser = null;
            interactable.useHandObjectAttachmentPoint = false;
            interactable.attachEaseIn = false;

            // 手のモデルはコントローラー位置に留まる（つかんだ場所を維持）
            interactable.handFollowTransform = false;
        }

        // 動的アタッチポイントを作成
        dynamicAttachPoint = new GameObject("DynamicAttachPoint");
        dynamicAttachPoint.transform.SetParent(transform);
        dynamicAttachPoint.transform.localPosition = Vector3.zero;
        dynamicAttachPoint.transform.localRotation = Quaternion.identity;
    }

    void Start()
    {
        var throwable = GetComponent<Throwable>();
        if (throwable != null)
        {
            throwable.attachmentFlags &= ~Hand.AttachmentFlags.SnapOnAttach;
            throwable.attachmentOffset = null;
            throwable.catchingSpeedThreshold = -1;
        }
    }

    private void HandHoverUpdate(Hand hand)
    {
        if (GetComponent<Throwable>() != null) return;

        GrabTypes startingGrabType = hand.GetGrabStarting();
        bool isGrabEnding = hand.IsGrabEnding(this.gameObject);

        if (!isGrabbed && startingGrabType != GrabTypes.None)
        {
            Transform handTransform = hand.transform;
            grabOffsetLocal = handTransform.InverseTransformPoint(transform.position);
            grabRotationLocal = Quaternion.Inverse(handTransform.rotation) * transform.rotation;

            Vector3 handPos = handTransform.position;
            Vector3 surfacePoint = handPos;

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Vector3 rayStart = handPos + transform.up * 0.5f;
                Ray ray = new Ray(rayStart, -transform.up);
                RaycastHit hit;
                if (col.Raycast(ray, out hit, 1.0f))
                {
                    surfacePoint = hit.point;
                    Debug.Log($"[CubeController] Surface point found: {surfacePoint}");
                }
            }

            dynamicAttachPoint.transform.position = surfacePoint;
            dynamicAttachPoint.transform.rotation = handTransform.rotation;

            hand.HoverLock(interactable);
            hand.AttachObject(gameObject, startingGrabType, attachmentFlags, dynamicAttachPoint.transform);

            attachedHand = hand;
            isGrabbed = true;

            Debug.Log($"[CubeController] Grabbed! Offset: {grabOffsetLocal}");
        }
        else if (isGrabbed && isGrabEnding)
        {
            hand.DetachObject(gameObject);
            hand.HoverUnlock(interactable);

            attachedHand = null;
            isGrabbed = false;

            Debug.Log("[CubeController] Released!");
        }
    }

    void LateUpdate()
    {
        if (isGrabbed && attachedHand != null)
        {
            Transform handTransform = attachedHand.transform;
            Vector3 targetPos = handTransform.TransformPoint(grabOffsetLocal);
            Quaternion targetRot = handTransform.rotation * grabRotationLocal;

            transform.position = targetPos;
            transform.rotation = targetRot;

            dynamicAttachPoint.transform.position = handTransform.position;
            dynamicAttachPoint.transform.rotation = handTransform.rotation;
        }
    }
#endif
}

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
[RequireComponent(typeof(Rigidbody))]
public class CubeController2 : MonoBehaviour
{
#if UNITY_ANDROID
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        rb = GetComponent<Rigidbody>();

        // 物理設定
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // つかんだ場所を維持（中心にスナップしない）
        grabInteractable.useDynamicAttach = true;

        // 速度追従（物理ベース：手の動きに物理的に追従）
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;

        // 投げ有効（手の速度を引き継ぐ）
        grabInteractable.throwOnDetach = true;

        // 片手のみ
        grabInteractable.selectMode = InteractableSelectMode.Single;
    }
#else
    private Interactable interactable;
    private Rigidbody rb;

    private Hand.AttachmentFlags attachmentFlags =
        Hand.defaultAttachmentFlags
        & (~Hand.AttachmentFlags.SnapOnAttach)
        & (~Hand.AttachmentFlags.DetachOthers);

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void HandHoverUpdate(Hand hand)
    {
        GrabTypes startingGrabType = hand.GetGrabStarting();
        bool isGrabEnding = hand.IsGrabEnding(gameObject);

        if (interactable.attachedToHand == null && startingGrabType != GrabTypes.None)
        {
            rb.isKinematic = true;
            rb.useGravity = false;

            hand.HoverLock(interactable);
            hand.AttachObject(gameObject, startingGrabType, attachmentFlags);
        }
        else if (isGrabEnding)
        {
            Vector3 releaseVel = hand.GetTrackedObjectVelocity();
            Vector3 releaseAngVel = hand.GetTrackedObjectAngularVelocity();

            hand.DetachObject(gameObject);
            hand.HoverUnlock(interactable);

            rb.isKinematic = false;
            rb.useGravity = true;

            rb.linearVelocity = releaseVel;
            rb.angularVelocity = releaseAngVel;
        }
    }
#endif
}

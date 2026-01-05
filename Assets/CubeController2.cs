using UnityEngine;
using Valve.VR.InteractionSystem;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class CubeController2 : MonoBehaviour
{
    private Interactable interactable;
    private Rigidbody rb;

    // 物理追従を有効にする（VelocityMovement を残す）
    // スナップや他を外したいなら必要なものだけ外す
    private Hand.AttachmentFlags attachmentFlags =
        Hand.defaultAttachmentFlags
        & (~Hand.AttachmentFlags.SnapOnAttach)     // 掴んだ瞬間のスナップは無効にする例
        & (~Hand.AttachmentFlags.DetachOthers);    // 他を自動デタッチしない例
                                                   // VelocityMovement は外さない！

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        rb = GetComponent<Rigidbody>();

        // 物理で動かしたい前提
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // 手がホバー中に毎フレーム呼ばれる
    private void HandHoverUpdate(Hand hand)
    {
        GrabTypes startingGrabType = hand.GetGrabStarting();
        bool isGrabEnding = hand.IsGrabEnding(gameObject);

        // 掴み開始
        if (interactable.attachedToHand == null && startingGrabType != GrabTypes.None)
        {
            // 掴んでいる間は手に追従させるため、物理衝突の影響を受けないようにする
            rb.isKinematic = true;
            rb.useGravity = false;

            hand.HoverLock(interactable);
            hand.AttachObject(gameObject, startingGrabType, attachmentFlags);
        }
        // 放す
        else if (isGrabEnding)
        {
            // 手の推定速度を拾う（投げたときに自然に飛ぶ）
            Vector3 releaseVel = hand.GetTrackedObjectVelocity();
            Vector3 releaseAngVel = hand.GetTrackedObjectAngularVelocity();

            hand.DetachObject(gameObject);
            hand.HoverUnlock(interactable);

            // 物理に戻す
            rb.isKinematic = false;
            rb.useGravity = true;

            // 放した瞬間の速度を与える
            rb.linearVelocity = releaseVel;
            rb.angularVelocity = releaseAngVel;

            // ★ 元位置・元回転に戻す処理はしない！
            // transform.position = oldPosition; // 削除
            // transform.rotation = oldRotation; // 削除
        }
    }
}

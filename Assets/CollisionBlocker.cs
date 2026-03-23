#if !UNITY_ANDROID
// CollisionBlocker.cs
// VRで掴んでいるときでも特定のコライダーを貫通しないようにする

using UnityEngine;
using Valve.VR.InteractionSystem;

public class CollisionBlocker : MonoBehaviour
{
    [Header("Blocking Target")]
    [Tooltip("貫通を防ぎたいコライダー（金属板など）")]
    public Collider blockingCollider;

    [Header("Settings")]
    [Tooltip("押し戻しの強さ")]
    public float pushBackStrength = 1.0f;
    [Tooltip("貫通判定のマージン距離")]
    public float penetrationMargin = 0.005f;
    [Tooltip("デバッグログを出力")]
    public bool debugLog = false;

    private Interactable interactable;
    private Hand attachedHand;
    private Collider myCollider;
    private Rigidbody rb;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        myCollider = GetComponentInChildren<Collider>();
        rb = GetComponent<Rigidbody>();

        // blockingColliderが未設定の場合、CylinderPolisherまたはMetalSwirlPolisherから取得
        if (!blockingCollider)
        {
            var cylPol = GetComponent<CylinderPolisher>();
            if (cylPol && cylPol.targetCollider)
            {
                blockingCollider = cylPol.targetCollider;
            }
            else
            {
                var metalPol = GetComponent<MetalSwirlPolisher>();
                if (metalPol && metalPol.targetCollider)
                {
                    blockingCollider = metalPol.targetCollider;
                }
            }
        }

        if (!blockingCollider)
        {
            Debug.LogWarning("[CollisionBlocker] blockingCollider が未設定です");
        }
    }

    void Start()
    {
        if (interactable)
        {
            interactable.onAttachedToHand += OnAttachedToHand;
            interactable.onDetachedFromHand += OnDetachedFromHand;
        }
    }

    void OnDestroy()
    {
        if (interactable)
        {
            interactable.onAttachedToHand -= OnAttachedToHand;
            interactable.onDetachedFromHand -= OnDetachedFromHand;
        }
    }

    void OnAttachedToHand(Hand hand)
    {
        attachedHand = hand;
        if (debugLog) Debug.Log($"[CollisionBlocker] Attached to hand: {hand.name}");
    }

    void OnDetachedFromHand(Hand hand)
    {
        attachedHand = null;
        if (debugLog) Debug.Log("[CollisionBlocker] Detached from hand");
    }

    void LateUpdate()
    {
        // 掴んでいるときのみ貫通防止を適用
        if (attachedHand == null || !blockingCollider) return;

        // 自分のコライダーがない場合はバウンディングボックスで判定
        if (!myCollider)
        {
            PreventPenetrationSimple();
            return;
        }

        PreventPenetration();
    }

    void PreventPenetration()
    {
        if (!myCollider || !blockingCollider) return;

        // Physics.ComputePenetration で貫通量を計算
        Vector3 myPos = myCollider.bounds.center;
        Quaternion myRot = transform.rotation;

        // blockingCollider が MeshCollider（非Convex）の場合は簡易判定
        if (blockingCollider is MeshCollider meshCol && !meshCol.convex)
        {
            PreventPenetrationSimple();
            return;
        }

        // ComputePenetration は両方が凸形状でないと動作しない
        // 簡易的な方法: ClosestPoint を使用
        Vector3 closestOnBlocking = blockingCollider.ClosestPoint(myPos);
        Vector3 closestOnMy = myCollider.ClosestPoint(closestOnBlocking);

        float dist = Vector3.Distance(closestOnBlocking, closestOnMy);
        Vector3 dirToBlocking = (closestOnBlocking - myPos).normalized;

        // 自分のコライダーの内側に blockingCollider の最近点がある場合 = 貫通
        bool isPenetrating = blockingCollider.bounds.Contains(closestOnMy) ||
                             Vector3.Dot(closestOnBlocking - closestOnMy, dirToBlocking) < 0;

        if (isPenetrating || dist < penetrationMargin)
        {
            // 金属板の法線方向に押し戻す
            Vector3 planeNormal = blockingCollider.transform.up;
            float penetrationDepth = penetrationMargin - dist + 0.01f;

            // 研磨機の位置が金属板の下にある場合、上に押し戻す
            Vector3 toGrinder = myPos - blockingCollider.bounds.center;
            float side = Vector3.Dot(toGrinder, planeNormal);

            Vector3 pushDir = (side >= 0) ? planeNormal : -planeNormal;
            Vector3 correction = pushDir * penetrationDepth * pushBackStrength;

            transform.position += correction;

            if (debugLog)
            {
                Debug.Log($"[CollisionBlocker] 貫通検出! 補正: {correction.magnitude:F4}m");
            }
        }
    }

    void PreventPenetrationSimple()
    {
        if (!blockingCollider) return;

        // シンプルな平面ベースの貫通防止
        // 金属板の上面を基準に判定
        Vector3 planeCenter = blockingCollider.bounds.center;
        Vector3 planeNormal = blockingCollider.transform.up;
        float planeHalfThickness = blockingCollider.bounds.extents.y;

        // 研磨機の底面の位置を推定
        Vector3 grinderPos = transform.position;
        Bounds grinderBounds = myCollider ? myCollider.bounds : new Bounds(grinderPos, Vector3.one * 0.05f);

        // 研磨機の下方向（localDownDirection があればそれを使用）
        Vector3 grinderDown = -transform.up;
        var cylPol = GetComponent<CylinderPolisher>();
        if (cylPol)
        {
            grinderDown = transform.TransformDirection(cylPol.localDownDirection).normalized;
        }

        // 研磨機の底面の中心
        Vector3 grinderBottom = grinderBounds.center + grinderDown * grinderBounds.extents.y;

        // 金属板の上面からの距離
        Vector3 toGrinderBottom = grinderBottom - planeCenter;
        float distFromPlane = Vector3.Dot(toGrinderBottom, planeNormal);

        // 金属板の上面の位置
        float planeTopY = planeHalfThickness;

        // 研磨機が金属板の上面より下にある場合
        if (distFromPlane < planeTopY + penetrationMargin)
        {
            float penetrationDepth = (planeTopY + penetrationMargin) - distFromPlane;
            Vector3 correction = planeNormal * penetrationDepth * pushBackStrength;

            transform.position += correction;

            if (debugLog)
            {
                Debug.Log($"[CollisionBlocker] 簡易貫通防止: 補正 {penetrationDepth:F4}m");
            }
        }
    }

    // OnTriggerStay でも追加の貫通防止
    void OnTriggerStay(Collider other)
    {
        if (attachedHand == null) return;
        if (other != blockingCollider) return;

        // トリガー接触中も押し戻し
        PreventPenetrationSimple();
    }

    void OnCollisionStay(Collision collision)
    {
        if (attachedHand == null) return;
        if (collision.collider != blockingCollider) return;

        // 接触点から押し戻し方向を計算
        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 correction = contact.normal * penetrationMargin * pushBackStrength;
            transform.position += correction;

            if (debugLog)
            {
                Debug.Log($"[CollisionBlocker] 接触補正: {correction.magnitude:F4}m");
            }
            break; // 最初の接触点のみ使用
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!blockingCollider) return;

        // 金属板の上面を表示
        Gizmos.color = Color.yellow;
        Vector3 planeCenter = blockingCollider.bounds.center;
        Vector3 planeNormal = blockingCollider.transform.up;
        float planeHalfThickness = blockingCollider.bounds.extents.y;

        Vector3 planeTop = planeCenter + planeNormal * planeHalfThickness;
        Gizmos.DrawWireSphere(planeTop, 0.02f);

        // マージンを表示
        Gizmos.color = Color.red;
        Vector3 marginPos = planeTop + planeNormal * penetrationMargin;
        Vector3 size = new Vector3(blockingCollider.bounds.size.x, 0.001f, blockingCollider.bounds.size.z);
        Gizmos.DrawWireCube(marginPos, size);
    }
#endif
}
#endif

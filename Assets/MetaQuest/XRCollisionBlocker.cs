// XRCollisionBlocker.cs
// CollisionBlockerのXR Interaction Toolkit版
// プラットフォーム非依存の貫通防止

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class XRCollisionBlocker : MonoBehaviour
{
    [Header("Blocking Target")]
    [Tooltip("貫通を防ぎたいコライダー（金属板など）")]
    public Collider blockingCollider;

    [Header("Settings")]
    public float pushBackStrength = 1.0f;
    public float penetrationMargin = 0.005f;
    public bool debugLog = false;

    private XRGrabInteractable grabInteractable;
    private bool isGrabbed = false;
    private Collider myCollider;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        myCollider = GetComponentInChildren<Collider>();
        rb = GetComponent<Rigidbody>();

        // blockingColliderが未設定の場合、PolisherのtargetColliderから取得
#if !UNITY_ANDROID
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
#endif

        if (!blockingCollider)
        {
            Debug.LogWarning("[XRCollisionBlocker] blockingCollider が未設定です");
        }
    }

    void Start()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        if (debugLog) Debug.Log("[XRCollisionBlocker] Grabbed");
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        if (debugLog) Debug.Log("[XRCollisionBlocker] Released");
    }

    void LateUpdate()
    {
        if (!isGrabbed || !blockingCollider) return;

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

        if (blockingCollider is MeshCollider meshCol && !meshCol.convex)
        {
            PreventPenetrationSimple();
            return;
        }

        Vector3 myPos = myCollider.bounds.center;
        Vector3 closestOnBlocking = blockingCollider.ClosestPoint(myPos);
        Vector3 closestOnMy = myCollider.ClosestPoint(closestOnBlocking);
        Vector3 dirToBlocking = (closestOnBlocking - myPos).normalized;

        float dist = Vector3.Distance(closestOnBlocking, closestOnMy);
        bool isPenetrating = blockingCollider.bounds.Contains(closestOnMy) ||
                             Vector3.Dot(closestOnBlocking - closestOnMy, dirToBlocking) < 0;

        if (isPenetrating || dist < penetrationMargin)
        {
            Vector3 planeNormal = blockingCollider.transform.up;
            float penetrationDepth = penetrationMargin - dist + 0.01f;

            Vector3 toGrinder = myPos - blockingCollider.bounds.center;
            float side = Vector3.Dot(toGrinder, planeNormal);

            Vector3 pushDir = (side >= 0) ? planeNormal : -planeNormal;
            Vector3 correction = pushDir * penetrationDepth * pushBackStrength;

            transform.position += correction;

            if (debugLog)
                Debug.Log($"[XRCollisionBlocker] 貫通検出! 補正: {correction.magnitude:F4}m");
        }
    }

    void PreventPenetrationSimple()
    {
        if (!blockingCollider) return;

        Vector3 planeCenter = blockingCollider.bounds.center;
        Vector3 planeNormal = blockingCollider.transform.up;
        float planeHalfThickness = blockingCollider.bounds.extents.y;

        Vector3 grinderPos = transform.position;
        Bounds grinderBounds = myCollider ? myCollider.bounds : new Bounds(grinderPos, Vector3.one * 0.05f);

        Vector3 grinderDown = -transform.up;
#if !UNITY_ANDROID
        var cylPol = GetComponent<CylinderPolisher>();
        if (cylPol)
            grinderDown = transform.TransformDirection(cylPol.localDownDirection).normalized;
#endif

        Vector3 grinderBottom = grinderBounds.center + grinderDown * grinderBounds.extents.y;
        Vector3 toGrinderBottom = grinderBottom - planeCenter;
        float distFromPlane = Vector3.Dot(toGrinderBottom, planeNormal);
        float planeTopY = planeHalfThickness;

        if (distFromPlane < planeTopY + penetrationMargin)
        {
            float penetrationDepth = (planeTopY + penetrationMargin) - distFromPlane;
            Vector3 correction = planeNormal * penetrationDepth * pushBackStrength;
            transform.position += correction;

            if (debugLog)
                Debug.Log($"[XRCollisionBlocker] 簡易貫通防止: 補正 {penetrationDepth:F4}m");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!isGrabbed) return;
        if (other != blockingCollider) return;
        PreventPenetrationSimple();
    }

    void OnCollisionStay(Collision collision)
    {
        if (!isGrabbed) return;
        if (collision.collider != blockingCollider) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 correction = contact.normal * penetrationMargin * pushBackStrength;
            transform.position += correction;
            if (debugLog)
                Debug.Log($"[XRCollisionBlocker] 接触補正: {correction.magnitude:F4}m");
            break;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!blockingCollider) return;

        Gizmos.color = Color.yellow;
        Vector3 planeCenter = blockingCollider.bounds.center;
        Vector3 planeNormal = blockingCollider.transform.up;
        float planeHalfThickness = blockingCollider.bounds.extents.y;

        Vector3 planeTop = planeCenter + planeNormal * planeHalfThickness;
        Gizmos.DrawWireSphere(planeTop, 0.02f);

        Gizmos.color = Color.red;
        Vector3 marginPos = planeTop + planeNormal * penetrationMargin;
        Vector3 size = new Vector3(blockingCollider.bounds.size.x, 0.001f, blockingCollider.bounds.size.z);
        Gizmos.DrawWireCube(marginPos, size);
    }
#endif
}

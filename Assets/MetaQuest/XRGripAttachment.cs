// XRGripAttachment.cs
// KenmaGripAttachmentのXR Interaction Toolkit版
// Meta Quest対応 - XRコントローラーへの固定

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Kenma（研磨対象）を手持ち位置（Temochi）に固定するスクリプト（XRI版）
/// </summary>
public class XRGripAttachment : MonoBehaviour
{
    [Header("手持ち位置設定")]
    [Tooltip("Kenmaを固定する位置（Temochi）のTransform")]
    public Transform gripPoint;

    [Tooltip("グリップポイントが未設定の場合、名前で自動検索")]
    public string gripPointName = "Temochi";

    [Header("オフセット調整")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("固定方法")]
    public AttachMode attachMode = AttachMode.Parent;

    public enum AttachMode
    {
        Parent,
        FollowUpdate,
        FixedJoint,
        Constraint
    }

    [Header("XR設定")]
    [Tooltip("XRコントローラーに追従する場合")]
    public bool followXRController = false;

    [Tooltip("追従するXRコントローラーのTransform（自動検出も可）")]
    public Transform xrControllerTransform;

    [Tooltip("Left/Rightの指定（自動検出用）")]
    public XRControllerHand preferredHand = XRControllerHand.Right;

    public enum XRControllerHand { Left, Right }

    private Rigidbody rb;
    private FixedJoint fixedJoint;
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // グリップポイントの自動検索
        if (gripPoint == null && !string.IsNullOrEmpty(gripPointName))
        {
            GameObject found = GameObject.Find(gripPointName);
            if (found != null)
            {
                gripPoint = found.transform;
                Debug.Log($"[XRGripAttachment] グリップポイント '{gripPointName}' を自動検出");
            }
        }

        // XRコントローラーの自動検索
        if (followXRController && xrControllerTransform == null)
        {
            FindXRController();
        }

        if (gripPoint == null && !followXRController)
        {
            Debug.LogWarning("[XRGripAttachment] グリップポイントが設定されていません");
            return;
        }

        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        SetupAttachment();
    }

    void FindXRController()
    {
        // XR Origin配下のコントローラーを検索
        var controllers = FindObjectsByType<ActionBasedController>(FindObjectsSortMode.None);
        foreach (var ctrl in controllers)
        {
            string name = ctrl.gameObject.name.ToLower();
            bool isLeft = name.Contains("left");
            bool isRight = name.Contains("right");

            if ((preferredHand == XRControllerHand.Left && isLeft) ||
                (preferredHand == XRControllerHand.Right && isRight))
            {
                xrControllerTransform = ctrl.transform;
                Debug.Log($"[XRGripAttachment] XRコントローラー自動検出: {ctrl.name}");
                return;
            }
        }

        // 見つからない場合は最初のコントローラーを使用
        if (controllers.Length > 0)
        {
            xrControllerTransform = controllers[0].transform;
            Debug.Log($"[XRGripAttachment] XRコントローラー (フォールバック): {controllers[0].name}");
        }
    }

    void SetupAttachment()
    {
        Transform target = GetTargetTransform();
        if (target == null) return;

        switch (attachMode)
        {
            case AttachMode.Parent:
                SetupParentAttachment(target);
                break;
            case AttachMode.FixedJoint:
                SetupFixedJoint(target);
                break;
            case AttachMode.Constraint:
                SetupConstraint();
                break;
            case AttachMode.FollowUpdate:
                break;
        }

        Debug.Log($"[XRGripAttachment] {attachMode} モードでアタッチ完了");
    }

    Transform GetTargetTransform()
    {
        if (followXRController && xrControllerTransform != null)
            return xrControllerTransform;
        return gripPoint;
    }

    void SetupParentAttachment(Transform target)
    {
        transform.SetParent(target);
        transform.localPosition = positionOffset;
        transform.localRotation = Quaternion.Euler(rotationOffset);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void SetupFixedJoint(Transform target)
    {
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;

        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb == null)
        {
            targetRb = target.gameObject.AddComponent<Rigidbody>();
            targetRb.isKinematic = true;
        }

        transform.position = target.TransformPoint(positionOffset);
        transform.rotation = target.rotation * Quaternion.Euler(rotationOffset);

        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = targetRb;
        fixedJoint.enableCollision = false;
    }

    void SetupConstraint()
    {
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        if (attachMode == AttachMode.FollowUpdate || attachMode == AttachMode.Constraint)
        {
            FollowTarget();
        }
    }

    void FollowTarget()
    {
        Transform target = GetTargetTransform();
        if (target == null) return;

        Vector3 targetPos = target.TransformPoint(positionOffset);
        Quaternion targetRot = target.rotation * Quaternion.Euler(rotationOffset);

        if (attachMode == AttachMode.Constraint && rb != null)
        {
            rb.MovePosition(targetPos);
            rb.MoveRotation(targetRot);
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }

    public void SetGripPoint(Transform newGripPoint)
    {
        gripPoint = newGripPoint;

        if (attachMode == AttachMode.Parent)
        {
            transform.SetParent(newGripPoint);
            transform.localPosition = positionOffset;
            transform.localRotation = Quaternion.Euler(rotationOffset);
        }
        else if (attachMode == AttachMode.FixedJoint && fixedJoint != null)
        {
            Destroy(fixedJoint);
            SetupFixedJoint(newGripPoint);
        }
    }

    public void Detach()
    {
        if (attachMode == AttachMode.Parent)
            transform.SetParent(null);
        else if (fixedJoint != null)
            Destroy(fixedJoint);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        Debug.Log("[XRGripAttachment] デタッチ完了");
    }

    public void ResetPosition()
    {
        transform.localPosition = initialLocalPos;
        transform.localRotation = initialLocalRot;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (gripPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gripPoint.position, 0.02f);
            Gizmos.DrawLine(transform.position, gripPoint.position);

            Vector3 attachPos = gripPoint.TransformPoint(positionOffset);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(attachPos, 0.015f);
        }
    }
#endif
}

#if !UNITY_ANDROID
using UnityEngine;
using Valve.VR.InteractionSystem;

/// <summary>
/// Kenma（研磨対象）を手持ち位置（Temochi）に固定するスクリプト
///
/// 使い方:
/// 1. このスクリプトをKenmaオブジェクトにアタッチ
/// 2. Inspectorで「手持ち位置」にTemochi用のTransformを設定
/// 3. Play時に自動で位置が固定される
/// </summary>
public class KenmaGripAttachment : MonoBehaviour
{
    [Header("手持ち位置設定")]
    [Tooltip("Kenmaを固定する位置（Temochi）のTransform")]
    public Transform gripPoint;

    [Tooltip("グリップポイントが未設定の場合、名前で自動検索")]
    public string gripPointName = "Temochi";

    [Header("オフセット調整")]
    [Tooltip("位置オフセット（ローカル座標）")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("回転オフセット（オイラー角）")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("固定方法")]
    public AttachMode attachMode = AttachMode.Parent;

    public enum AttachMode
    {
        Parent,         // 親子関係で固定（推奨）
        FollowUpdate,   // Updateで追従
        FixedJoint,     // FixedJointで物理的に固定
        Constraint      // 位置制約（Rigidbody使用）
    }

    [Header("VR設定")]
    [Tooltip("VRコントローラーに追従する場合")]
    public bool followVRController = false;

    [Tooltip("追従するHand (Left/Right)")]
    public Hand vrHand;

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
                Debug.Log($"[KenmaGrip] グリップポイント '{gripPointName}' を自動検出");
            }
        }

        // VRハンドの自動検索
        if (followVRController && vrHand == null)
        {
            vrHand = FindObjectOfType<Hand>();
            if (vrHand != null)
            {
                Debug.Log($"[KenmaGrip] VR Hand を自動検出: {vrHand.name}");
            }
        }

        if (gripPoint == null && !followVRController)
        {
            Debug.LogWarning("[KenmaGrip] グリップポイントが設定されていません");
            return;
        }

        // 初期位置を記録
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // アタッチモードに応じて設定
        SetupAttachment();
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
                SetupConstraint(target);
                break;

            case AttachMode.FollowUpdate:
                // Update()で処理
                break;
        }

        Debug.Log($"[KenmaGrip] {attachMode} モードでアタッチ完了");
    }

    Transform GetTargetTransform()
    {
        if (followVRController && vrHand != null)
        {
            return vrHand.transform;
        }
        return gripPoint;
    }

    void SetupParentAttachment(Transform target)
    {
        // 親子関係を設定
        transform.SetParent(target);

        // オフセット適用
        transform.localPosition = positionOffset;
        transform.localRotation = Quaternion.Euler(rotationOffset);

        // Rigidbodyがある場合はKinematicに
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void SetupFixedJoint(Transform target)
    {
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = false;

        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb == null)
        {
            targetRb = target.gameObject.AddComponent<Rigidbody>();
            targetRb.isKinematic = true;
        }

        // 初期位置を設定
        transform.position = target.TransformPoint(positionOffset);
        transform.rotation = target.rotation * Quaternion.Euler(rotationOffset);

        // FixedJointで接続
        fixedJoint = gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = targetRb;
        fixedJoint.enableCollision = false;
    }

    void SetupConstraint(Transform target)
    {
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
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

    /// <summary>
    /// 手持ち位置を動的に変更
    /// </summary>
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

    /// <summary>
    /// アタッチを解除
    /// </summary>
    public void Detach()
    {
        if (attachMode == AttachMode.Parent)
        {
            transform.SetParent(null);
        }
        else if (fixedJoint != null)
        {
            Destroy(fixedJoint);
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Debug.Log("[KenmaGrip] デタッチ完了");
    }

    /// <summary>
    /// 初期位置にリセット
    /// </summary>
    public void ResetPosition()
    {
        transform.localPosition = initialLocalPos;
        transform.localRotation = initialLocalRot;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // グリップポイントを可視化
        if (gripPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gripPoint.position, 0.02f);
            Gizmos.DrawLine(transform.position, gripPoint.position);

            // オフセット後の位置
            Vector3 attachPos = gripPoint.TransformPoint(positionOffset);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(attachPos, 0.015f);
        }
    }
#endif
}
#endif

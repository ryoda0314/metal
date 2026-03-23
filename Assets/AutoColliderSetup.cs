using UnityEngine;

/// <summary>
/// このスクリプトをアタッチすると、Start時に自動で当たり判定を追加します。
/// Kenma_Model のモデル用。
/// </summary>
public class AutoColliderSetup : MonoBehaviour
{
    public enum ColliderType
    {
        Auto,           // MeshFilterがあればMesh、なければBox
        MeshConvex,     // MeshCollider (Convex ON) - 動く物体用
        MeshNonConvex,  // MeshCollider (Convex OFF) - 静的物体用
        Box,            // BoxCollider
        Sphere,         // SphereCollider
        Capsule         // CapsuleCollider
    }

    [Header("コライダー設定")]
    [Tooltip("追加するコライダーの種類")]
    public ColliderType colliderType = ColliderType.Auto;

    [Tooltip("子オブジェクトにも再帰的に追加")]
    public bool includeChildren = true;

    [Header("Rigidbody設定")]
    [Tooltip("Rigidbodyを追加するか")]
    public bool addRigidbody = true;

    [Tooltip("Kinematic（VR手動操作用）")]
    public bool isKinematic = true;

    [Tooltip("重力を使用")]
    public bool useGravity = false;

    [Header("Sphereコライダー用")]
    public float sphereRadius = 0.05f;

    [Header("Capsuleコライダー用")]
    public float capsuleRadius = 0.05f;
    public float capsuleHeight = 0.2f;

    void Start()
    {
        SetupColliders(gameObject);
        Debug.Log($"[AutoColliderSetup] '{gameObject.name}' のコライダーセットアップ完了");
    }

    void SetupColliders(GameObject target)
    {
        // 既存のコライダーがなければ追加
        if (target.GetComponent<Collider>() == null)
        {
            AddCollider(target);
        }

        // 子オブジェクトも処理
        if (includeChildren)
        {
            foreach (Transform child in target.transform)
            {
                SetupColliders(child.gameObject);
            }
        }

        // Rigidbody追加（ルートのみ）
        if (addRigidbody && target == gameObject)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = target.AddComponent<Rigidbody>();
            }
            rb.isKinematic = isKinematic;
            rb.useGravity = useGravity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    void AddCollider(GameObject target)
    {
        MeshFilter mf = target.GetComponent<MeshFilter>();

        switch (colliderType)
        {
            case ColliderType.Auto:
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = target.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }
                else
                {
                    target.AddComponent<BoxCollider>();
                }
                break;

            case ColliderType.MeshConvex:
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = target.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
                break;

            case ColliderType.MeshNonConvex:
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = target.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }
                break;

            case ColliderType.Box:
                target.AddComponent<BoxCollider>();
                break;

            case ColliderType.Sphere:
                SphereCollider sc = target.AddComponent<SphereCollider>();
                sc.radius = sphereRadius;
                break;

            case ColliderType.Capsule:
                CapsuleCollider cc = target.AddComponent<CapsuleCollider>();
                cc.radius = capsuleRadius;
                cc.height = capsuleHeight;
                break;
        }
    }

    /// <summary>
    /// エディタ上でコライダーの範囲を可視化
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is MeshCollider mesh && mesh.sharedMesh != null)
            {
                Gizmos.DrawWireMesh(mesh.sharedMesh);
            }
        }
    }
}

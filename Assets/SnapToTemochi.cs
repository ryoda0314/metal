using UnityEngine;

/// <summary>
/// Kenmaをtemochiの位置に固定するシンプルなスクリプト
///
/// 使い方:
/// 1. Kenmaオブジェクトにこのスクリプトをアタッチ
/// 2. Inspectorで「temochi」オブジェクトをドラッグ
/// 3. Play → 自動で固定される
/// </summary>
public class SnapToTemochi : MonoBehaviour
{
    [Header("固定先")]
    [Tooltip("temochiオブジェクトをここにドラッグ")]
    public Transform temochi;

    [Header("自動検索")]
    [Tooltip("temochiが未設定の場合、名前で自動検索する")]
    public bool autoFindTemochi = true;
    public string temochiName = "temochi";

    [Header("オフセット")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("固定モード")]
    public bool parentToTemochi = true;  // 親子関係で固定
    public bool keepUpdating = false;    // 毎フレーム更新（親子関係でない場合）

    void Start()
    {
        // temochiを自動検索
        if (temochi == null && autoFindTemochi)
        {
            GameObject found = GameObject.Find(temochiName);
            if (found != null)
            {
                temochi = found.transform;
                Debug.Log($"[SnapToTemochi] '{temochiName}' を自動検出しました");
            }
            else
            {
                Debug.LogWarning($"[SnapToTemochi] '{temochiName}' が見つかりません");
                return;
            }
        }

        if (temochi == null)
        {
            Debug.LogError("[SnapToTemochi] temochiが設定されていません");
            return;
        }

        // 固定実行
        SnapToPosition();
    }

    void Update()
    {
        // 親子関係でない場合、毎フレーム追従
        if (keepUpdating && !parentToTemochi && temochi != null)
        {
            SnapToPosition();
        }
    }

    /// <summary>
    /// temochiの位置に固定
    /// </summary>
    [ContextMenu("Snap To Temochi")]
    public void SnapToPosition()
    {
        if (temochi == null) return;

        if (parentToTemochi)
        {
            // 親子関係で固定
            transform.SetParent(temochi);
            transform.localPosition = positionOffset;
            transform.localRotation = Quaternion.Euler(rotationOffset);
        }
        else
        {
            // 位置を直接設定
            transform.position = temochi.TransformPoint(positionOffset);
            transform.rotation = temochi.rotation * Quaternion.Euler(rotationOffset);
        }

        // Rigidbodyがあれば停止
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Debug.Log($"[SnapToTemochi] '{gameObject.name}' を '{temochi.name}' に固定しました");
    }

    /// <summary>
    /// 固定を解除
    /// </summary>
    [ContextMenu("Detach")]
    public void Detach()
    {
        transform.SetParent(null);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        Debug.Log($"[SnapToTemochi] '{gameObject.name}' の固定を解除しました");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (temochi != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, temochi.position);
            Gizmos.DrawWireSphere(temochi.position, 0.02f);
        }
    }
#endif
}

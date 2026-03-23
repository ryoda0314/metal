using UnityEngine;

/// <summary>
/// Temochiポイント（手持ち位置）の可視化用コンポーネント
/// 空のGameObjectにアタッチして、位置を確認しやすくします
/// </summary>
public class TemochiGizmo : MonoBehaviour
{
    [Header("Gizmo表示設定")]
    public Color gizmoColor = Color.yellow;
    public float gizmoSize = 0.03f;
    public bool showAxes = true;

    [Header("スナップ設定")]
    [Tooltip("子オブジェクトをこの位置にスナップする")]
    public bool snapChildOnStart = false;

    void Start()
    {
        if (snapChildOnStart && transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
            }
        }
    }

    void OnDrawGizmos()
    {
        // 球体で位置を表示
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSize);

        // 軸方向を表示
        if (showAxes)
        {
            float axisLength = gizmoSize * 3f;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.right * axisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up * axisLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * axisLength);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 選択時はより大きく表示
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.DrawSphere(transform.position, gizmoSize * 1.5f);

        // ラベル表示
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * gizmoSize * 2, "Temochi");
#endif
    }
}

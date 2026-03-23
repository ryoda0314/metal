using UnityEngine;
using UnityEditor;

/// <summary>
/// ポリッシャーモデル専用の精密コライダー設定
/// 右クリックメニューから即座に実行可能
/// </summary>
public class PolisherColliderFix
{
    [MenuItem("GameObject/Kenma/ポリッシャー用コライダー (精密)", false, 0)]
    static void SetupPolisherPrecise()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトを選択してください", "OK");
            return;
        }

        Undo.RecordObject(go, "Setup Polisher Precise Colliders");

        // 既存コライダー削除
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            Undo.DestroyObjectImmediate(c);
        }

        int count = 0;

        // 各子メッシュにConvex MeshColliderを追加
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true; // VR操作用にConvex必須
            count++;
        }

        // SkinnedMeshRendererも処理
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;

            MeshCollider mc = Undo.AddComponent<MeshCollider>(smr.gameObject);
            mc.sharedMesh = smr.sharedMesh;
            mc.convex = true;
            count++;
        }

        // Rigidbody設定
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        EditorUtility.SetDirty(go);
        Debug.Log($"[PolisherFix] {count}個のConvex MeshCollider生成完了");
    }

    [MenuItem("GameObject/Kenma/金属板用コライダー (非Convex)", false, 1)]
    static void SetupMetalPlatePrecise()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトを選択してください", "OK");
            return;
        }

        Undo.RecordObject(go, "Setup Metal Plate Colliders");

        // 既存コライダー削除
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            Undo.DestroyObjectImmediate(c);
        }

        int count = 0;

        // MeshCollider (非Convex - 静的物体用)
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false; // 静的なので非Convex可能
            count++;
        }

        // Rigidbody (静的)
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(go);
        Debug.Log($"[MetalPlate] {count}個のMeshCollider生成完了");
    }

    [MenuItem("GameObject/Kenma/全コライダー削除", false, 20)]
    static void RemoveAllColliders()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return;

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            Undo.DestroyObjectImmediate(c);
        }
        Debug.Log($"[Remove] {cols.Length}個のコライダー削除");
    }
}

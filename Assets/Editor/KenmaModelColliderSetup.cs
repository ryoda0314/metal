using UnityEngine;
using UnityEditor;

/// <summary>
/// Kenma_Model フォルダのモデルに物理的な当たり判定を追加するエディタツール
/// メニュー: Tools > Kenma Model > Add Colliders
/// </summary>
public class KenmaModelColliderSetup : EditorWindow
{
    [MenuItem("Tools/Kenma Model/Add Colliders to Selected")]
    static void AddCollidersToSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Kenma Collider Setup",
                "シーン内のオブジェクトを選択してください。", "OK");
            return;
        }

        int count = 0;
        foreach (var go in selected)
        {
            count += AddCollidersRecursive(go);
        }

        EditorUtility.DisplayDialog("Kenma Collider Setup",
            $"{count} 個のコライダーを追加しました。", "OK");
    }

    [MenuItem("Tools/Kenma Model/Setup Polisher (物理あり)")]
    static void SetupPolisher()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Polisher Setup",
                "ポリッシャーオブジェクトを選択してください。", "OK");
            return;
        }

        Undo.RecordObject(selected, "Setup Polisher Collider");

        // MeshCollider を追加（Convex ON - 動く物体なので）
        MeshFilter mf = selected.GetComponent<MeshFilter>();
        if (mf == null) mf = selected.GetComponentInChildren<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            MeshCollider mc = selected.GetComponent<MeshCollider>();
            if (mc == null) mc = Undo.AddComponent<MeshCollider>(selected);

            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true; // 動く物体はConvex必須
            Debug.Log($"[Polisher] MeshCollider (Convex) 追加: {selected.name}");
        }
        else
        {
            // MeshFilterがない場合はSphereCollider
            SphereCollider sc = selected.GetComponent<SphereCollider>();
            if (sc == null) sc = Undo.AddComponent<SphereCollider>(selected);
            sc.radius = 0.05f;
            Debug.Log($"[Polisher] SphereCollider 追加: {selected.name}");
        }

        // Rigidbody 追加
        Rigidbody rb = selected.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(selected);

        rb.isKinematic = true;  // VRで手動移動
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        EditorUtility.SetDirty(selected);
        Debug.Log($"[Polisher] セットアップ完了: {selected.name}");
        EditorUtility.DisplayDialog("Polisher Setup",
            $"ポリッシャー '{selected.name}' のセットアップ完了！\n- MeshCollider (Convex)\n- Rigidbody (Kinematic)", "OK");
    }

    [MenuItem("Tools/Kenma Model/Setup Kenma Metal (静的)")]
    static void SetupKenmaMetal()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Kenma Setup",
                "研磨対象（金属板）オブジェクトを選択してください。", "OK");
            return;
        }

        Undo.RecordObject(selected, "Setup Kenma Metal Collider");

        // MeshCollider 追加（Convex OFF - 静的で凹面対応）
        MeshFilter mf = selected.GetComponent<MeshFilter>();
        if (mf == null) mf = selected.GetComponentInChildren<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            MeshCollider mc = selected.GetComponent<MeshCollider>();
            if (mc == null) mc = Undo.AddComponent<MeshCollider>(selected);

            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false; // 静的オブジェクトはConvex不要
            Debug.Log($"[Kenma] MeshCollider 追加: {selected.name}");
        }
        else
        {
            // MeshFilterがない場合はBoxCollider
            BoxCollider bc = selected.GetComponent<BoxCollider>();
            if (bc == null) bc = Undo.AddComponent<BoxCollider>(selected);
            Debug.Log($"[Kenma] BoxCollider 追加: {selected.name}");
        }

        // Rigidbody 追加（静的）
        Rigidbody rb = selected.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(selected);

        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(selected);
        Debug.Log($"[Kenma] セットアップ完了: {selected.name}");
        EditorUtility.DisplayDialog("Kenma Setup",
            $"金属板 '{selected.name}' のセットアップ完了！\n- MeshCollider (非Convex)\n- Rigidbody (Static)", "OK");
    }

    [MenuItem("Tools/Kenma Model/Setup Hand")]
    static void SetupHand()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Hand Setup",
                "手のオブジェクトを選択してください。", "OK");
            return;
        }

        Undo.RecordObject(selected, "Setup Hand Collider");

        // 手には複数のSphereCollider/CapsuleColliderを追加
        // 簡易版: 子オブジェクトにSphereColliderを追加

        int added = 0;
        foreach (Transform child in selected.GetComponentsInChildren<Transform>())
        {
            // ボーンや関節にコライダーを追加
            if (child.name.ToLower().Contains("finger") ||
                child.name.ToLower().Contains("hand") ||
                child.name.ToLower().Contains("palm") ||
                child.name.ToLower().Contains("thumb") ||
                child.name.ToLower().Contains("index") ||
                child.name.ToLower().Contains("middle") ||
                child.name.ToLower().Contains("ring") ||
                child.name.ToLower().Contains("pinky") ||
                child.name.ToLower().Contains("wrist"))
            {
                if (child.GetComponent<Collider>() == null)
                {
                    SphereCollider sc = Undo.AddComponent<SphereCollider>(child.gameObject);
                    sc.radius = 0.01f; // 1cm
                    sc.isTrigger = true; // トリガーとして使用
                    added++;
                }
            }
        }

        // ルートにRigidbody
        Rigidbody rb = selected.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(selected);
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(selected);
        Debug.Log($"[Hand] セットアップ完了: {selected.name}, {added}個のコライダー追加");
        EditorUtility.DisplayDialog("Hand Setup",
            $"手 '{selected.name}' のセットアップ完了！\n- {added}個のSphereCollider\n- Rigidbody (Kinematic)", "OK");
    }

    static int AddCollidersRecursive(GameObject go)
    {
        int count = 0;

        // MeshFilterがあればMeshCollider追加
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            if (go.GetComponent<Collider>() == null)
            {
                MeshCollider mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                count++;
                Debug.Log($"[Auto] MeshCollider 追加: {go.name}");
            }
        }

        // 子オブジェクトも処理
        foreach (Transform child in go.transform)
        {
            count += AddCollidersRecursive(child.gameObject);
        }

        return count;
    }
}

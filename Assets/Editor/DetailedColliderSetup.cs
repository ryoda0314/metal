using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// モデルの形状に合わせて詳細な当たり判定を自動生成するエディタツール
/// </summary>
public class DetailedColliderSetup : EditorWindow
{
    private GameObject targetObject;
    private bool useConvexDecomposition = true;
    private int maxCollidersPerMesh = 8;
    private float colliderPadding = 0.001f;
    private bool addToChildren = true;
    private bool createCompoundCollider = true;
    private float minPartSize = 0.01f;

    [MenuItem("Tools/Kenma Model/詳細コライダー設定ウィンドウ")]
    static void ShowWindow()
    {
        GetWindow<DetailedColliderSetup>("詳細コライダー設定");
    }

    void OnGUI()
    {
        GUILayout.Label("詳細コライダー自動生成", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("対象オブジェクト", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("設定", EditorStyles.boldLabel);

        addToChildren = EditorGUILayout.Toggle("子オブジェクトにも追加", addToChildren);
        createCompoundCollider = EditorGUILayout.Toggle("複合コライダー生成", createCompoundCollider);
        useConvexDecomposition = EditorGUILayout.Toggle("凸分割を使用", useConvexDecomposition);
        maxCollidersPerMesh = EditorGUILayout.IntSlider("最大コライダー数/メッシュ", maxCollidersPerMesh, 1, 16);
        colliderPadding = EditorGUILayout.Slider("パディング", colliderPadding, 0f, 0.01f);
        minPartSize = EditorGUILayout.Slider("最小パーツサイズ", minPartSize, 0.001f, 0.1f);

        EditorGUILayout.Space();

        if (GUILayout.Button("既存コライダーを削除", GUILayout.Height(25)))
        {
            if (targetObject != null)
            {
                RemoveAllColliders(targetObject);
            }
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("詳細コライダーを生成", GUILayout.Height(40)))
        {
            if (targetObject != null)
            {
                GenerateDetailedColliders(targetObject);
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", "対象オブジェクトを選択してください", "OK");
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUILayout.Label("プリセット", EditorStyles.boldLabel);

        if (GUILayout.Button("ポリッシャー用プリセット"))
        {
            if (targetObject != null) SetupPolisherDetailed(targetObject);
        }

        if (GUILayout.Button("手モデル用プリセット"))
        {
            if (targetObject != null) SetupHandDetailed(targetObject);
        }

        if (GUILayout.Button("金属板用プリセット"))
        {
            if (targetObject != null) SetupMetalPlateDetailed(targetObject);
        }
    }

    void RemoveAllColliders(GameObject root)
    {
        Undo.RecordObject(root, "Remove All Colliders");

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        int count = colliders.Length;

        foreach (var col in colliders)
        {
            Undo.DestroyObjectImmediate(col);
        }

        Debug.Log($"[DetailedCollider] {count}個のコライダーを削除しました");
    }

    void GenerateDetailedColliders(GameObject root)
    {
        Undo.RecordObject(root, "Generate Detailed Colliders");

        int totalColliders = 0;

        if (addToChildren)
        {
            // 全ての子オブジェクトを処理
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                totalColliders += ProcessGameObject(child.gameObject);
            }
        }
        else
        {
            totalColliders = ProcessGameObject(root);
        }

        // Rigidbodyをルートに追加
        if (root.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = Undo.AddComponent<Rigidbody>(root);
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        EditorUtility.SetDirty(root);
        Debug.Log($"[DetailedCollider] 合計 {totalColliders} 個のコライダーを生成しました");
        EditorUtility.DisplayDialog("完了", $"{totalColliders} 個のコライダーを生成しました", "OK");
    }

    int ProcessGameObject(GameObject go)
    {
        int count = 0;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();

        Mesh mesh = null;
        if (mf != null) mesh = mf.sharedMesh;
        else if (smr != null) mesh = smr.sharedMesh;

        if (mesh == null) return 0;

        // メッシュのバウンディングボックスが小さすぎる場合はスキップ
        if (mesh.bounds.size.magnitude < minPartSize) return 0;

        if (createCompoundCollider && useConvexDecomposition)
        {
            // メッシュを分割して複数のコライダーを生成
            count = CreateConvexDecompositionColliders(go, mesh);
        }
        else
        {
            // 単一のコライダーを最適な形状で生成
            count = CreateOptimalCollider(go, mesh);
        }

        return count;
    }

    int CreateConvexDecompositionColliders(GameObject go, Mesh mesh)
    {
        int count = 0;
        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;

        // メッシュの形状を分析
        float aspectXY = size.x / Mathf.Max(size.y, 0.001f);
        float aspectXZ = size.x / Mathf.Max(size.z, 0.001f);
        float aspectYZ = size.y / Mathf.Max(size.z, 0.001f);

        // 細長い形状の場合は複数のカプセル/ボックスで近似
        if (aspectXY > 3f || aspectXZ > 3f || aspectYZ > 3f)
        {
            count = CreateElongatedColliders(go, mesh, bounds);
        }
        // 平らな形状の場合
        else if (aspectXY < 0.3f || aspectXZ < 0.3f || aspectYZ < 0.3f)
        {
            count = CreateFlatColliders(go, mesh, bounds);
        }
        // それ以外は凸包分割
        else
        {
            count = CreateSubdividedConvexColliders(go, mesh, bounds);
        }

        return count;
    }

    int CreateElongatedColliders(GameObject go, Mesh mesh, Bounds bounds)
    {
        int count = 0;
        Vector3 size = bounds.size;

        // 最も長い軸を特定
        int longestAxis = 0;
        float maxSize = size.x;
        if (size.y > maxSize) { longestAxis = 1; maxSize = size.y; }
        if (size.z > maxSize) { longestAxis = 2; maxSize = size.z; }

        // 長い軸に沿って分割
        int segments = Mathf.Min(maxCollidersPerMesh, Mathf.CeilToInt(maxSize / 0.03f));
        segments = Mathf.Max(2, segments);

        for (int i = 0; i < segments; i++)
        {
            float t = (i + 0.5f) / segments;

            Vector3 localPos = bounds.center;
            Vector3 localSize = size;

            switch (longestAxis)
            {
                case 0: // X軸が最長
                    localPos.x = bounds.min.x + size.x * t;
                    localSize.x = size.x / segments + colliderPadding * 2;
                    break;
                case 1: // Y軸が最長
                    localPos.y = bounds.min.y + size.y * t;
                    localSize.y = size.y / segments + colliderPadding * 2;
                    break;
                case 2: // Z軸が最長
                    localPos.z = bounds.min.z + size.z * t;
                    localSize.z = size.z / segments + colliderPadding * 2;
                    break;
            }

            // サイズに応じてカプセルかボックスを選択
            float minDim = Mathf.Min(localSize.x, Mathf.Min(localSize.y, localSize.z));
            float maxDim = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z));

            if (maxDim / minDim > 2f)
            {
                CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(go);
                cc.center = localPos;
                cc.radius = minDim * 0.5f;
                cc.height = maxDim;
                cc.direction = longestAxis;
            }
            else
            {
                BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                bc.center = localPos;
                bc.size = localSize;
            }
            count++;
        }

        return count;
    }

    int CreateFlatColliders(GameObject go, Mesh mesh, Bounds bounds)
    {
        int count = 0;
        Vector3 size = bounds.size;

        // 最も薄い軸を特定
        int thinnestAxis = 0;
        float minSize = size.x;
        if (size.y < minSize) { thinnestAxis = 1; minSize = size.y; }
        if (size.z < minSize) { thinnestAxis = 2; minSize = size.z; }

        // グリッド状に分割
        int gridSize = Mathf.Min(4, Mathf.CeilToInt(Mathf.Sqrt(maxCollidersPerMesh)));

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                float ti = (i + 0.5f) / gridSize;
                float tj = (j + 0.5f) / gridSize;

                Vector3 localPos = bounds.center;
                Vector3 localSize = size;

                switch (thinnestAxis)
                {
                    case 0: // X軸が最薄（YZ平面）
                        localPos.y = bounds.min.y + size.y * ti;
                        localPos.z = bounds.min.z + size.z * tj;
                        localSize.y = size.y / gridSize + colliderPadding;
                        localSize.z = size.z / gridSize + colliderPadding;
                        break;
                    case 1: // Y軸が最薄（XZ平面）
                        localPos.x = bounds.min.x + size.x * ti;
                        localPos.z = bounds.min.z + size.z * tj;
                        localSize.x = size.x / gridSize + colliderPadding;
                        localSize.z = size.z / gridSize + colliderPadding;
                        break;
                    case 2: // Z軸が最薄（XY平面）
                        localPos.x = bounds.min.x + size.x * ti;
                        localPos.y = bounds.min.y + size.y * tj;
                        localSize.x = size.x / gridSize + colliderPadding;
                        localSize.y = size.y / gridSize + colliderPadding;
                        break;
                }

                BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                bc.center = localPos;
                bc.size = localSize;
                count++;
            }
        }

        return count;
    }

    int CreateSubdividedConvexColliders(GameObject go, Mesh mesh, Bounds bounds)
    {
        int count = 0;

        // 8分割（オクツリー的に）
        int divisions = Mathf.Min(2, Mathf.CeilToInt(Mathf.Pow(maxCollidersPerMesh, 1f / 3f)));

        Vector3 size = bounds.size;
        Vector3 cellSize = size / divisions;

        for (int x = 0; x < divisions; x++)
        {
            for (int y = 0; y < divisions; y++)
            {
                for (int z = 0; z < divisions; z++)
                {
                    Vector3 localPos = bounds.min + new Vector3(
                        cellSize.x * (x + 0.5f),
                        cellSize.y * (y + 0.5f),
                        cellSize.z * (z + 0.5f)
                    );

                    // その領域にメッシュの頂点があるかチェック
                    Bounds cellBounds = new Bounds(localPos, cellSize);
                    bool hasVertices = false;

                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        if (cellBounds.Contains(vertex))
                        {
                            hasVertices = true;
                            break;
                        }
                    }

                    if (hasVertices)
                    {
                        BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                        bc.center = localPos;
                        bc.size = cellSize + Vector3.one * colliderPadding;
                        count++;
                    }
                }
            }
        }

        // コライダーが生成されなかった場合は単一のBoxColliderを追加
        if (count == 0)
        {
            BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
            bc.center = bounds.center;
            bc.size = bounds.size;
            count = 1;
        }

        return count;
    }

    int CreateOptimalCollider(GameObject go, Mesh mesh)
    {
        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;

        // 形状分析
        float volume = size.x * size.y * size.z;
        float sphereRadius = bounds.extents.magnitude;
        float sphereVolume = (4f / 3f) * Mathf.PI * Mathf.Pow(sphereRadius, 3);

        // 球に近い形状か
        if (volume / sphereVolume > 0.5f)
        {
            SphereCollider sc = Undo.AddComponent<SphereCollider>(go);
            sc.center = bounds.center;
            sc.radius = sphereRadius * 0.8f;
            return 1;
        }

        // カプセル形状か
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z));

        if (maxDim / minDim > 2.5f)
        {
            CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(go);
            cc.center = bounds.center;

            if (size.x >= size.y && size.x >= size.z)
            {
                cc.direction = 0;
                cc.radius = Mathf.Max(size.y, size.z) * 0.4f;
                cc.height = size.x;
            }
            else if (size.y >= size.x && size.y >= size.z)
            {
                cc.direction = 1;
                cc.radius = Mathf.Max(size.x, size.z) * 0.4f;
                cc.height = size.y;
            }
            else
            {
                cc.direction = 2;
                cc.radius = Mathf.Max(size.x, size.y) * 0.4f;
                cc.height = size.z;
            }
            return 1;
        }

        // デフォルトはBoxCollider
        BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
        bc.center = bounds.center;
        bc.size = size;
        return 1;
    }

    // ========== プリセット ==========

    void SetupPolisherDetailed(GameObject root)
    {
        Undo.RecordObject(root, "Setup Polisher Detailed");
        RemoveAllColliders(root);

        int count = 0;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in children)
        {
            string nameLower = t.name.ToLower();
            MeshFilter mf = t.GetComponent<MeshFilter>();

            if (mf == null || mf.sharedMesh == null) continue;

            Bounds bounds = mf.sharedMesh.bounds;

            // ディスク/パッド部分（研磨面）
            if (nameLower.Contains("disk") || nameLower.Contains("disc") ||
                nameLower.Contains("pad") || nameLower.Contains("head") ||
                nameLower.Contains("buff"))
            {
                // 円筒形のコライダー（複数のBoxで近似）
                count += CreateCylindricalCollider(t.gameObject, bounds, 8);
            }
            // グリップ/ハンドル部分
            else if (nameLower.Contains("grip") || nameLower.Contains("handle") ||
                     nameLower.Contains("body"))
            {
                count += CreateElongatedColliders(t.gameObject, mf.sharedMesh, bounds);
            }
            // その他の部品
            else
            {
                count += CreateOptimalCollider(t.gameObject, mf.sharedMesh);
            }
        }

        // ルートにもコライダーがない場合
        if (count == 0)
        {
            MeshFilter rootMf = root.GetComponent<MeshFilter>();
            if (rootMf != null && rootMf.sharedMesh != null)
            {
                count = CreateSubdividedConvexColliders(root, rootMf.sharedMesh, rootMf.sharedMesh.bounds);
            }
        }

        // Rigidbody設定
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(root);
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        EditorUtility.SetDirty(root);
        Debug.Log($"[Polisher] {count}個の詳細コライダーを生成");
        EditorUtility.DisplayDialog("ポリッシャー設定完了", $"{count}個のコライダーを生成しました", "OK");
    }

    int CreateCylindricalCollider(GameObject go, Bounds bounds, int segments)
    {
        int count = 0;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // 最も薄い軸を円筒の軸とする
        int axis = 1; // デフォルトY軸
        float height = size.y;
        float radius = Mathf.Max(size.x, size.z) * 0.5f;

        if (size.x < size.y && size.x < size.z)
        {
            axis = 0;
            height = size.x;
            radius = Mathf.Max(size.y, size.z) * 0.5f;
        }
        else if (size.z < size.x && size.z < size.y)
        {
            axis = 2;
            height = size.z;
            radius = Mathf.Max(size.x, size.y) * 0.5f;
        }

        // 円周上にBoxColliderを配置
        for (int i = 0; i < segments; i++)
        {
            float angle = (2f * Mathf.PI * i) / segments;
            float nextAngle = (2f * Mathf.PI * (i + 1)) / segments;

            float midAngle = (angle + nextAngle) * 0.5f;
            float segmentWidth = 2f * radius * Mathf.Sin(Mathf.PI / segments);

            Vector3 offset = Vector3.zero;
            Vector3 boxSize = Vector3.one * segmentWidth;

            switch (axis)
            {
                case 0: // X軸
                    offset = new Vector3(0, Mathf.Cos(midAngle) * radius * 0.7f, Mathf.Sin(midAngle) * radius * 0.7f);
                    boxSize = new Vector3(height, segmentWidth, segmentWidth);
                    break;
                case 1: // Y軸
                    offset = new Vector3(Mathf.Cos(midAngle) * radius * 0.7f, 0, Mathf.Sin(midAngle) * radius * 0.7f);
                    boxSize = new Vector3(segmentWidth, height, segmentWidth);
                    break;
                case 2: // Z軸
                    offset = new Vector3(Mathf.Cos(midAngle) * radius * 0.7f, Mathf.Sin(midAngle) * radius * 0.7f, 0);
                    boxSize = new Vector3(segmentWidth, segmentWidth, height);
                    break;
            }

            BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
            bc.center = center + offset;
            bc.size = boxSize;
            count++;
        }

        // 中央にもう一つ
        BoxCollider centerBox = Undo.AddComponent<BoxCollider>(go);
        centerBox.center = center;
        switch (axis)
        {
            case 0: centerBox.size = new Vector3(height, radius, radius); break;
            case 1: centerBox.size = new Vector3(radius, height, radius); break;
            case 2: centerBox.size = new Vector3(radius, radius, height); break;
        }
        count++;

        return count;
    }

    void SetupHandDetailed(GameObject root)
    {
        Undo.RecordObject(root, "Setup Hand Detailed");
        RemoveAllColliders(root);

        int count = 0;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        // ボーン名のパターン
        string[] fingerNames = { "thumb", "index", "middle", "ring", "pinky", "little" };
        string[] jointNames = { "proximal", "intermediate", "distal", "tip", "meta", "01", "02", "03", "04" };

        foreach (Transform t in children)
        {
            string nameLower = t.name.ToLower();

            // 指の関節
            bool isFinger = false;
            foreach (string finger in fingerNames)
            {
                if (nameLower.Contains(finger))
                {
                    isFinger = true;
                    break;
                }
            }

            if (isFinger)
            {
                // 指先は小さい球
                if (nameLower.Contains("tip") || nameLower.Contains("distal") || nameLower.Contains("03") || nameLower.Contains("04"))
                {
                    CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(t.gameObject);
                    cc.radius = 0.006f;
                    cc.height = 0.02f;
                    cc.direction = 0; // X軸方向
                    count++;
                }
                // 中間関節
                else if (nameLower.Contains("intermediate") || nameLower.Contains("02"))
                {
                    CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(t.gameObject);
                    cc.radius = 0.007f;
                    cc.height = 0.025f;
                    cc.direction = 0;
                    count++;
                }
                // 基部関節
                else
                {
                    CapsuleCollider cc = Undo.AddComponent<CapsuleCollider>(t.gameObject);
                    cc.radius = 0.008f;
                    cc.height = 0.03f;
                    cc.direction = 0;
                    count++;
                }
            }
            // 手のひら
            else if (nameLower.Contains("palm") || nameLower.Contains("hand") || nameLower.Contains("wrist"))
            {
                BoxCollider bc = Undo.AddComponent<BoxCollider>(t.gameObject);
                bc.size = new Vector3(0.08f, 0.02f, 0.08f);
                count++;
            }
        }

        // Rigidbody
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(root);
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(root);
        Debug.Log($"[Hand] {count}個の詳細コライダーを生成");
        EditorUtility.DisplayDialog("手モデル設定完了", $"{count}個のコライダーを生成しました", "OK");
    }

    void SetupMetalPlateDetailed(GameObject root)
    {
        Undo.RecordObject(root, "Setup Metal Plate Detailed");
        RemoveAllColliders(root);

        int count = 0;

        // 金属板には高解像度のMeshColliderを使用
        MeshFilter mf = root.GetComponent<MeshFilter>();
        if (mf == null) mf = root.GetComponentInChildren<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            MeshCollider mc = Undo.AddComponent<MeshCollider>(root);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false; // 静的オブジェクトなのでConvex不要
            count++;

            // 追加で、表面に沿った細かいコライダーを生成
            Bounds bounds = mf.sharedMesh.bounds;
            count += CreateFlatColliders(root, mf.sharedMesh, bounds);
        }

        // Rigidbody（静的）
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(root);
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(root);
        Debug.Log($"[Metal] {count}個の詳細コライダーを生成");
        EditorUtility.DisplayDialog("金属板設定完了", $"{count}個のコライダーを生成しました", "OK");
    }
}

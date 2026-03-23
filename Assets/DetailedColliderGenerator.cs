using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// メッシュの形状に合わせて詳細な当たり判定を自動生成するランタイムスクリプト
/// モデルにアタッチして使用
/// </summary>
public class DetailedColliderGenerator : MonoBehaviour
{
    [Header("生成設定")]
    [Tooltip("子オブジェクトにも再帰的に追加")]
    public bool includeChildren = true;

    [Tooltip("1メッシュあたりの最大コライダー数")]
    [Range(1, 32)]
    public int maxCollidersPerMesh = 8;

    [Tooltip("コライダー間のパディング")]
    [Range(0f, 0.02f)]
    public float padding = 0.002f;

    [Tooltip("無視する最小サイズ")]
    [Range(0.001f, 0.05f)]
    public float minPartSize = 0.005f;

    [Header("Rigidbody設定")]
    public bool addRigidbody = true;
    public bool isKinematic = true;
    public bool useGravity = false;

    [Header("プリセット")]
    public ColliderPreset preset = ColliderPreset.Auto;

    public enum ColliderPreset
    {
        Auto,           // 形状を自動判定
        Polisher,       // 研磨機用（円筒+グリップ）
        Hand,           // 手モデル用（関節ごと）
        MetalPlate,     // 金属板用（平面）
        Detailed        // 最大限細かく
    }

    [Header("デバッグ")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0, 1, 0, 0.3f);

    private List<Collider> generatedColliders = new List<Collider>();

    void Start()
    {
        GenerateColliders();
    }

    [ContextMenu("コライダーを生成")]
    public void GenerateColliders()
    {
        // 既存のコライダーをクリア
        ClearGeneratedColliders();

        switch (preset)
        {
            case ColliderPreset.Polisher:
                SetupAsPolisher();
                break;
            case ColliderPreset.Hand:
                SetupAsHand();
                break;
            case ColliderPreset.MetalPlate:
                SetupAsMetalPlate();
                break;
            case ColliderPreset.Detailed:
                SetupDetailed();
                break;
            default:
                SetupAuto();
                break;
        }

        // Rigidbody追加
        if (addRigidbody)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = isKinematic;
            rb.useGravity = useGravity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Debug.Log($"[DetailedCollider] {gameObject.name}: {generatedColliders.Count}個のコライダーを生成");
    }

    [ContextMenu("コライダーをクリア")]
    public void ClearGeneratedColliders()
    {
        foreach (var col in generatedColliders)
        {
            if (col != null)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }
        }
        generatedColliders.Clear();
    }

    void SetupAuto()
    {
        Transform[] targets = includeChildren ?
            GetComponentsInChildren<Transform>(true) :
            new Transform[] { transform };

        foreach (Transform t in targets)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();

            Mesh mesh = mf?.sharedMesh ?? smr?.sharedMesh;
            if (mesh == null) continue;

            Bounds bounds = mesh.bounds;
            if (bounds.size.magnitude < minPartSize) continue;

            AnalyzeAndCreateColliders(t.gameObject, mesh, bounds);
        }
    }

    void AnalyzeAndCreateColliders(GameObject go, Mesh mesh, Bounds bounds)
    {
        Vector3 size = bounds.size;

        // アスペクト比を計算
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float midDim = size.x + size.y + size.z - maxDim - minDim;

        float elongation = maxDim / Mathf.Max(minDim, 0.001f);
        float flatness = minDim / Mathf.Max(midDim, 0.001f);

        // 細長い形状
        if (elongation > 3f)
        {
            CreateElongatedColliders(go, bounds, maxDim, minDim);
        }
        // 平らな形状
        else if (flatness < 0.3f)
        {
            CreateFlatColliders(go, bounds);
        }
        // コンパクトな形状
        else if (elongation < 1.5f)
        {
            CreateCompactColliders(go, bounds, mesh);
        }
        // 中間的な形状
        else
        {
            CreateSubdividedColliders(go, mesh, bounds);
        }
    }

    void CreateElongatedColliders(GameObject go, Bounds bounds, float maxDim, float minDim)
    {
        Vector3 size = bounds.size;

        // 最長軸を特定
        int axis = 0;
        if (size.y >= size.x && size.y >= size.z) axis = 1;
        else if (size.z >= size.x && size.z >= size.y) axis = 2;

        int segments = Mathf.Clamp(Mathf.CeilToInt(maxDim / 0.02f), 2, maxCollidersPerMesh);

        for (int i = 0; i < segments; i++)
        {
            float t = (i + 0.5f) / segments;

            Vector3 localPos = bounds.center;
            float segmentLength = maxDim / segments;

            switch (axis)
            {
                case 0:
                    localPos.x = bounds.min.x + size.x * t;
                    break;
                case 1:
                    localPos.y = bounds.min.y + size.y * t;
                    break;
                case 2:
                    localPos.z = bounds.min.z + size.z * t;
                    break;
            }

            CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
            cc.center = localPos;
            cc.direction = axis;
            cc.radius = minDim * 0.45f;
            cc.height = segmentLength + padding;
            generatedColliders.Add(cc);
        }
    }

    void CreateFlatColliders(GameObject go, Bounds bounds)
    {
        Vector3 size = bounds.size;

        // 最薄軸を特定
        int thinAxis = 0;
        if (size.y <= size.x && size.y <= size.z) thinAxis = 1;
        else if (size.z <= size.x && size.z <= size.y) thinAxis = 2;

        int gridSize = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(maxCollidersPerMesh)), 2, 4);

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                float ti = (i + 0.5f) / gridSize;
                float tj = (j + 0.5f) / gridSize;

                Vector3 localPos = bounds.center;
                Vector3 localSize = size;

                switch (thinAxis)
                {
                    case 0:
                        localPos.y = bounds.min.y + size.y * ti;
                        localPos.z = bounds.min.z + size.z * tj;
                        localSize.y = size.y / gridSize + padding;
                        localSize.z = size.z / gridSize + padding;
                        break;
                    case 1:
                        localPos.x = bounds.min.x + size.x * ti;
                        localPos.z = bounds.min.z + size.z * tj;
                        localSize.x = size.x / gridSize + padding;
                        localSize.z = size.z / gridSize + padding;
                        break;
                    case 2:
                        localPos.x = bounds.min.x + size.x * ti;
                        localPos.y = bounds.min.y + size.y * tj;
                        localSize.x = size.x / gridSize + padding;
                        localSize.y = size.y / gridSize + padding;
                        break;
                }

                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.center = localPos;
                bc.size = localSize;
                generatedColliders.Add(bc);
            }
        }
    }

    void CreateCompactColliders(GameObject go, Bounds bounds, Mesh mesh)
    {
        Vector3 size = bounds.size;
        float avgSize = (size.x + size.y + size.z) / 3f;

        // 球に近い場合
        float sphereVolume = (4f / 3f) * Mathf.PI * Mathf.Pow(avgSize * 0.5f, 3);
        float boxVolume = size.x * size.y * size.z;

        if (boxVolume / sphereVolume > 0.4f && boxVolume / sphereVolume < 2f)
        {
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.center = bounds.center;
            sc.radius = avgSize * 0.45f;
            generatedColliders.Add(sc);
        }
        else
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = bounds.center;
            bc.size = size;
            generatedColliders.Add(bc);
        }
    }

    void CreateSubdividedColliders(GameObject go, Mesh mesh, Bounds bounds)
    {
        int divisions = 2;
        Vector3 cellSize = bounds.size / divisions;
        Vector3[] vertices = mesh.vertices;

        for (int x = 0; x < divisions; x++)
        {
            for (int y = 0; y < divisions; y++)
            {
                for (int z = 0; z < divisions; z++)
                {
                    Vector3 cellCenter = bounds.min + new Vector3(
                        cellSize.x * (x + 0.5f),
                        cellSize.y * (y + 0.5f),
                        cellSize.z * (z + 0.5f)
                    );

                    Bounds cellBounds = new Bounds(cellCenter, cellSize);

                    // 頂点がセル内にあるかチェック
                    bool hasVertex = false;
                    foreach (Vector3 v in vertices)
                    {
                        if (cellBounds.Contains(v))
                        {
                            hasVertex = true;
                            break;
                        }
                    }

                    if (hasVertex)
                    {
                        BoxCollider bc = go.AddComponent<BoxCollider>();
                        bc.center = cellCenter;
                        bc.size = cellSize + Vector3.one * padding;
                        generatedColliders.Add(bc);
                    }
                }
            }
        }
    }

    void SetupAsPolisher()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform t in children)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Bounds bounds = mf.sharedMesh.bounds;
            Vector3 size = bounds.size;

            string name = t.name.ToLower();

            // 研磨ディスク部分
            if (name.Contains("disk") || name.Contains("disc") || name.Contains("pad") || name.Contains("head"))
            {
                CreateCylinderApproximation(t.gameObject, bounds, 6);
            }
            // グリップ/ボディ
            else if (name.Contains("grip") || name.Contains("handle") || name.Contains("body"))
            {
                float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                CreateElongatedColliders(t.gameObject, bounds, maxDim, minDim);
            }
            else
            {
                AnalyzeAndCreateColliders(t.gameObject, mf.sharedMesh, bounds);
            }
        }

        // コライダーがない場合はルートに生成
        if (generatedColliders.Count == 0)
        {
            MeshFilter rootMf = GetComponent<MeshFilter>();
            if (rootMf != null && rootMf.sharedMesh != null)
            {
                CreateSubdividedColliders(gameObject, rootMf.sharedMesh, rootMf.sharedMesh.bounds);
            }
        }
    }

    void CreateCylinderApproximation(GameObject go, Bounds bounds, int segments)
    {
        Vector3 size = bounds.size;

        // 最薄軸を円筒の軸とする
        int axis = 1;
        float height = size.y;
        float radius = Mathf.Max(size.x, size.z) * 0.5f;

        if (size.x < size.y && size.x < size.z)
        {
            axis = 0; height = size.x;
            radius = Mathf.Max(size.y, size.z) * 0.5f;
        }
        else if (size.z < size.x && size.z < size.y)
        {
            axis = 2; height = size.z;
            radius = Mathf.Max(size.x, size.y) * 0.5f;
        }

        // 中央のコライダー
        BoxCollider centerBox = go.AddComponent<BoxCollider>();
        centerBox.center = bounds.center;
        switch (axis)
        {
            case 0: centerBox.size = new Vector3(height, radius * 1.2f, radius * 1.2f); break;
            case 1: centerBox.size = new Vector3(radius * 1.2f, height, radius * 1.2f); break;
            case 2: centerBox.size = new Vector3(radius * 1.2f, radius * 1.2f, height); break;
        }
        generatedColliders.Add(centerBox);

        // 周囲のコライダー
        for (int i = 0; i < segments; i++)
        {
            float angle = (2f * Mathf.PI * i) / segments;
            float segmentWidth = radius * 0.6f;

            Vector3 offset = Vector3.zero;
            switch (axis)
            {
                case 0:
                    offset = new Vector3(0, Mathf.Cos(angle) * radius * 0.6f, Mathf.Sin(angle) * radius * 0.6f);
                    break;
                case 1:
                    offset = new Vector3(Mathf.Cos(angle) * radius * 0.6f, 0, Mathf.Sin(angle) * radius * 0.6f);
                    break;
                case 2:
                    offset = new Vector3(Mathf.Cos(angle) * radius * 0.6f, Mathf.Sin(angle) * radius * 0.6f, 0);
                    break;
            }

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = bounds.center + offset;

            switch (axis)
            {
                case 0: bc.size = new Vector3(height, segmentWidth, segmentWidth); break;
                case 1: bc.size = new Vector3(segmentWidth, height, segmentWidth); break;
                case 2: bc.size = new Vector3(segmentWidth, segmentWidth, height); break;
            }
            generatedColliders.Add(bc);
        }
    }

    void SetupAsHand()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        string[] fingerParts = { "thumb", "index", "middle", "ring", "pinky", "little" };
        string[] jointParts = { "tip", "distal", "intermediate", "proximal", "meta", "01", "02", "03", "04" };

        foreach (Transform t in children)
        {
            string name = t.name.ToLower();

            bool isFinger = false;
            foreach (string part in fingerParts)
            {
                if (name.Contains(part)) { isFinger = true; break; }
            }

            if (isFinger)
            {
                float radius = 0.007f;
                float height = 0.025f;

                if (name.Contains("tip") || name.Contains("distal") || name.Contains("04"))
                {
                    radius = 0.005f; height = 0.015f;
                }
                else if (name.Contains("proximal") || name.Contains("01"))
                {
                    radius = 0.008f; height = 0.03f;
                }

                CapsuleCollider cc = t.gameObject.AddComponent<CapsuleCollider>();
                cc.radius = radius;
                cc.height = height;
                cc.direction = 0;
                generatedColliders.Add(cc);
            }
            else if (name.Contains("palm") || name.Contains("hand"))
            {
                BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.08f, 0.025f, 0.08f);
                generatedColliders.Add(bc);
            }
            else if (name.Contains("wrist"))
            {
                CapsuleCollider cc = t.gameObject.AddComponent<CapsuleCollider>();
                cc.radius = 0.025f;
                cc.height = 0.05f;
                cc.direction = 0;
                generatedColliders.Add(cc);
            }
        }
    }

    void SetupAsMetalPlate()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) mf = GetComponentInChildren<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            // メインのMeshCollider
            MeshCollider mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            generatedColliders.Add(mc);
        }
    }

    void SetupDetailed()
    {
        maxCollidersPerMesh = 16; // 最大数を増やす
        SetupAuto();
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;

        foreach (Collider col in generatedColliders)
        {
            if (col == null) continue;

            Gizmos.matrix = col.transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                DrawWireCapsule(capsule);
            }
        }
    }

    void DrawWireCapsule(CapsuleCollider cc)
    {
        Vector3 center = cc.center;
        float radius = cc.radius;
        float height = cc.height;

        Vector3 up = Vector3.up;
        if (cc.direction == 0) up = Vector3.right;
        else if (cc.direction == 2) up = Vector3.forward;

        Vector3 top = center + up * (height * 0.5f - radius);
        Vector3 bottom = center - up * (height * 0.5f - radius);

        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);
    }
}

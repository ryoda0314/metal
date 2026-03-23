using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// メッシュの実際の頂点データに基づいて、ぴったりフィットするコライダーを生成
/// </summary>
public class PreciseColliderSetup : EditorWindow
{
    private GameObject targetObject;
    private float scaleFactor = 1.0f;
    private float shrinkAmount = 0.0f; // コライダーを縮小
    private int subdivisions = 3;
    private bool useVertexClustering = true;
    private float clusterThreshold = 0.02f;

    [MenuItem("Tools/Kenma Model/精密コライダー生成")]
    static void ShowWindow()
    {
        GetWindow<PreciseColliderSetup>("精密コライダー");
    }

    void OnGUI()
    {
        GUILayout.Label("精密コライダー生成", EditorStyles.boldLabel);
        GUILayout.Label("メッシュ頂点に基づいてタイトなコライダーを生成", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("対象", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        scaleFactor = EditorGUILayout.Slider("スケール係数", scaleFactor, 0.5f, 1.5f);
        shrinkAmount = EditorGUILayout.Slider("縮小量", shrinkAmount, 0f, 0.02f);
        subdivisions = EditorGUILayout.IntSlider("分割数", subdivisions, 1, 6);
        useVertexClustering = EditorGUILayout.Toggle("頂点クラスタリング", useVertexClustering);
        if (useVertexClustering)
        {
            clusterThreshold = EditorGUILayout.Slider("クラスタ閾値", clusterThreshold, 0.005f, 0.05f);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("既存コライダー削除", GUILayout.Height(25)))
        {
            if (targetObject != null) RemoveColliders(targetObject);
        }

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.cyan;

        if (GUILayout.Button("頂点ベース精密コライダー生成", GUILayout.Height(35)))
        {
            if (targetObject != null) GeneratePreciseColliders(targetObject);
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("OBB (回転ボックス) コライダー生成", GUILayout.Height(35)))
        {
            if (targetObject != null) GenerateOBBColliders(targetObject);
        }

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("凸包メッシュコライダー生成", GUILayout.Height(35)))
        {
            if (targetObject != null) GenerateConvexMeshColliders(targetObject);
        }

        GUI.backgroundColor = Color.white;
    }

    void RemoveColliders(GameObject root)
    {
        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) Undo.DestroyObjectImmediate(c);
        Debug.Log($"[Precise] {cols.Length}個削除");
    }

    /// <summary>
    /// メッシュの実際の頂点位置に基づいてタイトなコライダーを生成
    /// </summary>
    void GeneratePreciseColliders(GameObject root)
    {
        Undo.RecordObject(root, "Generate Precise Colliders");
        int total = 0;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] vertices = mesh.vertices;

            if (vertices.Length == 0) continue;

            // ワールド座標に変換
            Vector3[] worldVerts = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                worldVerts[i] = t.TransformPoint(vertices[i]);
            }

            // 頂点クラスタリング
            if (useVertexClustering)
            {
                List<List<Vector3>> clusters = ClusterVertices(worldVerts, clusterThreshold);

                foreach (var cluster in clusters)
                {
                    if (cluster.Count < 4) continue;

                    // クラスタの境界を計算
                    Bounds clusterBounds = CalculateTightBounds(cluster.ToArray());

                    // ローカル座標に戻す
                    Vector3 localCenter = t.InverseTransformPoint(clusterBounds.center);
                    Vector3 localSize = Vector3.Scale(clusterBounds.size,
                        new Vector3(1f / t.lossyScale.x, 1f / t.lossyScale.y, 1f / t.lossyScale.z));

                    // 縮小適用
                    localSize = localSize * scaleFactor - Vector3.one * shrinkAmount;
                    if (localSize.x <= 0 || localSize.y <= 0 || localSize.z <= 0) continue;

                    BoxCollider bc = Undo.AddComponent<BoxCollider>(t.gameObject);
                    bc.center = localCenter;
                    bc.size = localSize;
                    total++;
                }
            }
            else
            {
                // 分割ベースのアプローチ
                total += CreateSubdividedColliders(t.gameObject, mesh, subdivisions);
            }
        }

        AddRigidbody(root);
        EditorUtility.SetDirty(root);
        Debug.Log($"[Precise] {total}個の精密コライダー生成完了");
    }

    List<List<Vector3>> ClusterVertices(Vector3[] vertices, float threshold)
    {
        List<List<Vector3>> clusters = new List<List<Vector3>>();
        bool[] assigned = new bool[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            if (assigned[i]) continue;

            List<Vector3> cluster = new List<Vector3> { vertices[i] };
            assigned[i] = true;

            for (int j = i + 1; j < vertices.Length; j++)
            {
                if (assigned[j]) continue;

                // クラスタ内のどれかの点に近ければ追加
                foreach (var p in cluster)
                {
                    if (Vector3.Distance(p, vertices[j]) < threshold)
                    {
                        cluster.Add(vertices[j]);
                        assigned[j] = true;
                        break;
                    }
                }
            }

            if (cluster.Count >= 4)
            {
                clusters.Add(cluster);
            }
        }

        return clusters;
    }

    Bounds CalculateTightBounds(Vector3[] points)
    {
        if (points.Length == 0) return new Bounds();

        Vector3 min = points[0];
        Vector3 max = points[0];

        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    int CreateSubdividedColliders(GameObject go, Mesh mesh, int divs)
    {
        Vector3[] verts = mesh.vertices;
        Bounds meshBounds = CalculateTightBounds(verts);

        int count = 0;
        Vector3 cellSize = meshBounds.size / divs;

        for (int x = 0; x < divs; x++)
        {
            for (int y = 0; y < divs; y++)
            {
                for (int z = 0; z < divs; z++)
                {
                    Vector3 cellMin = meshBounds.min + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                    Vector3 cellMax = cellMin + cellSize;
                    Bounds cellBounds = new Bounds();
                    cellBounds.SetMinMax(cellMin, cellMax);

                    // このセル内の頂点を収集
                    List<Vector3> cellVerts = new List<Vector3>();
                    foreach (var v in verts)
                    {
                        if (cellBounds.Contains(v))
                        {
                            cellVerts.Add(v);
                        }
                    }

                    if (cellVerts.Count < 3) continue;

                    // セル内頂点のタイト境界
                    Bounds tightBounds = CalculateTightBounds(cellVerts.ToArray());

                    Vector3 size = tightBounds.size * scaleFactor - Vector3.one * shrinkAmount;
                    if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f) continue;

                    BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                    bc.center = tightBounds.center;
                    bc.size = size;
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// OBB (Oriented Bounding Box) - 回転を考慮した最小ボックス
    /// </summary>
    void GenerateOBBColliders(GameObject root)
    {
        Undo.RecordObject(root, "Generate OBB Colliders");
        int total = 0;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] verts = mesh.vertices;

            if (verts.Length < 4) continue;

            // PCA風に主軸を求める（簡易版）
            Vector3 center = Vector3.zero;
            foreach (var v in verts) center += v;
            center /= verts.Length;

            // 共分散行列の計算
            float[,] cov = new float[3, 3];
            foreach (var v in verts)
            {
                Vector3 d = v - center;
                cov[0, 0] += d.x * d.x;
                cov[0, 1] += d.x * d.y;
                cov[0, 2] += d.x * d.z;
                cov[1, 1] += d.y * d.y;
                cov[1, 2] += d.y * d.z;
                cov[2, 2] += d.z * d.z;
            }
            cov[1, 0] = cov[0, 1];
            cov[2, 0] = cov[0, 2];
            cov[2, 1] = cov[1, 2];

            // 最大固有ベクトルを近似（べき乗法）
            Vector3 axis1 = PowerIteration(cov);
            Vector3 axis2 = Vector3.Cross(axis1, Vector3.up).normalized;
            if (axis2.sqrMagnitude < 0.01f) axis2 = Vector3.Cross(axis1, Vector3.right).normalized;
            Vector3 axis3 = Vector3.Cross(axis1, axis2).normalized;

            // 各軸への投影で範囲を計算
            float min1 = float.MaxValue, max1 = float.MinValue;
            float min2 = float.MaxValue, max2 = float.MinValue;
            float min3 = float.MaxValue, max3 = float.MinValue;

            foreach (var v in verts)
            {
                Vector3 d = v - center;
                float p1 = Vector3.Dot(d, axis1);
                float p2 = Vector3.Dot(d, axis2);
                float p3 = Vector3.Dot(d, axis3);

                min1 = Mathf.Min(min1, p1); max1 = Mathf.Max(max1, p1);
                min2 = Mathf.Min(min2, p2); max2 = Mathf.Max(max2, p2);
                min3 = Mathf.Min(min3, p3); max3 = Mathf.Max(max3, p3);
            }

            Vector3 size = new Vector3(max1 - min1, max2 - min2, max3 - min3) * scaleFactor;
            size -= Vector3.one * shrinkAmount;

            if (size.x <= 0 || size.y <= 0 || size.z <= 0) continue;

            // OBBの中心
            Vector3 obbCenter = center + axis1 * (min1 + max1) * 0.5f
                                       + axis2 * (min2 + max2) * 0.5f
                                       + axis3 * (min3 + max3) * 0.5f;

            BoxCollider bc = Undo.AddComponent<BoxCollider>(t.gameObject);
            bc.center = obbCenter;
            bc.size = size;
            total++;
        }

        AddRigidbody(root);
        EditorUtility.SetDirty(root);
        Debug.Log($"[OBB] {total}個のOBBコライダー生成完了");
    }

    Vector3 PowerIteration(float[,] matrix, int iterations = 10)
    {
        Vector3 v = new Vector3(1, 1, 1).normalized;

        for (int i = 0; i < iterations; i++)
        {
            Vector3 next = new Vector3(
                matrix[0, 0] * v.x + matrix[0, 1] * v.y + matrix[0, 2] * v.z,
                matrix[1, 0] * v.x + matrix[1, 1] * v.y + matrix[1, 2] * v.z,
                matrix[2, 0] * v.x + matrix[2, 1] * v.y + matrix[2, 2] * v.z
            );
            v = next.normalized;
        }

        return v;
    }

    /// <summary>
    /// Convex MeshCollider - メッシュ形状に最もフィット
    /// </summary>
    void GenerateConvexMeshColliders(GameObject root)
    {
        Undo.RecordObject(root, "Generate Convex Mesh Colliders");
        int total = 0;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // Convex MeshCollider（動く物体用）
            MeshCollider mc = Undo.AddComponent<MeshCollider>(t.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;

            total++;
        }

        AddRigidbody(root);
        EditorUtility.SetDirty(root);
        Debug.Log($"[Convex] {total}個の凸包コライダー生成完了");
    }

    void AddRigidbody(GameObject root)
    {
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(root);
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
}

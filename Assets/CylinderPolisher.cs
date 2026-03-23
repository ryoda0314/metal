#if !UNITY_ANDROID
// CylinderPolisher.cs
// 研磨機の削る部分に取り付けた円柱コライダー（MeshCollider対応）を当たり判定に用いて研磨模様をつける
// MetalSwirlPolisher.cs を参考に、円柱の接触面で研磨パターンを適用

using UnityEngine;
using System.Collections.Generic;
using Valve.VR.InteractionSystem;

[DisallowMultipleComponent]
public class CylinderPolisher : MonoBehaviour
{
    // ====== 参照 ======
    [Header("Polish Target (assign Cube/Plane)")]
    public Renderer targetRenderer;          // 金属板の MeshRenderer
    public MeshCollider targetCollider;      // MeshCollider（Convex OFF）

    [Header("Grinding Part Collider")]
    [Tooltip("研磨機の削る部分のコライダー（MeshCollider, BoxCollider, CapsuleCollider等）")]
    public Collider grindingCollider;        // 任意のコライダー（平べったい円柱のMeshColliderもOK）
    [Tooltip("研磨面の下方向（ローカル座標）- 通常は-Y")]
    public Vector3 localDownDirection = Vector3.down;

    // ====== マスク ======
    [Header("Mask Texture")]
    [Range(256, 4096)] public int maskSize = 1024;
    [Tooltip("R=全黒クリア / T=全白デバッグ")]
    public bool enableDebugKeys = true;

    // ====== ブラシ選択 ======
    enum BrushShape { SwirlArc, SolidCircle, LineSweep, CrossHatch, Block, Tornado, SCurve, WavyLine, IvyVine, ScaleStack, Feather, Vibration }

    // ====== プリセット ======
    public enum PolishPreset
    {
        EngineTurn_RandomSwirl,   // ランダム角の半月ブラシ
        Hairline_Velocity,        // 進行方向に沿った直線ヘアライン
        CrossHatch_Velocity,      // 進行方向＋交差角
        Dots_Stipple,             // 円スタンプ
        Concentric_FromPoint,     // 指定中心から見た"円周接線"
        Spiral_Trail,             // 進行方向にねじりを加えたスパイラル
        Antique_BlockRandom,      // ランダム角の短い矩形
        Antique_BlockVelocity,    // 進行方向に沿うアンティークヘアー
        Tornado_Swirl,            // トルネード（大きな渦巻き）
        Aurora_SCurve,            // オーロラ（S字カーブヘアライン）
        Sander_WavyCross,         // サンダー（交差する波線）
        AntiqueStripe_Random,     // アンティークストライプ（ランダム方向ヘアライン）
        Ivy_Intertwine,           // アイビー（蔦が絡み合う複雑なパターン）
        RandomScale_Layered,      // ランダムスケール（多層の鱗模様）
        Feather_Soft,             // フェザー（羽毛のような質感）
        Vibration_Matte           // バイブレーション（無方向のマット仕上げ）
    }

    [Header("Preset")]
    public PolishPreset preset = PolishPreset.EngineTurn_RandomSwirl;
    [Tooltip("Concentric 用の中心（ワールド座標）")]
    public Vector3 concentricCenterWorld = Vector3.zero;
    [Tooltip("Spiral の1スタンプあたりのねじり量（度）")]
    public float spiralTwistDegPerStamp = 15f;

    [Header("Angle Control")]
    [Tooltip("手動オフセット角（度）")]
    public float angleBiasDeg = 0f;
    [Tooltip("速度方向に沿ってブラシ角度を決定")]
    public bool useVelocityAngle = true;

    [Header("Antique Block (Velocity-aligned)")]
    [Tooltip("進行方向に対する角度ゆらぎ（度）")]
    public float blockAngleJitterDeg = 10f;

    // ---- Line Sweep（直線ヘアライン） ----
    [Header("Line Sweep")]
    [Range(0.005f, 0.15f)] public float lineLengthUv = 0.08f;
    [Range(0.002f, 0.05f)] public float lineWidthUv = 0.008f;
    [Range(0.05f, 1.00f)] public float lineStrength = 0.60f;

    // ---- Swirl（半月の刷毛：エンジンターン）----
    [Header("Swirl (Engine Turn)")]
    [Range(0.005f, 0.08f)] public float swirlRadiusUv = 0.03f;
    [Range(0.05f, 1.00f)] public float swirlStrength = 0.70f;
    [Range(0.30f, 0.95f)] public float arcLenPi = 0.70f;
    [Range(4f, 28f)] public float stripesFreq = 12f;
    [Range(0.30f, 1.20f)] public float ellipseYScale = 0.60f;

    // ---- Solid Circle ----
    [Header("Solid Circle")]
    [Range(0.005f, 0.08f)] public float circleRadiusUv = 0.035f;
    [Range(0.05f, 1.00f)] public float circleStrength = 0.70f;

    // ---- Cross Hatch ----
    [Header("Cross Hatch")]
    [Range(0.005f, 0.15f)] public float crossLenUv = 0.06f;
    [Range(0.002f, 0.05f)] public float crossWidUv = 0.008f;
    [Range(0.05f, 1.00f)] public float crossStrength = 0.50f;
    [Range(0f, 90f)] public float crossAngleDeg = 60f;

    // ---- Block Stroke ----
    [Header("Block Stroke")]
    [Range(0.01f, 0.10f)] public float blockLengthUv = 0.05f;
    [Range(0.01f, 0.05f)] public float blockWidthUv = 0.03f;
    [Range(0.05f, 1.00f)] public float blockStrength = 0.70f;

    // ---- Tornado ----
    [Header("Tornado")]
    [Range(0.02f, 0.15f)] public float tornadoRadiusUv = 0.08f;
    [Range(0.05f, 1.00f)] public float tornadoStrength = 0.65f;
    [Range(1f, 8f)] public float tornadoSpirals = 3f;
    [Range(0.2f, 0.8f)] public float tornadoFadeOut = 0.5f;

    // ---- Aurora（オーロラ：S字カーブ）----
    [Header("Aurora (S-Curve)")]
    [Range(0.03f, 0.20f)] public float auroraLengthUv = 0.12f;
    [Range(0.002f, 0.03f)] public float auroraWidthUv = 0.008f;
    [Range(0.05f, 1.00f)] public float auroraStrength = 0.60f;
    [Range(0.5f, 3f)] public float auroraCurvature = 1.5f;

    // ---- Sander（サンダー：交差波線）----
    [Header("Sander (Wavy Cross)")]
    [Range(0.03f, 0.15f)] public float sanderLengthUv = 0.10f;
    [Range(0.002f, 0.02f)] public float sanderWidthUv = 0.006f;
    [Range(0.05f, 1.00f)] public float sanderStrength = 0.55f;
    [Range(2f, 8f)] public float sanderWaveFreq = 4f;
    [Range(0.005f, 0.03f)] public float sanderWaveAmp = 0.015f;

    // ---- Antique Stripe（アンティークストライプ）----
    [Header("Antique Stripe")]
    [Range(0.02f, 0.12f)] public float stripeWidthUv = 0.04f;
    [Range(0.05f, 0.25f)] public float stripeLengthUv = 0.15f;
    [Range(0.05f, 1.00f)] public float stripeStrength = 0.65f;
    [Range(0f, 90f)] public float stripeAngleJitterDeg = 45f;

    // ---- Ivy（アイビー：蔦が絡み合うパターン）----
    [Header("Ivy (Intertwining Vines)")]
    [Range(0.03f, 0.15f)] public float ivyLengthUv = 0.08f;
    [Range(0.003f, 0.02f)] public float ivyWidthUv = 0.006f;
    [Range(0.05f, 1.00f)] public float ivyStrength = 0.55f;
    [Range(1f, 4f)] public float ivyCurviness = 2f;
    [Range(2, 5)] public int ivyBranches = 3;

    // ---- Random Scale（ランダムスケール：多層の鱗）----
    [Header("Random Scale")]
    [Range(0.01f, 0.05f)] public float scaleSizeUv = 0.025f;
    [Range(0.1f, 1.0f)] public float scaleDensity = 0.6f;
    [Range(1, 4)] public int scaleLayerCount = 2;

    // ---- Feather（フェザー：羽毛）----
    [Header("Feather")]
    [Range(0.02f, 0.1f)] public float featherLengthUv = 0.06f;
    [Range(0.005f, 0.03f)] public float featherWidthUv = 0.015f;
    [Range(0.1f, 1.0f)] public float featherCurve = 0.5f;

    // ---- Vibration（バイブレーション：無方向マット）----
    [Header("Vibration")]
    [Range(0.002f, 0.02f)] public float vibrioRadiusUv = 0.008f;
    [Range(0.5f, 2.0f)] public float vibrioDensity = 1.0f;
    [Range(0f, 180f)] public float vibrioArcAngle = 120f;

    // ====== 接触設定 ======
    [Header("Contact Settings")]
    [Tooltip("研磨面のサンプル点の密度（X方向）")]
    [Range(3, 20)] public int sampleCountX = 8;
    [Tooltip("研磨面のサンプル点の密度（Z方向）")]
    [Range(3, 20)] public int sampleCountZ = 8;
    [Tooltip("接触判定のマージン距離（研磨面と金属板の許容距離）")]
    public float contactMargin = 0.03f; // 3cm - 実際の距離に合わせて調整
    [Tooltip("レイキャストの開始高さオフセット")]
    public float rayStartOffset = 0.05f;

    [Header("Debug")]
    [Tooltip("デバッグログを出力")]
    public bool debugLog = true;
    [Tooltip("デバッグログの出力間隔（秒）")]
    public float debugLogInterval = 0.5f;
    private float lastDebugLogTime;

    // ====== Shader Property 名 ======
    [Header("Shader Properties")]
    [SerializeField] string propPolishMask = "_PolishMask";
    [SerializeField] string propBaseSmooth = "_BaseSmoothness";
    [SerializeField] string propPolishedSmooth = "_PolishedSmoothness";
    [SerializeField] string propBaseMetal = "_BaseMetallic";
    [SerializeField] string propPolishedMetal = "_PolishedMetallic";

    [Header("Default Material Values")]
    [Range(0f, 1f)] public float defaultBaseSmooth = 0.10f;
    [Range(0f, 1f)] public float defaultPolishedSmooth = 0.98f;
    [Range(0f, 1f)] public float defaultBaseMetal = 0.60f;
    [Range(0f, 1f)] public float defaultPolishedMetal = 1.00f;

    [Header("Realism")]
    [Range(0f, 1f)] public float abrasiveJitter = 0.5f;

    // ====== 内部変数 ======
    Texture2D maskTex;
    Material runtimeMat;
    System.Random rng = new System.Random();
    Rigidbody rb;
    Vector3 prevPos;
    Interactable interactable;
    float lastHapticTime;
    int totalBrushStrokes; // ブラシ描画の累積回数
    float spiralAccum;     // スパイラル用蓄積角

    // ====== 振動フィードバック ======
    void TriggerHapticFeedback(float duration, float frequency, float amplitude)
    {
        if (Time.time - lastHapticTime < 0.01f) return;
        lastHapticTime = Time.time;

        if (interactable && interactable.attachedToHand)
        {
            interactable.attachedToHand.TriggerHapticPulse((ushort)(duration * 1000000f), frequency, amplitude);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        interactable = GetComponent<Interactable>();
        prevPos = transform.position;

        if (!targetRenderer) Debug.LogWarning("[CylinderPolisher] targetRenderer 未設定");
        if (!targetCollider) Debug.LogWarning("[CylinderPolisher] targetCollider 未設定");
        if (!grindingCollider)
        {
            // 自身または子から任意のColliderを探す（MeshCollider優先）
            grindingCollider = GetComponentInChildren<MeshCollider>();
            if (!grindingCollider)
                grindingCollider = GetComponentInChildren<Collider>();
            if (!grindingCollider)
                Debug.LogWarning("[CylinderPolisher] grindingCollider 未設定");
            else
                Debug.Log($"[CylinderPolisher] grindingCollider 自動検出: {grindingCollider.name} ({grindingCollider.GetType().Name})");
        }

        // MeshColliderの場合、Rigidbodyがあればkinematicにする（Concaveエラー回避）
        if (grindingCollider is MeshCollider meshCol && !meshCol.convex)
        {
            Rigidbody grindRb = grindingCollider.GetComponent<Rigidbody>();
            if (grindRb != null && !grindRb.isKinematic)
            {
                Debug.Log("[CylinderPolisher] MeshCollider(非Convex)のRigidbodyをkinematicに設定");
                grindRb.isKinematic = true;
            }
        }
    }

    void Start()
    {
        if (!targetRenderer) return;

        // ランタイム専用マテリアル
        runtimeMat = targetRenderer.material;

        // マスク作成（全黒＝未研磨）
        maskTex = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false, true);
        ClearMask();

        // Shaderへセット
        runtimeMat.SetTexture(propPolishMask, maskTex);
        runtimeMat.SetFloat(propBaseSmooth, defaultBaseSmooth);
        runtimeMat.SetFloat(propPolishedSmooth, defaultPolishedSmooth);
        runtimeMat.SetFloat(propBaseMetal, defaultBaseMetal);
        runtimeMat.SetFloat(propPolishedMetal, defaultPolishedMetal);

        Debug.Log("[CylinderPolisher] 初期化完了");
        Debug.Log($"[CylinderPolisher] 設定:");
        Debug.Log($"  targetRenderer: {(targetRenderer ? targetRenderer.name : "未設定")}");
        Debug.Log($"  targetCollider: {(targetCollider ? targetCollider.name : "未設定")}");
        Debug.Log($"  grindingCollider: {(grindingCollider ? grindingCollider.name + " (" + grindingCollider.GetType().Name + ")" : "未設定")}");
        Debug.Log($"  localDownDirection: {localDownDirection}");
        Debug.Log($"  contactMargin: {contactMargin}");
        Debug.Log($"  rayStartOffset: {rayStartOffset}");
        Debug.Log($"  preset: {preset}");

        // シェーダープロパティの存在確認
        Debug.Log($"[CylinderPolisher] シェーダー: {runtimeMat.shader.name}");
        bool hasMask = runtimeMat.HasProperty(propPolishMask);
        bool hasBaseSmooth = runtimeMat.HasProperty(propBaseSmooth);
        bool hasPolishedSmooth = runtimeMat.HasProperty(propPolishedSmooth);
        Debug.Log($"[CylinderPolisher] シェーダープロパティ確認:");
        Debug.Log($"  {propPolishMask}: {(hasMask ? "あり" : "なし")}");
        Debug.Log($"  {propBaseSmooth}: {(hasBaseSmooth ? "あり" : "なし")}");
        Debug.Log($"  {propPolishedSmooth}: {(hasPolishedSmooth ? "あり" : "なし")}");

        if (!hasMask)
        {
            Debug.LogWarning($"[CylinderPolisher] 警告: シェーダーに {propPolishMask} プロパティがありません！研磨効果が表示されない可能性があります。");
        }
    }

    void LateUpdate()
    {
        if (!targetCollider || runtimeMat == null || maskTex == null || !grindingCollider) return;

        // 速度計算
        Vector3 velWorld = Vector3.zero;
        if (rb != null && !rb.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            velWorld = rb.linearVelocity;
#else
            velWorld = rb.velocity;
#endif
        }
        else
        {
            if (Time.deltaTime > 0f)
                velWorld = (grindingCollider.transform.position - prevPos) / Mathf.Max(Time.deltaTime, 1e-6f);
        }
        prevPos = grindingCollider.transform.position;

        // 研磨部の接触点をサンプリング
        List<Vector2> contactUVs = GetGrindingContactUVs();

        bool isTouching = contactUVs.Count > 0;

        if (isTouching)
        {
            if (debugLog && (Time.time - lastDebugLogTime > debugLogInterval))
            {
                Debug.Log($"[CylinderPolisher] ★接触検出! UV数: {contactUVs.Count}, ブラシ描画開始");
            }

            // 接線方向への速度を計算（金属板の法線に対して）
            Vector3 planeNormal = targetCollider.transform.up;
            Vector3 velTangent = velWorld - Vector3.Dot(velWorld, planeNormal) * planeNormal;

            // 各接触点でブラシ描画
            foreach (Vector2 uv in contactUVs)
            {
                float angle = ComputeAngleForPreset(uv, velTangent);
                PaintBrushAtUV(uv, angle);
            }
        }

        // 振動フィードバック
        if (interactable && interactable.attachedToHand)
        {
            if (isTouching)
            {
                float speed = velWorld.magnitude;
                if (speed > 0.02f)
                {
                    float amp = Mathf.Clamp(0.2f + speed * 1.5f, 0f, 1f);
                    float freq = Mathf.Clamp(100f + speed * 300f, 100f, 300f);
                    TriggerHapticFeedback(0.01f, freq, amp);
                }
                else
                {
                    TriggerHapticFeedback(0.01f, 50f, 0.15f);
                }
            }
            else
            {
                TriggerHapticFeedback(0.01f, 20f, 0.05f);
            }
        }

        // デバッグキー
        if (enableDebugKeys)
        {
            if (Input.GetKeyDown(KeyCode.R)) ClearMask();
            if (Input.GetKeyDown(KeyCode.T)) FillWhite();
            if (Input.GetKeyDown(KeyCode.M)) DebugMaskStatus(); // マスク状態を確認
        }
    }

    /// <summary>
    /// 研磨部コライダーの底面から金属板への接触点をサンプリングし、UV座標を返す
    /// MeshCollider、BoxCollider、CapsuleCollider等に対応
    /// </summary>
    List<Vector2> GetGrindingContactUVs()
    {
        List<Vector2> uvs = new List<Vector2>();

        bool shouldLog = debugLog && (Time.time - lastDebugLogTime > debugLogInterval);

        if (!grindingCollider || !targetCollider)
        {
            if (shouldLog)
            {
                Debug.LogWarning($"[CylinderPolisher] コライダー未設定: grindingCollider={grindingCollider != null}, targetCollider={targetCollider != null}");
                lastDebugLogTime = Time.time;
            }
            return uvs;
        }

        Transform grindTr = grindingCollider.transform;
        Bounds bounds = grindingCollider.bounds; // ワールド座標のバウンディングボックス

        // 研磨面の下方向（ワールド座標）
        Vector3 worldDown = grindTr.TransformDirection(localDownDirection).normalized;

        // 金属板の法線方向
        Vector3 planeNormal = targetCollider.transform.up;

        // バウンディングボックスの底面をサンプリング
        // ローカル座標系で底面のグリッドを作成
        Vector3 localRight = grindTr.right;
        Vector3 localForward = grindTr.forward;

        // boundsのサイズからサンプリング範囲を決定
        float extentX = bounds.extents.x;
        float extentZ = bounds.extents.z;

        // MeshColliderの場合、より正確な底面位置を取得
        Vector3 bottomCenter = bounds.center + worldDown * bounds.extents.y;

        if (shouldLog)
        {
            Debug.Log($"[CylinderPolisher] === サンプリング開始 ===");
            Debug.Log($"  grindingCollider: {grindingCollider.name} (type: {grindingCollider.GetType().Name})");
            Debug.Log($"  bounds.center: {bounds.center}, bounds.size: {bounds.size}");
            Debug.Log($"  worldDown: {worldDown}");
            Debug.Log($"  bottomCenter: {bottomCenter}");
            Debug.Log($"  planeNormal: {planeNormal}");
            Debug.Log($"  targetCollider位置: {targetCollider.bounds.center}");
        }

        int totalSamples = 0;
        int nearColliderCount = 0;
        int rayHitCount = 0;
        int distOkCount = 0;
        int uvOkCount = 0;
        float minDist = float.MaxValue;
        float maxDist = float.MinValue;

        for (int i = 0; i < sampleCountX; i++)
        {
            float tx = (sampleCountX > 1) ? (float)i / (sampleCountX - 1) : 0.5f;
            float offsetX = Mathf.Lerp(-extentX, extentX, tx);

            for (int j = 0; j < sampleCountZ; j++)
            {
                float tz = (sampleCountZ > 1) ? (float)j / (sampleCountZ - 1) : 0.5f;
                float offsetZ = Mathf.Lerp(-extentZ, extentZ, tz);

                totalSamples++;

                // サンプル点（バウンディングボックスの底面）
                Vector3 samplePoint = bottomCenter + localRight * offsetX + localForward * offsetZ;

                // この点が実際にコライダー内にあるかチェック（MeshColliderの形状を考慮）
                Vector3 checkPoint = samplePoint - worldDown * 0.01f; // 少し内側
                if (!IsPointNearCollider(checkPoint, 0.02f))
                    continue;

                nearColliderCount++;

                // レイを金属板に向かって飛ばす
                Vector3 rayStart = samplePoint - worldDown * rayStartOffset; // 少し上から
                Vector3 rayDir = -planeNormal;
                Ray ray = new Ray(rayStart, rayDir);

                if (targetCollider.Raycast(ray, out RaycastHit hit, rayStartOffset + contactMargin + 0.1f))
                {
                    rayHitCount++;

                    // 研磨面と金属板の距離をチェック
                    float dist = Vector3.Distance(samplePoint, hit.point);
                    minDist = Mathf.Min(minDist, dist);
                    maxDist = Mathf.Max(maxDist, dist);

                    if (dist <= contactMargin)
                    {
                        distOkCount++;
                        Vector2 uv = hit.textureCoord;
                        if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
                        {
                            uvOkCount++;
                            uvs.Add(uv);
                        }
                    }
                }
            }
        }

        if (shouldLog)
        {
            Debug.Log($"[CylinderPolisher] === サンプリング結果 ===");
            Debug.Log($"  総サンプル数: {totalSamples}");
            Debug.Log($"  コライダー近傍: {nearColliderCount}");
            Debug.Log($"  レイヒット: {rayHitCount}");
            Debug.Log($"  距離OK (margin={contactMargin}): {distOkCount}");
            Debug.Log($"  UV有効: {uvOkCount}");
            if (rayHitCount > 0)
            {
                Debug.Log($"  距離範囲: {minDist:F4} ~ {maxDist:F4}");
            }
            lastDebugLogTime = Time.time;
        }

        return uvs;
    }

    /// <summary>
    /// 指定した点がコライダーの近くにあるかチェック
    /// 非Convex MeshColliderにも対応
    /// </summary>
    bool IsPointNearCollider(Vector3 point, float maxDistance)
    {
        if (!grindingCollider) return false;

        // MeshCollider（非Convex）の場合はClosestPointが使えないので、
        // バウンディングボックス + レイキャストで判定
        if (grindingCollider is MeshCollider meshCol && !meshCol.convex)
        {
            // まずバウンディングボックス内かチェック
            Bounds bounds = grindingCollider.bounds;
            bounds.Expand(maxDistance * 2f); // マージンを追加
            if (!bounds.Contains(point))
                return false;

            // 実際のメッシュ形状に対してレイキャストでチェック
            // 複数方向からレイを飛ばして、いずれかがヒットすれば近くにあると判定
            Vector3[] directions = {
                Vector3.up, Vector3.down,
                Vector3.left, Vector3.right,
                Vector3.forward, Vector3.back
            };

            foreach (Vector3 dir in directions)
            {
                Ray ray = new Ray(point, dir);
                if (meshCol.Raycast(ray, out RaycastHit hit, maxDistance))
                {
                    return true;
                }
            }

            // バウンディングボックス内だが、メッシュから離れている可能性
            // 円形の場合、角のサンプル点が外れることがあるのでバウンディングボックス判定を緩める
            // バウンディングボックスの中心からの距離で判定
            Vector3 boundsCenter = grindingCollider.bounds.center;
            Vector3 toPoint = point - boundsCenter;

            // XZ平面での距離（円柱の場合）
            float xzDist = new Vector2(toPoint.x, toPoint.z).magnitude;
            float maxXZExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);

            return xzDist <= maxXZExtent + maxDistance;
        }
        else
        {
            // BoxCollider, SphereCollider, CapsuleCollider, Convex MeshCollider
            Vector3 closest = grindingCollider.ClosestPoint(point);
            float dist = Vector3.Distance(point, closest);
            return dist <= maxDistance;
        }
    }

    // ====== プリセット→角度/ブラシ決定 ======
    float ComputeAngleForPreset(Vector2 uv, Vector3 velocityTangent)
    {
        float angle = angleBiasDeg * Mathf.Deg2Rad;

        switch (preset)
        {
            case PolishPreset.EngineTurn_RandomSwirl:
                return Rand(-Mathf.PI, Mathf.PI);

            case PolishPreset.Hairline_Velocity:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle;

            case PolishPreset.CrossHatch_Velocity:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle;

            case PolishPreset.Concentric_FromPoint:
                {
                    // UV座標からワールド座標を推定（簡易）
                    Vector3 p = targetCollider.bounds.center;
                    Vector3 c = concentricCenterWorld;
                    Vector3 t = (p - c);
                    if (t.sqrMagnitude < 1e-6f) t = Vector3.right;

                    Vector3 n = targetCollider.transform.up;
                    Vector3 e1 = Vector3.Cross(n, Vector3.up);
                    if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.Cross(n, Vector3.right);
                    e1.Normalize();
                    Vector3 e2 = Vector3.Cross(n, e1);

                    float rx = Vector3.Dot(t, e1);
                    float ry = Vector3.Dot(t, e2);
                    float radialAngle = Mathf.Atan2(ry, rx);
                    float tangentAngle = radialAngle + Mathf.PI * 0.5f;
                    return tangentAngle + angle;
                }

            case PolishPreset.Spiral_Trail:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                spiralAccum += spiralTwistDegPerStamp * Mathf.Deg2Rad;
                return angle + spiralAccum;

            case PolishPreset.Antique_BlockVelocity:
                {
                    float a = angle;
                    if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                        a = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                    float jitter = blockAngleJitterDeg * Mathf.Deg2Rad;
                    a += Rand(-jitter, jitter);
                    return a;
                }

            case PolishPreset.Tornado_Swirl:
                return Rand(-Mathf.PI, Mathf.PI);

            case PolishPreset.Aurora_SCurve:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle + Rand(-0.3f, 0.3f);

            case PolishPreset.Sander_WavyCross:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle;

            case PolishPreset.AntiqueStripe_Random:
                {
                    float jitter = stripeAngleJitterDeg * Mathf.Deg2Rad;
                    return Rand(-jitter, jitter);
                }

            case PolishPreset.Ivy_Intertwine:
            case PolishPreset.RandomScale_Layered:
            case PolishPreset.Feather_Soft:
            case PolishPreset.Vibration_Matte:
                return Rand(-Mathf.PI, Mathf.PI);

            case PolishPreset.Dots_Stipple:
            case PolishPreset.Antique_BlockRandom:
            default:
                return Rand(-Mathf.PI, Mathf.PI);
        }
    }

    void GetBrushForPreset(out BrushShape brush)
    {
        switch (preset)
        {
            case PolishPreset.EngineTurn_RandomSwirl: brush = BrushShape.SwirlArc; break;
            case PolishPreset.Hairline_Velocity: brush = BrushShape.LineSweep; break;
            case PolishPreset.CrossHatch_Velocity: brush = BrushShape.CrossHatch; break;
            case PolishPreset.Dots_Stipple: brush = BrushShape.SolidCircle; break;
            case PolishPreset.Concentric_FromPoint: brush = BrushShape.LineSweep; break;
            case PolishPreset.Spiral_Trail: brush = BrushShape.LineSweep; break;
            case PolishPreset.Antique_BlockVelocity: brush = BrushShape.Block; break;
            case PolishPreset.Antique_BlockRandom: brush = BrushShape.Block; break;
            case PolishPreset.Tornado_Swirl: brush = BrushShape.Tornado; break;
            case PolishPreset.Aurora_SCurve: brush = BrushShape.SCurve; break;
            case PolishPreset.Sander_WavyCross: brush = BrushShape.WavyLine; break;
            case PolishPreset.AntiqueStripe_Random: brush = BrushShape.LineSweep; break;
            case PolishPreset.Ivy_Intertwine: brush = BrushShape.IvyVine; break;
            case PolishPreset.RandomScale_Layered: brush = BrushShape.ScaleStack; break;
            case PolishPreset.Feather_Soft: brush = BrushShape.Feather; break;
            case PolishPreset.Vibration_Matte: brush = BrushShape.Vibration; break;
            default: brush = BrushShape.SwirlArc; break;
        }
    }

    void PaintBrushAtUV(Vector2 uv, float angle)
    {
        totalBrushStrokes++;

        GetBrushForPreset(out BrushShape brush);

        switch (brush)
        {
            case BrushShape.SwirlArc:
                PaintSwirlAtUV(uv);
                break;
            case BrushShape.SolidCircle:
                PaintSolidCircle(uv);
                break;
            case BrushShape.LineSweep:
                PaintLineSweep(uv, angle);
                break;
            case BrushShape.CrossHatch:
                PaintCrossHatch(uv, angle);
                break;
            case BrushShape.Block:
                PaintBlockStroke(uv, angle);
                break;
            case BrushShape.Tornado:
                PaintTornado(uv);
                break;
            case BrushShape.SCurve:
                PaintSCurve(uv, angle);
                break;
            case BrushShape.WavyLine:
                PaintWavyLine(uv, angle);
                break;
            case BrushShape.IvyVine:
                PaintIvyVine(uv, angle);
                break;
            case BrushShape.ScaleStack:
                PaintScaleStack(uv, angle);
                break;
            case BrushShape.Feather:
                PaintFeather(uv, angle);
                break;
            case BrushShape.Vibration:
                PaintVibration(uv, angle);
                break;
        }

        // 100回ごとにログ出力
        if (debugLog && totalBrushStrokes % 100 == 0)
        {
            Debug.Log($"[CylinderPolisher] ブラシ描画累計: {totalBrushStrokes} 回, 最新UV: ({uv.x:F3}, {uv.y:F3})");
        }
    }

    // ====== マスク操作 ======
    void ClearMask()
    {
        Color32[] buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 255);
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
        Debug.Log("[CylinderPolisher] マスクをクリアしました");
    }

    void FillWhite()
    {
        Color32[] buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(255, 255, 255, 255);
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
        Debug.Log("[CylinderPolisher] マスクを白で塗りつぶしました");
    }

    public void ResetMask()
    {
        if (maskTex == null) return;
        ClearMask();
    }

    void DebugMaskStatus()
    {
        if (maskTex == null)
        {
            Debug.Log("[CylinderPolisher] マスクテクスチャが未作成です");
            return;
        }

        Color[] pixels = maskTex.GetPixels();
        int totalPixels = pixels.Length;
        int polishedPixels = 0;
        float maxValue = 0f;
        float totalValue = 0f;

        for (int i = 0; i < pixels.Length; i++)
        {
            float v = pixels[i].r;
            if (v > 0.01f) polishedPixels++;
            if (v > maxValue) maxValue = v;
            totalValue += v;
        }

        float avgValue = totalValue / totalPixels;
        float polishedPercent = (float)polishedPixels / totalPixels * 100f;

        Debug.Log($"[CylinderPolisher] === マスクテクスチャ状態 ===");
        Debug.Log($"  サイズ: {maskSize}x{maskSize} ({totalPixels} pixels)");
        Debug.Log($"  研磨済みピクセル: {polishedPixels} ({polishedPercent:F2}%)");
        Debug.Log($"  最大値: {maxValue:F3}");
        Debug.Log($"  平均値: {avgValue:F6}");
        Debug.Log($"  ブラシ累計: {totalBrushStrokes} 回");

        if (polishedPixels == 0)
        {
            Debug.LogWarning("[CylinderPolisher] マスクに研磨痕がありません！ブラシが描画されていない可能性があります。");
        }
    }

    // ====== ユーティリティ ======
    float Rand(float a, float b) => (float)(a + (b - a) * rng.NextDouble());

    float GetNoise(int x, int y)
    {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    // ====== ブラシ実装 ======

    // Swirl（半月の刷毛：エンジンターン）
    void PaintSwirlAtUV(Vector2 uv)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * (W - 1));
        int cy = Mathf.RoundToInt(Mathf.Clamp01(uv.y) * (H - 1));

        float r0 = swirlRadiusUv * W * Rand(0.85f, 1.15f);
        float sigmaR = Mathf.Max(1f, r0 * 0.25f);
        float arcLen = Mathf.PI * Mathf.Clamp(arcLenPi, 0.05f, 1.2f);
        float angle = Rand(-Mathf.PI, Mathf.PI);

        int rPx = Mathf.CeilToInt(r0 + sigmaR * 2f);
        int x0 = Mathf.Max(0, cx - rPx), x1 = Mathf.Min(W - 1, cx + rPx);
        int y0 = Mathf.Max(0, cy - rPx), y1 = Mathf.Min(H - 1, cy + rPx);
        int w = x1 - x0 + 1, h = y1 - y0 + 1;

        Color[] block = maskTex.GetPixels(x0, y0, w, h);
        float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);

        for (int j = 0; j < h; j++)
        {
            int y = y0 + j;
            for (int i = 0; i < w; i++)
            {
                int x = x0 + i;
                float dx = x - cx, dy = y - cy;

                float rx = ca * dx + sa * dy;
                float ry = -sa * dx + ca * dy;
                float ex = rx;
                float ey = ry * Mathf.Max(0.0001f, ellipseYScale);

                float r = Mathf.Sqrt(ex * ex + ey * ey);
                float th = Mathf.Atan2(ey, ex);

                float thWrap = Mathf.Repeat(th + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
                float arcMask = Mathf.Clamp01(1f - Mathf.Abs(thWrap) / (arcLen * 0.5f));
                if (arcMask <= 0f) continue;

                float ring = Mathf.Exp(-0.5f * Mathf.Pow((r - r0) / sigmaR, 2f));
                float stripes = 0.5f + 0.5f * Mathf.Cos(th * stripesFreq);
                float outer = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((r - (r0 + sigmaR)) / sigmaR));

                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float add = swirlStrength * arcMask * ring * stripes * Mathf.Max(outer, 0.2f) * noise;
                int idx = j * w + i;
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + add);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        }
        maskTex.SetPixels(x0, y0, w, h, block);
        maskTex.Apply(false, false);
    }

    // Solid Circle（円スタンプ）
    void PaintSolidCircle(Vector2 uv)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));
        int r = Mathf.CeilToInt(circleRadiusUv * W);

        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;
        int r2 = r * r;

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int lx = x - cx, ly = y - cy;
                if (lx * lx + ly * ly > r2) continue;
                int idx = (y - y0) * w + (x - x0);
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + circleStrength * noise);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Line Sweep（直線ヘアライン）
    void PaintLineSweep(Vector2 uv, float angleRad)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));
        float halfLenPx = 0.5f * lineLengthUv * W;
        float halfWidPx = 0.5f * lineWidthUv * W;

        int r = Mathf.CeilToInt(Mathf.Max(halfLenPx, halfWidPx) * 1.6f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        float ca = Mathf.Cos(angleRad), sa = Mathf.Sin(angleRad);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ex = ca * dx + sa * dy;
                float ey = -sa * dx + ca * dy;

                float along = Mathf.Abs(ex) / halfLenPx;
                float across = Mathf.Abs(ey) / halfWidPx;
                if (along > 1f || across > 1f) continue;

                float fall = Mathf.Exp(-2.5f * (along * along + across * across));
                int idx = (y - y0) * w + (x - x0);
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + lineStrength * fall * noise);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Cross Hatch（直交2方向）
    void PaintCrossHatch(Vector2 uv, float baseAngleRad)
    {
        PaintLineSweep(uv, baseAngleRad);
        PaintLineSweep(uv, baseAngleRad + crossAngleDeg * Mathf.Deg2Rad);
    }

    // Block Stroke（アンティークヘアー）
    void PaintBlockStroke(Vector2 uv, float angleRad)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        int r = Mathf.CeilToInt(Mathf.Max(blockLengthUv, blockWidthUv) * W * 1.5f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        float halfLen = 0.5f * blockLengthUv * W;
        float halfWid = 0.5f * blockWidthUv * W;
        float ca = Mathf.Cos(angleRad), sa = Mathf.Sin(angleRad);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ex = ca * dx + sa * dy;
                float ey = -sa * dx + ca * dy;

                if (Mathf.Abs(ex) > halfLen || Mathf.Abs(ey) > halfWid) continue;

                float along = Mathf.Abs(ex) / halfLen;
                float across = Mathf.Abs(ey) / halfWid;
                float fall = Mathf.Exp(-3f * (along * along + across * across));

                int idx = (y - y0) * w + (x - x0);
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + blockStrength * fall * noise);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Tornado（トルネード：大渦巻き）
    void PaintTornado(Vector2 uv)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * (W - 1));
        int cy = Mathf.RoundToInt(Mathf.Clamp01(uv.y) * (H - 1));

        float radiusPx = tornadoRadiusUv * W * Rand(0.9f, 1.1f);
        int rPx = Mathf.CeilToInt(radiusPx * 1.3f);
        int x0 = Mathf.Max(0, cx - rPx), x1 = Mathf.Min(W - 1, cx + rPx);
        int y0 = Mathf.Max(0, cy - rPx), y1 = Mathf.Min(H - 1, cy + rPx);
        int w = x1 - x0 + 1, h = y1 - y0 + 1;

        Color[] block = maskTex.GetPixels(x0, y0, w, h);
        float angleOffset = Rand(-Mathf.PI, Mathf.PI);

        for (int j = 0; j < h; j++)
        {
            int y = y0 + j;
            for (int i = 0; i < w; i++)
            {
                int x = x0 + i;
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float th = Mathf.Atan2(dy, dx);

                if (r > radiusPx) continue;

                float normalizedR = r / radiusPx;
                float spiralPhase = th + normalizedR * tornadoSpirals * Mathf.PI * 2f + angleOffset;
                float spiralLine = 0.5f + 0.5f * Mathf.Cos(spiralPhase * 4f);
                float radialFade = Mathf.Pow(1f - normalizedR, tornadoFadeOut);

                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float add = tornadoStrength * spiralLine * radialFade * noise;

                int idx = j * w + i;
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + add);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        }
        maskTex.SetPixels(x0, y0, w, h, block);
        maskTex.Apply(false, false);
    }

    // S-Curve（オーロラ：S字カーブヘアライン）
    void PaintSCurve(Vector2 uv, float angleRad)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float halfLenPx = 0.5f * auroraLengthUv * W;
        float halfWidPx = 0.5f * auroraWidthUv * W;
        float curvePx = auroraCurvature * halfWidPx * 8f;

        int r = Mathf.CeilToInt(Mathf.Max(halfLenPx, halfWidPx + curvePx) * 1.5f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        float ca = Mathf.Cos(angleRad), sa = Mathf.Sin(angleRad);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ex = ca * dx + sa * dy;
                float ey = -sa * dx + ca * dy;

                float t = ex / halfLenPx;
                if (Mathf.Abs(t) > 1f) continue;

                float curveOffset = Mathf.Sin(t * Mathf.PI) * curvePx;
                float distFromCurve = Mathf.Abs(ey - curveOffset);

                if (distFromCurve > halfWidPx * 2f) continue;

                float along = Mathf.Abs(t);
                float across = distFromCurve / halfWidPx;
                float fall = Mathf.Exp(-2f * (along * along * 0.5f + across * across));

                int idx = (y - y0) * w + (x - x0);
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + auroraStrength * fall * noise);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Wavy Line（サンダー：湾曲・交差するライン）
    void PaintWavyLine(Vector2 uv, float angleRad)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float halfLenPx = 0.5f * sanderLengthUv * W;
        float halfWidPx = 0.5f * sanderWidthUv * W;
        float waveAmpPx = sanderWaveAmp * W;

        int r = Mathf.CeilToInt(Mathf.Max(halfLenPx, halfWidPx + waveAmpPx) * 1.5f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        float angle1 = angleRad + Rand(-0.2f, 0.2f);
        float angle2 = angleRad + Mathf.PI * 0.4f + Rand(-0.2f, 0.2f);

        for (int pass = 0; pass < 2; pass++)
        {
            float a = (pass == 0) ? angle1 : angle2;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float phaseOffset = Rand(0, Mathf.PI * 2f);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float ex = ca * dx + sa * dy;
                    float ey = -sa * dx + ca * dy;

                    float t = ex / halfLenPx;
                    if (Mathf.Abs(t) > 1f) continue;

                    float waveOffset = Mathf.Sin(t * sanderWaveFreq * Mathf.PI + phaseOffset) * waveAmpPx;
                    float distFromWave = Mathf.Abs(ey - waveOffset);

                    if (distFromWave > halfWidPx * 2f) continue;

                    float along = Mathf.Abs(t);
                    float across = distFromWave / halfWidPx;
                    float fall = Mathf.Exp(-2f * (along * along * 0.3f + across * across));

                    int idx = (y - y0) * w + (x - x0);
                    float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                    float old = block[idx].r;
                    float nw = Mathf.Clamp01(old + sanderStrength * fall * noise * 0.7f);
                    block[idx] = new Color(nw, nw, nw, 1f);
                }
        }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Ivy Vine（アイビー：蔦が絡み合うパターン）
    void PaintIvyVine(Vector2 uv, float baseAngle)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float halfLenPx = 0.5f * ivyLengthUv * W;
        float halfWidPx = 0.5f * ivyWidthUv * W;

        int r = Mathf.CeilToInt(halfLenPx * 1.5f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        for (int branch = 0; branch < ivyBranches; branch++)
        {
            float branchAngle = baseAngle + (branch - ivyBranches / 2f) * 0.8f + Rand(-0.3f, 0.3f);
            float curvePhase = Rand(0, Mathf.PI * 2f);
            float curveDir = (branch % 2 == 0) ? 1f : -1f;

            float ca = Mathf.Cos(branchAngle), sa = Mathf.Sin(branchAngle);
            float curveAmpPx = ivyCurviness * halfLenPx * 0.3f;

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float ex = ca * dx + sa * dy;
                    float ey = -sa * dx + ca * dy;

                    float t = ex / halfLenPx;
                    if (Mathf.Abs(t) > 1f) continue;

                    float curveFactor = Mathf.Sin(t * Mathf.PI * ivyCurviness + curvePhase) * curveDir;
                    float curveOffset = curveFactor * curveAmpPx;
                    float twist = Mathf.Sin(t * Mathf.PI * 2f + curvePhase * 0.5f) * curveAmpPx * 0.3f;
                    curveOffset += twist;

                    float distFromCurve = Mathf.Abs(ey - curveOffset);

                    if (distFromCurve > halfWidPx * 3f) continue;

                    float taper = 1f - Mathf.Abs(t) * 0.5f;
                    float effectiveWidth = halfWidPx * taper;

                    float across = distFromCurve / Mathf.Max(effectiveWidth, 0.1f);
                    float fall = Mathf.Exp(-2f * across * across);
                    float alongFade = 1f - Mathf.Pow(Mathf.Abs(t), 3f);

                    int idx = (y - y0) * w + (x - x0);
                    float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                    float old = block[idx].r;
                    float add = ivyStrength * fall * alongFade * noise * (0.5f + 0.5f / ivyBranches);
                    float nw = Mathf.Clamp01(old + add);
                    block[idx] = new Color(nw, nw, nw, 1f);
                }
        }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Random Scale（ランダムスケール：多層の鱗）
    void PaintScaleStack(Vector2 uv, float baseAngle)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float scaleRadiusPx = scaleSizeUv * W * 0.5f;
        int r = Mathf.CeilToInt(scaleRadiusPx * 2.5f);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        int layers = scaleLayerCount;

        for (int l = 0; l < layers; l++)
        {
            float layerOffset = (l * 1.5f * scaleRadiusPx);
            float dxL = -Mathf.Cos(baseAngle) * layerOffset;
            float dyL = -Mathf.Sin(baseAngle) * layerOffset;

            int count = Mathf.RoundToInt(5 * scaleDensity);
            for (int k = 0; k < count; k++)
            {
                float angleVar = Rand(-0.5f, 0.5f);
                float thisAngle = baseAngle + angleVar;
                float distVar = Rand(-0.5f, 0.5f) * scaleRadiusPx;

                float ox = dxL + Mathf.Cos(thisAngle + Mathf.PI*0.5f) * distVar;
                float oy = dyL + Mathf.Sin(thisAngle + Mathf.PI*0.5f) * distVar;

                float drawCx = cx + ox;
                float drawCy = cy + oy;

                float ca = Mathf.Cos(thisAngle), sa = Mathf.Sin(thisAngle);

                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - drawCx, dy = y - drawCy;
                    float ex = ca * dx + sa * dy;
                    float ey = -sa * dx + ca * dy;

                    float distSq = dx*dx + dy*dy;
                    float dist = Mathf.Sqrt(distSq);

                    float rim = Mathf.Abs(dist - scaleRadiusPx);
                    if (rim < 2f && ex < scaleRadiusPx * 0.5f)
                    {
                        float fall = Mathf.Max(0, 1f - rim / 2f);

                        int idx = (y - y0) * w + (x - x0);
                        float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                        float add = 0.8f * fall * noise * (1f / layers);
                        float old = block[idx].r;
                        block[idx].r = Mathf.Clamp01(old + add);
                        block[idx].g = block[idx].r;
                        block[idx].b = block[idx].r;
                    }
                }
            }
        }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Feather（フェザー：羽毛）
    void PaintFeather(Vector2 uv, float baseAngle)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float lenPx = featherLengthUv * W;
        float widPx = featherWidthUv * W;

        int r = Mathf.CeilToInt(lenPx);
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(W - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(H - 1, cy + r);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        float hitStr = 0.5f;
        float curveBend = featherCurve * 0.5f;

        float ca2 = Mathf.Cos(baseAngle), sa2 = Mathf.Sin(baseAngle);

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = x - cx, dy = y - cy;
            float ex = ca2 * dx + sa2 * dy;
            float ey = -sa2 * dx + ca2 * dy;

            float t = ex / (lenPx * 0.5f);
            if (Mathf.Abs(t) > 1f) continue;

            float curveOffset = (t * t) * lenPx * curveBend;
            float distFromShaft = ey - curveOffset;

            float barbDir = Mathf.Sign(distFromShaft);
            float barbFactor = (ex + distFromShaft * barbDir * 2f);

            float barbPat = Mathf.Sin(barbFactor * 1.5f);

            float shape = 1f - (t*t + (distFromShaft*distFromShaft)/(widPx*widPx)*4f);
            if (shape < 0) continue;

            if (barbPat > 0)
            {
                int idx = (y - y0) * w + (x - x0);
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float val = hitStr * shape * noise;
                float old = block[idx].r;
                block[idx].r = Mathf.Clamp01(old + val);
                block[idx].g = block[idx].r;
                block[idx].b = block[idx].r;
            }
        }

        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // Vibration (バイブレーション：無方向マット)
    void PaintVibration(Vector2 uv, float baseAngle)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float rPx = vibrioRadiusUv * W;

        int count = Mathf.RoundToInt(20 * vibrioDensity);

        float scatterR = rPx * 2f;
        int scatterRi = Mathf.CeilToInt(scatterR);

        int x0 = Mathf.Max(0, cx - scatterRi), x1 = Mathf.Min(W - 1, cx + scatterRi);
        int y0 = Mathf.Max(0, cy - scatterRi), y1 = Mathf.Min(H - 1, cy + scatterRi);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        for (int i = 0; i < count; i++)
        {
            Vector2 rndPos = Random.insideUnitCircle * scatterR;

            int px = Mathf.RoundToInt(rndPos.x);
            int py = Mathf.RoundToInt(rndPos.y);

            int bx = (cx + px) - x0;
            int by = (cy + py) - y0;

            if (bx >= 0 && bx < w && by >= 0 && by < (y1-y0+1))
            {
                 int idx = by * w + bx;
                 float old = block[idx].r;
                 float add = 0.3f * abrasiveJitter;
                 block[idx].r = Mathf.Clamp01(old + add);
                 block[idx].g = block[idx].r;
                 block[idx].b = block[idx].r;
            }
        }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!grindingCollider) return;

        // コライダーのバウンディングボックスを描画
        Gizmos.color = Color.green;
        Bounds bounds = grindingCollider.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        // 研磨面の下方向を表示
        Transform grindTr = grindingCollider.transform;
        Vector3 worldDown = grindTr.TransformDirection(localDownDirection).normalized;
        Vector3 bottomCenter = bounds.center + worldDown * bounds.extents.y;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bounds.center, bottomCenter);
        Gizmos.DrawWireSphere(bottomCenter, 0.01f);

        // サンプル点をプレビュー
        Gizmos.color = Color.yellow;
        Vector3 localRight = grindTr.right;
        Vector3 localForward = grindTr.forward;
        float extentX = bounds.extents.x;
        float extentZ = bounds.extents.z;

        for (int i = 0; i < sampleCountX; i++)
        {
            float tx = (sampleCountX > 1) ? (float)i / (sampleCountX - 1) : 0.5f;
            float offsetX = Mathf.Lerp(-extentX, extentX, tx);

            for (int j = 0; j < sampleCountZ; j++)
            {
                float tz = (sampleCountZ > 1) ? (float)j / (sampleCountZ - 1) : 0.5f;
                float offsetZ = Mathf.Lerp(-extentZ, extentZ, tz);

                Vector3 samplePoint = bottomCenter + localRight * offsetX + localForward * offsetZ;
                Gizmos.DrawWireSphere(samplePoint, 0.002f);
            }
        }

        // ターゲットへの接続
        if (targetCollider)
        {
            Gizmos.color = Color.cyan;
            Vector3 closest = targetCollider.bounds.ClosestPoint(bounds.center);
            Gizmos.DrawLine(bounds.center, closest);
        }
    }
#endif
}
#endif

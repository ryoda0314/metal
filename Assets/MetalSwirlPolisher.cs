// MetalSwirlPolisher.cs  完全版
// 近接UVへ“磨きマスク”を書き足して Metallic / Smoothness を上げる。
// ブラシ: SwirlArc / SolidCircle / LineSweep / CrossHatch / Block(アンティークヘアー風)
// プリセット: 進行方向連動・同心円・スパイラル等 + ★Antique_BlockVelocity(進行方向)

using UnityEngine;

[DisallowMultipleComponent]
public class MetalSwirlPolisher : MonoBehaviour
{
    // ====== 参照 ======
    [Header("Polish Target (assign Cube)")]
    public Renderer targetRenderer;          // Cube / Plane の MeshRenderer
    public MeshCollider targetCollider;      // MeshCollider（BoxCollider不可・Convex OFF）

    // ====== レイと接触 ======
    [Header("Ray / Contact")]
    [Tooltip("Polisher(Sphere等) → Cube に飛ばすレイの長さ")]
    public float rayLength = 0.7f;
    [Tooltip("この距離以内で“磨く”判定（hit.distance 基準）")]
    public float contactDistance = 0.05f;

    // ====== マスク ======
    [Header("Mask Texture")]
    [Range(256, 4096)] public int maskSize = 1024;
    [Tooltip("R=全黒クリア / T=全白デバッグ")]
    public bool enableDebugKeys = true;

    // ====== ブラシ選択 ======
    enum BrushShape { SwirlArc, SolidCircle, LineSweep, CrossHatch, Block }

    // ====== プリセット ======
    public enum PolishPreset
    {
        EngineTurn_RandomSwirl,   // ランダム角の半月ブラシ
        Hairline_Velocity,        // 進行方向に沿った直線ヘアライン
        CrossHatch_Velocity,      // 進行方向＋交差角
        Dots_Stipple,             // 円スタンプ
        Concentric_FromPoint,     // 指定中心から見た“円周接線”
        Spiral_Trail,             // 進行方向にねじりを加えたスパイラル
        Antique_BlockRandom,      // ランダム角の短い矩形
        Antique_BlockVelocity     // ★進行方向に沿うアンティークヘアー
    }

    [Header("Preset")]
    public PolishPreset preset = PolishPreset.EngineTurn_RandomSwirl;
    [Tooltip("Concentric 用の中心（ワールド座標）")]
    public Vector3 concentricCenterWorld = Vector3.zero;
    [Tooltip("Spiral の1スタンプあたりのねじり量（度）")]
    public float spiralTwistDegPerStamp = 15f;

    [Header("Angle Control")]
    [Tooltip("手動オフセット角（度）")] public float angleBiasDeg = 0f;
    [Tooltip("Rigidbody 速度から角度を自動決定")] public bool useVelocityAngle = true;

    [Header("Antique Block (Velocity-aligned)")]
    [Tooltip("進行方向に対する角度ゆらぎ（度）")]
    public float blockAngleJitterDeg = 10f;

    // ---- Swirl（半月の刷毛） ----
    [Header("Swirl (Engine Turn)")]
    [Range(0.005f, 0.08f)] public float swirlRadiusUv = 0.03f;
    [Range(0.05f, 1.00f)] public float swirlStrength = 0.70f;
    [Range(0.30f, 0.95f)] public float arcLenPi = 0.70f; // π倍
    [Range(4f, 28f)] public float stripesFreq = 12f;
    [Range(0.30f, 1.20f)] public float ellipseYScale = 0.60f;

    // ---- Solid Circle ----
    [Header("Solid Circle")]
    [Range(0.005f, 0.08f)] public float circleRadiusUv = 0.035f;
    [Range(0.05f, 1.00f)] public float circleStrength = 0.70f;

    // ---- Line Sweep（直線ヘアライン） ----
    [Header("Line Sweep")]
    [Range(0.005f, 0.15f)] public float lineLengthUv = 0.08f;
    [Range(0.002f, 0.05f)] public float lineWidthUv = 0.008f;
    [Range(0.05f, 1.00f)] public float lineStrength = 0.60f;

    // ---- Cross Hatch（直交2方向） ----
    [Header("Cross Hatch")]
    [Range(0.005f, 0.15f)] public float crossLenUv = 0.06f;
    [Range(0.002f, 0.05f)] public float crossWidUv = 0.008f;
    [Range(0.05f, 1.00f)] public float crossStrength = 0.50f;
    [Range(0f, 90f)] public float crossAngleDeg = 60f;

    // ---- Block Stroke（アンティークヘアー）----
    [Header("Antique Block")]
    [Range(0.01f, 0.10f)] public float blockLengthUv = 0.05f;
    [Range(0.01f, 0.05f)] public float blockWidthUv = 0.03f;
    [Range(0.05f, 1.00f)] public float blockStrength = 0.70f;

    // ====== Shader Property 名（Graph の Reference 名と一致必須） ======
    [Header("Shader Properties (must match Shader Graph Reference)")]
    [SerializeField] string propPolishMask = "_PolishMask";
    [SerializeField] string propBaseSmooth = "_BaseSmoothness";
    [SerializeField] string propPolishedSmooth = "_PolishedSmoothness";
    [SerializeField] string propBaseMetal = "_BaseMetallic";
    [SerializeField] string propPolishedMetal = "_PolishedMetallic";
    [SerializeField] string propBaseColor = "_BaseColor";

    [Header("Default Material Values")]
    [Range(0f, 1f)] public float defaultBaseSmooth = 0.10f;
    [Range(0f, 1f)] public float defaultPolishedSmooth = 0.98f;
    [Range(0f, 1f)] public float defaultBaseMetal = 0.60f;
    [Range(0f, 1f)] public float defaultPolishedMetal = 1.00f;
    public Color defaultBaseColor = new Color(0.48f, 0.48f, 0.50f); // 明るめ金属地

    // ====== 内部 ======
    Texture2D maskTex;
    Material runtimeMat;
    System.Random rng = new System.Random();
    Rigidbody rb;
    float spiralAccum;                 // スパイラル用蓄積角
    Vector3 prevPos;                   // Rigidbody 無いときの速度推定用

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!targetRenderer) Debug.LogWarning("[Polisher] targetRenderer 未設定");
        if (!targetCollider) Debug.LogWarning("[Polisher] targetCollider 未設定（MeshCollider, Convex OFF）");
        prevPos = transform.position;
    }

    void Start()
    {
        if (!targetRenderer) return;

        // 他コンポーネントの PropertyBlock 上書きを無効化
        targetRenderer.SetPropertyBlock(null);

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
        if (runtimeMat.HasProperty(propBaseColor))
            runtimeMat.SetColor(propBaseColor, defaultBaseColor);

        // Property存在チェック
        string[] props = { propPolishMask, propBaseSmooth, propPolishedSmooth, propBaseMetal, propPolishedMetal };
        foreach (var p in props)
            if (!runtimeMat.HasProperty(p))
                Debug.LogError($"[Polisher] Material property not found: {p}");
    }

    void Update()
    {
        if (!targetCollider || runtimeMat == null || maskTex == null) return;

        // 射線：Polisher → Cube 最接近点へ
        Vector3 closest = targetCollider.bounds.ClosestPoint(transform.position);
        Vector3 dir = (closest - transform.position).normalized;

        // ワールド速度（Rigidbody が無ければ前フレーム差分）
        Vector3 velWorld = Vector3.zero;
        if (rb != null)
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
                velWorld = (transform.position - prevPos) / Mathf.Max(Time.deltaTime, 1e-6f);
        }
        prevPos = transform.position;

        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, rayLength, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == targetCollider && hit.distance <= contactDistance)
            {
                Vector2 uv = hit.textureCoord;

                // 接線座標系（t1,t2）を作り、速度を投影
                Vector3 n = hit.normal;
                Vector3 t1 = Vector3.Cross(n, Vector3.up);
                if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(n, Vector3.right);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(n, t1);

                Vector3 velTan = t1 * Vector3.Dot(velWorld, t1) + t2 * Vector3.Dot(velWorld, t2);

                float angle = ComputeAngleForPreset(hit, velTan);
                BrushShape brush; GetBrushForPreset(out brush);

                // ブラシ描画
                switch (brush)
                {
                    case BrushShape.SwirlArc: PaintSwirlAtUV(uv); break;
                    case BrushShape.SolidCircle: PaintSolidCircle(uv); break;
                    case BrushShape.LineSweep: PaintLineSweep(uv, angle); break;
                    case BrushShape.CrossHatch: PaintCrossHatch(uv, angle); break;
                    case BrushShape.Block: PaintBlockStroke(uv, angle); break;
                }
            }
        }

        // デバッグキー
        if (enableDebugKeys)
        {
            if (Input.GetKeyDown(KeyCode.R)) ClearMask(); // 全黒
            if (Input.GetKeyDown(KeyCode.T)) FillWhite(); // 全白
        }
    }

    // ====== プリセット→角度/ブラシ決定 ======
    float ComputeAngleForPreset(RaycastHit hit, Vector3 velocityTangent)
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
                return angle; // Cross側で交差方向も描く

            case PolishPreset.Concentric_FromPoint:
                {
                    Vector3 p = hit.point;
                    Vector3 c = concentricCenterWorld;
                    Vector3 t = (p - c);
                    if (t.sqrMagnitude < 1e-6f) t = Vector3.right;

                    // 接線基底へ投影
                    Vector3 n = hit.normal;
                    Vector3 e1 = Vector3.Cross(n, Vector3.up);
                    if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.Cross(n, Vector3.right);
                    e1.Normalize();
                    Vector3 e2 = Vector3.Cross(n, e1);

                    float rx = Vector3.Dot(t, e1);
                    float ry = Vector3.Dot(t, e2);
                    float radialAngle = Mathf.Atan2(ry, rx);
                    float tangentAngle = radialAngle + Mathf.PI * 0.5f; // 円周接線
                    return tangentAngle + angle;
                }

            case PolishPreset.Spiral_Trail:
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                spiralAccum += spiralTwistDegPerStamp * Mathf.Deg2Rad;
                return angle + spiralAccum;

            case PolishPreset.Antique_BlockVelocity: // ★進行方向に沿うアンティークヘアー
                {
                    float a = angle;
                    if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                        a = Mathf.Atan2(velocityTangent.z, velocityTangent.x);

                    float jitter = blockAngleJitterDeg * Mathf.Deg2Rad;
                    a += Random.Range(-jitter, jitter);
                    return a;
                }

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
            case PolishPreset.Concentric_FromPoint: brush = BrushShape.LineSweep; break; // SwirlでもOK
            case PolishPreset.Spiral_Trail: brush = BrushShape.LineSweep; break; // SwirlでもOK
            case PolishPreset.Antique_BlockVelocity: brush = BrushShape.Block; break; // ★追加
            case PolishPreset.Antique_BlockRandom: brush = BrushShape.Block; break;
            default: brush = BrushShape.SwirlArc; break;
        }
    }

    // ====== 共通ユーティリティ ======
    void ClearMask()
    {
        Color32[] buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 255); // 黒＝未研磨
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
    }
    void FillWhite()
    {
        Color32[] buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(255, 255, 255, 255); // 白＝全面研磨
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
    }
    float Rand(float a, float b) => (float)(a + (b - a) * rng.NextDouble());

    // ====== ブラシ実装 ======

    // A) Swirl（半月の刷毛：エンジンターン）
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

                float rx = ca * dx + sa * dy;   // 回転
                float ry = -sa * dx + ca * dy;
                float ex = rx;
                float ey = ry * Mathf.Max(0.0001f, ellipseYScale); // 楕円つぶし

                float r = Mathf.Sqrt(ex * ex + ey * ey);
                float th = Mathf.Atan2(ey, ex);

                float thWrap = Mathf.Repeat(th + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
                float arcMask = Mathf.Clamp01(1f - Mathf.Abs(thWrap) / (arcLen * 0.5f));
                if (arcMask <= 0f) continue;

                float ring = Mathf.Exp(-0.5f * Mathf.Pow((r - r0) / sigmaR, 2f)); // リング
                float stripes = 0.5f + 0.5f * Mathf.Cos(th * stripesFreq);        // 毛並み
                float outer = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((r - (r0 + sigmaR)) / (sigmaR)));

                float add = swirlStrength * arcMask * ring * stripes * Mathf.Max(outer, 0.2f);
                int idx = j * w + i;
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + add);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        }
        maskTex.SetPixels(x0, y0, w, h, block);
        maskTex.Apply(false, false);
    }

    // B) Solid Circle（円スタンプ）
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
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + circleStrength);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // C) Line Sweep（直線ヘアライン）
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
                float ex = ca * dx + sa * dy;   // 長さ方向
                float ey = -sa * dx + ca * dy;  // 幅方向

                float along = Mathf.Abs(ex) / halfLenPx;
                float across = Mathf.Abs(ey) / halfWidPx;
                if (along > 1f || across > 1f) continue;

                float fall = Mathf.Exp(-2.5f * (along * along + across * across));
                int idx = (y - y0) * w + (x - x0);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + lineStrength * fall);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // D) Cross Hatch（直交2方向）
    void PaintCrossHatch(Vector2 uv, float baseAngleRad)
    {
        PaintLineSweep(uv, baseAngleRad);
        PaintLineSweep(uv, baseAngleRad + crossAngleDeg * Mathf.Deg2Rad);
    }

    // E) Block Stroke（アンティークヘアー）
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
                float ex = ca * dx + sa * dy;   // 長さ方向
                float ey = -sa * dx + ca * dy;  // 幅方向

                if (Mathf.Abs(ex) > halfLen || Mathf.Abs(ey) > halfWid) continue;

                float along = Mathf.Abs(ex) / halfLen;
                float across = Mathf.Abs(ey) / halfWid;
                float fall = Mathf.Exp(-3f * (along * along + across * across)); // ソフトエッジ

                int idx = (y - y0) * w + (x - x0);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + blockStrength * fall);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!targetCollider) return;
        Gizmos.color = Color.cyan;
        Vector3 closest = targetCollider.bounds.ClosestPoint(transform.position);
        Gizmos.DrawLine(transform.position, closest);
        Gizmos.DrawWireSphere(closest, 0.01f);
    }
#endif
}

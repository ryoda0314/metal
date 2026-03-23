// MetalSwirlPolisher.cs  完全版
// 近接UVへ”磨きマスク”を書き足して Metallic / Smoothness を上げる。
// ブラシ: SwirlArc / SolidCircle / LineSweep / CrossHatch / Block(アンティークヘアー風)
// プリセット: 進行方向連動・同心円・スパイラル等 + ★Antique_BlockVelocity(進行方向)

using UnityEngine;
using System.Collections;
#if UNITY_ANDROID
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#else
using Valve.VR.InteractionSystem;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // Fix: Run AFTER CubeController to enforce surface constraint
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
    [Tooltip("この距離以内で“磨く”判定（hit.distance 基準） -> 半径 + マージン")]
    public float polishMargin = 0.015f; // 反発が起きる直前から反応させる（1.5cm猶予）

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
        Concentric_FromPoint,     // 指定中心から見た“円周接線”
        Spiral_Trail,             // 進行方向にねじりを加えたスパイラル
        Antique_BlockRandom,      // ランダム角の短い矩形
        Antique_BlockVelocity,    // ★進行方向に沿うアンティークヘアー
        Tornado_Swirl,            // ★トルネード（大きな渦巻き）
        Aurora_SCurve,            // ★オーロラ（S字カーブヘアライン）
        Sander_WavyCross,         // ★サンダー（交差する波線）
        AntiqueStripe_Random,     // ★アンティークストライプ（ランダム方向ヘアライン）
        Ivy_Intertwine,           // ★アイビー（蔦が絡み合う複雑なパターン）
        RandomScale_Layered,      // ★ランダムスケール（多層の鱗模様）
        Feather_Soft,             // ★フェザー（羽毛のような質感）
        Vibration_Matte           // ★バイブレーション（無方向のマット仕上げ）
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

    // ---- Tornado（トルネード：大渦巻き）----
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
    [Range(1, 4)] public int scaleLayerCount = 2; // 重ね塗り回数

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

    [Header("Realism")]
    [Tooltip("ブラシのザラつき（個々の研磨粒子の再現）")]
    [Range(0f, 1f)] public float abrasiveJitter = 0.5f;


    // ====== 内部 ======
    PolisherControllerMount mount; // コントローラー固定スクリプト（あれば Mode B）
    Texture2D maskTex;
    Material runtimeMat;
    System.Random rng = new System.Random();
    Rigidbody rb;
    float spiralAccum;                 // スパイラル用蓄積角
    Vector3 prevPos;                   // Rigidbody 無いときの速度推定用
#if UNITY_ANDROID
    XRGrabInteractable xrGrabInteractable; // XRI用
#else
    Interactable interactable;         // キャッシュ用
#endif
    float lastHapticTime;              // 振動の連続防止用
    
    // === 振動フィードバック ===
    void TriggerHapticFeedback(float duration, float frequency, float amplitude)
    {
        // 振動の連続発生を防ぐ（高頻度で呼ぶので間隔は短く）
        if (Time.time - lastHapticTime < 0.01f) return;
        lastHapticTime = Time.time;

        // mount がある場合は mount 経由で送信
        if (mount != null && mount.isMounted)
        {
            mount.SendHaptic(amplitude, duration);
            return;
        }

#if UNITY_ANDROID
        if (currentXRController != null)
            currentXRController.SendHapticImpulse(amplitude, duration);
#else
        Hand hapticHand = currentHand ?? (interactable != null ? interactable.attachedToHand : null);
        if (hapticHand != null)
            hapticHand.TriggerHapticPulse((ushort)(duration * 1000000f), frequency, amplitude);
#endif
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mount = GetComponent<PolisherControllerMount>(); // マウントがあれば Mode B

#if UNITY_ANDROID
        if (mount == null) // Mode A: グラブ有効
        {
            xrGrabInteractable = GetComponent<XRGrabInteractable>();
            if (xrGrabInteractable == null)
            {
                // 親にある場合（研磨面が子オブジェクト）→ 親の XRGrabInteractable を使う
                xrGrabInteractable = GetComponentInParent<XRGrabInteractable>();
                if (xrGrabInteractable != null)
                {
                    Debug.Log("[Polisher] 親の XRGrabInteractable を使用");
                }
                else
                {
                    // ルートオブジェクトなら自動追加（Rigidbodyは既にあるので安全）
                    xrGrabInteractable = gameObject.AddComponent<XRGrabInteractable>();
                    Debug.Log("[Polisher] XRGrabInteractable を自動追加しました");
                }
            }
        }
        // Mode B（mount あり）: グラブ無効化は mount 側が担当
#else
        interactable = GetComponent<Interactable>();
        if (interactable == null)
            interactable = GetComponentInParent<Interactable>(); // 親にある場合（研磨面が子オブジェクト）
        // Mode B（mount あり）: グラブ無効化は mount 側が担当
#endif
        if (!targetRenderer) Debug.LogWarning("[Polisher] targetRenderer 未設定");
        if (!targetCollider) Debug.LogWarning("[Polisher] targetCollider 未設定（MeshCollider, Convex OFF）");
        prevPos = transform.position;
    }

    void Start()
    {
        Debug.Log($"[Polisher] Start() on {gameObject.name}: targetRenderer={(targetRenderer != null ? targetRenderer.name : "NULL")}, targetCollider={(targetCollider != null ? targetCollider.name : "NULL")}");
        if (!targetRenderer)
        {
            Debug.LogWarning($"[Polisher] Start() aborted: targetRenderer is NULL on {gameObject.name}");
            return;
        }

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
        
        // 当たり判定の自動設定
        SetupCollision();

        // mount が無い場合のみ Mode A（グラブ）のセットアップ
        if (mount == null)
        {
#if UNITY_ANDROID
            // === XR Interaction Toolkit setup (Meta Quest) ===
            if (xrGrabInteractable != null)
            {
                xrGrabInteractable.useDynamicAttach = true;
                xrGrabInteractable.throwOnDetach = false;
                xrGrabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
                xrGrabInteractable.selectMode = InteractableSelectMode.Single;

                xrGrabInteractable.selectEntered.AddListener((SelectEnterEventArgs args) =>
                {
                    var interactorComponent = args.interactorObject as Component;
                    if (interactorComponent != null)
                        currentXRController = interactorComponent.GetComponentInParent<XRBaseController>();
                    Debug.Log("[Polisher] XR Grab started");
                });

                xrGrabInteractable.selectExited.AddListener((SelectExitEventArgs args) =>
                {
                    currentXRController = null;
                    Debug.Log("[Polisher] XR Grab ended. Stopping.");
                    StartCoroutine(ForceStopRoutine());
                });
            }
#else
            // === SteamVR Throwable/Interactable setup ===
            var throwable = GetComponent<Throwable>();
            if (throwable == null) throwable = GetComponentInParent<Throwable>(); // 親にある場合
            if (throwable != null)
            {
                Debug.Log("[Polisher] Throwable found. Configuring stop-on-release.");
                throwable.releaseVelocityStyle = ReleaseStyle.NoChange;
                throwable.attachmentFlags &= ~Hand.AttachmentFlags.SnapOnAttach;
                throwable.attachmentOffset = null;
                throwable.catchingSpeedThreshold = -1;

                throwable.onDetachFromHand.AddListener(() =>
                {
                    Debug.Log("[Polisher] Detached. Force stopping next frame.");
                    StartCoroutine(ForceStopRoutine());
                });

                var throwableInteractable = throwable.GetComponent<Interactable>();
                if (throwableInteractable != null)
                {
                    throwableInteractable.hideHandOnAttach = false;
                    throwableInteractable.hideSkeletonOnAttach = false;
                    throwableInteractable.highlightOnHover = false;
                }
            }
            else
            {
                // interactable は Awake で GetComponentInParent 済み（親にある場合も取得）
                if (interactable != null)
                {
                    Debug.Log("[Polisher] Interactable found. Setting up Hand Visual Decoupling.");

                    interactable.hideHandOnAttach = false;
                    interactable.hideSkeletonOnAttach = false;
                    interactable.highlightOnHover = false;

                    interactable.onAttachedToHand += (hand) =>
                    {
                        currentHand = hand;
                        Transform visualRoot = null;

                        if (hand.mainRenderModel != null)
                            visualRoot = hand.mainRenderModel.transform;
                        else if (hand.skeleton != null)
                            visualRoot = hand.skeleton.transform;

                        if (visualRoot != null)
                        {
                            currentHandVisual = visualRoot;
                            currentHandVisual.SetParent(this.transform, true);
                            Debug.Log($"[Polisher] Hand Attached. Visual Parented to Tool (Root: {visualRoot.name})");
                        }
                        else
                        {
                            Debug.LogError("[Polisher] Hand Attached but NO Visual found. Parenting fix will NOT work.");
                        }
                    };

                    interactable.onDetachedFromHand += (hand) =>
                    {
                        Debug.Log("[Polisher] Detached. Resetting Hand Visual.");
                        if (currentHandVisual != null)
                        {
                            currentHandVisual.SetParent(hand.transform, true);
                            currentHandVisual.localPosition = Vector3.zero;
                            currentHandVisual.localRotation = Quaternion.identity;
                            currentHandVisual.localScale = Vector3.one;
                        }
                        currentHandVisual = null;
                        currentHand = null;
                        StartCoroutine(ForceStopRoutine());
                    };
                }
                else
                {
                    Debug.LogWarning("[Polisher] Neither Throwable nor Interactable component found! Stop-on-release may not work.");
                }
            }

            // ★中心を持たされないようにする (Poserを無効化)
            if (interactable != null)
            {
                interactable.skeletonPoser = null;
                interactable.useHandObjectAttachmentPoint = false;
            }
#endif
        }
        else
        {
            Debug.Log("[Polisher] PolisherControllerMount 検出 — グラブ設定スキップ（コントローラー固定モード）");
        }
    }

    IEnumerator ForceStopRoutine()
    {
        yield return new WaitForFixedUpdate();

        // 自分の GameObject に直接ついた Rigidbody のみ操作する
        // 親の Rigidbody は CubeController2 が管理するので触らない
        if (rb && rb.gameObject == gameObject)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            Debug.Log("[Polisher] Force stopped (isKinematic = true).");
        }
    }

    void SetupCollision()
    {
        // === 球（Polisher）の設定 ===
        // Rigidbody追加（まだなければ）
        if (!rb)
        {
            // 親に Rigidbody がある場合（研磨面が子オブジェクト）は追加しない
            Rigidbody parentRb = GetComponentInParent<Rigidbody>();
            if (parentRb != null)
            {
                rb = parentRb;
                Debug.Log("[Polisher] 親の Rigidbody を使用");
            }
            else
            {
                rb = gameObject.AddComponent<Rigidbody>();
                Debug.Log("[Polisher] Rigidbody added to polisher");
                rb.isKinematic = true;  // VRで手動移動するため
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }
        else if (rb.gameObject == gameObject)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        
        // Collider確認・追加
        Collider polisherCol = GetComponent<Collider>();
        if (!polisherCol)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.05f; // 5cm radius
            Debug.Log("[Polisher] SphereCollider added to polisher");
        }
        else
        {
            polisherCol.isTrigger = false;  // 確実にTriggerをOFF
        }
        
        // === 金属板（Target）の設定 ===
        if (targetCollider)
        {
            targetCollider.isTrigger = false;  // Triggerじゃないことを確認
            targetCollider.convex = false;     // 凹面対応（MeshColliderの場合）
            
            // TargetにもRigidbody（Static扱い）
            Rigidbody targetRb = targetCollider.GetComponent<Rigidbody>();
            if (!targetRb)
            {
                targetRb = targetCollider.gameObject.AddComponent<Rigidbody>();
                Debug.Log("[Polisher] Rigidbody added to target");
            }
            targetRb.isKinematic = true;  // 動かない
            targetRb.useGravity = false;
        }
        
        Debug.Log("[Polisher] Collision setup complete");
    }
    
    // === 押し返し処理 ===
    void OnCollisionStay(Collision collision)
    {
        // 金属板との衝突だけ処理
        if (targetCollider == null) return;
        if (collision.collider != targetCollider) return;
        // mount あり or 子オブジェクト: 位置を自力で変えない
        if (mount != null) return;
        if (rb != null && rb.gameObject != gameObject) return;
        
        // 衝突点から押し出し方向を計算
        foreach (ContactPoint contact in collision.contacts)
        {
            // 衝突法線方向に押し出す
            Vector3 pushDirection = contact.normal;
            float penetration = contact.separation; // 負の値 = めり込み
            
            if (penetration < 0)
            {
                // めり込んだ分だけ押し戻す
                transform.position += pushDirection * (-penetration + 0.001f);
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (targetCollider != null && collision.collider == targetCollider)
        {
            Debug.Log("[Polisher] Contact with metal plate");
        }
    }

    void LateUpdate()
    {
        if (!targetCollider || runtimeMat == null || maskTex == null) 
        {
             Debug.LogError($"[Polisher] LateUpdate Aborted: TargetCol={targetCollider!=null}, Mat={runtimeMat!=null}, Mask={maskTex!=null}");
             return;
        }

        // === 距離計測 & 押し返し & 研磨判定 ===
        // 上方からレイを飛ばす（めり込んでも確実にヒットする）
        Vector3 planeNormal = targetCollider.transform.up;

        float polisherRadius = 0.02f;
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc)
        {
            polisherRadius = sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        }
        Vector3 rayOrigin = transform.position + planeNormal * 0.5f;
        Ray checkRay = new Ray(rayOrigin, -planeNormal);

        // ワールド速度（Rigidbody が無ければ前フレーム差分）
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
                velWorld = (transform.position - prevPos) / Mathf.Max(Time.deltaTime, 1e-6f);
        }
        prevPos = transform.position;

        bool isTouching = false;

        // デバッグ: レイ情報（毎秒1回）
        if (Time.frameCount % 60 == 0)
            Debug.Log($"[Polisher DEBUG] pos={transform.position}, rayOrigin={rayOrigin}, dir={-planeNormal}, polisherRadius={polisherRadius:F3}");

        if (targetCollider.Raycast(checkRay, out RaycastHit hit, 1.0f))
        {
            // 符号付き距離（正=表面の上、負=めり込み）
            float currentDist = Vector3.Dot(transform.position - hit.point, planeNormal);

            // デバッグ: ヒット情報（毎秒1回）
            if (Time.frameCount % 60 == 0)
                Debug.Log($"[Polisher DEBUG] RAY HIT! dist={currentDist:F4}, threshold={polisherRadius + polishMargin:F4}, abs={Mathf.Abs(currentDist):F4}, uv={hit.textureCoord}");

            // 押し返し（ルートオブジェクトのみ）
            bool isChild = (rb != null && rb.gameObject != gameObject);
            if (mount == null && !isChild && currentDist < polisherRadius)
            {
                Vector3 newPos = transform.position + planeNormal * (polisherRadius - currentDist);
                transform.position = newPos;
                currentDist = polisherRadius;
            }

            // 研磨判定: 表面近く（めり込み含む）なら研磨
            if (Mathf.Abs(currentDist) <= (polisherRadius + polishMargin))
            {
                isTouching = true;

                bool canPaint = (mount == null) || mount.isTriggerActive;
                if (canPaint)
                {
                    Vector2 uv = hit.textureCoord;

                    Vector3 n = hit.normal;
                    Vector3 t1 = Vector3.Cross(n, Vector3.up);
                    if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.Cross(n, Vector3.right);
                    t1.Normalize();
                    Vector3 t2 = Vector3.Cross(n, t1);

                    Vector3 velTan = t1 * Vector3.Dot(velWorld, t1) + t2 * Vector3.Dot(velWorld, t2);

                    float angle = ComputeAngleForPreset(hit, velTan);
                    BrushShape brush; GetBrushForPreset(out brush);

                    switch (brush)
                    {
                        case BrushShape.SwirlArc: PaintSwirlAtUV(uv); break;
                        case BrushShape.SolidCircle: PaintSolidCircle(uv); break;
                        case BrushShape.LineSweep: PaintLineSweep(uv, angle); break;
                        case BrushShape.CrossHatch: PaintCrossHatch(uv, angle); break;
                        case BrushShape.Block: PaintBlockStroke(uv, angle); break;
                        case BrushShape.Tornado: PaintTornado(uv); break;
                        case BrushShape.SCurve: PaintSCurve(uv, angle); break;
                        case BrushShape.WavyLine: PaintWavyLine(uv, angle); break;
                        case BrushShape.IvyVine: PaintIvyVine(uv, angle); break;
                        case BrushShape.ScaleStack: PaintScaleStack(uv, angle); break;
                        case BrushShape.Feather: PaintFeather(uv, angle); break;
                        case BrushShape.Vibration: PaintVibration(uv, angle); break;
                    }
                }
            }
        }
        else
        {
            // デバッグ: レイミス（毎秒1回）
            if (Time.frameCount % 60 == 0)
                Debug.LogWarning($"[Polisher DEBUG] RAY MISS! targetCollider={targetCollider.name}, targetPos={targetCollider.transform.position}, polisherPos={transform.position}");
        }

        // === 振動フィードバック (3段階) ===
        bool hasHapticTarget = (mount != null && mount.isMounted);
        if (!hasHapticTarget)
        {
#if UNITY_ANDROID
            hasHapticTarget = (currentXRController != null);
#else
            hasHapticTarget = (currentHand != null || (interactable != null && interactable.attachedToHand != null));
#endif
        }
        if (hasHapticTarget)
        {
            if (isTouching)
            {
                // 2. 触れているとき & 3. 削るとき
                float speed = velWorld.magnitude;
                
                if (speed > 0.02f) 
                {
                    // 3. 削るとき（動いている）: 強く高い振動（ガリガリ感）
                    float amp = Mathf.Clamp(0.2f + speed * 1.5f, 0f, 1f);
                    float freq = Mathf.Clamp(100f + speed * 300f, 100f, 300f); 
                    TriggerHapticFeedback(0.01f, freq, amp);
                }
                else
                {
                    // 2. 触れているとき（止まっている）: 少し強めの低音振動（接触感）
                    TriggerHapticFeedback(0.01f, 50f, 0.15f);
                }
            }
            else
            {
                // 1. 持っているとき（非接触）: 微弱なアイドリング振動
                TriggerHapticFeedback(0.01f, 20f, 0.05f);
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

            case PolishPreset.Tornado_Swirl: // ★トルネード
                return Rand(-Mathf.PI, Mathf.PI); // ランダム角度で描く

            case PolishPreset.Aurora_SCurve: // ★オーロラ
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle + Rand(-0.3f, 0.3f); // 少しランダムな揺れ

            case PolishPreset.Sander_WavyCross: // ★サンダー
                if (useVelocityAngle && velocityTangent.sqrMagnitude > 1e-8f)
                    angle = Mathf.Atan2(velocityTangent.z, velocityTangent.x);
                return angle;

            case PolishPreset.AntiqueStripe_Random: // ★アンティークストライプ
                {
                    float jitter = stripeAngleJitterDeg * Mathf.Deg2Rad;
                    return Rand(-jitter, jitter);
                }

            case PolishPreset.Ivy_Intertwine: // ★アイビー
                return Rand(-Mathf.PI, Mathf.PI);

            case PolishPreset.RandomScale_Layered: // ★ランダムスケール
            case PolishPreset.Feather_Soft:        // ★フェザー
            case PolishPreset.Vibration_Matte:     // ★バイブレーション
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
            case PolishPreset.Tornado_Swirl: brush = BrushShape.Tornado; break; // ★追加
            case PolishPreset.Aurora_SCurve: brush = BrushShape.SCurve; break; // ★追加
            case PolishPreset.Sander_WavyCross: brush = BrushShape.WavyLine; break; // ★追加
            case PolishPreset.AntiqueStripe_Random: brush = BrushShape.LineSweep; break; // ★追加
            case PolishPreset.Ivy_Intertwine: brush = BrushShape.IvyVine; break;
            case PolishPreset.RandomScale_Layered: brush = BrushShape.ScaleStack; break;
            case PolishPreset.Feather_Soft: brush = BrushShape.Feather; break;
            case PolishPreset.Vibration_Matte: brush = BrushShape.Vibration; break;
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

    // ====== Editor用 公開メソッド ======
    /// <summary>マスクが初期化済みか（外部から安全に呼べるか判定用）</summary>
    public bool IsReady => maskTex != null;

    /// <summary>研磨マスクをリセット（黒＝未研磨状態）</summary>
    public void ResetMask()
    {
        if (maskTex == null) return;
        ClearMask();
        Debug.Log($"[Polisher] マスクをリセットしました on {gameObject.name}");
    }

    /// <summary>研磨マスクを全面研磨（白＝研磨済み状態）</summary>
    public void FillMaskWhite()
    {
        if (maskTex == null) return;
        FillWhite();
        Debug.Log($"[Polisher] 全面を研磨済みにしました on {gameObject.name}");
    }

    /// <summary>現在のプリセットで全面を塗りつぶす</summary>
    public void FillWithPreset()
    {
        if (maskTex == null) return;

        ClearMask();

        BrushShape brush;
        GetBrushForPreset(out brush);

        // ブラシサイズに基づくステップ幅を決定
        float step = GetFillStepForBrush(brush);

        int count = 0;
        for (float uy = step * 0.5f; uy < 1f; uy += step)
        {
            for (float ux = step * 0.5f; ux < 1f; ux += step)
            {
                Vector2 uv = new Vector2(ux, uy);
                float angle = ComputeAngleForFill(uv);

                switch (brush)
                {
                    case BrushShape.SwirlArc:    PaintSwirlAtUV(uv); break;
                    case BrushShape.SolidCircle:  PaintSolidCircle(uv); break;
                    case BrushShape.LineSweep:    PaintLineSweep(uv, angle); break;
                    case BrushShape.CrossHatch:   PaintCrossHatch(uv, angle); break;
                    case BrushShape.Block:        PaintBlockStroke(uv, angle); break;
                    case BrushShape.Tornado:      PaintTornado(uv); break;
                    case BrushShape.SCurve:       PaintSCurve(uv, angle); break;
                    case BrushShape.WavyLine:     PaintWavyLine(uv, angle); break;
                    case BrushShape.IvyVine:      PaintIvyVine(uv, angle); break;
                    case BrushShape.ScaleStack:   PaintScaleStack(uv, angle); break;
                    case BrushShape.Feather:      PaintFeather(uv, angle); break;
                    case BrushShape.Vibration:    PaintVibration(uv, angle); break;
                }
                count++;
            }
        }
        Debug.Log($"[Polisher] FillWithPreset: {preset}, strokes={count}");
    }

    float ComputeAngleForFill(Vector2 uv)
    {
        float angle = angleBiasDeg * Mathf.Deg2Rad;

        switch (preset)
        {
            case PolishPreset.EngineTurn_RandomSwirl:
            case PolishPreset.Tornado_Swirl:
            case PolishPreset.Ivy_Intertwine:
            case PolishPreset.RandomScale_Layered:
            case PolishPreset.Feather_Soft:
            case PolishPreset.Vibration_Matte:
            case PolishPreset.Dots_Stipple:
            case PolishPreset.Antique_BlockRandom:
                return Rand(-Mathf.PI, Mathf.PI);

            case PolishPreset.Hairline_Velocity:
            case PolishPreset.Sander_WavyCross:
            case PolishPreset.CrossHatch_Velocity:
                return angle;

            case PolishPreset.Concentric_FromPoint:
            {
                float dx = uv.x - 0.5f;
                float dy = uv.y - 0.5f;
                return Mathf.Atan2(dy, dx) + Mathf.PI * 0.5f + angle;
            }

            case PolishPreset.Spiral_Trail:
                return angle + Mathf.Atan2(uv.y - 0.5f, uv.x - 0.5f);

            case PolishPreset.Antique_BlockVelocity:
                return angle + Rand(-blockAngleJitterDeg * Mathf.Deg2Rad, blockAngleJitterDeg * Mathf.Deg2Rad);

            case PolishPreset.Aurora_SCurve:
                return angle + Rand(-0.3f, 0.3f);

            case PolishPreset.AntiqueStripe_Random:
                return Rand(-stripeAngleJitterDeg * Mathf.Deg2Rad, stripeAngleJitterDeg * Mathf.Deg2Rad);

            default:
                return Rand(-Mathf.PI, Mathf.PI);
        }
    }

    float GetFillStepForBrush(BrushShape brush)
    {
        switch (brush)
        {
            case BrushShape.SwirlArc:    return swirlRadiusUv * 1.2f;
            case BrushShape.SolidCircle: return circleRadiusUv * 1.2f;
            case BrushShape.LineSweep:   return lineLengthUv * 0.4f;
            case BrushShape.CrossHatch:  return crossLenUv * 0.5f;
            case BrushShape.Block:       return blockLengthUv * 0.6f;
            case BrushShape.Tornado:     return tornadoRadiusUv * 0.8f;
            case BrushShape.SCurve:      return auroraLengthUv * 0.3f;
            case BrushShape.WavyLine:    return sanderLengthUv * 0.4f;
            case BrushShape.IvyVine:     return ivyLengthUv * 0.4f;
            case BrushShape.ScaleStack:  return scaleSizeUv * 1.0f;
            case BrushShape.Feather:     return featherLengthUv * 0.4f;
            case BrushShape.Vibration:   return vibrioRadiusUv * 1.5f;
            default: return 0.03f;
        }
    }

    float Rand(float a, float b) => (float)(a + (b - a) * rng.NextDouble());
    float GetNoise(int x, int y)
    {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

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
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + circleStrength * noise);
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
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + lineStrength * fall * noise);
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
                float noise = Mathf.Lerp(1f, GetNoise(x, y), abrasiveJitter);
                float old = block[idx].r;
                float nw = Mathf.Clamp01(old + blockStrength * fall * noise);
                block[idx] = new Color(nw, nw, nw, 1f);
            }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // F) Tornado（トルネード：大きな渦巻きパターン）
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

                // スパイラル状のラインを描く
                float normalizedR = r / radiusPx;
                float spiralPhase = th + normalizedR * tornadoSpirals * Mathf.PI * 2f + angleOffset;
                float spiralLine = 0.5f + 0.5f * Mathf.Cos(spiralPhase * 4f); // 複数の渦線

                // 外側に向かってフェードアウト
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

    // G) S-Curve（オーロラ：S字カーブヘアライン）
    void PaintSCurve(Vector2 uv, float angleRad)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float halfLenPx = 0.5f * auroraLengthUv * W;
        float halfWidPx = 0.5f * auroraWidthUv * W;
        float curvePx = auroraCurvature * halfWidPx * 8f; // カーブの振幅

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
                // 回転適用
                float ex = ca * dx + sa * dy;   // 長さ方向
                float ey = -sa * dx + ca * dy;  // 幅方向

                // 長さ方向に沿ったS字カーブを計算
                float t = ex / halfLenPx; // -1 to 1
                if (Mathf.Abs(t) > 1f) continue;

                // S字カーブ: sin関数でカーブを作る
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

    // H) Wavy Line（サンダー：湾曲・交差するライン）
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

        // 2本の交差するラインを描く
        float angle1 = angleRad + Rand(-0.2f, 0.2f);
        float angle2 = angleRad + Mathf.PI * 0.4f + Rand(-0.2f, 0.2f); // 約72度ずらす

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

                    // 波形のオフセット
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

    // I) Ivy Vine（アイビー：蔦が絡み合うパターン）
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

        // 複数の蔦を描く（分岐）
        for (int branch = 0; branch < ivyBranches; branch++)
        {
            // 各蔦に異なる角度とカーブを与える
            float branchAngle = baseAngle + (branch - ivyBranches / 2f) * 0.8f + Rand(-0.3f, 0.3f);
            float curvePhase = Rand(0, Mathf.PI * 2f);
            float curveDir = (branch % 2 == 0) ? 1f : -1f; // 交互に曲がる方向を変える
            
            float ca = Mathf.Cos(branchAngle), sa = Mathf.Sin(branchAngle);
            float curveAmpPx = ivyCurviness * halfLenPx * 0.3f;

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float ex = ca * dx + sa * dy;   // 長さ方向
                    float ey = -sa * dx + ca * dy;  // 幅方向

                    float t = ex / halfLenPx; // -1 to 1
                    if (Mathf.Abs(t) > 1f) continue;

                    // S字カーブ + サイン波でより有機的な蔦を表現
                    float curveFactor = Mathf.Sin(t * Mathf.PI * ivyCurviness + curvePhase) * curveDir;
                    float curveOffset = curveFactor * curveAmpPx;
                    
                    // 蔦が絡み合う感じを出すため、二次成分も追加
                    float twist = Mathf.Sin(t * Mathf.PI * 2f + curvePhase * 0.5f) * curveAmpPx * 0.3f;
                    curveOffset += twist;

                    float distFromCurve = Mathf.Abs(ey - curveOffset);

                    if (distFromCurve > halfWidPx * 3f) continue;

                    // 蔦は先端に向かって細くなる
                    float taper = 1f - Mathf.Abs(t) * 0.5f;
                    float effectiveWidth = halfWidPx * taper;
                    
                    float across = distFromCurve / Mathf.Max(effectiveWidth, 0.1f);
                    float fall = Mathf.Exp(-2f * across * across);

                    // 端に向かってフェードアウト
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

    // J) Random Scale（ランダムスケール：多層の鱗）
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
            // 各レイヤーで少しずらす
            float layerOffset = (l * 1.5f * scaleRadiusPx);
            // 進行方向反対側に層を作るイメージ
            float dxL = -Mathf.Cos(baseAngle) * layerOffset;
            float dyL = -Mathf.Sin(baseAngle) * layerOffset;

            // 密度に応じてランダムに鱗を配置
            int count = Mathf.RoundToInt(5 * scaleDensity);
            for (int k = 0; k < count; k++)
            {
                float angleVar = Rand(-0.5f, 0.5f); 
                float thisAngle = baseAngle + angleVar;
                float distVar = Rand(-0.5f, 0.5f) * scaleRadiusPx;
                
                // 中心からのオフセット
                float ox = dxL + Mathf.Cos(thisAngle + Mathf.PI*0.5f) * distVar;
                float oy = dyL + Mathf.Sin(thisAngle + Mathf.PI*0.5f) * distVar;

                float drawCx = cx + ox;
                float drawCy = cy + oy;
                
                float ca = Mathf.Cos(thisAngle), sa = Mathf.Sin(thisAngle);

                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - drawCx, dy = y - drawCy;
                    float ex = ca * dx + sa * dy; // 前後
                    float ey = -sa * dx + ca * dy; // 左右

                    // 半円（鱗）の形状: ex < 0 (後ろ側) && 距離が半径付近
                    // 簡易的にU字カーブを描く
                    // 放物線: ey^2 = 2 * p * ex 的な
                    
                    // シンプルに円弧の下半分
                    // ex² + ey² = R²
                    float distSq = dx*dx + dy*dy;
                    float dist = Mathf.Sqrt(distSq);

                    // 鱗の縁だけを描画
                    float rim = Mathf.Abs(dist - scaleRadiusPx);
                    if (rim < 2f && ex < scaleRadiusPx * 0.5f) // 前方は描かない（重なり表現）
                    {
                        float fall = Mathf.Max(0, 1f - rim / 2f);
                        // 角度制限（半月状にする）
                        // exが正の方向（進行方向）は描かない, あるいは角度で切る
                        
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

    // K) Feather（フェザー：羽毛）
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

        // 羽軸（Shaft）のカーブ
        float curveBend = featherCurve * 0.5f; 

        // 多数の細かい毛を描く
        int filaments = Mathf.RoundToInt(lenPx * 2f); 
        
        for (int i = 0; i < filaments; i++)
        {
            float t = (float)i / filaments; // 0..1 (根元〜先端)
            float tRemap = t * 2f - 1f; // -1..1
            
            // 羽軸上の点
            float shaftX = tRemap * lenPx * 0.5f; // 前後
            float shaftCurveY = (tRemap * tRemap) * lenPx * curveBend; // カーブ
            
            // ローカル座標での羽軸位置
            // これをワールド回転させる
            float ca = Mathf.Cos(baseAngle), sa = Mathf.Sin(baseAngle);
            
            // 2方向に毛が生える
            for (int side = -1; side <= 1; side += 2)
            {
                // 毛の角度: 羽軸に対して斜め（45度くらい）
                float barbAngle = baseAngle + side * (Mathf.PI * 0.25f + t * 0.2f);
                float bca = Mathf.Cos(barbAngle), bsa = Mathf.Sin(barbAngle);

                // 毛の長さ（楕円形に分布）
                float barbLen = widPx * Mathf.Sin(t * Mathf.PI);

                // 線分を描画する関数がないので、点群で近似 or 1本の長い線
                // ここでは簡易的に羽軸周辺にノイズに指向性を持たせる
                
                // ピクセル走査のほうが早いかも
            }
        }
        
        // ピクセルベース実装
        float ca2 = Mathf.Cos(baseAngle), sa2 = Mathf.Sin(baseAngle);
        
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = x - cx, dy = y - cy;
            float ex = ca2 * dx + sa2 * dy; // 軸方向
            float ey = -sa2 * dx + ca2 * dy; // 幅方向
            
            // 軸のカーブ
            float t = ex / (lenPx * 0.5f); // -1..1
            if (Mathf.Abs(t) > 1f) continue;
            
            float curveOffset = (t * t) * lenPx * curveBend;
            float distFromShaft = ey - curveOffset; // 符号あり距離
            
            // 羽毛のテクスチャ感: 
            // 距離に応じて、斜めの縞模様を入れる
            // 縞の角度は distFromShaft の符号で反転
            float barbDir = Mathf.Sign(distFromShaft);
            float barbFactor = (ex + distFromShaft * barbDir * 2f); // 斜め成分
            
            float barbPat = Mathf.Sin(barbFactor * 1.5f); // 縞々の周波数
            
            // 全体形状（楕円）
            float shape = 1f - (t*t + (distFromShaft*distFromShaft)/(widPx*widPx)*4f);
            if (shape < 0) continue;
            
            if (barbPat > 0) // 縞の谷間は描かない
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

    // L) Vibration (バイブレーション：無方向マット)
    void PaintVibration(Vector2 uv, float baseAngle)
    {
        int W = maskSize, H = maskSize;
        int cx = Mathf.RoundToInt(uv.x * (W - 1));
        int cy = Mathf.RoundToInt(uv.y * (H - 1));

        float rPx = vibrioRadiusUv * W;
        int r = Mathf.CeilToInt(rPx);
        
        // ランダムに細かく散らす
        int count = Mathf.RoundToInt(20 * vibrioDensity);
        
        // 範囲を少し広げて、その中に散らす
        float scatterR = rPx * 2f;
        int scatterRi = Mathf.CeilToInt(scatterR);
        
        int x0 = Mathf.Max(0, cx - scatterRi), x1 = Mathf.Min(W - 1, cx + scatterRi);
        int y0 = Mathf.Max(0, cy - scatterRi), y1 = Mathf.Min(H - 1, cy + scatterRi);
        Color[] block = maskTex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        int w = x1 - x0 + 1;

        for (int i = 0; i < count; i++)
        {
            // 中心からランダムな位置
            Vector2 rndPos = Random.insideUnitCircle * scatterR;
            // 少し楕円の軌跡（バイブレーションの動き）
            // 小さな円弧を描く
            
            int px = Mathf.RoundToInt(rndPos.x);
            int py = Mathf.RoundToInt(rndPos.y);
            
            // 小さな円(スクラッチ)を描画

             
            int bx = (cx + px) - x0;
            int by = (cy + py) - y0;
            
            if (bx >= 0 && bx < w && by >= 0 && by < (y1-y0+1))
            {
                 int idx = by * w + bx;
                 float old = block[idx].r;
                 float add = 0.3f * abrasiveJitter; // 薄く加算
                 block[idx].r = Mathf.Clamp01(old + add);
                 block[idx].g = block[idx].r;
                 block[idx].b = block[idx].r;
            }
        }
        maskTex.SetPixels(x0, y0, w, y1 - y0 + 1, block);
        maskTex.Apply(false, false);
    }

    // ====== VR Interaction Fields (Mode A) ======
#if UNITY_ANDROID
    private XRBaseController currentXRController;
#else
    private Transform currentHandVisual;
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;
    private Hand currentHand;
#endif

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

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_ANDROID
using UnityEngine.XR;
#else
using Valve.VR; // SteamVR namespace
#endif

public class PolishUIController : MonoBehaviour
{
    [Header("Target")]
    public MetalSwirlPolisher polisher;
#if !UNITY_ANDROID
    public CylinderPolisher cylinderPolisher;
#endif

    [Header("UI Settings")]
    public Vector3 uiOffset = new Vector3(0f, 0.1f, 0f); 
    [Tooltip("Manual size if matchTargetSize is false")]
    public Vector2 manualSize = new Vector2(0.6f, 0.8f);
    public bool matchTargetSize = false;
    [Tooltip("Multiplier applied to the calculated size")]
    public float sizeMultiplier = 1.0f;
    [Tooltip("Extra padding for the black background (world units)")]
    public float backgroundPadding = 0.01f;
    public float uiScale = 1.0f; 
    [Tooltip("If true, UI faces camera. If false, UI aligns with the Target Plate surface.")]
    public bool faceCamera = false;
    [Tooltip("If true, display follows target. If false, display stays where created (grabbable).")]
    public bool followTarget = false;
    public Font uiFont;

    
    [Header("Audio")]
    public AudioClip soundHover;
    public AudioClip soundClick;
    public AudioClip soundMenuOpen; // ★追加: メニュー起動音
    AudioSource audioSource;
    
    [Header("Gesture Settings")]
    public bool enableGesture = true;
    [Tooltip("下振りの判定速度閾値 (m/s)")]
    public float gestureVelocityThreshold = 2.0f; // 10.0f was too high, resetting to 2.0f
    [Tooltip("前回開いた時刻（連続暴発防止）")]
    private float lastOpenTime = 0f;
    // State Management
    private enum MenuState { Main, Sub }
    private enum MenuCategory { Hairline, Pattern, Finish, Tool }
    
    // Suppress warning about unused field (it is logically used for state tracking, though mostly read-only now)
    #pragma warning disable 0414 
    private MenuState currentMenuState = MenuState.Main;
    #pragma warning restore 0414




#if UNITY_ANDROID
    private InputDevice xrRightController;
    private bool menuButtonPrev = false;
#else
    private Valve.VR.SteamVR_Behaviour_Pose rightHandPose;
    private Valve.VR.SteamVR_Behaviour_Skeleton rightHandSkeleton; // ★Skeleton for finger tracking
#endif
    
    [Header("Debug")]
    public bool showDebugMarker = false;

    Canvas myCanvas;
    GameObject debugCube;
    private GameObject displayObj; // ★Fix: Promote to class member

    [ContextMenu("Rebuild UI")]
    public void RebuildUI()
    {
        // cleanup children
        var children = new System.Collections.Generic.List<GameObject>();
        foreach(Transform child in transform) children.Add(child.gameObject);
        foreach(var child in children) DestroyImmediate(child);
        
        myCanvas = null;
        debugCube = null; 
        Start();
    }
    
    void Start()
    {
        Debug.Log("[PolishUI] Start() called");
        try {
            if (!polisher) polisher = GetComponent<MetalSwirlPolisher>();
            // targetRenderer がある（実際に研磨する）インスタンスを優先
            if (!polisher || !polisher.targetRenderer)
            {
                MetalSwirlPolisher[] all = FindObjectsByType<MetalSwirlPolisher>(FindObjectsSortMode.None);
                foreach (var p in all)
                {
                    if (p.targetRenderer != null)
                    {
                        polisher = p;
                        break;
                    }
                }
                // fallback
                if (!polisher && all.Length > 0) polisher = all[0];
            }

            Debug.Log($"[PolishUI] Polisher found: {(polisher != null ? polisher.name : "NULL")}, hasRenderer={polisher?.targetRenderer != null}");
#if !UNITY_ANDROID
            // CylinderPolisher も探す (SteamVR のみ)
            if (!cylinderPolisher) cylinderPolisher = GetComponent<CylinderPolisher>();
#if UNITY_2023_1_OR_NEWER
            if (!cylinderPolisher) cylinderPolisher = FindFirstObjectByType<CylinderPolisher>();
#else
            if (!cylinderPolisher) cylinderPolisher = FindObjectOfType<CylinderPolisher>();
#endif
            Debug.Log($"[PolishUI] CylinderPolisher found: {(cylinderPolisher != null ? cylinderPolisher.name : "NULL")}");
#endif
            
            // Robust Font Setup
            if (!uiFont) {
#if UNITY_EDITOR
                try {
                    uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                } catch {}
#endif
            }
            if (!uiFont) {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts != null && fonts.Length > 0) uiFont = fonts[0];
            }
            if (!uiFont) uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
    
            Debug.Log($"[PolishUI] Font: {(uiFont != null ? uiFont.name : "NULL")}");
    
            CreateUI();
            CreateEventSystemIfNeeded();
            
            Debug.Log($"[PolishUI] CreateUI done. myCanvas null? {myCanvas == null}");
            
            // Setup AudioSource
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D Sound
    
#if UNITY_ANDROID
            // XR Controller 取得
            xrRightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Debug.Log($"[PolishUI] XR Right Controller valid: {xrRightController.isValid}");
#else
            // Find Right Hand Pose
            var poses = FindObjectsByType<SteamVR_Behaviour_Pose>(FindObjectsSortMode.None);
            foreach (var pose in poses)
            {
                if (pose.inputSource == Valve.VR.SteamVR_Input_Sources.RightHand)
                {
                    rightHandPose = pose;
                    Debug.Log("[PolishUI] Right Hand Pose found: " + pose.name);
                    break;
                }
            }

            // Find Right Hand Skeleton (Globally)
            var skeletons = FindObjectsByType<Valve.VR.SteamVR_Behaviour_Skeleton>(FindObjectsSortMode.None);
            foreach (var skel in skeletons)
            {
                if (skel.inputSource == Valve.VR.SteamVR_Input_Sources.RightHand)
                {
                    rightHandSkeleton = skel;
                    Debug.Log("[PolishUI] Right Hand Skeleton found (Global): " + skel.name);
                    break;
                }
            }

            // Fallback: Local Search (if global failed)
            if (!rightHandSkeleton && rightHandPose != null)
            {
                 rightHandSkeleton = rightHandPose.GetComponent<Valve.VR.SteamVR_Behaviour_Skeleton>();
                 if (!rightHandSkeleton) rightHandSkeleton = rightHandPose.GetComponentInChildren<Valve.VR.SteamVR_Behaviour_Skeleton>();

                 if (rightHandSkeleton) Debug.Log("[PolishUI] Right Hand Skeleton found (Local): " + rightHandSkeleton.name);
                 else Debug.LogError("[PolishUI] CRITICAL: Right Hand Skeleton NOT found. Pose detection will NOT work.");
            }
#endif
    
            // 最初は非表示にしておく（ジェスチャーで出すため）
            if (enableGesture)
            {
                ToggleMenu(false);
            }
        
        } catch (System.Exception e) {
            Debug.LogError("[PolishUI] CRITIAL ERROR in Start(): " + e);
        }
    }

    System.Collections.IEnumerator OpeningAnimation()
    {
        if (myCanvas == null) yield break;
        Transform root = myCanvas.transform.parent; // DisplayRoot
        Vector3 finalScale = root.localScale;
        
        // 縦につぶした状態から開始
        root.localScale = new Vector3(finalScale.x, 0.01f, finalScale.z);
        
        float t = 0;
        while(t < 1f)
        {
            t += Time.deltaTime * 5f; // Speed
            float val = Mathf.SmoothStep(0, 1, t);
            root.localScale = new Vector3(finalScale.x, Mathf.Lerp(0.01f, finalScale.y, val), finalScale.z);
            yield return null;
        }
        root.localScale = finalScale;
    }

    void Update()
    {
        if (!enableGesture) return;

#if UNITY_ANDROID
        // コントローラー再取得
        if (!xrRightController.isValid)
            xrRightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!xrRightController.isValid) return;

        // Bボタン（secondaryButton）でトグル
        bool menuButton = false;
        xrRightController.TryGetFeatureValue(CommonUsages.secondaryButton, out menuButton);
        if (menuButton && !menuButtonPrev)
        {
            if (displayObj && displayObj.activeSelf)
                ToggleMenu(false);
            else
                ShowMenuAtHand();
            lastOpenTime = Time.time;
        }
        menuButtonPrev = menuButton;

        // 下振りジェスチャー（ポーズ判定なし — Quest コントローラーにスケルトン無し）
        if (xrRightController.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 vel))
        {
            if (vel.y < -gestureVelocityThreshold && Time.time - lastOpenTime > 0.8f)
            {
                ShowMenuAtHand();
                lastOpenTime = Time.time;
            }
            else if (vel.y > gestureVelocityThreshold && Time.time - lastOpenTime > 0.8f)
            {
                ToggleMenu(false);
                lastOpenTime = Time.time;
            }
        }
#else
        if (rightHandPose == null) return;

        // ジェスチャー検知: 下向きの速度が一定以上 (SAO風: 上から下へシュッ！)
        Vector3 vel = rightHandPose.GetVelocity();

        // デバッグ: スペースキーでも開く
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("[UI] Debug Key Pressed. Opening Menu.");
            ShowMenuAtHand();
        }

        // 速度条件を満たしたときだけポーズ判定を行う
        if (vel.y < -gestureVelocityThreshold)
        {
            if (IsPointingPose())
            {
                if (Time.time - lastOpenTime > 0.8f)
                {
                    ShowMenuAtHand();
                    lastOpenTime = Time.time;
                }
            }
        }
        else if (vel.y > gestureVelocityThreshold)
        {
             if (IsPointingPose())
             {
                 if (Time.time - lastOpenTime > 0.8f)
                 {
                     ToggleMenu(false);
                     lastOpenTime = Time.time;
                 }
             }
        }
#endif
    }

#if !UNITY_ANDROID
    bool IsPointingPose()
    {
        if (!rightHandSkeleton)
        {
             Debug.LogWarning("[UI] Pose Check Failed: Skeleton component is MISSING.");
             return false;
        }

        if (rightHandSkeleton.skeletonAction == null)
        {
            Debug.LogWarning("[UI] Skeleton found but 'Skeleton Action' is NULL.");
            return false;
        }

        float index = rightHandSkeleton.indexCurl;
        float middle = rightHandSkeleton.middleCurl;
        float ring = rightHandSkeleton.ringCurl;
        float pinky = rightHandSkeleton.pinkyCurl;

        bool indexStraight = index < 0.55f;
        bool othersCurled = middle > 0.35f && ring > 0.35f && pinky > 0.35f;

        return indexStraight && othersCurled;
    }
#endif

    void ShowMenuAtHand()
    {
        if (!displayObj) return;

        // 手の位置
#if UNITY_ANDROID
        Vector3 handPos = Vector3.zero;
        xrRightController.TryGetFeatureValue(CommonUsages.devicePosition, out handPos);
#else
        Vector3 handPos = rightHandPose.transform.position;
#endif
        // カメラ（HMD）の位置と向き
        Camera cam = Camera.main;
        if (!cam) 
        {
            // カメラが見つからない場合、手の位置の少し前、手の方を向く
            displayObj.transform.position = handPos + Vector3.forward * 0.4f;
            displayObj.transform.LookAt(handPos);
            displayObj.transform.Rotate(0, 180, 0);
        }
        else
        {
            Vector3 camPos = cam.transform.position;
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0; // 水平方向のみ使う（見やすさのため）
            camForward.Normalize();

            // 出現位置: 手の位置から参照しつつ、視線の少し前方
            // ユーザーリクエスト: 「起動するときのやり方も似せて、それをしたら目の前に表示させて」
            // 目の前 = カメラの前方。高さは手の振り下ろし位置に合わせるイメージ
            
            // 目の前 0.8m (Previous 0.4m was too close)
            Vector3 spawnPos = camPos + camForward * 0.8f; 
            
            // 高さ: 目の高さより20cm下くらいが見やすい
            spawnPos.y = camPos.y - 0.2f;

            // 手の位置があまりにも離れている場合は手の近くに出す？
            // いえ、SAOは「視界の中」に出るのが基本なので、カメラ基準で良い。
            // ただし、ジェスチャーをした手の位置に応じて左右にずらすのもアリだが、
            // シンプルに「目の前」が一番使いやすい。

            displayObj.transform.position = spawnPos;
            
            // 向き: カメラの方を向く（ビルボード）
            displayObj.transform.LookAt(cam.transform);
            displayObj.transform.Rotate(0, 180, 0); // UIはZ-が正面なので反転
        }

        ToggleMenu(true);
        Debug.Log("[UI] Gesture Detected! Opening Menu.");
            PlaySound(soundMenuOpen);
            // Trigger Opening Animation
            StartCoroutine(AnimateMenuOpen());
    }

    public void ToggleMenu(bool open)
    {
        if (!displayObj) return;
        
        displayObj.SetActive(open);
        
        if (open)
        {
            // Reset state or play animation
            if (myCanvas)
            {
                Transform root = myCanvas.transform.parent;
                // アニメーション再生
                StartCoroutine(OpeningAnimation());
            }
        }
    }

    
    void Reset()
    {
        uiOffset = new Vector3(0f, 0.2f, 0f);
        uiScale = 1.0f;
        faceCamera = false;
        polisher = GetComponent<MetalSwirlPolisher>();
#if !UNITY_ANDROID
        cylinderPolisher = GetComponent<CylinderPolisher>();
#endif
        matchTargetSize = true;
    }

    void LateUpdate()
    {
        // Only move if followTarget is enabled
        if (!followTarget) return;

        // どちらかのポリッシャーからターゲットレンダラーを取得
        Renderer targetRend = null;
        if (polisher != null && polisher.targetRenderer != null) targetRend = polisher.targetRenderer;
#if !UNITY_ANDROID
        if (targetRend == null && cylinderPolisher != null && cylinderPolisher.targetRenderer != null) targetRend = cylinderPolisher.targetRenderer;
#endif

        if (myCanvas != null && targetRend != null)
        {
            Transform targetTr = targetRend.transform;
            Transform rootToMove = myCanvas.transform.parent ? myCanvas.transform.parent : myCanvas.transform;
            
            // Ensure we don't try to move ourselves if we are the root
            if (rootToMove == this.transform) return;

            if (faceCamera && Camera.main != null)
            {
                Vector3 targetPos = targetTr.position + targetTr.TransformDirection(uiOffset);
                rootToMove.position = targetPos;
                rootToMove.forward = Camera.main.transform.forward;
            }
            else
            {
                rootToMove.rotation = targetTr.rotation;
                Vector3 center = targetTr.position;
                Vector3 pos = center + targetTr.TransformDirection(uiOffset);
                rootToMove.position = pos;
            }

            if (showDebugMarker)
            {
                if (!debugCube)
                {
                    debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    debugCube.name = "DEBUG_UI_POS";
                    debugCube.transform.localScale = Vector3.one * 0.1f;
                    debugCube.GetComponent<Renderer>().material.color = Color.red;
                    DestroyImmediate(debugCube.GetComponent<Collider>());
                }
                debugCube.transform.position = rootToMove.position;
                debugCube.transform.rotation = rootToMove.rotation;
            }
            else if (debugCube)
            {
                DestroyImmediate(debugCube);
            }
        }
    }

    void CreateEventSystemIfNeeded()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
#else
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
#endif
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    void CreateUI()
    {
        Debug.Log("[PolishUI] CreateUI() started");
        
        // Calculate Size
        Vector2 displaySize = manualSize;
        Renderer targetRend = null;
        if (polisher && polisher.targetRenderer) targetRend = polisher.targetRenderer;
#if !UNITY_ANDROID
        if (targetRend == null && cylinderPolisher && cylinderPolisher.targetRenderer) targetRend = cylinderPolisher.targetRenderer;
#endif

        if (matchTargetSize && targetRend)
        {
            // Use the TARGET RENDERER (metal plate) scale, not the polisher scale
            Vector3 targetScale = targetRend.transform.lossyScale;
            displaySize.x = targetScale.x;
            displaySize.y = Mathf.Max(targetScale.z, targetScale.y);
        }
        
        // Apply size multiplier
        displaySize *= sizeMultiplier;

        // VR最小サイズ強制（Inspectorのシリアライズ値が小さい場合でも保証）
        displaySize.x = Mathf.Max(displaySize.x, 0.6f);
        displaySize.y = Mathf.Max(displaySize.y, 0.8f);

        Debug.Log($"[PolishUI] displaySize: {displaySize} (multiplier: {sizeMultiplier})");



        // 1. Root Object (The "Display" itself) - NOT parented to anything for independence
        displayObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        displayObj.name = "PolishUI_DisplayRoot";
        DestroyImmediate(displayObj.GetComponent<Collider>());
        
        // Position at metal plate location initially (but NOT as a child)
        if (targetRend)
        {
            Transform targetTr = targetRend.transform;
            displayObj.transform.position = targetTr.position + targetTr.TransformDirection(uiOffset);
            displayObj.transform.rotation = targetTr.rotation;
        }
        else
        {
            displayObj.transform.position = this.transform.position + uiOffset;
        }
        
        targetMenuScale = new Vector3(displaySize.x + backgroundPadding * 2, displaySize.y + backgroundPadding * 2, 0.05f); 
        displayObj.transform.localScale = targetMenuScale; 
        
        // Add collider for grabbing
        BoxCollider bc = displayObj.AddComponent<BoxCollider>();
        bc.size = Vector3.one;
        
        // Add Rigidbody for physics interaction (kinematic so it doesn't fall)
        Rigidbody rb = displayObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        
        Renderer r = displayObj.GetComponent<Renderer>();
        if (r) {
            // User requested to REMOVE transparent board.
            // We disable the renderer so only buttons are visible.
            r.enabled = false; 
        }
        
        Debug.Log($"[PolishUI] DisplayRoot created at {displayObj.transform.position}, scale {displayObj.transform.localScale}");

        // 2. Canvas
        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(displayObj.transform, false);
        // Position on front face (local Z = -0.5 of the unit cube)
        canvasObj.transform.localPosition = new Vector3(0f, 0f, -0.51f); 
        // No rotation needed - text was mirrored because of 180 rotation
        canvasObj.transform.localRotation = Quaternion.identity; 
        
        float canvasBaseWidth = 300f;
        float pixelScale = displaySize.x / canvasBaseWidth; 
        pixelScale = Mathf.Max(pixelScale, 0.0005f);
        
        // Compensate for non-uniform parent scale so UI pixels remain square
        float yScaleCompensation = targetMenuScale.x / targetMenuScale.y;
        canvasObj.transform.localScale = new Vector3(pixelScale, pixelScale * yScaleCompensation, pixelScale);

        myCanvas = canvasObj.AddComponent<Canvas>();
        myCanvas.renderMode = RenderMode.WorldSpace;

        // Fix: Set explicit generic resolution for the WorldSpace canvas
        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        if (!canvasRt) canvasRt = canvasObj.AddComponent<RectTransform>();
        // Match aspect ratio of displaySize, adjusted for Y scale compensation
        float aspect = displaySize.y / displaySize.x;
        canvasRt.sizeDelta = new Vector2(canvasBaseWidth, canvasBaseWidth * aspect / yScaleCompensation);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        Debug.Log($"[PolishUI] Canvas created, size: {canvasRt.sizeDelta}, pixelScale: {pixelScale}");

        // 3. ScrollView setup for SAO UI
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(canvasObj.transform, false);
        RectTransform scrollRt = scrollViewObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.sizeDelta = Vector2.zero;

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewRt = viewportObj.AddComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.sizeDelta = Vector2.zero; // Matches parent size (displaySize)
        viewRt.pivot = new Vector2(0, 1);
        
        // Remove Image component from Viewport to avoid masking issues if Mask was added
        // And ensure no Mask component exists

        /*
        Image maskImg = viewportObj.AddComponent<Image>();
        maskImg.color = new Color(0,0,0,0); // invisible
        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        */

        // Content (Root for Columns)
        GameObject contentObj = new GameObject("ColumnsContainer");
        contentObj.transform.SetParent(viewportObj.transform, false);
        columnsRoot = contentObj.transform;

        // Horizontal Layout for Columns
        HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = false; // Columns determine their own height
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = false; 
        hlg.childForceExpandWidth = false; // Don't stretch columns, let them stack left
        hlg.spacing = 10;
        hlg.padding = new RectOffset(20, 20, 20, 20);
        hlg.childAlignment = TextAnchor.UpperLeft; // Fix: Anchor to top so columns don't shift down
        
        // Add ContentSizeFitter to make the container grow with columns
        ContentSizeFitter rootCsf = contentObj.AddComponent<ContentSizeFitter>();
        rootCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Content scale
        RectTransform contentRt = contentObj.GetComponent<RectTransform>();
        if (!contentRt) contentRt = contentObj.AddComponent<RectTransform>();
        
        // Fix: Anchors should be Top-Left for ContentSizeFitter to work reliably (Standard ScrollView setup)
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(0, 1);
        contentRt.pivot = new Vector2(0, 1);
        contentRt.sizeDelta = Vector2.zero; // Let SizeFitter handle it

        scrollRect.content = contentRt;
        scrollRect.viewport = viewRt;
        scrollRect.horizontal = true; 
        scrollRect.vertical = false;

        // Initialize Column List
        activeColumns = new System.Collections.Generic.List<GameObject>();

        // Initial View: Main Menu (Column 0)
        SpawnMainMenuColumn();
        
        Debug.Log("[PolishUI] CreateUI() completed (Cascade Style)");
    }

    // --- SAO Cascade Logic ---
    
    // Track active columns
    private System.Collections.Generic.List<GameObject> activeColumns;
    private Transform columnsRoot;
    private Vector3 targetMenuScale; // Store for animation

    // Helper to clean up columns from a specific index onwards
    void CleanupColumns(int startIndex)
    {
        if (activeColumns == null) return;
        for (int i = activeColumns.Count - 1; i >= startIndex; i--)
        {
            Destroy(activeColumns[i]);
            activeColumns.RemoveAt(i);
        }
    }

    // Helper to create a basic column (Vertical Layout)
    GameObject CreateColumnObject(int index)
    {
        // 2026-01-09 Fix: Fallback if columnsRoot lost reference
        if (columnsRoot == null)
        {
             Debug.LogWarning("[PolishUI] columnsRoot lost! Attempting to find ColumnsContainer...");
             var found = transform.Find("PolishUI_DisplayRoot/Canvas/ScrollView/Viewport/ColumnsContainer");
             if (!found && displayObj) found = displayObj.transform.Find("Canvas/ScrollView/Viewport/ColumnsContainer");
             
             // Fallback: Global search (risky but better than root)
             if (!found) 
             {
                 GameObject go = GameObject.Find("ColumnsContainer");
                 if (go) found = go.transform;
             }
             
             if (found) columnsRoot = found;
             else Debug.LogError("[PolishUI] CRITICAL: Could not recover ColumnsContainer!");
        }

        GameObject colObj = new GameObject($"Column_{index}");
        if (columnsRoot)
        {
            colObj.transform.SetParent(columnsRoot, false);
        }
        else
        {
             // Failsafe
             colObj.transform.SetParent(this.transform, false);
        }

        colObj.transform.localScale = Vector3.one; 
        colObj.transform.localPosition = Vector3.zero;
        
        // Background Image (Semi-transparent vertical strip)
        Image bg = colObj.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.2f); // Lighter background for SAO feel

        // Layout
        VerticalLayoutGroup vlg = colObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = false; 
        vlg.spacing = 10; // Fix: Increase spacing for better separation
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = colObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; 

        // Ensure minimum width
        LayoutElement le = colObj.AddComponent<LayoutElement>();
        le.minWidth = 220;
        
        activeColumns.Add(colObj);
        
        // Trigger Cascade Animation (Fade + Slide)
        StartCoroutine(AnimateColumnEntry(colObj.transform));
        
        return colObj;
    }
    
    // --- Animations ---

    // 1. Column Cascade Entry
    System.Collections.IEnumerator AnimateColumnEntry(Transform colTr)
    {
        CanvasGroup cg = colTr.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        
        // Initial offset (Slide from left slightly)
        // Note: LayoutGroup normally controls position, but we can animate local offset via modification or wait for layout?
        // Actually, HorizontalLayoutGroup overrides position. 
        // Trick: We can animate CanvasGroup alpha and maybe scale? 
        // Let's do Scale 0->1 on Y axis + Alpha fade.
        
        colTr.localScale = new Vector3(1f, 0f, 1f); 
        
        float timer = 0f;
        float duration = 0.25f;
        
        while(timer < duration)
        {
            if (colTr == null || cg == null) yield break; // Safety check

            timer += Time.deltaTime;
            float t = timer / duration;
            // EaseOutBack
            float scale = t; 
            
            cg.alpha = t;
            colTr.localScale = new Vector3(1f, scale, 1f);
            
            yield return null;
        }
        
        if (colTr != null && cg != null)
        {
            colTr.localScale = Vector3.one;
            cg.alpha = 1f;
        }
    }

    // 2. Menu Opening (Vertical Line -> Expand)
    System.Collections.IEnumerator AnimateMenuOpen()
    {
         // Assume displayObj is the root
         if(!displayObj) yield break;
         
         float timer = 0f;
         float duration = 0.4f;
         
         Vector3 startScale = new Vector3(0f, 1f, 0f); // Thin vertical line
         Vector3 endScale = new Vector3(1f, 1f, 1f); // However displayObj.localScale was set to specific size in CreateUI
         // Note: In CreateUI we set scaling based on displaySize.
         // Let's animate a multiplier or just re-calculate.
         // Better: Animate the "Canvas" or "ScrollView" scale? 
         // Let's animate the whole displayObj scale but remember its target.
         
         // Use cached target scale
         Vector3 targetScale = targetMenuScale;
         
         // Failsafe: Recalculate if lost (e.g. Hot Reload)
         if(targetScale.sqrMagnitude < 0.001f) {
             float w = manualSize.x * sizeMultiplier + backgroundPadding * 2;
             float h = manualSize.y * sizeMultiplier + backgroundPadding * 2;
             targetScale = new Vector3(w, h, 0.05f);
             targetMenuScale = targetScale;
         }
         // Current logic sets displayObj scale directly.
         
         while(timer < duration)
         {
             timer += Time.deltaTime;
             float t = timer / duration;
             // Elastic Ease Out
             float t2 = t - 1;
             float p = t2 * t2 * t2 + 1; // Cubic Ease Out
             
             // Expand from vertical center
             displayObj.transform.localScale = Vector3.Lerp(new Vector3(0, targetScale.y, targetScale.z), targetScale, p);
             yield return null;
         }
         displayObj.transform.localScale = targetScale;
    }
    


    void SpawnMainMenuColumn()
    {
        CleanupColumns(0); // Clear all
        GameObject col = CreateColumnObject(0);
        
        // Header
        CreateText(col.transform, "Menu", 26, Color.cyan);

        // Category Buttons
        CreateCategoryButton(col.transform, 0, MenuCategory.Hairline, "Hairline");
        CreateCategoryButton(col.transform, 0, MenuCategory.Pattern, "Pattern");
        CreateCategoryButton(col.transform, 0, MenuCategory.Finish, "Finish");
        CreateCategoryButton(col.transform, 0, MenuCategory.Tool, "System");
    }
    
    // 2. Spawn Column 1 (Sub Items for Category)
    void SpawnSubMenuColumn(MenuCategory category)
    {
        CleanupColumns(1); // Clear current sub-menu if any
        GameObject col = CreateColumnObject(1);

        CreateText(col.transform, category.ToString(), 24, Color.white);

        if (category == MenuCategory.Tool)
        {
            CreateActionButton(col.transform, "Reset", new Color(0.3f, 0.3f, 0.3f), () => {
                MetalSwirlPolisher[] all = FindObjectsByType<MetalSwirlPolisher>(FindObjectsSortMode.None);
                foreach (var p in all) p.ResetMask();
#if !UNITY_ANDROID
                if (cylinderPolisher) cylinderPolisher.ResetMask();
#endif
                Debug.Log($"[PolishUI] Reset: {all.Length} polishers");
                PlaySound(soundClick);
            });
            CreateActionButton(col.transform, "Fill White", new Color(0.3f, 0.3f, 0.3f), () => {
                MetalSwirlPolisher[] all = FindObjectsByType<MetalSwirlPolisher>(FindObjectsSortMode.None);
                foreach (var p in all) p.FillMaskWhite();
                Debug.Log($"[PolishUI] FillWhite: {all.Length} polishers");
                PlaySound(soundClick);
            });
        }
        else
        {
            var presets = System.Enum.GetValues(typeof(MetalSwirlPolisher.PolishPreset));
            foreach (MetalSwirlPolisher.PolishPreset p in presets)
            {
                if (GetCategoryForPreset(p) == category)
                {
                    CreateButton(col.transform, p);
                }
            }
        }
        
        // Force Layout Rebuild
        Canvas.ForceUpdateCanvases();
    }

    // Track currently open category for toggle behavior
    private MenuCategory? currentOpenCategory = null;

    // Customized Category Button (Round/Icon style)
    void CreateCategoryButton(Transform parentColumn, int columnIndex, MenuCategory category, string label)
    {
        GameObject btnObj = new GameObject($"CatBtn_{label}");
        btnObj.transform.SetParent(parentColumn, false);
        btnObj.transform.localScale = Vector3.one; // Fix: Ensure scale is 1
        btnObj.transform.localPosition = Vector3.zero;

        Image img = btnObj.AddComponent<Image>();
        // Load standard 'Knob' for round icon background
        Sprite knob = Resources.Load<Sprite>("Knob"); // Standard Unity UI sprite
        if (knob) img.sprite = knob;
        
        var defaultColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        img.color = defaultColor;


        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        btn.onClick.AddListener(() => {
            Debug.Log($"[PolishUI] Category button pressed: {category}, currentOpen={currentOpenCategory}");
            PlaySound(soundClick);

            // Toggle Logic
            if (currentOpenCategory == category)
            {
                // Already open -> Close it
                CleanupColumns(1);
                currentOpenCategory = null;
                
                // Visual Reset (All dark)
                foreach(Transform child in parentColumn) {
                    Image childImg = child.GetComponent<Image>();
                    if(childImg) childImg.color = defaultColor;
                }
            }
            else
            {
                // Different or Closed -> Open it
                SpawnSubMenuColumn(category);
                currentOpenCategory = category;

                // Visual Update: Reset all siblings, Highlight this
                foreach(Transform child in parentColumn) {
                    Image childImg = child.GetComponent<Image>();
                    if(childImg) childImg.color = defaultColor;
                }
                img.color = new Color(1f, 0.66f, 0f, 0.9f); // SAO Orange (#FFAA00)
            }
        });

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 100;
        le.preferredHeight = 100;
        le.minWidth = 100;
        le.preferredWidth = 100;

        // Text/Icon
        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = label.Substring(0, 1); // First letter as Icon placeholder
        txt.font = uiFont;
        txt.fontSize = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        // Full label below? or just icon. Let's do Side text
        GameObject subTxtObj = new GameObject("SubLabel");
        subTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform rt = subTxtObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Text subTxt = subTxtObj.AddComponent<Text>();
        subTxt.text = label;
        subTxt.font = uiFont;
        subTxt.fontSize = 16;
        subTxt.alignment = TextAnchor.LowerCenter;
        subTxt.color = new Color(0.8f, 0.8f, 0.8f);
        
        // VRTouch
        btnObj.AddComponent<VRTouchButton>().scaleOnHover = false; // Fix: No scaling
    }




    MenuCategory GetCategoryForPreset(MetalSwirlPolisher.PolishPreset p)
    {
        switch(p)
        {
            case MetalSwirlPolisher.PolishPreset.Ivy_Intertwine:
            case MetalSwirlPolisher.PolishPreset.RandomScale_Layered:
            case MetalSwirlPolisher.PolishPreset.Feather_Soft:
                return MenuCategory.Pattern;
            
            case MetalSwirlPolisher.PolishPreset.Vibration_Matte:
                return MenuCategory.Finish;

            default: 
                return MenuCategory.Hairline; // Default (Circle, Cross, Hairline, Tornado, Aurora, Sander, Antique)
        }
    }


    void CreateActionButton(Transform parent, string label, Color btnColor, System.Action onClick)
    {
        Debug.Log("[PolishUI] Creating Action Button: " + label);
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent, false);
        btnObj.transform.localScale = Vector3.one; // Explicitly set scale
        btnObj.transform.localPosition = Vector3.zero; // Reset position
        // ...

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = btnColor;
        cb.highlightedColor = new Color(btnColor.r * 1.2f, btnColor.g * 1.2f, btnColor.b * 1.2f, 1f);
        cb.pressedColor = new Color(btnColor.r * 0.7f, btnColor.g * 0.7f, btnColor.b * 0.7f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        btn.onClick.AddListener(() => onClick());

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 80;
        le.preferredHeight = 80;
        le.flexibleWidth = 1;

        // Add VR touch support
        VRTouchButton touchBtn = btnObj.AddComponent<VRTouchButton>();
        // Add Hover Sound Event
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => { PlaySound(ClipType.Hover); });
        trigger.triggers.Add(entry);

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        txt.font = uiFont;
        txt.fontSize = 26;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
    }


    // ClearContent removed (Legacy)

    void CreateButton(Transform parent, MetalSwirlPolisher.PolishPreset preset)
    {
        GameObject btnObj = new GameObject("Btn_" + preset);
        btnObj.transform.SetParent(parent, false);
        btnObj.transform.localScale = Vector3.one; // Fix: Ensure scale is 1
        btnObj.transform.localPosition = Vector3.zero;

        Image img = btnObj.AddComponent<Image>();
        // SAO Style: Dark Grey for unselected
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        cb.highlightedColor = new Color(1f, 0.6f, 0f, 0.9f); // SAO Orange Highlight
        cb.pressedColor = new Color(1f, 0.8f, 0.4f, 1f);
        cb.selectedColor = new Color(1f, 0.6f, 0f, 0.9f);
        btn.colors = cb;

        btn.onClick.AddListener(() => {
            // 全ての MetalSwirlPolisher にプリセットを適用
            MetalSwirlPolisher[] allPolishers = FindObjectsByType<MetalSwirlPolisher>(FindObjectsSortMode.None);
            foreach (var p in allPolishers)
                p.preset = preset;
            Debug.Log($"[PolishUI] Preset set: {preset} ({allPolishers.Length} polishers)");
#if !UNITY_ANDROID
            // CylinderPolisher にも同じプリセットを設定
            if (cylinderPolisher)
            {
                CylinderPolisher.PolishPreset cylPreset = (CylinderPolisher.PolishPreset)System.Enum.Parse(
                    typeof(CylinderPolisher.PolishPreset), preset.ToString());
                cylinderPolisher.preset = cylPreset;
            }
#endif
            PlaySound(soundClick);
        });

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 80;
        le.preferredHeight = 80;
        le.flexibleWidth = 1f;
        le.minWidth = 220;

        // Add VR touch support
        VRTouchButton touchBtn = btnObj.AddComponent<VRTouchButton>();
        touchBtn.scaleOnHover = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        // Clean up the preset name
        string displayName = preset.ToString().Replace("_", " ");
        txt.text = displayName;
        txt.font = uiFont;
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        // SAO風: 白いテキスト
        txt.color = new Color(0.9f, 1f, 1f);
        
        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
    }

    void CreateText(Transform parent, string content, int size, Color col)
    {
        GameObject go = new GameObject("Txt_" + content);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = uiFont;
        t.fontSize = size;
        t.color = col;
        t.alignment = TextAnchor.MiddleCenter;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 10;
        le.preferredHeight = size + 10;
        le.flexibleHeight = 0;
    }

    void PlaySound(ClipType type)
    {
        // 簡易的な音生成も可能だが、今はプレースホルダー
        if (audioSource && type == ClipType.Click && soundClick) audioSource.PlayOneShot(soundClick);
        if (audioSource && type == ClipType.Hover && soundHover) audioSource.PlayOneShot(soundHover);
    }
    void PlaySound(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }
    
    enum ClipType { Hover, Click }

    void OnDrawGizmos()
    {
        if (!polisher) polisher = GetComponent<MetalSwirlPolisher>();
#if UNITY_2023_1_OR_NEWER
        if (!polisher) polisher = FindFirstObjectByType<MetalSwirlPolisher>();
#else
        if (!polisher) polisher = FindObjectOfType<MetalSwirlPolisher>();
#endif
#if !UNITY_ANDROID
        if (!cylinderPolisher) cylinderPolisher = GetComponent<CylinderPolisher>();
#if UNITY_2023_1_OR_NEWER
        if (!cylinderPolisher) cylinderPolisher = FindFirstObjectByType<CylinderPolisher>();
#else
        if (!cylinderPolisher) cylinderPolisher = FindObjectOfType<CylinderPolisher>();
#endif
#endif
        // MetalSwirlPolisher または CylinderPolisher のどちらかがあればGizmoを描画
        Renderer targetRend = null;
        if (polisher && polisher.targetRenderer) targetRend = polisher.targetRenderer;
#if !UNITY_ANDROID
        if (targetRend == null && cylinderPolisher && cylinderPolisher.targetRenderer) targetRend = cylinderPolisher.targetRenderer;
#endif

        if (targetRend)
        {
            Gizmos.color = Color.green;
            Transform t = targetRend.transform;
            Vector3 center = t.position;
            Vector3 pos = center + t.TransformDirection(uiOffset);

            // Gizmo visualizes the estimated size
            Vector3 size = manualSize;
            if (matchTargetSize)
            {
                 Vector3 s = t.lossyScale;
                 size.x = s.x;
                 size.y = Mathf.Max(s.z, s.y);
            }

            if (faceCamera)
            {
                Gizmos.DrawWireSphere(pos, 0.05f);
            }
            else
            {
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(pos, t.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.05f));
                Gizmos.matrix = old;
            }
            Gizmos.DrawLine(center, pos);
        }
    }
}

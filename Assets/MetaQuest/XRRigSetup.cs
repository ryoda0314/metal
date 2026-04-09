// XRRigSetup.cs
// Meta Quest / OpenXR用のXR Originセットアップヘルパー
// シーンに配置するとXR Originリグを自動構築する

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class XRRigSetup : MonoBehaviour
{
    [Header("Setup")]
    public bool autoSetup = true;
    public bool disableExistingCameras = true;

    [Tooltip("視点の高さ調整（マイナスで低く）")]
    public float cameraHeightOffset = 0f;

    [Tooltip("手のモデルPrefab（任意・未設定ならSteamVRグローブを自動検出、無ければ簡易モデルを生成）")]
    public GameObject leftHandModel;
    public GameObject rightHandModel;

    [Header("Interaction")]
    public float directInteractionRange = 0.1f;
    public bool enableRayInteraction = true;

    [Header("Hand Physics（手の物理衝突）")]
    [Tooltip("手が物体を押し返す物理衝突を有効にする")]
    public bool enableHandPhysics = true;
    [Tooltip("手の物理コライダー半径")]
    public float handPhysicsRadius = 0.05f;

    [Header("Mixed Reality (Passthrough)")]
    [Tooltip("Meta Questでパススルー（背景透過MR）を有効にする")]
    public bool enablePassthrough = true;

    [Header("Locomotion")]
    public bool enableTeleport = false;
    public bool enableSmoothMove = true;
    public float moveSpeed = 2f;
    public bool enableSnapTurn = true;
    public float snapTurnAngle = 45f;

    [HideInInspector] public Transform leftController;
    [HideInInspector] public Transform rightController;
    [HideInInspector] public Transform headCamera;

#if UNITY_EDITOR
    // エディタでコンポーネント追加・リセット時にSteamVRのグローブモデルを自動検出
    void OnValidate()
    {
        if (leftHandModel == null)
        {
            leftHandModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SteamVR/Models/vr_glove_left_model_slim.fbx");
            if (leftHandModel == null)
                leftHandModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SteamVR/Prefabs/vr_glove_left_model_slim.prefab");
        }
        if (rightHandModel == null)
        {
            rightHandModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SteamVR/Models/vr_glove_right_model_slim.fbx");
            if (rightHandModel == null)
                rightHandModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SteamVR/Prefabs/vr_glove_right_model_slim.prefab");
        }
    }
#endif

    void Start()
    {
        // XR Rigの高さを調整（cameraHeightOffsetで視点の高さを補正）
        transform.localPosition = new Vector3(transform.localPosition.x, cameraHeightOffset, transform.localPosition.z);

        if (autoSetup)
        {
            SetupRig();
        }
    }

    public void SetupRig()
    {
        // 既存のXR Originがあればスキップ
        var existingOrigin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (existingOrigin != null)
        {
            Debug.Log("[XRRigSetup] 既存のXR Originを検出。スキップ。");
            return;
        }

        // 既存のカメラとAudioListenerを無効化
        if (disableExistingCameras)
        {
            DisableExistingCameras();
        }

        Debug.Log("[XRRigSetup] XR Originリグを構築中...");

        // --- XR Origin ---
        var xrOriginObj = new GameObject("XR Origin (Meta Quest)");
        xrOriginObj.transform.SetParent(transform);
        xrOriginObj.transform.localPosition = Vector3.zero;
        xrOriginObj.transform.localRotation = Quaternion.identity;

        var xrOrigin = xrOriginObj.AddComponent<Unity.XR.CoreUtils.XROrigin>();
        // Floor基準: Questのガーディアン境界の床を基準にする
        xrOrigin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
        // カメラオフセットを0にする（Floor基準なのでオフセット不要）
        xrOrigin.CameraYOffset = 0f;

        // --- Camera Offset ---
        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(xrOriginObj.transform);
        cameraOffset.transform.localPosition = Vector3.zero;
        xrOrigin.CameraFloorOffsetObject = cameraOffset;

        // --- Main Camera ---
        var cameraObj = new GameObject("XR Main Camera");
        cameraObj.transform.SetParent(cameraOffset.transform);
        cameraObj.transform.localPosition = Vector3.zero;
        cameraObj.tag = "MainCamera";

        var camera = cameraObj.AddComponent<Camera>();
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;

#if UNITY_ANDROID
        if (enablePassthrough)
        {
            // パススルー用: 背景を透明にしてQuestのカメラ映像を見せる
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
        else
        {
            camera.clearFlags = CameraClearFlags.Skybox;
        }
#else
        camera.clearFlags = CameraClearFlags.Skybox;
#endif

        cameraObj.AddComponent<AudioListener>();

        // TrackedPoseDriver - ヘッドトラッキング
        var headPoseDriver = cameraObj.AddComponent<TrackedPoseDriver>();
        headPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        headPoseDriver.positionAction = new InputAction("HeadPosition", binding: "<XRHMD>/centerEyePosition");
        headPoseDriver.rotationAction = new InputAction("HeadRotation", binding: "<XRHMD>/centerEyeRotation");
        headPoseDriver.positionAction.Enable();
        headPoseDriver.rotationAction.Enable();

        xrOrigin.Camera = camera;
        headCamera = cameraObj.transform;

        // --- Left Controller ---
        leftController = CreateController(cameraOffset.transform, "Left Controller",
            "<XRController>{LeftHand}", leftHandModel, true);

        // --- Right Controller ---
        rightController = CreateController(cameraOffset.transform, "Right Controller",
            "<XRController>{RightHand}", rightHandModel, false);

        // --- Interaction Manager ---
        if (FindAnyObjectByType<XRInteractionManager>() == null)
        {
            var managerObj = new GameObject("XR Interaction Manager");
            managerObj.transform.SetParent(xrOriginObj.transform);
            managerObj.AddComponent<XRInteractionManager>();
        }

        // --- Passthrough (MR) ---
#if UNITY_ANDROID
        if (enablePassthrough)
        {
            // ARSession が必要（AR Foundation サブシステムの起動に必須）
            if (FindAnyObjectByType<ARSession>() == null)
            {
                var arSessionObj = new GameObject("AR Session");
                arSessionObj.transform.SetParent(xrOriginObj.transform);
                arSessionObj.AddComponent<ARSession>();
                Debug.Log("[XRRigSetup] ARSession 作成");
            }

            // ARCameraManager + ARCameraBackground でパススルーを有効化
            // OpenXR設定の「Meta Quest: Camera (Passthrough)」機能と連携して
            // Quest のカメラ映像を背景に描画する
            cameraObj.AddComponent<ARCameraManager>();
            Debug.Log("[XRRigSetup] パススルー有効化: ARSession + ARCameraManager");

            // パススルー時に白い地面が邪魔になるので非表示にする
            HideGroundForPassthrough();
        }
#endif

        Debug.Log("[XRRigSetup] XR Originリグ構築完了!");
    }

    void DisableExistingCameras()
    {
        // 既存のカメラを全て無効化
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            cam.enabled = false;
            Debug.Log($"[XRRigSetup] 既存カメラ無効化: {cam.gameObject.name}");
        }

        // 既存のAudioListenerも無効化（重複防止）
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in listeners)
        {
            listener.enabled = false;
        }

        // SteamVR Playerがあれば無効化
        var player = GameObject.Find("Player");
        if (player != null)
        {
#if UNITY_ANDROID
            // Android では SteamVR アセンブリが除外済みなので
            // Player を無効化しない（配下の金属板等が一緒に消えるのを防ぐ）
            // カメラとAudioListenerは上で既に無効化済み
            Debug.Log("[XRRigSetup] Android: SteamVR Player は無効化せず残す（子オブジェクト保護）");
#else
            player.SetActive(false);
            Debug.Log("[XRRigSetup] SteamVR Player を無効化");
#endif
        }
    }

    Transform CreateController(Transform parent, string name,
        string devicePath, GameObject handModel, bool isLeft)
    {
        var controllerObj = new GameObject(name);
        controllerObj.transform.SetParent(parent);
        controllerObj.transform.localPosition = Vector3.zero;

        // ActionBasedController: トラッキング + 入力 + ハプティクスを統合管理
        var controller = controllerObj.AddComponent<ActionBasedController>();

        // トラッキング（位置・回転）
        controller.positionAction = MakeAction("Position", devicePath + "/devicePosition");
        controller.rotationAction = MakeAction("Rotation", devicePath + "/deviceRotation");
        controller.trackingStateAction = MakeAction("TrackingState", devicePath + "/trackingState");

        if (isLeft)
        {
            // 左手: グリップ = つかむ（のみ）
            controller.selectAction = MakeAction("Select", devicePath + "/grip", InputActionType.Button);
            controller.selectActionValue = MakeAction("SelectValue", devicePath + "/grip");
        }
        else
        {
            // 右手: トリガー = つかむ、グリップ = 研磨
            controller.selectAction = MakeAction("Select", devicePath + "/trigger", InputActionType.Button);
            controller.selectActionValue = MakeAction("SelectValue", devicePath + "/trigger");
            controller.activateAction = MakeAction("Activate", devicePath + "/grip", InputActionType.Button);
            controller.activateActionValue = MakeAction("ActivateValue", devicePath + "/grip");
        }

        // ハプティクス用デバイス識別
        controller.hapticDeviceAction = MakeAction("Haptic", devicePath + "/devicePosition");

        controller.enableInputTracking = true;
        controller.enableInputActions = true;

        // --- 物理衝突（手が物体を押し返す） ---
        if (enableHandPhysics)
        {
            var controllerRb = controllerObj.AddComponent<Rigidbody>();
            controllerRb.isKinematic = true;   // トラッキングで動くのでキネマティック
            controllerRb.useGravity = false;
            controllerRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // 手のひら周辺の物理コライダー（非トリガー＝物体を押し返す）
            var handPhysicsObj = new GameObject("Hand Physics Collider");
            handPhysicsObj.transform.SetParent(controllerObj.transform);
            handPhysicsObj.transform.localPosition = new Vector3(0f, -0.02f, 0.04f);

            var handCol = handPhysicsObj.AddComponent<SphereCollider>();
            handCol.isTrigger = false;
            handCol.radius = handPhysicsRadius;

            // 指先付近の追加コライダー
            var fingerPhysicsObj = new GameObject("Finger Physics Collider");
            fingerPhysicsObj.transform.SetParent(controllerObj.transform);
            fingerPhysicsObj.transform.localPosition = new Vector3(0f, 0f, 0.08f);

            var fingerCol = fingerPhysicsObj.AddComponent<SphereCollider>();
            fingerCol.isTrigger = false;
            fingerCol.radius = handPhysicsRadius * 0.6f;
        }

        // --- Direct Interactor (直接つかみ) ---
        // XRI 3.xではInteractorとControllerが同じGameObjectにある必要がある
        var directInteractor = controllerObj.AddComponent<XRDirectInteractor>();

        var sphereCol = controllerObj.AddComponent<SphereCollider>();
        sphereCol.isTrigger = true;
        sphereCol.radius = directInteractionRange;

        // --- Ray Interactor ---
        if (enableRayInteraction)
        {
            var rayObj = new GameObject("Ray Interactor");
            rayObj.transform.SetParent(controllerObj.transform);
            rayObj.transform.localPosition = Vector3.zero;

            var rayInteractor = rayObj.AddComponent<XRRayInteractor>();
            rayInteractor.maxRaycastDistance = 10f;

            var lineRenderer = rayObj.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.005f;
            lineRenderer.endWidth = 0.005f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(0.3f, 0.8f, 1f, 0.5f);
            lineRenderer.endColor = new Color(0.3f, 0.8f, 1f, 0.1f);

            rayObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
        }

        // --- 手のビジュアル ---
        if (handModel != null)
        {
            var instance = Instantiate(handModel, controllerObj.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // SteamVRスクリプトを除去（Quest上でエラー防止）
            CleanupSteamVRComponents(instance);

            // URPマテリアルを保証（ピンク表示防止）
            EnsureURPMaterials(instance);

            // --- 指アニメーション（ボタン入力で手を握る） ---
            var handAnimator = instance.AddComponent<XRHandAnimator>();
            handAnimator.Initialize(isLeft, devicePath);

            Debug.Log($"[XRRigSetup] 手モデル適用: {handModel.name} ({name})");
        }
        else
        {
            // 手モデル未設定 → 簡易コントローラーモデルを生成
            CreateDefaultHandVisual(controllerObj.transform, isLeft);
        }

        return controllerObj.transform;
    }

    /// <summary>
    /// SteamVR依存コンポーネントをインスタンスから除去する
    /// （FBXやPrefabにSteamVR用スクリプトが付いている場合の安全策）
    /// </summary>
    void CleanupSteamVRComponents(GameObject instance)
    {
        // Animator は残す（ポーズ用）
        // SteamVR系のMonoBehaviourを探して除去
        var allBehaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var behaviour in allBehaviours)
        {
            if (behaviour == null) continue;
            string typeName = behaviour.GetType().FullName;
            if (typeName != null && (typeName.Contains("Valve.VR") || typeName.Contains("SteamVR")))
            {
                Debug.Log($"[XRRigSetup] SteamVRコンポーネント除去: {typeName}");
                Destroy(behaviour);
            }
        }
    }

    /// <summary>
    /// SteamVRシェーダーがURP非対応でピンクにならないよう、マテリアルを補正する
    /// </summary>
    void EnsureURPMaterials(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null) return;

        foreach (var r in renderers)
        {
            var materials = r.materials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].shader == null) continue;
                string shaderName = materials[i].shader.name;
                // URP/Built-in以外のシェーダー（SteamVR独自等）を置換
                if (!shaderName.StartsWith("Universal Render Pipeline") &&
                    !shaderName.StartsWith("Sprites") &&
                    !shaderName.StartsWith("UI/"))
                {
                    Color originalColor = Color.gray;
                    if (materials[i].HasProperty("_Color"))
                        originalColor = materials[i].color;
                    else if (materials[i].HasProperty("_BaseColor"))
                        originalColor = materials[i].GetColor("_BaseColor");

                    var newMat = new Material(urpLitShader);
                    newMat.SetColor("_BaseColor", originalColor);
                    materials[i] = newMat;
                    changed = true;
                }
            }
            if (changed) r.materials = materials;
        }
    }

    InputActionProperty MakeAction(string actionName, string binding, InputActionType type = InputActionType.Value)
    {
        var action = new InputAction(name: actionName, type: type, binding: binding);
        action.Enable();
        return new InputActionProperty(action);
    }

    void CreateDefaultHandVisual(Transform parent, bool isLeft)
    {
        Color mainColor = isLeft ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.15f, 0.15f, 0.15f);
        Color accentColor = isLeft ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);

        var visual = new GameObject(isLeft ? "Left Hand Visual" : "Right Hand Visual");
        visual.transform.SetParent(parent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // --- グリップ（持ち手）---
        var grip = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        grip.name = "Grip";
        grip.transform.SetParent(visual.transform);
        grip.transform.localPosition = new Vector3(0f, -0.04f, 0.02f);
        grip.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        grip.transform.localScale = new Vector3(0.04f, 0.055f, 0.04f);
        DestroyCollider(grip);
        SetColor(grip, mainColor);

        // --- 本体ヘッド部分（上部の平たい部分）---
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visual.transform);
        head.transform.localPosition = new Vector3(0f, 0.01f, 0.035f);
        head.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        head.transform.localScale = new Vector3(0.04f, 0.015f, 0.07f);
        DestroyCollider(head);
        SetColor(head, mainColor);

        // --- トラッキングリング（Questコントローラー特有の輪）---
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Tracking Ring";
        ring.transform.SetParent(visual.transform);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0.035f);
        ring.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        ring.transform.localScale = new Vector3(0.07f, 0.003f, 0.07f);
        DestroyCollider(ring);
        SetColor(ring, new Color(0.25f, 0.25f, 0.25f));

        // --- トリガー ---
        var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "Trigger";
        trigger.transform.SetParent(visual.transform);
        trigger.transform.localPosition = new Vector3(0f, -0.015f, 0.045f);
        trigger.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        trigger.transform.localScale = new Vector3(0.012f, 0.025f, 0.008f);
        DestroyCollider(trigger);
        SetColor(trigger, new Color(0.1f, 0.1f, 0.1f));

        // --- ボタンエリア（サムスティック風の丸） ---
        var thumbstick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        thumbstick.name = "Thumbstick";
        thumbstick.transform.SetParent(visual.transform);
        thumbstick.transform.localPosition = new Vector3(0f, 0.02f, 0.02f);
        thumbstick.transform.localRotation = Quaternion.identity;
        thumbstick.transform.localScale = new Vector3(0.015f, 0.005f, 0.015f);
        DestroyCollider(thumbstick);
        SetColor(thumbstick, new Color(0.05f, 0.05f, 0.05f));

        // --- ボタン A/B or X/Y ---
        var button1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        button1.name = isLeft ? "X Button" : "A Button";
        button1.transform.SetParent(visual.transform);
        button1.transform.localPosition = new Vector3(0f, 0.02f, 0.045f);
        button1.transform.localScale = new Vector3(0.01f, 0.003f, 0.01f);
        DestroyCollider(button1);
        SetColor(button1, accentColor);

        var button2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        button2.name = isLeft ? "Y Button" : "B Button";
        button2.transform.SetParent(visual.transform);
        button2.transform.localPosition = new Vector3(0.012f, 0.02f, 0.035f);
        button2.transform.localScale = new Vector3(0.01f, 0.003f, 0.01f);
        DestroyCollider(button2);
        SetColor(button2, accentColor * 0.7f);

        // --- ポインター先端（白い光点） ---
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Pointer Tip";
        tip.transform.SetParent(visual.transform);
        tip.transform.localPosition = new Vector3(0f, 0.005f, 0.075f);
        tip.transform.localScale = new Vector3(0.008f, 0.008f, 0.008f);
        DestroyCollider(tip);
        var tipRenderer = tip.GetComponent<Renderer>();
        if (tipRenderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = Color.white;
            tipRenderer.material = mat;
        }
    }

    void DestroyCollider(GameObject obj)
    {
        var col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void SetColor(GameObject obj, Color color)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    /// <summary>
    /// パススルー時に白い地面（Plane等）を非表示にする
    /// </summary>
    void HideGroundForPassthrough()
    {
        // シーン内の "Plane" オブジェクトを探して非表示にする
        var allObjects = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var mr in allObjects)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Plane")
            {
                mr.enabled = false;
                Debug.Log($"[XRRigSetup] パススルー: 地面メッシュ非表示 '{mr.gameObject.name}'");
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Force Setup")]
    void ForceSetup()
    {
        SetupRig();
    }
#endif
}

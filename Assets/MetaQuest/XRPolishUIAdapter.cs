// XRPolishUIAdapter.cs
// XR Interaction Toolkit対応のUI操作アダプター
// 下振りジェスチャーやボタンでメニューCanvas表示をトグル

using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// XRコントローラーのジェスチャー/ボタンでUIメニューをトグル。
///
/// 機能:
/// - 下振りジェスチャーでメニュー表示（XRコントローラー速度ベース）
/// - ボタン入力でメニュートグル（Bボタン / Yボタン）
/// </summary>
public class XRPolishUIAdapter : MonoBehaviour
{
    [Header("Input")]
    public XRNode controllerNode = XRNode.RightHand;

    [Header("Gesture")]
    [Tooltip("下振りの判定速度閾値 (m/s)")]
    public float gestureVelocityThreshold = 2.0f;

    [Header("Button Alternative")]
    [Tooltip("メニューボタンでも開閉可能にする")]
    public bool enableButtonToggle = true;

    [Header("UI Target")]
    [Tooltip("トグルするCanvasを直接指定（未設定の場合は子から検索）")]
    public Canvas targetCanvas;

    private InputDevice controller;
    private float lastGestureTime = 0f;
    private bool menuButtonPrev = false;

    void Start()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInChildren<Canvas>(true);
        RefreshController();
    }

    void RefreshController()
    {
        controller = InputDevices.GetDeviceAtXRNode(controllerNode);
    }

    void Update()
    {
        if (!controller.isValid)
        {
            RefreshController();
            return;
        }

        CheckGesture();

        if (enableButtonToggle)
            CheckButtonToggle();
    }

    void CheckGesture()
    {
        if (Time.time - lastGestureTime < 1.0f) return;

        if (controller.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity))
        {
            if (velocity.y < -gestureVelocityThreshold)
            {
                lastGestureTime = Time.time;
                ToggleMenu();
                Debug.Log($"[XRPolishUI] 下振りジェスチャー検出: vel.y={velocity.y:F2}");
            }
        }
    }

    void CheckButtonToggle()
    {
        bool menuButton = false;
        controller.TryGetFeatureValue(CommonUsages.secondaryButton, out menuButton);

        if (menuButton && !menuButtonPrev)
        {
            ToggleMenu();
            Debug.Log("[XRPolishUI] ボタンでメニュートグル");
        }

        menuButtonPrev = menuButton;
    }

    void ToggleMenu()
    {
        if (targetCanvas != null)
        {
            targetCanvas.gameObject.SetActive(!targetCanvas.gameObject.activeSelf);
        }
    }
}

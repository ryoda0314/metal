using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// XRI版 CubeController: つかんだ位置を保持する精密グラブ（Meta Quest対応）
/// SteamVR版 CubeController と同等の機能を XR Interaction Toolkit で実現。
/// GameObjectに XRGrabInteractable コンポーネントも必要。
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class XRCubeController : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // 精密つかみ: つかんだ場所を維持（中心にスナップしない）
        grabInteractable.useDynamicAttach = true;

        // 即座に手に追従
        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;

        // 投げ無効（手を離したらその場に留まる）
        grabInteractable.throwOnDetach = false;

        // 片手のみ（もう片方の手でつかむと元の手から離れる）
        grabInteractable.selectMode = InteractableSelectMode.Single;
    }
}

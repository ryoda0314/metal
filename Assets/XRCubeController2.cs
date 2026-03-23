using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// XRI版 CubeController2: 物理ベースのグラブ＋投げ（Meta Quest対応）
/// SteamVR版 CubeController2 と同等の機能を XR Interaction Toolkit で実現。
/// GameObjectに XRGrabInteractable と Rigidbody コンポーネントも必要。
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class XRCubeController2 : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // 物理設定
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // つかんだ場所を維持（中心にスナップしない）
        grabInteractable.useDynamicAttach = true;

        // 速度追従（物理ベース：手の動きに物理的に追従）
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;

        // 投げ有効（手の速度を引き継ぐ）
        grabInteractable.throwOnDetach = true;

        // 片手のみ
        grabInteractable.selectMode = InteractableSelectMode.Single;
    }
}

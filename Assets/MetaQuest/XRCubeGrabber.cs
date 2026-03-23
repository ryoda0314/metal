// XRCubeGrabber.cs
// CubeControllerのXR Interaction Toolkit版
// Meta Quest / OpenXR対応の精密つかみ

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class XRCubeGrabber : MonoBehaviour
{
    // 精密つかみ用：つかんだ時のオフセットを保存
    private Vector3 grabOffsetLocal;
    private Quaternion grabRotationLocal;
    private Transform attachedInteractorTransform;
    private bool isGrabbed = false;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // XRGrabInteractable の設定
        // 精密つかみ: Instantaneous（瞬時にアタッチ、スナップしない）
        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabInteractable.throwOnDetach = false;
        grabInteractable.useDynamicAttach = true; // つかんだ場所を維持

        // イベント登録
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        attachedInteractorTransform = args.interactorObject.GetAttachTransform(grabInteractable);

        // 手とオブジェクトの相対位置を保存
        grabOffsetLocal = attachedInteractorTransform.InverseTransformPoint(transform.position);
        grabRotationLocal = Quaternion.Inverse(attachedInteractorTransform.rotation) * transform.rotation;

        isGrabbed = true;

        // Rigidbodyをキネマティックに
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Debug.Log($"[XRCubeGrabber] Grabbed! Offset: {grabOffsetLocal}");
    }

    void OnReleased(SelectExitEventArgs args)
    {
        attachedInteractorTransform = null;
        isGrabbed = false;

        // Rigidbodyを元に戻す
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Debug.Log("[XRCubeGrabber] Released!");
    }

    void LateUpdate()
    {
        // つかんでいる間、保存したオフセットを使って位置を強制更新
        if (isGrabbed && attachedInteractorTransform != null)
        {
            Vector3 targetPos = attachedInteractorTransform.TransformPoint(grabOffsetLocal);
            Quaternion targetRot = attachedInteractorTransform.rotation * grabRotationLocal;

            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }
}

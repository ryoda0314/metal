using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a UI Button pressable by physical touch in VR.
/// Attach to a button GameObject that has a Collider.
/// </summary>
[RequireComponent(typeof(Button))]
public class VRTouchButton : MonoBehaviour
{
    Button button;
    float cooldown = 0.3f;
    float lastPressTime = -1f;
    
    [Tooltip("Visual feedback when touched")]
    public bool scaleOnHover = true;
    public bool pushOnPress = true; // New: Physical push effect
    Vector3 originalScale;
    
    private BoxCollider touchCollider;
    private bool colliderNeedsResize = false;

    void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;

        // Ensure we have a collider
        Collider col = GetComponent<Collider>();
        if (!col)
        {
            touchCollider = gameObject.AddComponent<BoxCollider>();
            touchCollider.isTrigger = true;
            // Defer proper sizing — layout may not be computed yet (ForceUpdateCanvases runs later)
            // Set a temporary size so the collider exists
            touchCollider.size = new Vector3(100f, 40f, 10f);
            colliderNeedsResize = true;
        }
    }

    void Start()
    {
        // Resize collider after layout pass has had a chance to run
        if (colliderNeedsResize && touchCollider != null)
        {
            ResizeCollider();
        }
    }

    void ResizeCollider()
    {
        RectTransform rt = GetComponent<RectTransform>();

        // Compute Z depth for adequate world-space touch zone thickness
        // WorldSpace Canvas under scaled parents can compress Z to sub-mm, causing tunneling
        float worldTouchDepth = 0.05f; // 5cm touch zone
        float lossyZ = Mathf.Abs(transform.lossyScale.z);
        float localZDepth = (lossyZ > 0.0001f) ? worldTouchDepth / lossyZ : 500f;
        localZDepth = Mathf.Max(localZDepth, 10f);

        if (rt)
        {
            float w = rt.rect.width;
            float h = rt.rect.height;
            // Fallback if rect not yet computed by layout
            if (w < 1f || h < 1f)
            {
                LayoutElement le = GetComponent<LayoutElement>();
                if (le)
                {
                    w = Mathf.Max(le.minWidth, le.preferredWidth);
                    h = Mathf.Max(le.minHeight, le.preferredHeight);
                }
                if (w < 1f) w = 100f;
                if (h < 1f) h = 40f;
            }
            touchCollider.size = new Vector3(w * 0.85f, h * 0.85f, localZDepth);
        }
        else
        {
            touchCollider.size = new Vector3(100f, 40f, localZDepth);
        }

        colliderNeedsResize = false;
        Debug.Log($"[VRTouch] {gameObject.name} collider size={touchCollider.size}, lossyZ={lossyZ:F6}, worldDepth={localZDepth * lossyZ:F4}m");
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if it's a hand/controller (you can filter by tag or layer)
        // Common tags: "Hand", "Controller", "Player"
        
        if (Time.time - lastPressTime < cooldown) return;
        
        if (button != null && button.interactable)
        {
            lastPressTime = Time.time;
            button.onClick.Invoke();
            
            // Visual/audio feedback
            if (scaleOnHover)
            {
                StartCoroutine(PressAnimation());
            }
            if (pushOnPress)
            {
                StartCoroutine(PushAnimation());
            }
            
            Debug.Log($"[VRTouch] Button pressed: {gameObject.name}");
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        // Optional: show hover state
        if (scaleOnHover)
        {
            // Fix: Don't change Y (Height) to preserve layout stability
            Vector3 target = originalScale;
            target.x *= 1.05f;
            target.z *= 1.05f;
            transform.localScale = target;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (scaleOnHover)
        {
            transform.localScale = originalScale;
        }
    }
    
    System.Collections.IEnumerator PressAnimation()
    {
        // Fix: Press animation also respects Y
        Vector3 target = originalScale;
        target.x *= 0.95f; // Slight shrink in width
        target.z *= 0.95f; 
        
        transform.localScale = target;
        yield return new WaitForSeconds(0.1f);
        transform.localScale = originalScale;
    }

    System.Collections.IEnumerator PushAnimation()
    {
        // Fix: Use CURRENT position because LayoutGroup sets X/Y after Awake
        Vector3 currentPos = transform.localPosition;
        
        // Push "in" (Z-axis only). Assuming rest Z is 0.
        // We use the current X/Y to avoid fighting the layout group.
        float pushDepth = 15f; 
        
        transform.localPosition = new Vector3(currentPos.x, currentPos.y, pushDepth);
        
        yield return new WaitForSeconds(0.15f);
        
        // Restore Z to 0, keeping potentially updated X/Y
        Vector3 releasePos = transform.localPosition;
        transform.localPosition = new Vector3(releasePos.x, releasePos.y, 0f);
    }
}

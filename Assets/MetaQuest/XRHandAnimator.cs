// XRHandAnimator.cs
// SteamVRグローブモデルの指をQuestコントローラー入力でアニメーションする
// FBXの休止ポーズを基準に、SteamVR OpenHand→Fist の差分回転を適用

using UnityEngine;
using UnityEngine.InputSystem;

public class XRHandAnimator : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty gripAction;
    public InputActionProperty triggerAction;
    public InputActionProperty thumbTouchAction;

    [Header("Settings")]
    public float smoothSpeed = 15f;

    private bool isLeft;
    private bool initialized;

    private float currentGrip;
    private float currentTrigger;
    private float currentThumb;

    // 指ボーンの情報
    private FingerBone[] thumbBones;
    private FingerBone[] indexBones;
    private FingerBone[] middleBones;
    private FingerBone[] ringBones;
    private FingerBone[] pinkyBones;

    struct FingerBone
    {
        public Transform transform;
        public Quaternion restRotation;  // FBXモデルの元の回転
        public Quaternion curlDelta;     // OpenHand→Fistの差分回転
    }

    // SteamVRボーンインデックス
    const int THUMB_0 = 2, THUMB_1 = 3, THUMB_2 = 4;
    const int INDEX_0 = 7, INDEX_1 = 8, INDEX_2 = 9;
    const int MIDDLE_0 = 12, MIDDLE_1 = 13, MIDDLE_2 = 14;
    const int RING_0 = 17, RING_1 = 18, RING_2 = 19;
    const int PINKY_0 = 22, PINKY_1 = 23, PINKY_2 = 24;

    // SteamVR ReferencePose_OpenHand (左右共通)
    static readonly Quaternion[] openPose = new Quaternion[31]
    {
        /* 0  root       */ Quaternion.identity,
        /* 1  wrist      */ Quaternion.identity,
        /* 2  thumb_0    */ new Quaternion(-0.46411175f, -0.623374f, 0.2721063f, 0.5674181f),
        /* 3  thumb_1    */ new Quaternion(0.08293856f, -0.019454371f, -0.055129882f, 0.9948384f),
        /* 4  thumb_2    */ new Quaternion(-0.0032133153f, -0.021866836f, 0.22201493f, 0.9747928f),
        /* 5  tip        */ Quaternion.identity,
        /* 6  idx_meta   */ Quaternion.identity,
        /* 7  index_0    */ new Quaternion(0.0070068412f, 0.039123755f, -0.08794935f, 0.9953317f),
        /* 8  index_1    */ new Quaternion(0.045808382f, -0.0021422536f, 0.0459431f, 0.9978909f),
        /* 9  index_2    */ new Quaternion(0.0018504566f, 0.022782495f, 0.013409463f, 0.9996488f),
        /* 10 tip        */ Quaternion.identity,
        /* 11 mid_meta   */ Quaternion.identity,
        /* 12 middle_0   */ new Quaternion(-0.16726136f, 0.0789587f, -0.06936778f, 0.9802945f),
        /* 13 middle_1   */ new Quaternion(0.018492563f, -0.013192348f, -0.05988611f, 0.99794674f),
        /* 14 middle_2   */ new Quaternion(-0.003327809f, 0.028225154f, 0.066315144f, 0.9973939f),
        /* 15 tip        */ Quaternion.identity,
        /* 16 ring_meta  */ Quaternion.identity,
        /* 17 ring_0     */ new Quaternion(-0.058696117f, 0.10181952f, -0.072495356f, 0.9904201f),
        /* 18 ring_1     */ new Quaternion(-0.0022397265f, -0.0000039300317f, -0.030081047f, 0.999545f),
        /* 19 ring_2     */ new Quaternion(-0.00072132144f, 0.012692659f, -0.040420394f, 0.9991019f),
        /* 20 tip        */ Quaternion.identity,
        /* 21 pinky_meta */ Quaternion.identity,
        /* 22 pinky_0    */ new Quaternion(-0.059614867f, 0.13516304f, -0.06913207f, 0.9866093f),
        /* 23 pinky_1    */ new Quaternion(0.0018961236f, 0.00013150928f, -0.10644623f, 0.99431664f),
        /* 24 pinky_2    */ new Quaternion(-0.00201019f, 0.052079126f, 0.073525675f, 0.99593055f),
        /* 25-30 */        Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity,
    };

    // SteamVR ReferencePose_Fist (左右共通)
    static readonly Quaternion[] fistPose = new Quaternion[31]
    {
        /* 0  root       */ Quaternion.identity,
        /* 1  wrist      */ Quaternion.identity,
        /* 2  thumb_0    */ new Quaternion(-0.2257035f, -0.836342f, 0.12641343f, 0.48333195f),
        /* 3  thumb_1    */ new Quaternion(-0.01330204f, 0.0829018f, -0.43944824f, 0.89433527f),
        /* 4  thumb_2    */ new Quaternion(0.00072834245f, -0.0012028969f, -0.58829284f, 0.80864674f),
        /* 5  tip        */ Quaternion.identity,
        /* 6  idx_meta   */ Quaternion.identity,
        /* 7  index_0    */ new Quaternion(-0.041852362f, 0.11180638f, -0.72633374f, 0.67689514f),
        /* 8  index_1    */ new Quaternion(-0.0005700487f, 0.115204416f, -0.81729656f, 0.56458294f),
        /* 9  index_2    */ new Quaternion(-0.010756178f, 0.027241308f, -0.66610956f, 0.7452787f),
        /* 10 tip        */ Quaternion.identity,
        /* 11 mid_meta   */ Quaternion.identity,
        /* 12 middle_0   */ new Quaternion(-0.09487112f, -0.05422859f, -0.7229027f, 0.68225396f),
        /* 13 middle_1   */ new Quaternion(0.0076794685f, -0.09769542f, -0.7635977f, 0.6382125f),
        /* 14 middle_2   */ new Quaternion(-0.06366954f, 0.00036316764f, -0.7530614f, 0.6548623f),
        /* 15 tip        */ Quaternion.identity,
        /* 16 ring_meta  */ Quaternion.identity,
        /* 17 ring_0     */ new Quaternion(-0.088269405f, 0.012672794f, -0.7085384f, 0.7000152f),
        /* 18 ring_1     */ new Quaternion(-0.0005935501f, -0.039828163f, -0.74642265f, 0.66427904f),
        /* 19 ring_2     */ new Quaternion(-0.027121458f, -0.005438834f, -0.7788164f, 0.62664175f),
        /* 20 tip        */ Quaternion.identity,
        /* 21 pinky_meta */ Quaternion.identity,
        /* 22 pinky_0    */ new Quaternion(-0.094065815f, 0.062634066f, -0.69046116f, 0.7144873f),
        /* 23 pinky_1    */ new Quaternion(0.00313052f, 0.03775632f, -0.7113834f, 0.7017823f),
        /* 24 pinky_2    */ new Quaternion(-0.008087321f, -0.003009417f, -0.7361885f, 0.6767216f),
        /* 25-30 */        Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity,
    };

    // ボーン名テーブル（finger部分のみ使用）
    static readonly string[] boneNamePatterns = new string[31]
    {
        null, null, // 0=root, 1=wrist → スキップ
        "finger_thumb_0_{0}", "finger_thumb_1_{0}", "finger_thumb_2_{0}",
        null, // 5=thumb_tip
        null, // 6=index_meta → スキップ（metacarpalは曲げない）
        "finger_index_0_{0}", "finger_index_1_{0}", "finger_index_2_{0}",
        null, // 10=index_tip
        null, // 11=middle_meta
        "finger_middle_0_{0}", "finger_middle_1_{0}", "finger_middle_2_{0}",
        null, // 15=middle_tip
        null, // 16=ring_meta
        "finger_ring_0_{0}", "finger_ring_1_{0}", "finger_ring_2_{0}",
        null, // 20=ring_tip
        null, // 21=pinky_meta
        "finger_pinky_0_{0}", "finger_pinky_1_{0}", "finger_pinky_2_{0}",
        null, // 25=pinky_tip
        null, null, null, null, null, // 26-30=aux
    };

    public void Initialize(bool leftHand, string devicePath)
    {
        isLeft = leftHand;

        if (gripAction.action == null || !gripAction.action.enabled)
        {
            var grip = new InputAction("HandGrip", InputActionType.Value, devicePath + "/grip");
            grip.Enable();
            gripAction = new InputActionProperty(grip);
        }
        if (triggerAction.action == null || !triggerAction.action.enabled)
        {
            var trigger = new InputAction("HandTrigger", InputActionType.Value, devicePath + "/trigger");
            trigger.Enable();
            triggerAction = new InputActionProperty(trigger);
        }
        if (thumbTouchAction.action == null || !thumbTouchAction.action.enabled)
        {
            var thumb = new InputAction("ThumbTouch", InputActionType.Value);
            thumb.AddBinding(devicePath + "/thumbstickTouched");
            thumb.AddBinding(devicePath + "/primaryTouched");
            thumb.Enable();
            thumbTouchAction = new InputActionProperty(thumb);
        }

        Invoke(nameof(FindBones), 0.1f);
    }

    void FindBones()
    {
        string suffix = isLeft ? "l" : "r";
        var allTransforms = GetComponentsInChildren<Transform>(true);

        thumbBones = BuildFingerBones(allTransforms, suffix, THUMB_0, THUMB_1, THUMB_2);
        indexBones = BuildFingerBones(allTransforms, suffix, INDEX_0, INDEX_1, INDEX_2);
        middleBones = BuildFingerBones(allTransforms, suffix, MIDDLE_0, MIDDLE_1, MIDDLE_2);
        ringBones = BuildFingerBones(allTransforms, suffix, RING_0, RING_1, RING_2);
        pinkyBones = BuildFingerBones(allTransforms, suffix, PINKY_0, PINKY_1, PINKY_2);

        int found = CountValid(thumbBones) + CountValid(indexBones) +
                    CountValid(middleBones) + CountValid(ringBones) + CountValid(pinkyBones);

        if (found > 0)
        {
            initialized = true;
            Debug.Log($"[XRHandAnimator] {(isLeft ? "左" : "右")}手ボーン検出: {found}本");
        }
        else
        {
            Debug.LogWarning($"[XRHandAnimator] {(isLeft ? "左" : "右")}手のボーンが見つかりません");
        }
    }

    FingerBone[] BuildFingerBones(Transform[] allTransforms, string suffix, params int[] boneIndices)
    {
        var result = new FingerBone[boneIndices.Length];
        for (int i = 0; i < boneIndices.Length; i++)
        {
            int idx = boneIndices[i];
            if (idx >= boneNamePatterns.Length || boneNamePatterns[idx] == null) continue;

            string boneName = boneNamePatterns[idx].Replace("{0}", suffix);
            foreach (var t in allTransforms)
            {
                if (t.name == boneName)
                {
                    // FBXの元の回転を保存（これが基準）
                    Quaternion rest = t.localRotation;
                    // OpenHand→Fistの差分回転を計算
                    Quaternion delta = Quaternion.Inverse(openPose[idx]) * fistPose[idx];

                    result[i] = new FingerBone
                    {
                        transform = t,
                        restRotation = rest,
                        curlDelta = delta
                    };
                    break;
                }
            }
        }
        return result;
    }

    int CountValid(FingerBone[] bones)
    {
        int count = 0;
        foreach (var b in bones)
            if (b.transform != null) count++;
        return count;
    }

    void Update()
    {
        if (!initialized) return;

        float targetGrip = gripAction.action != null ? gripAction.action.ReadValue<float>() : 0f;
        float targetTrigger = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;
        float targetThumb = thumbTouchAction.action != null ? thumbTouchAction.action.ReadValue<float>() : 0f;

        currentGrip = Mathf.Lerp(currentGrip, targetGrip, Time.deltaTime * smoothSpeed);
        currentTrigger = Mathf.Lerp(currentTrigger, targetTrigger, Time.deltaTime * smoothSpeed);
        currentThumb = Mathf.Lerp(currentThumb, targetThumb, Time.deltaTime * smoothSpeed);

        CurlFinger(thumbBones, currentThumb);
        CurlFinger(indexBones, currentTrigger);
        CurlFinger(middleBones, currentGrip);
        CurlFinger(ringBones, currentGrip);
        CurlFinger(pinkyBones, currentGrip);
    }

    void CurlFinger(FingerBone[] bones, float t)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].transform == null) continue;
            // FBXの休止ポーズに、OpenHand→Fistの差分をt量だけ適用
            bones[i].transform.localRotation =
                bones[i].restRotation * Quaternion.Slerp(Quaternion.identity, bones[i].curlDelta, t);
        }
    }
}

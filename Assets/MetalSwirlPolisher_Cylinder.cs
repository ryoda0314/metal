using UnityEngine;

[DisallowMultipleComponent]
public class MetalSwirlPolisher_Cylinder : MonoBehaviour
{
    [Header("Target Renderer & Slot")]
    public Renderer targetRenderer;      // 磨かれる側（Cube等）
    [Tooltip("MeshRenderer の Materials の何番目を使うか (0始まり)")]
    public int materialIndex = 0;

    [Header("Shader Property Names")]
    public string maskProperty = "_PolishMask";
    public string baseMetalProp = "_BaseMetallic";
    public string polMetalProp = "_PolishedMetallic";
    public string baseSmoothProp = "_BaseSmoothness";
    public string polSmoothProp = "_PolishedSmoothness";

    [Header("Mask Texture")]
    public int maskSize = 512;

    // 取得したマテリアルとマスク
    public Material runtimeMat { get; private set; }
    public Texture2D maskTex { get; private set; }

    int lastMatInstanceID = -1;

    void Start()
    {
        if (!targetRenderer) { Debug.LogError("[EnsureMask] targetRenderer 未設定"); enabled = false; return; }

        // 1) 対象スロットのマテリアルをクローンして差し替え
        var mats = targetRenderer.materials; // ← ここで自動的にインスタンス化されるが、明示的に差し替える
        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            Debug.LogError($"[EnsureMask] materialIndex={materialIndex} が範囲外 (len={mats.Length})");
            enabled = false; return;
        }
        mats[materialIndex] = new Material(mats[materialIndex]); // クローン
        targetRenderer.materials = mats;

        runtimeMat = mats[materialIndex];
        lastMatInstanceID = runtimeMat.GetInstanceID();

        // 2) マスク作成＆差し込み
        maskTex = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false, true);
        ClearMask();
        if (!runtimeMat.HasProperty(maskProperty))
            Debug.LogError($"[EnsureMask] Material に {maskProperty} がありません（ShaderGraphの Reference 名を確認）");

        runtimeMat.SetTexture(maskProperty, maskTex);

        // 見た目の差を強調
        TrySet(runtimeMat, baseMetalProp, 0.1f);
        TrySet(runtimeMat, polMetalProp, 1.0f);
        TrySet(runtimeMat, baseSmoothProp, 0.05f);
        TrySet(runtimeMat, polSmoothProp, 1.0f);

        Debug.Log($"[EnsureMask] Ready. Renderer={targetRenderer.name}, Slot={materialIndex}, Mat={runtimeMat.name} (id:{lastMatInstanceID})");
    }

    void LateUpdate()
    {
        // 3) 別のマテリアルに差し替えられていないか監視（他スクリプト対策）
        var mats = targetRenderer.materials;
        if (materialIndex >= 0 && materialIndex < mats.Length)
        {
            if (!ReferenceEquals(mats[materialIndex], runtimeMat))
            {
                Debug.LogWarning("[EnsureMask] 他から差し替え検知 → こちらのランタイムマテリアルを再設定");
                mats[materialIndex] = runtimeMat;
                targetRenderer.materials = mats;
            }
        }

        // 4) 念のため、毎フレーム _PolishMask が自分のテクスチャか確認し、違ったら戻す
        if (runtimeMat && runtimeMat.HasProperty(maskProperty))
        {
            var current = runtimeMat.GetTexture(maskProperty);
            if (!ReferenceEquals(current, maskTex))
            {
                Debug.LogWarning("[EnsureMask] _PolishMask が別Texに変わっていたため再設定");
                runtimeMat.SetTexture(maskProperty, maskTex);
            }
        }
    }

    public void ClearMask()
    {
        var buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 255);
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
    }

    public void FillWhite()
    {
        var buf = new Color32[maskSize * maskSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(255, 255, 255, 255);
        maskTex.SetPixels32(buf);
        maskTex.Apply(false, false);
    }

    void TrySet(Material m, string prop, float v)
    {
        if (!string.IsNullOrEmpty(prop) && m.HasProperty(prop)) m.SetFloat(prop, v);
    }
}

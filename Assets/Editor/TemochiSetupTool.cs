#if !UNITY_ANDROID
using UnityEngine;
using UnityEditor;

/// <summary>
/// Temochi（手持ち位置）の設定ツール
/// </summary>
public class TemochiSetupTool : EditorWindow
{
    private GameObject kenmaObject;
    private GameObject temochiObject;
    private Vector3 positionOffset = Vector3.zero;
    private Vector3 rotationOffset = Vector3.zero;
    private KenmaGripAttachment.AttachMode attachMode = KenmaGripAttachment.AttachMode.Parent;

    [MenuItem("Tools/Kenma Model/Temochi（手持ち）設定")]
    static void ShowWindow()
    {
        GetWindow<TemochiSetupTool>("Temochi設定");
    }

    void OnGUI()
    {
        GUILayout.Label("Temochi（手持ち位置）設定", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "KenmaをTemochi位置に固定します。\n" +
            "1. Kenma（固定される側）を選択\n" +
            "2. Temochi（固定先）を選択\n" +
            "3. 「固定」ボタンをクリック",
            MessageType.Info);

        EditorGUILayout.Space();

        kenmaObject = (GameObject)EditorGUILayout.ObjectField("Kenma (固定される側)", kenmaObject, typeof(GameObject), true);
        temochiObject = (GameObject)EditorGUILayout.ObjectField("Temochi (固定先)", temochiObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("オフセット調整", EditorStyles.boldLabel);
        positionOffset = EditorGUILayout.Vector3Field("位置オフセット", positionOffset);
        rotationOffset = EditorGUILayout.Vector3Field("回転オフセット", rotationOffset);

        EditorGUILayout.Space();
        attachMode = (KenmaGripAttachment.AttachMode)EditorGUILayout.EnumPopup("固定方法", attachMode);

        EditorGUILayout.Space();

        // Temochiポイント作成
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("新しいTemochiポイントを作成", GUILayout.Height(30)))
        {
            CreateTemochiPoint();
        }

        EditorGUILayout.Space();

        // 固定実行
        GUI.backgroundColor = Color.green;
        EditorGUI.BeginDisabledGroup(kenmaObject == null || temochiObject == null);
        if (GUILayout.Button("KenmaをTemochiに固定", GUILayout.Height(40)))
        {
            AttachKenmaToTemochi();
        }
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        // 固定解除
        GUI.backgroundColor = Color.yellow;
        EditorGUI.BeginDisabledGroup(kenmaObject == null);
        if (GUILayout.Button("固定を解除", GUILayout.Height(25)))
        {
            DetachKenma();
        }
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.white;
    }

    void CreateTemochiPoint()
    {
        // 選択中のオブジェクトの子としてTemochiポイントを作成
        GameObject parent = Selection.activeGameObject;

        GameObject temochi = new GameObject("Temochi");

        if (parent != null)
        {
            temochi.transform.SetParent(parent.transform);
            temochi.transform.localPosition = Vector3.zero;
            temochi.transform.localRotation = Quaternion.identity;
        }
        else
        {
            temochi.transform.position = Vector3.zero;
        }

        // 可視化用のGizmoコンポーネントを追加
        temochi.AddComponent<TemochiGizmo>();

        temochiObject = temochi;
        Selection.activeGameObject = temochi;

        Undo.RegisterCreatedObjectUndo(temochi, "Create Temochi Point");
        Debug.Log($"[Temochi] 新しいTemochiポイントを作成: {temochi.name}");
    }

    void AttachKenmaToTemochi()
    {
        Undo.RecordObject(kenmaObject, "Attach Kenma to Temochi");

        // 既存のKenmaGripAttachmentを取得または追加
        KenmaGripAttachment attachment = kenmaObject.GetComponent<KenmaGripAttachment>();
        if (attachment == null)
        {
            attachment = Undo.AddComponent<KenmaGripAttachment>(kenmaObject);
        }

        // 設定を適用
        attachment.gripPoint = temochiObject.transform;
        attachment.positionOffset = positionOffset;
        attachment.rotationOffset = rotationOffset;
        attachment.attachMode = attachMode;

        // エディタ上で即座に位置を反映（Parentモードの場合）
        if (attachMode == KenmaGripAttachment.AttachMode.Parent)
        {
            kenmaObject.transform.SetParent(temochiObject.transform);
            kenmaObject.transform.localPosition = positionOffset;
            kenmaObject.transform.localRotation = Quaternion.Euler(rotationOffset);
        }
        else
        {
            // 位置を移動
            kenmaObject.transform.position = temochiObject.transform.TransformPoint(positionOffset);
            kenmaObject.transform.rotation = temochiObject.transform.rotation * Quaternion.Euler(rotationOffset);
        }

        EditorUtility.SetDirty(kenmaObject);
        Debug.Log($"[Temochi] '{kenmaObject.name}' を '{temochiObject.name}' に固定しました");
    }

    void DetachKenma()
    {
        Undo.RecordObject(kenmaObject, "Detach Kenma");

        // 親子関係を解除
        kenmaObject.transform.SetParent(null);

        // コンポーネントを削除
        KenmaGripAttachment attachment = kenmaObject.GetComponent<KenmaGripAttachment>();
        if (attachment != null)
        {
            Undo.DestroyObjectImmediate(attachment);
        }

        EditorUtility.SetDirty(kenmaObject);
        Debug.Log($"[Temochi] '{kenmaObject.name}' の固定を解除しました");
    }
}
#endif

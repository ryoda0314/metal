// MetalSwirlPolisherEditor.cs
// カスタムInspector: リセットボタンを追加

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MetalSwirlPolisher))]
public class MetalSwirlPolisherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // デフォルトのInspectorを描画
        DrawDefaultInspector();

        MetalSwirlPolisher polisher = (MetalSwirlPolisher)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("研磨マスク操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // リセットボタン（黒にクリア）
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // 赤っぽい色
        if (GUILayout.Button("🔄 リセット (黒)", GUILayout.Height(30)))
        {
            polisher.ResetMask();
        }

        // 全面研磨ボタン（白で埋める）
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f); // 緑っぽい色
        if (GUILayout.Button("✨ 全面研磨 (白)", GUILayout.Height(30)))
        {
            polisher.FillMaskWhite();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("リセット: 研磨前の状態に戻す\n全面研磨: 全体を磨いた状態にする", MessageType.Info);
    }
}

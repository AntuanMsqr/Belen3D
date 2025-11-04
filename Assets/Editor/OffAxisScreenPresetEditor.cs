using UnityEditor;
using UnityEngine;
using Belen.Rendering;

[CustomEditor(typeof(OffAxisScreenPreset))]
public class OffAxisScreenPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var preset = (OffAxisScreenPreset)target;

        EditorGUI.BeginChangeCheck();
        preset.target = (OffAxisCamera)EditorGUILayout.ObjectField("Target", preset.target, typeof(OffAxisCamera), true);
        preset.preset = (OffAxisScreenPreset.Preset)EditorGUILayout.EnumPopup("Preset", preset.preset);
        preset.autoApply = EditorGUILayout.Toggle("Auto Apply", preset.autoApply);

        using (new EditorGUI.DisabledScope(preset.preset != OffAxisScreenPreset.Preset.Custom))
        {
            preset.diagonalInches = EditorGUILayout.FloatField("Diagonal (in)", preset.diagonalInches);
            preset.aspectX = EditorGUILayout.IntField("Aspect X", preset.aspectX);
            preset.aspectY = EditorGUILayout.IntField("Aspect Y", preset.aspectY);
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(preset);
            if (preset.autoApply)
            {
                preset.ApplyToTarget();
                if (preset.target != null)
                    EditorUtility.SetDirty(preset.target);
            }
        }

        GUILayout.Space(8);
        if (GUILayout.Button("Apply Preset To Target"))
        {
            preset.ApplyToTarget();
            if (preset.target != null)
                EditorUtility.SetDirty(preset.target);
        }
    }
}


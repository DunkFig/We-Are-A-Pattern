#if UNITY_EDITOR
using HSD.AudioBounce.Utilities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using Color = System.Drawing.Color;

namespace HSD.AudioBounce.Logistics
{
    [CustomEditor(typeof(AB_AudioSourceVolumetric))]
    public class AB_AudioSourceVolumetricEditor : Editor
    {
        private BoxBoundsHandle boxHandle = new BoxBoundsHandle();
        
        private Vector3 initialCenter;
        private bool isEditing = false; // To track the editing mode
        private ReorderableList list;

        private void OnEnable()
        {
            list = new ReorderableList(serializedObject, serializedObject.FindProperty("audioAreas"), true, true, true,
                true);

            // Override the behavior when a new item is added
            list.onAddCallback = (ReorderableList l) =>
            {
                var index = l.serializedProperty.arraySize;
                l.serializedProperty.arraySize++;
                l.index = index;
                var element = l.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("size").vector3Value = Vector3.one; // Set size to 1,1,1
                serializedObject.ApplyModifiedProperties();
            };
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();

            //Button to toggle editing mode
            if (GUILayout.Button(isEditing ? "Stop Editing" : "Edit"))
            {
                isEditing = !isEditing;
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            //Draw the label for each area
            AB_AudioSourceVolumetric areaComponent = (AB_AudioSourceVolumetric)target;
            for (int i = 0; i < areaComponent.audioAreas.Count; i++)
            {
                AB_AudioSourceVolumetric.AudioArea area = areaComponent.audioAreas[i];
                Vector3 worldSpaceCenter = areaComponent.transform.position + area.offset;

                // Draw label for the area
                Handles.Label(worldSpaceCenter + new Vector3(0, area.size.y / 2 + 0.5f, 0),
                    $"{areaComponent.name} Area {i + 1}"); // Adjust the label position if needed
            }

            
            
            if (!isEditing) return; // If not in editing mode, don't display the handles

            

            for (int i = 0; i < areaComponent.audioAreas.Count; i++)
            {
                AB_AudioSourceVolumetric.AudioArea area = areaComponent.audioAreas[i];
                Vector3 worldSpaceCenter = areaComponent.transform.position + area.offset;

                // Handle for offsetting the box
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldSpaceCenter = Handles.PositionHandle(worldSpaceCenter, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(areaComponent, "Change Audio Area Offset");
                    area.offset = newWorldSpaceCenter - areaComponent.transform.position;
                    areaComponent.audioAreas[i] = area; 
                }

                // Handle for resizing the box
                boxHandle.center = worldSpaceCenter;
                boxHandle.size = area.size;
                boxHandle.SetColor(AB_Utilities.ToColor32(Color.Salmon));
                
                initialCenter = boxHandle.center;

                EditorGUI.BeginChangeCheck();
                boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(areaComponent, "Change Audio Area Bounds");

                    Vector3 centerDifference = boxHandle.center - initialCenter;
                    area.offset += centerDifference;
                    area.size = boxHandle.size;

                    areaComponent.audioAreas[i] = area;
                }
            }
        }

    }
}

#endif

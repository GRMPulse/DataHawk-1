﻿#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Eliot.BehaviourEditor.Editor
{
    /// <summary>
    /// Editor extention for Behaviour objects.
    /// </summary>
    [CustomEditor(typeof(EliotBehaviour))]
    [CanEditMultipleObjects]
    public class BehaviourEditor : UnityEditor.Editor
    {
        /// Wheather the usr wants to turn on the developer tools.
        private bool _devTools;
        /// Json string of the Behaviour object.
        private string _text;
        /// All occurances of this string in object's Json be replaced...
        private string _replaceThis;
        /// ... with this string.
        private string _withThis;
        
        /// <summary>
        /// This function is called when the object is loaded.
        /// </summary>
        private void OnEnable()
        {
            _text = serializedObject.FindProperty("_json").stringValue;
        }

        /// <summary>
        /// Draw the new inspector properties.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            _devTools = EditorGUILayout.Toggle("Developer tools", _devTools);
            var labelSkin = GUI.skin.label;
            labelSkin.richText = true;
            EditorGUILayout.TextArea("<size=10>Do not mess around with developer tools unless " +
                                       "you know\nwhat you are doing. Otherwise it can lead to loss of data.</size>", labelSkin);
            if (_devTools)
            {
                EditorGUILayout.Space();
                
                _text = EditorGUILayout.TextArea(_text);

                if (GUILayout.Button("Save Json"))
                {
                    var behaviour = Selection.activeObject as EliotBehaviour;
                    if (behaviour)
                    {
                        behaviour.Json = _text;
                    }
                }

                _replaceThis = EditorGUILayout.TextField("Replace this", _replaceThis);
                _withThis = EditorGUILayout.TextField("With this", _withThis);

                if (GUILayout.Button("Replace"))
                {
                    var objects = Selection.objects;
                    foreach (var obj in objects)
                    {
                        var behaviour = obj as EliotBehaviour;
                        if (behaviour)
                        {
                            behaviour.Json = behaviour.Json.Replace(_replaceThis, _withThis);
                        }
                    }
                }

                if (GUILayout.Button("Update Version"))
                {
                    _text = BehaviourVersionManager.UpdateJson(_text);
                    var behaviour = Selection.activeObject as EliotBehaviour;
                    if (behaviour) behaviour.Json = _text;
                }
            }
        }       
    }
}
#endif
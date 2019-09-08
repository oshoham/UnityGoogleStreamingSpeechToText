using System;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace GoogleCloudStreamingSpeechToText {
    [CustomEditor(typeof(StreamingRecognizer))]
    public class StreamingRecognizerEditor : Editor {
        public override void OnInspectorGUI() {
            StreamingRecognizer listener = target as StreamingRecognizer;
            if (listener == null) {
                return;
            }

            serializedObject.Update();

            SerializedProperty startOnAwake = serializedObject.FindProperty("startOnAwake");
            SerializedProperty returnInterimResults = serializedObject.FindProperty("returnInterimResults");
            SerializedProperty enableDebugLogging = serializedObject.FindProperty("enableDebugLogging");
            SerializedProperty onStartListening = serializedObject.FindProperty("onStartListening");
            SerializedProperty onStopListening = serializedObject.FindProperty("onStopListening");
            SerializedProperty onFinalResult = serializedObject.FindProperty("onFinalResult");
            SerializedProperty onInterimResult = serializedObject.FindProperty("onInterimResult");

            string[] microphoneNames = Microphone.devices;
            int microphoneIndex = string.IsNullOrEmpty(listener.microphoneName)
                ? 0
                : Array.IndexOf(microphoneNames, listener.microphoneName);
            if (microphoneIndex == -1) {
                microphoneIndex = 0;
            }

            listener.microphoneName =
                microphoneNames[EditorGUILayout.Popup("Microphone", microphoneIndex, microphoneNames)];

            EditorGUILayout.PropertyField(startOnAwake);
            EditorGUILayout.PropertyField(returnInterimResults);
            EditorGUILayout.PropertyField(enableDebugLogging);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (listener.IsListening()) {
                if (GUILayout.Button("Stop")) {
                    listener.StopListening();
                }
            } else {
                if (GUILayout.Button("Start")) {
                    listener.StartListening();
                }
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(onStartListening);
            EditorGUILayout.PropertyField(onStopListening);
            EditorGUILayout.PropertyField(onFinalResult);
            if (listener.returnInterimResults) {
                EditorGUILayout.PropertyField(onInterimResult);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif

/**
  Copyright 2019 Google LLC

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

        https://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
**/

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// A custom editor for the RemoteConfigSyncBehaviour, adding a few features:
  /// * The KeyPrefix control is only shown if syncBehaviour.PrefixSource == PrefixSource.Custom.
  /// * The IncludeSubFields control is only shown if syncBehaviour.SyncAllFields == true.
  /// * Buttons to update sync targets and open the RemoteConfigSyncWindow are added.
  /// </summary>
  [CustomEditor(typeof(RemoteConfigSyncBehaviour))]
  public class SyncBehaviourEditor : UnityEditor.Editor {
    private const string stylePath =
        "Assets/firebase-unity-solutions/Firebase_RemoteConfig/Editor/Resources/sync_styles.uss";
    private VisualElement rootVisualElement;
    private EnumField prefixSourceField;
    private PropertyField keyPrefixField;
    private PropertyField syncAllField;
    private PropertyField includeSubFieldsField;
    private RemoteConfigSyncBehaviour syncBehaviour;

    /// <summary>
    /// Overridden to trigger when the window is shown or target is switched.
    /// </summary>
    public override VisualElement CreateInspectorGUI() {
      return rootVisualElement;
    }

    /// <summary>
    /// Creates the UI elements and registers callbacks.
    /// </summary>
    public void OnEnable() {
      syncBehaviour = target as RemoteConfigSyncBehaviour;
      rootVisualElement = new VisualElement();
      rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath));

      // Show the Script field (not-editable) like standard inspectors do.
      var script = MonoScript.FromMonoBehaviour(syncBehaviour);
      var scriptField = new ObjectField("Script") {
        value = script
      };
      scriptField.SetEnabled(false);
      rootVisualElement.Add(scriptField);

      // PrefixSource enum dropdown control. When PrefixSource changes, observe whether the
      // KeyPrefix field should be shown/hidden.
      var prefixSourceProp = serializedObject.FindProperty("PrefixSource");
      prefixSourceField = new EnumField("Prefix Source", syncBehaviour.PrefixSource);
      prefixSourceField.BindProperty(prefixSourceProp);
      prefixSourceField.RegisterCallback<ChangeEvent<Enum>>(OnPrefixSourceChange);
      rootVisualElement.Add(prefixSourceField);

      // KeyPrefix control, only shown if syncBehaviour.PrefixSource == PrefixSource.Custom.
      var keyPrefixProperty = serializedObject.FindProperty("KeyPrefix");
      keyPrefixField = new PropertyField(keyPrefixProperty);
      if (syncBehaviour.PrefixSource == PrefixSource.Custom) {
        rootVisualElement.Add(keyPrefixField);
      }

      // Sync All Fields control.
      var syncAllProp = serializedObject.FindProperty("SyncAllFields");
      syncAllField = new PropertyField(syncAllProp);
      syncAllField.RegisterCallback<ChangeEvent<bool>>(OnSyncAllFieldsChange);
      rootVisualElement.Add(syncAllField);

      // Include sub-fields
      var includeSubProp = serializedObject.FindProperty("IncludeSubFields");
      includeSubFieldsField = new PropertyField(includeSubProp);
      if (syncBehaviour.SyncAllFields) {
        rootVisualElement.Add(includeSubFieldsField);
      }

      var buttonContainer = new TemplateContainer();
      // Use row class to place buttons side-by-side.
      buttonContainer.AddToClassList("row");

      // Add a button that can invoke the SyncFields function on the object during gameplay.
      var syncButton = new Button(() => SyncFields()) {
        text = "Sync Fields"
      };
      buttonContainer.Add(syncButton);

      // Add a button that prompts RemoteConfigSyncUIWindow to update sync targets.
      var updateButton = new Button(() => SyncWindow.RefreshSyncTargets()) {
        text = "Update targets"
      };
      buttonContainer.Add(updateButton);

      // Add button to open the RemoteConfigSyncWindow.
      var rcWindowButton = new Button(() => SyncWindow.OpenWindow()) {
        text = "Open Sync Window"
      };
      buttonContainer.Add(rcWindowButton);
      rootVisualElement.Add(buttonContainer);
    }

    private void SyncFields() {
      if (EditorApplication.isPlaying) {
        // While in play mode, invoke the coroutine as normal.
        syncBehaviour.SyncFields();
      } else {
        // Otherwise sync all fields immediately, as coroutines don't run in editor mode.
        var routine = syncBehaviour.SyncFieldsCR();
        while (routine.MoveNext()) {
        }
      }
    }

    /// <summary>
    /// Callback when the PrefixSource field changes, to show/hide the KeyPrefix field.
    /// </summary>
    /// <param name="evt">Change event for PrefixSource control.</param>
    private void OnPrefixSourceChange(ChangeEvent<Enum> evt) {
      syncBehaviour.PrefixSource = (PrefixSource)evt.newValue;
      if (syncBehaviour.PrefixSource == PrefixSource.Custom) {
        rootVisualElement.Insert(rootVisualElement.IndexOf(prefixSourceField) + 1, keyPrefixField);
        keyPrefixField.Bind(serializedObject);
      } else {
        keyPrefixField.RemoveFromHierarchy();
      }
    }

    /// <summary>
    /// Callback when the SyncAll field changes, to show/hide the IncludeSubFields field.
    /// </summary>
    /// <param name="evt">Change event for SyncAllFields control.</param>
    private void OnSyncAllFieldsChange(ChangeEvent<bool> evt) {
      if (evt.newValue) {
        rootVisualElement.Insert(
            rootVisualElement.IndexOf(syncAllField) + 1,
            includeSubFieldsField);
        includeSubFieldsField.Bind(serializedObject);
      } else {
        syncBehaviour.IncludeSubFields = false;
        includeSubFieldsField.RemoveFromHierarchy();
      }
    }
  }
}

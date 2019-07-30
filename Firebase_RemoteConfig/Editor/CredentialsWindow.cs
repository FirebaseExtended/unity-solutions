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

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// Simple EditorWindow with a single Text Asset field, prompting the user to set from
  /// downloaded google-credentials.json file.
  /// </summary>
  public class CredentialsWindow : EditorWindow {
    /// <summary>
    /// Prompt to open the credentials window.
    /// </summary>
    [MenuItem("Window/Firebase/Remote Config/Credentials")]
    private static void ShowCredentialsWindow(MenuCommand command) {
      OpenWindow();
    }

    /// <summary>
    /// Public method to open the window, so it can be triggered externally.
    /// </summary>
    public static void OpenWindow() {
      // Open next to SceneView if possible.
      GetWindow<CredentialsWindow>("Remote Config Credentials", true, typeof(SceneView));
    }

    /// <summary>
    /// Runs when window first opens and creates the UI/registers callbacks.
    /// </summary>
    public void OnEnable() {
      // If the credential path EditorPrefs key is set, find the associated TextAsset file
      // to initialize the ObjectField.
      TextAsset credentialFile = null;
      if (EditorPrefs.HasKey(SyncDataManager.CredentialPathKey)) {
        var credentialPath = EditorPrefs.GetString(SyncDataManager.CredentialPathKey);
        credentialFile = AssetDatabase.LoadAssetAtPath<TextAsset>(credentialPath);
      }

      var credentialField = new ObjectField("Google Credential File") {
        value = credentialFile
      };
      credentialField.RegisterCallback<ChangeEvent<Object>>(SetCredentialPath);
      rootVisualElement.Add(credentialField);

      credentialField.objectType = typeof(TextAsset);
    }

    /// <summary>
    /// Callback when the Text Asset field is assigned. Checks the file is a valid google
    /// credential file before setting the EditorPrefs key. Logs an error otherwise.
    /// </summary>
    private void SetCredentialPath(ChangeEvent<Object> evt) {
      if (evt.newValue == null) {
        EditorPrefs.DeleteKey(SyncDataManager.CredentialPathKey);
        return;
      }

      // Verify that file is valid GoogleCredential.
      var credentialPath = AssetDatabase.GetAssetPath(evt.newValue);
      EditorPrefs.SetString(SyncDataManager.CredentialPathKey, credentialPath);
      if (!SyncDataManager.TryGetGoogleCredential(out var credential)) {
        Debug.LogError("Invalid google credential file format. Please download " +
            "from Cloud Console at https://console.cloud.google.com.");
        ((ObjectField)evt.target).value = null;
      }
    }
  }
}

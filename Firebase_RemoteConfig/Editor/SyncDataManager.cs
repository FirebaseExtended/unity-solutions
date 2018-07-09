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

using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// Static class that handles retrieving/updating RemoteConfig data via web requests.
  ///
  /// This class is used only in Editor mode, to support syncing to Remote Config via REST
  /// API. During play mode, the Unity RemoteConfig API is used to retrieve values instead.
  /// </summary>
  public static class SyncDataManager {
    public static readonly string CredentialPathKey = "FirebaseRemoteConfigCredentialPath";
    public static string LocalFilePath => Application.persistentDataPath + "/remote-config.json";

    private static readonly string getUrl =
        "https://firebaseremoteconfig.googleapis.com/v1/projects/{0}/remoteConfig";
    private static string remoteConfigUrl =>
        string.Format(getUrl, FirebaseApp.DefaultInstance.Options.ProjectId);
    private static readonly string remoteConfigScope =
        "https://www.googleapis.com/auth/firebase.remoteconfig";
    private static readonly string oauthUrl = "https://accounts.google.com/o/oauth2/auth";

    /// <summary>
    /// The last RemoteConfigData retrieved via REST API.
    /// </summary>
    public static RemoteConfigData CurrentData;

    /// <summary>
    /// Event handler for when Remote Config data is retrieved successfully.
    /// </summary>
    public static event EventHandler<RemoteConfigData> DataRetrieved;

    /// <summary>
    /// Event handler for when new Remote Config data is uploaded successfully.
    /// </summary>
    public static event EventHandler<RemoteConfigData> DataUploaded;

    /// <summary>
    /// Event handler for when an error occurs when downloading or uploading data.
    /// </summary>
    public static event EventHandler<ErrorArgs> RetrieveError;

    /// <summary>
    /// Actions that need to execute on the main thread, but may be triggered from non-main threads
    /// during Task execution. Queued here so they occur on the Update loop in Editor.
    /// </summary>
    private static List<Action> QueuedCallbacks = new List<Action>();

    /// <summary>
    /// Coroutines that are executing on the main thread. Editor windows have no ExecuteCoroutine
    /// function, so one is simulated here by subscribing to the Editor's update loop whenever
    /// there are any coroutines in this list.
    /// </summary>
    private static List<IEnumerator> ExecutingCoroutines = new List<IEnumerator>();

    /// <summary>
    /// Firebase access token retrieved via google credentials.
    /// </summary>
    private static string accessToken;

    /// <summary>
    /// Sets a GoogleCredential based on the google credential asset file set via the setup window.
    /// </summary>
    /// <param name="credential">GoogleCredential to set on success.</param>
    /// <returns>True if credential file was valid format.</returns>
    public static bool TryGetGoogleCredential(out GoogleCredential credential) {
      credential = null;
      if (!EditorPrefs.HasKey(CredentialPathKey)) {
        return false;
      }

      var credentialPath = EditorPrefs.GetString(CredentialPathKey);
      try {
        credential = GoogleCredential.FromFile(credentialPath);
      } catch (Exception) {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Begins the process of retrieving RemoteConfig data.
    /// Triggers `DataRetrieved` event when complete.
    /// </summary>
    public static void GetRemoteConfigDataAsync() {
      if (string.IsNullOrWhiteSpace(accessToken)) {
        GetAccessToken(PlaceRemoteConfigGetRequest);
      } else {
        PlaceRemoteConfigGetRequest();
      }
    }

    /// <summary>
    /// Uploads new data to Remote Config on server via REST API.
    /// </summary>
    /// <param name="newData">Data to upload to Remote Config.</param>
    public static void UpdateRemoteConfigAsync(RemoteConfigData newData) {
      if (string.IsNullOrWhiteSpace(accessToken)) {
        GetAccessToken(() => UpdateRemoteConfig(newData));
      } else {
        UpdateRemoteConfig(newData);
      }
    }

    /// <summary>
    /// Retrieves an access token from a GoogleCredential, then triggers an optional callback.
    /// </summary>
    /// <param name="andThen">Optional callback to invoke after access token is retrieved.</param>
    private static void GetAccessToken(Action andThen = null) {
      // Only retrieve a new access token if one hasn't already been retrieved.
      if (string.IsNullOrWhiteSpace(accessToken)) {
        if (!TryGetGoogleCredential(out var credential)) {
          RetrieveError?.Invoke(null, new ErrorArgs(
            new Exception("Invalid google credential file."),
            invalidCreds: true));
          return;
        }
        credential.CreateScoped(remoteConfigScope)
            .UnderlyingCredential
            .GetAccessTokenForRequestAsync(oauthUrl)
                .ContinueWith(task => {
                  if (task.Status == TaskStatus.Faulted) {
                    accessToken = "";
                    var exception = new Exception(
                        $"Exception retrieving access token from Firebase.",
                        task.Exception);
                    RetrieveError?.Invoke(null, new ErrorArgs(exception));
                    return;
                  }
                  accessToken = task.Result;
                  if (string.IsNullOrWhiteSpace(accessToken)) {
                    RetrieveError?.Invoke(null, new ErrorArgs(new ArgumentNullException(
                        "Empty access token retrieved from Firebase.")));
                    return;
                  }
                  if (andThen != null) {
                    QueueCallback(andThen);
                  }
                });
      } else if (andThen != null) {
        QueueCallback(andThen);
      }
    }

    /// <summary>
    /// Once a valid access token is retrieved, place the GET request to retrieve
    /// RemoteConfig data from firebase.
    /// </summary>
    private static void PlaceRemoteConfigGetRequest() {
      var req = UnityWebRequest.Get(remoteConfigUrl);
      req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
      req.SendWebRequest().completed += op => {
        if (!op.isDone) {
          return;
        }
        if (req.responseCode == 401L) {
          // Access Token has expired - get a new one and retry.
          accessToken = null;
          GetAccessToken(PlaceRemoteConfigGetRequest);
          return;
        }
        SetCurrentData(req.downloadHandler.text);
        DataRetrieved?.Invoke(CurrentData, CurrentData);
      };
    }

    /// <summary>
    /// Upload new RemoteConfigData via REST API.
    /// </summary>
    /// <param name="newData">New data to upload to Remote Config.</param>
    private static void UpdateRemoteConfig(RemoteConfigData newData) {
      var newRemoteConfigData = JsonConvert.SerializeObject(newData);
      var webRequest = UnityWebRequest.Put(remoteConfigUrl, newRemoteConfigData);
      webRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
      webRequest.SetRequestHeader("Content-Type", "application/json; UTF8");
      webRequest.SetRequestHeader("If-Match", "*");

      string data = "";
      webRequest.SendWebRequest().completed += op => {
        data = webRequest.downloadHandler.text;
        if (webRequest.isHttpError) {
          var exception = new Exception(
              $"Error uploading RemoteConfig: {webRequest.error}\nData: {data}");
          RetrieveError.Invoke(null, new ErrorArgs(exception));
          return;
        }

        SetCurrentData(data);
        DataUploaded(CurrentData, CurrentData);
      };
    }

    /// <summary>
    /// Deserialize the JSON RemoteConfigData into the CurrentData field.
    /// </summary>
    /// <param name="json">String data to deserialize.</param>
    private static void SetCurrentData(string json) {
      // Save to local docs file so it doesn't have to be retrieved on every compile time.
      if (!File.Exists(LocalFilePath)) {
        File.Create(LocalFilePath).Close();
      }
      File.WriteAllText(LocalFilePath, json);

      CurrentData = RemoteConfigData.Deserialize(json);
    }

    /// <summary>
    /// Queue an action to be executed by the main thread on the next update.
    /// </summary>
    /// <param name="action">Callback to perform on next update loop.</param>
    private static void QueueCallback(Action action) {
      if (ExecutingCoroutines.Count == 0 && QueuedCallbacks.Count == 0) {
        EditorApplication.update += ExecuteCallbacksAndCoroutines;
      }

      QueuedCallbacks.Add(action);
    }

    /// <summary>
    /// While there are any executing coroutines or queued callbacks, this function runs once
    /// every update loop. Once there is none left of either, unsubscribes itself until another
    /// coroutine or callback is scheduled.
    ///
    /// This is necessary because some code, such as triggering event handlers and placing
    /// UnityWebRequests, needs to be on the main thread, and can't be triggered from within a
    /// Task callback.
    /// </summary>
    private static void ExecuteCallbacksAndCoroutines() {
      if (ExecutingCoroutines.Count == 0 && QueuedCallbacks.Count == 0) {
        // No callbacks or coroutines to execute: unsubscribe from EditorApplication.update.
        EditorApplication.update -= ExecuteCallbacksAndCoroutines;
        return;
      }

      // Execute queued callbacks.
      while (QueuedCallbacks.Count > 0) {
        var callback = QueuedCallbacks[0];
        if (callback != null) {
          callback.Invoke();
        }
        QueuedCallbacks.RemoveAt(0);
      }

      // Move forward each executing coroutine one step.
      for (int i = 0; i < ExecutingCoroutines.Count; i++) {
        // If coroutine is complete, remove from list (and decrement `i` so none are skipped).
        if (ExecutingCoroutines[i] == null || !ExecutingCoroutines[i].MoveNext()) {
          ExecutingCoroutines.RemoveAt(i--);
        }
      }
    }
  }
}

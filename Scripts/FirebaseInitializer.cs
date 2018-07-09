/**
  Copyright 2018 Google LLC

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
using System.Collections.Generic;
using Firebase.ConfigAutoSync;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Firebase.Unity {
  /// <summary>
  /// This class statically initializes the Firebase app, checking and fixing dependencies.
  /// When another class wants to get a reference to Firebase, it should use
  /// FirebaseInitializer.Initialize(callback) to ensure the app is initialized first.
  /// </summary>
  public static class FirebaseInitializer {
    private static List<Action<DependencyStatus>> initializedCallbacks =
      new List<Action<DependencyStatus>>();
    private static List<Action> activateFetchCallbacks = new List<Action>();
    private static DependencyStatus dependencyStatus;
    private static bool initialized = false;
    private static bool fetching = false;
    private static bool activateFetched = false;

    /// <summary>
    /// Invoke this with a callback to perform some action once the Firebase App is initialized.
    /// If the Firebase App is already initialized, the callback will be invoked immediately.
    /// </summary>
    /// <param name="callback">The callback to perform once initialized.</param>
    public static void Initialize(Action<DependencyStatus> callback) {
      lock (initializedCallbacks) {
        if (initialized) {
          callback(dependencyStatus);
          return;
        } else {
          initializedCallbacks.Add(callback);
        }
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
          lock (initializedCallbacks) {
            dependencyStatus = task.Result;
            initialized = true;
            CallInitializedCallbacks();
          }
        });
      }
    }

    /// <summary>
    /// Calls FetchAsync and ActivateFetched on FirebaseRemoteConfig, initializing its values and
    /// triggering provided callbacks once.
    /// </summary>
    /// <param name="callback">Callback to schedule after RemoteConfig is initialized.</param>
    /// <param name="forceRefresh">If true, force refresh of RemoteConfig params.</param>
    public static void RemoteConfigActivateFetched(Action callback, bool forceRefresh=false) {
      lock (activateFetchCallbacks) {
        if (activateFetched && !forceRefresh) {
          callback();
          return;
        } else {
          activateFetchCallbacks.Add(callback);
        }
        if (!fetching) {
#if UNITY_EDITOR
          var settings = FirebaseRemoteConfig.Settings;
          settings.IsDeveloperMode = true;
          FirebaseRemoteConfig.Settings = settings;
#endif
          // Get the default values from the current SyncTargets.
          var syncObjects = Resources.FindObjectsOfTypeAll<RemoteConfigSyncBehaviour>();
          var syncTargets = SyncTargetManager.FindTargets(syncObjects).GetFlattenedTargets();
          var defaultValues = new Dictionary<string, object>();
          foreach (var target in syncTargets.Values) {
            defaultValues[target.FullKeyString] = target.Value;
          }

          Initialize(status => {
            FirebaseRemoteConfig.SetDefaults(defaultValues);
            FirebaseRemoteConfig.FetchAsync(TimeSpan.Zero).ContinueWith(task => {
              lock (activateFetchCallbacks) {
                fetching = false;
                activateFetched = true;
                var newlyActivated = FirebaseRemoteConfig.ActivateFetched();
                CallActivateFetchedCallbacks();
              }
            });
          });
        }
      }
    }

    private static void CallInitializedCallbacks() {
      lock (initializedCallbacks) {
        foreach (var callback in initializedCallbacks) {
          callback(dependencyStatus);
        }
        initializedCallbacks.Clear();
      }
    }

    private static void CallActivateFetchedCallbacks() {
      lock (activateFetchCallbacks) {
        foreach (var callback in activateFetchCallbacks) {
          callback.Invoke();
        }
        activateFetchCallbacks.Clear();
      }
    }
  }
}

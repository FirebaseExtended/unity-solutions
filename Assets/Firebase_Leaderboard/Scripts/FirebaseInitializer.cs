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

namespace Firebase.Leaderboard {
  /// <summary>
  /// This class statically initializes the Firebase app, checking and fixing dependencies.
  /// When another class wants to get a reference to Firebase, it should use
  /// FirebaseInitializer.Initialize(callback) to ensure the app is initialized first.
  /// </summary>
  public static class FirebaseInitializer {
    private static List<System.Action<Firebase.DependencyStatus>> initializedMethods =
      new List<System.Action<Firebase.DependencyStatus>>();
    private static Firebase.DependencyStatus dependencyStatus;
    private static bool initialized = false;

    /// <summary>
    /// Invoke this with a callback to perform some action once the Firebase App is initialized.
    /// If the Firebase App is already initialized, the callback will be invoked immediately.
    /// </summary>
    /// <param name="initializedMethod">The callback to perform once initialized.</param>
    public static void Initialize(System.Action<Firebase.DependencyStatus> initializedMethod) {
      lock (initializedMethods) {
        if (initialized) {
          initializedMethod(dependencyStatus);
          return;
        } else {
          initializedMethods.Add(initializedMethod);
        }
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
          lock (initializedMethods) {
            dependencyStatus = task.Result;
            initialized = true;
            CallInitializedMethods();
          }
        });
      }
    }

    private static void CallInitializedMethods() {
      lock (initializedMethods) {
        foreach (var initializedMethod in initializedMethods) {
          initializedMethod(dependencyStatus);
        }
        initializedMethods.Clear();
      }
    }
  }
}

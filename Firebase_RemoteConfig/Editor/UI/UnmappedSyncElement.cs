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

using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// A UI SyncElement field for RemoteConfig parameters whose keys did not match any
  /// discovered SyncTargets. This may mean the targets are in a different scene, or
  /// these can just be unmapped sync values.
  /// </summary>
  public class UnmappedSyncElement : SyncTypeElement<string, TextField> {
    /// <summary>
    /// Constructor with the unmapped RemoteConfigParameter.
    /// </summary>
    /// <param name="param">The RemoteConfigParameter with no matching SyncTarget.</param>
    public UnmappedSyncElement(RemoteConfigParameter param) : base(param) {
    }

    /// <summary>
    /// For unmapped parameters, updating sync choice just means toggling
    /// the sync attribute and updating UI state.
    /// </summary>
    /// <param name="newValue">New value of the sync Toggle.</param>
    public override void UpdateSyncChoice(bool newValue) {
      if (newValue) {
        Param.SetValue(Param.GetValue(null, true) ?? "");
      } else {
        Param.UnsetValue();
      }
      UpdateDirtyState(newValue);
    }
  }
}

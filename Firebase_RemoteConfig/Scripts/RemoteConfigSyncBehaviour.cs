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

using Firebase.RemoteConfig;
using Firebase.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// Attach this behaviour to any GameObject asset or prefab to indicate
  /// that it contains fields that should be synced to/from Remote Config.
  /// </summary>
  public class RemoteConfigSyncBehaviour : MonoBehaviour {
    /// <summary>
    /// By default, keys will be prefixed by the name of the Component class in which they are
    /// found.
    /// To use the name of the GameObject instead (e.g. to differentiate between instances of the
    /// same type), select "GameObject." To use a custom prefix string (or no prefix), select
    /// "Custom".
    /// </summary>
    public PrefixSource PrefixSource = PrefixSource.Component;

    /// <summary>
    /// Only used if PrefixSource = Custom is selected. This is a user-defined string used as a
    /// prefix for this sync target.
    /// </summary>
    public string KeyPrefix;

    /// <summary>
    /// By default, only fields with the [RemoteConfigSync] attribute will be synced to/from
    /// Remote Config. If set to true, instead all fields (given the other parameters below)
    /// will be kept in sync without requiring the attribute.
    ///
    /// You can use the [RemoteConfigSkipSync] attribute to ignore specific fields in components
    /// which otherwise sync all fields.
    /// </summary>
    public bool SyncAllFields;

    /// <summary>
    /// Set this to recursively check the fields of Object fields with the [Serializable] attribute.
    /// Only applies to non-RemoteConfigSync-tagged fields when SyncAllFields is selected.
    /// </summary>
    public bool IncludeSubFields;

    /// <summary>
    /// Subscribe to this event to be notified when syncing is complete.
    /// </summary>
    public event EventHandler SyncComplete;

    /// <summary>
    /// Set to true once this object's SyncTargets have been retrieved. A flag is needed instead of
    /// calling the Sync function directly, because retrieving RemoteConfig data can only be done
    /// on the main thread, not as a response to FirebaseInitialized.
    /// </summary>
    private bool readyToSync = false;

    /// <summary>
    /// A set of KeyValuePairs mapping SyncTargets to their sources for this Object.
    /// </summary>
    private Dictionary<object, List<SyncTarget>> _flattenedTargets;
    private Dictionary<object, List<SyncTarget>> flattenedTargets {
      get {
        if (_flattenedTargets == null) {
          _flattenedTargets = SyncTargetManager.AddNewSyncContainer(this);
        }
        return _flattenedTargets;
      }
    }

    /// <summary>
    /// When a component with RemoteConfigSyncBehaviour is created, sync its field values
    /// with the values in RemoteConfig.
    /// </summary>
    private void Start() {
      // Find all the fields that require syncing in the other components of this GameObject.
      FirebaseInitializer.RemoteConfigActivateFetched(() => {
        readyToSync = true;
      });
    }

    /// <summary>
    /// During update, listen for flag saying the object is ready to sync its fields.
    /// </summary>
    private void Update() {
      if (readyToSync) {
        readyToSync = false;
        SyncFields();
      }
    }

    /// <summary>
    /// Get the key prefix for this object based on the PrefixSource field and the name of the
    /// component containing target fields.
    /// </summary>
    public string GetKeyPrefix(Component component) {
      switch (PrefixSource) {
      case PrefixSource.Component:
        return component.GetType().Name;
      case PrefixSource.GameObject:
        return component.gameObject.name;
      case PrefixSource.Custom:
        return KeyPrefix;
      }
      return null;
    }

    /// <summary>
    /// Trigger a call to get the SyncTargets for this GameObject, then sync those targets
    /// with RemoteConfig.
    /// </summary>
    public void SyncFields() {
      StartCoroutine(SyncFieldsCR());
    }

    /// <summary>
    /// Target syncing done as a coroutine, breaking between each sourceObject with field targets,
    /// to prevent attempting to set too many fields at once.
    /// </summary>
    public IEnumerator SyncFieldsCR() {
      if (flattenedTargets == null) {
        Debug.LogWarning($"No sync targets found for {name}");
        yield break;
      }
      foreach (var kv in flattenedTargets) {
        var sourceObject = kv.Key;
        var targets = kv.Value;
        foreach (var target in targets) {
          var value = FirebaseRemoteConfig.GetValue(target.FullKeyString);
          if (value.Source == ValueSource.RemoteValue) {
            if (target.Field.GetValue(sourceObject)?.ToString() == value.StringValue) {
              continue;
            }
            object typedValue = value.StringValue;
            if (typeof(bool).IsAssignableFrom(target.Field.FieldType)) {
              typedValue = value.BooleanValue;
            } else if (typeof(double).IsAssignableFrom(target.Field.FieldType)) {
              typedValue = value.DoubleValue;
            } else if (typeof(int).IsAssignableFrom(target.Field.FieldType)) {
              typedValue = (int)value.LongValue;
            }
            target.Field.SetValue(sourceObject, typedValue);
          } else {
            Debug.Log($"No RemoteConfig value found for key {target.FullKeyString}");
          }
          yield return 0;
        }
      }
      SyncComplete?.Invoke(this, null);
    }
  }

  /// <summary>
  /// Enum representing the different sources for a key prefix on SyncTargetContainers.
  /// </summary>
  [Serializable]
  public enum PrefixSource {
    Component,
    GameObject,
    Custom
  }
}

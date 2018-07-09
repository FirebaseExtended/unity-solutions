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

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// This static class finds all the SyncItems available.
  /// Objects with valid targets are GameObjects in the current scene, and Prefabs which have the
  /// RemoteConfigSyncBehaviour attached & enabled.
  /// Sync targets are fields discovered on other Components attached to the object, or potentially
  /// nested objects within those components. Which fields are valid SyncTargets depends on the
  /// values of the attached RemoteConfigSyncBehaviour.
  /// </summary>
  public static class SyncTargetManager {
    /// <summary>
    /// Alias to a map of RemoteConfigSyncBehaviour to all the targets found on its GameObject.
    /// These objects are further mapped to the source objects that contain the fields specifically
    /// referenced by the SyncTarget, so that when discovered/created at runtime the field values
    /// can be easily set by RemoteConfigSyncBehaviour without affecting existing targets.
    /// </summary>
    private class BehaviourTargets :
        Dictionary<RemoteConfigSyncBehaviour, Dictionary<object, List<SyncTarget>>> {
    }

    /// <summary>
    /// Keep track of components already processed to avoid duplicated work.
    /// </summary>
    private static readonly BehaviourTargets foundTargets = new BehaviourTargets();

    /// <summary>
    /// SyncTargetContainer parent of all the discovered sync targets.
    /// </summary>
    private static SyncTargetContainer _syncTargets;
    private static SyncTargetContainer SyncTargets {
      get {
        if (_syncTargets == null) {
          _syncTargets = new SyncTargetContainer();
        }
        return _syncTargets;
      }
      set => _syncTargets = value;
    }

    /// <summary>
    /// Find all the valid SyncTargets from the provided RemoteConfigSyncBehaviour instances.
    /// </summary>
    /// <param name="syncObjects">Scene and Prefab objects from which to find targets.</param>
    /// <returns>Top-level SyncTargetContainer ancestor of all discovered targets.</returns>
    public static SyncTargetContainer FindTargets(
        IEnumerable<RemoteConfigSyncBehaviour> syncObjects) {
      _syncTargets = new SyncTargetContainer();
      foundTargets.Clear();
      foreach (var syncBehaviour in syncObjects) {
        AddNewSyncContainer(syncBehaviour);
      }

      return _syncTargets;
    }

    /// <summary>
    /// Add the SyncTargets found on a GameObject with a RemoteConfigSyncBehaviour component.
    /// </summary>
    /// <param name="syncBehaviour">RemoteConfigSyncBehaviour on the target GameObject.</param>
    /// <returns>Top-level SyncTargetContainer of the gameObject.</returns>
    public static Dictionary<object, List<SyncTarget>> AddNewSyncContainer(
        RemoteConfigSyncBehaviour syncBehaviour) {
      if (foundTargets.ContainsKey(syncBehaviour)) {
        return foundTargets[syncBehaviour];
      }
      if (!syncBehaviour.enabled) {
        return null;
      }

      // Cycle through all other Components on the GameObject, finding SyncTarget fields.
      var components = syncBehaviour.GetComponents<MonoBehaviour>();
      var objectTargets = new Dictionary<object, List<SyncTarget>>();
      foreach (var component in components) {
        if (component == syncBehaviour) {
          continue;
        }
        // Top level key path is determined by the PrefixSource field on RemoteConfigSyncBehaviour.
        var keys = new List<string>();
        var componentKey = syncBehaviour.GetKeyPrefix(component);
        if (!string.IsNullOrWhiteSpace(componentKey)) {
          keys.Add(componentKey);
        }
        AddSyncContainer(syncBehaviour, component, keys, objectTargets);
      }

      foundTargets[syncBehaviour] = objectTargets;
      return objectTargets;
    }

    /// <summary>
    /// Adds the container Component and all the targets found within it.
    /// </summary>
    /// <param name="component">
    /// The RemoteConfigSyncBehaviour component on the top-level component.
    /// </param>
    /// <param name="sourceObject">The source object containing this level of SyncTargets.</param>
    /// <param name="keys">Key path to the current source object.</param>
    /// <param name="foundTargets">Dictionary to add SyncTargets to as they are found.</param>
    /// <param name="path">Object path to the current level, to prevent circular references.</param>
    /// <param name="ancestorSyncAttr">
    /// Reference to an ancestor RemoteConfigSyncAttribute, if any.
    /// </param>
    private static void AddSyncContainer(
        RemoteConfigSyncBehaviour component,
        object sourceObject,
        List<string> keys,
        Dictionary<object, List<SyncTarget>> foundTargets,
        List<object> path = null,
        RemoteConfigSyncAttribute ancestorSyncAttr = null) {
      // Quit out of circular references.
      if (path == null) {
        path = new List<object>();
      }
      if (path.Contains(sourceObject)) {
        return;
      }
      path.Add(sourceObject);
      BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
      var fields = new List<FieldInfo>(sourceObject.GetType().GetFields(flags));

      if (component.SyncAllFields || ancestorSyncAttr != null) {
        // If `component.SyncAllFields` or object is a nested sub field, add all fields except
        // those with the `[RemoteConfigSkipSync]` Attribute.
        fields.RemoveAll(f => f.GetCustomAttribute<RemoteConfigSkipSyncAttribute>() != null);
      } else {
        // Otherwise, only add fields with the `[RemoteConfigSync]` Attribute.
        fields.RemoveAll(f => f.GetCustomAttribute<RemoteConfigSyncAttribute>() == null);
      }

      foreach (var field in fields) {
        CheckSyncMember(
            component,
            sourceObject,
            path,
            keys,
            field,
            foundTargets,
            ancestorSyncAttr);
      }
    }

    /// <summary>
    /// Check if the field of a sourceObject should be added as a SyncTarget.
    /// </summary>
    /// <param name="component">The RemoteConfigSyncBehaviour that started this path.</param>
    /// <param name="sourceObject">The object directly containing the field to check.</param>
    /// <param name="path">The object path from the top-level component to this field.</param>
    /// <param name="keys">The key path from the top-level container to this field.</param>
    /// <param name="field">The FieldInfo describing the field being checked.</param>
    /// <param name="foundTargets">Map containing all the targets found in this hierarchy.</param>
    /// <param name="ancestorSyncAttr">
    /// Non-null if this field descends from a field tagged with [RemoteConfigSync].
    /// </param>
    private static void CheckSyncMember(
        RemoteConfigSyncBehaviour component,
        object sourceObject,
        List<object> path,
        List<string> keys,
        FieldInfo field,
        Dictionary<object, List<SyncTarget>> foundTargets,
        RemoteConfigSyncAttribute ancestorSyncAttr) {
      // If field is tagged with [RemoteConfigSync], it may affect the key used for this target.
      var syncAttr = field.GetCustomAttribute<RemoteConfigSyncAttribute>();
      if (syncAttr != null && !string.IsNullOrWhiteSpace(syncAttr.Key)) {
        keys = new List<string> { syncAttr.Key };
      } else {
        keys = new List<string>(keys) { field.Name };
      }

      // Add the member to SyncTargets.
      // If it is a primitive/string type, add its literal value as a SyncTarget.
      if (field.FieldType.IsPrimitive || field.FieldType == typeof(string)) {
        var syncTarget = SyncTargets.AddSyncTarget(field, sourceObject, keys);
        if (syncTarget == null) {
          return;
        }
        if (!foundTargets.ContainsKey(sourceObject)) {
          foundTargets[sourceObject] = new List<SyncTarget>();
        }
        foundTargets[sourceObject].Add(syncTarget);
        return;
      }

      // If field is a serializable data object, and component.SyncAllFields is true or this field
      // (or its ancestor field) are tagged with [RemoteConfigSync], add the object's fields as
      // SyncTargets, and this object will be mapped to a SyncTargetContainer.
      if (field.FieldType.IsSerializable &&
          (component.IncludeSubFields || ancestorSyncAttr != null || syncAttr != null)) {
        AddSyncContainer(
            component,
            field.GetValue(sourceObject),
            keys,
            foundTargets,
            path,
            ancestorSyncAttr ?? syncAttr);
      }
    }
  }
}

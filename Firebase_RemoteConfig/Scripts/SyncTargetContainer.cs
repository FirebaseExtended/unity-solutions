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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// A type of SyncItem that acts as a container for other SyncItems (targets and containers).
  /// This item does not have its own parameter in Remote Config; rather, its FullKeyString is a
  /// prefix for all of its descendent targets.
  /// </summary>
  public class SyncTargetContainer : SyncItem {
    /// <summary>
    /// Map of keys to this container's direct children.
    /// </summary>
    public Dictionary<string, SyncItem> Items = new Dictionary<string, SyncItem>();

    /// <summary>
    /// Adds the new target to this tree, whether it's a direct child or a descendent.
    /// </summary>
    /// <param name="field">The field that describes the local SyncTarget.</param>
    /// <param name="sourceObject">The object on which the field was found.</param>
    /// <param name="key">The key path from this object to the key.</param>
    /// <returns>The newly created SyncTarget.</returns>
    public SyncTarget AddSyncTarget(FieldInfo field, object sourceObject, List<string> key) {
      // Base error case: if no keys remain, discard with warning.
      if (key.Count == 0) {
        Debug.LogWarning("No SyncTarget could be created for field " + field.Name);
        return null;
      }
      var fullChildKey = new List<string>(FullKey) {
        key[0]
      };
      // If key path is only 1 item, the new SyncTarget is direct child of this container.
      if (key.Count == 1) {
        // If the target already exists, add the new source object to it so that it can sync all
        // fields when requested.
        // Otherwise, create the new target and add it to this container's children.
        SyncTarget target;
        if (Items.ContainsKey(key[0])) {
          if (!(Items[key[0]] is SyncTarget)) {
            // If existing target is actually a container, it can't be added as a target now.
            Debug.LogWarning(
              $"Cannot add {fullChildKey} as target, already registered as a container.");
            return null;
          }
          target = Items[key[0]] as SyncTarget;
          if (target.SourceObjects.Contains(sourceObject)) {
            return target;
          }
          target.SourceObjects.Add(sourceObject);
        } else {
          Items[key[0]] = target = new SyncTarget(field, sourceObject, fullChildKey);
        }
        return target;
      }

      // If key list is longer, the new target is an indirect descendent of this group.
      // Traverse down until it can be added to its parent (creating new containers as necessary).
      SyncTargetContainer childContainer;
      if (!Items.ContainsKey(key[0])) {
        Items[key[0]] = childContainer = new SyncTargetContainer {
          FullKey = fullChildKey
        };
      } else {
        childContainer = Items[key[0]] as SyncTargetContainer;
      }

      // Recurse to child container to continue.
      return childContainer.AddSyncTarget(field, sourceObject, key.GetRange(1, key.Count - 1));
    }

    /// <summary>
    /// Finds a sync item within this item's hierarchy.
    /// </summary>
    public SyncItem Find(string key) {
      return Find(key.Split(splitKeySeparator, StringSplitOptions.RemoveEmptyEntries).ToList());
    }

    /// <summary>
    /// Traverse down through this item's hierarchy to find the target with the given key path.
    /// Implemented in SyncTargetContainer, as an individual SyncTarget has no
    /// </summary>
    public virtual SyncItem Find(List<string> key) {
      if (key.Count == 0) {
        return null;
      }

      if (!Items.TryGetValue(key[0], out SyncItem child)) {
        return null;
      }

      if (key.Count == 1) {
        return child;
      }

      // Can't traverse down through path of a SyncTarget.
      if (!(child is SyncTargetContainer)) {
        return null;
      }

      return (child as SyncTargetContainer).Find(key.GetRange(1, key.Count - 1));
    }

    /// <summary>
    /// Flatten this tree of potentially nested SyncTargets and SyncTargetContainers
    /// into a map of key -> SyncTarget.
    /// </summary>
    /// <returns>Flattened map of key -> SyncTarget.</returns>
    public Dictionary<string, SyncTarget> GetFlattenedTargets() {
      var flattened = new Dictionary<string, SyncTarget>();
      foreach (var item in Items.Values) {
        if (item is SyncTarget) {
          flattened[item.FullKeyString] = item as SyncTarget;
        } else {
          var subFlattened = (item as SyncTargetContainer).GetFlattenedTargets();
          foreach (var kv in subFlattened) {
            flattened[kv.Key] = kv.Value;
          }
        }
      }
      return flattened;
    }
  }
}

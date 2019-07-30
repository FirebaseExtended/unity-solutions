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
using System.Linq;

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// Base class for an item in the sync target hierarchy, either a direct target
  /// (a field with a value to sync) or a data container of such targets.
  /// </summary>
  public abstract class SyncItem {
    protected static readonly string KEY_SEPARATOR = "__";
    protected static readonly string[] splitKeySeparator = new string[] { KEY_SEPARATOR };

    /// <summary>
    /// The full key path to this field as a list of key item strings.
    /// </summary>
    public List<string> FullKey = new List<string>();

    /// <summary>
    /// Convenience getter for the key path as a string, joined by KEY_SEPARATOR.
    /// </summary>
    public string FullKeyString {
      get {
        return string.Join(KEY_SEPARATOR, FullKey);
      }
    }

    /// <summary>
    /// This item's reference key, relative to its parent.
    /// </summary>
    public string Key {
      get {
        return FullKey.Last();
      }
    }
  }
}

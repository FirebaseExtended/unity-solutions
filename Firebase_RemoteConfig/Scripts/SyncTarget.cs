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

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// A field on a synced object to sync to and from Remote Config in Editor and during gameplay.
  /// Each target may be mapped to multiple source objects
  /// </summary>
  public class SyncTarget : SyncItem {
    /// <summary>
    /// The FieldInfo for the field as it is declared on the source object type.
    /// </summary>
    public FieldInfo Field;

    /// <summary>
    /// List of the source objects found with the above FieldInfo matching this SyncTarget's key.
    /// </summary>
    public List<object> SourceObjects = new List<object>();

    /// <summary>
    /// Gets or sets the target value to/from the source objects found.
    /// </summary>
    public object Value {
      get {
        if (SourceObjects.Count == 0) {
          UnityEngine.Debug.LogWarning("Cannot get sync value with no source objects.");
          return null;
        }
        return Field.GetValue(SourceObjects[0]);
      }
      set {
        SourceObjects.ForEach(ob => Field.SetValue(ob, value));
      }
    }

    /// <summary>
    /// Standard constructor containing required fields.
    /// </summary>
    /// <param name="field">The FieldInfo for the target field.</param>
    /// <param name="sourceObject">The first source object found with the given field.</param>
    /// <param name="fullKey">The full key path for the discovered target.</param>
    public SyncTarget(FieldInfo field, object sourceObject, List<string> fullKey) {
      Field = field;
      FullKey = new List<string>(fullKey);
      SourceObjects.Add(sourceObject);
    }

    public override bool Equals(object obj) {
      return obj is SyncTarget member &&
             EqualityComparer<MemberInfo>.Default.Equals(Field, member.Field) &&
             FullKeyString == member.FullKeyString;
    }

    public override int GetHashCode() {
      var hashCode = -1781094297;
      hashCode = hashCode * -1521134295 + EqualityComparer<MemberInfo>.Default.GetHashCode(Field);
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Key);
      return hashCode;
    }
  }
}

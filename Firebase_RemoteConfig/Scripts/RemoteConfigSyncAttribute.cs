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

namespace Firebase.ConfigAutoSync {
  /// <summary>
  /// Attach this attribute to a field on a Unity Object to make it discoverable to
  /// RemoteConfigSyncBehaviour.
  /// </summary>
  [AttributeUsage(AttributeTargets.Field, Inherited = true)]
  public class RemoteConfigSyncAttribute : Attribute {
    /// <summary>
    /// Use this to override the generated parameter name for this field. The field will not
    /// contain any of the prefixes of its path, this will be the entire identifier.
    /// </summary>
    public string Key;

    public RemoteConfigSyncAttribute() {
    }

    /// <summary>
    /// A constructor supplying the unique key for this field.
    /// </summary>
    /// <param name="key">The unique key for the field.</param>
    public RemoteConfigSyncAttribute(string key) {
      Key = key;
    }
  }
}

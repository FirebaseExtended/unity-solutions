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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Firebase.ConfigAutoSync
{
  /// <summary>
  /// Serializable version of RemoteConfig data in the format expected by the REST API, as:
  /// {
  ///  "parameters":[
  ///    {
  ///      "key":"welcome_message",
  ///      "value_options":[
  ///        {
  ///          "value":"Welcome to this sample app"
  ///        }
  ///      ]
  ///    },
  ///    {
  ///      "key":"welcome_message_caps",
  ///      "value_options":[
  ///        {
  ///          "value":"false"
  ///        }
  ///      ]
  ///    }
  ///  ],
  ///  "version":{
  ///    "version_number": "42",
  ///    "update_time":"2018-05-11T18:46:40Z",
  ///    "update_user":{
  ///      "name":"Jane Developer",
  ///      "email":"jane@developer.org",
  ///      "imageUrl":"http://image.google.com/path-to-profile-photo-for-jane"
  ///    },
  ///    "description":"Adding welcome messages",
  ///    "origin":"CONSOLE",
  ///    "update_type":"INCREMENTAL_UPDATE"
  ///  }
  ///}
  /// </summary>
  [Serializable]
  public class RemoteConfigData {
    /// <summary>
    /// Parameters mapped to their keys.
    /// </summary>
    public Dictionary<string, RemoteConfigParameter> parameters =
        new Dictionary<string, RemoteConfigParameter>();
    /// <summary>
    /// Conditions created in the Firebase console.
    /// </summary>
    public List<RemoteConfigCondition> conditions = new List<RemoteConfigCondition>();
    public VersionInfo version {
      get;
      set;
    }

    public static RemoteConfigData Deserialize(string json) {
      var deserialized = JsonConvert.DeserializeObject<RemoteConfigData>(json);
      foreach (var kv in deserialized.parameters) {
        kv.Value.Key = kv.Key;
      }
      return deserialized;
    }

    /// <summary>
    /// Creates a new RemoteConfigData object whose default and conditional values are set from
    /// this instance's locally modified values.
    /// </summary>
    /// <returns>New RemoteConfigData with updated conditional/default values.</returns>
    public RemoteConfigData CreateUploadData() {
      var newData = new RemoteConfigData {
        conditions = conditions
      };

      foreach (var kv in parameters) {
        var newParam = kv.Value.CreateUploadData();
        if (newParam != null) {
          newData.parameters[kv.Key] = newParam;
        }
      }
      return newData;
    }

    /// <summary>
    /// If a parameter exists for the given key, return it. Otherwise, create a new parameter
    /// with the optional starting local value.
    /// </summary>
    /// <param name="key">Parameter key to retrieve or create.</param>
    /// <param name="localValue">Optional starting local value if param is created.</param>
    /// <returns>The retrieved or created parameter.</returns>
    public RemoteConfigParameter GetOrCreateParameter(string key, string localValue = null) {
      if (!parameters.ContainsKey(key)) {
        parameters[key] = new RemoteConfigParameter {
          Key = key,
          defaultValue = new RemoteConfigValue()
        };
        parameters[key].SetValue(localValue);
      }
      return parameters[key];
    }
  }

  /// <summary>
  /// The value data object for a parameter default or conditional value.
  /// </summary>
  [Serializable]
  public class RemoteConfigValue {
    /// <summary>
    /// Value as a string. RemoteConfig parameters can also be ints, doubles, or bools, but
    /// string is a unifying type for serialization.
    /// </summary>
    public string value;

    /// <summary>
    /// For ease of initialization, allow assignment by string.
    /// </summary>
    /// <param name="value">String to convert.</param>
    public static implicit operator RemoteConfigValue(string value) =>
        new RemoteConfigValue { value = value };
  }

  /// <summary>
  /// A parameter in RemoteConfig, including a default value and conditional values.
  /// Also defines several non-serialized properties tracking local values, in order to
  /// show when values are "dirty" (not in-sync with server state) in the UI.
  /// </summary>
  [Serializable]
  public class RemoteConfigParameter {
    /// <summary>
    /// Default value of the param, if no RemoteConfig conditions apply.
    /// </summary>
    public RemoteConfigValue defaultValue;

    /// <summary>
    /// Values for each RemoteConfig condition, if any.
    /// </summary>
    public Dictionary<string, RemoteConfigValue> conditionalValues =
        new Dictionary<string, RemoteConfigValue>();

    /// <summary>
    /// Track non-serialized Key for this parameter for use in code.
    /// </summary>
    [JsonIgnore] public string Key;

    /// <summary>
    /// Convenience getter indicating the parameter has a synced value.
    /// </summary>
    [JsonIgnore] public bool existsOnServer => defaultValue.value != null;

    /// <summary>
    /// Used to track the value as it is edited locally, before being synced to Remote Config.
    /// </summary>
    [JsonIgnore] public RemoteConfigValue _localValue;
    [JsonIgnore]
    public RemoteConfigValue LocalValue {
      get {
        if (_localValue == null) {
          _localValue = new RemoteConfigValue {
            value = defaultValue?.value
          };
        }
        return _localValue;
      }
    }

    /// <summary>
    /// Local changes made to conditional values, not (yet) synced with Remote Config.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, RemoteConfigValue> localConditionalValues =
        new Dictionary<string, RemoteConfigValue>();

    /// <summary>
    /// Returns true if the param has a non-null synced or local conditional value.
    /// </summary>
    /// <param name="conditionName">Condition name to check.</param>
    /// <returns>True if there a synced and/or non-null local conditional value.</returns>
    public bool HasConditionalValue(string conditionName) {
      if (string.IsNullOrWhiteSpace(conditionName)) {
        return LocalValue.value != null;
      }
      if (localConditionalValues.TryGetValue(
          conditionName,
          out RemoteConfigValue conditionalValue)) {
        return conditionalValue?.value != null;
      }
      if (conditionalValues.TryGetValue(conditionName, out conditionalValue)) {
        return conditionalValue?.value != null;
      }
      return false;
    }

    /// <summary>
    /// Check if the local value for given condition matches the synced value.
    /// </summary>
    /// <param name="conditionName">Name of the condition to check, if any.</param>
    /// <returns>True if the local value matches the synced value.</returns>
    public bool IsInSync(string conditionName = null) {
      // If no condition value, check default synced vs local value.
      if (string.IsNullOrWhiteSpace(conditionName)) {
        // Whitespace == null for this purpose.
        if (string.IsNullOrWhiteSpace(defaultValue?.value) &&
            string.IsNullOrWhiteSpace(LocalValue.value)) {
          return true;
        }
        return defaultValue?.value == LocalValue?.value;
      }

      // If no local conditional value, then synced value
      if (!localConditionalValues.ContainsKey(conditionName)) {
        return true;
      }

      if (!conditionalValues.ContainsKey(conditionName)) {
        return false;
      }

      return conditionalValues[conditionName]?.value ==
          localConditionalValues[conditionName]?.value;
    }

    /// <summary>
    /// Gets the default or conditional value for this parameter.
    /// </summary>
    /// <param name="conditionName">Name of conditional value to get, or default.</param>
    /// <param name="syncedValue">If true, get the synced value instead of the local one.</param>
    /// <returns>Local or synced conditional value.</returns>
    public RemoteConfigValue GetValue(string conditionName = null, bool syncedValue = false) {
      if (syncedValue) {
        // Return the synced value instead of any local override.
        if (string.IsNullOrWhiteSpace(conditionName)) {
          return defaultValue;
        }

        if (conditionalValues.ContainsKey(conditionName)) {
          return conditionalValues[conditionName];
        }

        return defaultValue;
      }

      if (string.IsNullOrWhiteSpace(conditionName)) {
        return LocalValue;
      }

      if (localConditionalValues.ContainsKey(conditionName)) {
        return localConditionalValues[conditionName];
      }

      if (conditionalValues.ContainsKey(conditionName)) {
        return conditionalValues[conditionName];
      }

      return LocalValue;
    }

    /// <summary>
    /// Shorthand method for setting a value to null.
    /// </summary>
    /// <param name="conditionName">Condition for which to unset the value, if any.</param>
    public void UnsetValue(string conditionName = null) => SetValue<string>(null, conditionName);

    /// <summary>
    /// Sets a local value for this parameter. If conditionName is provided, sets the conditional
    /// value; otherwise, sets the default value.
    /// </summary>
    /// <param name="value">The new value. If null, removes value instead.</param>
    /// <param name="conditionName">Condition to set the value for, if any.</param>
    public void SetValue<T>(T value, string conditionName = null) {
      if (string.IsNullOrWhiteSpace(conditionName)) {
        LocalValue.value = value?.ToString();
        return;
      }

      // If value is null, remove the conditional value.
      if (value == null) {
        // If there is a synced conditional value, return to the synced state:
        // set the local conditional value to null so that it will be removed before sync.
        // Otherwise, remove the local conditional value for the condition.
        if (conditionalValues.ContainsKey(conditionName)) {
          localConditionalValues[conditionName] = new RemoteConfigValue {
            value = null
          };
        } else {
          localConditionalValues.Remove(conditionName);
        }
        return;
      }
      if (!localConditionalValues.ContainsKey(conditionName)) {
        localConditionalValues[conditionName] = new RemoteConfigValue();
      }
      localConditionalValues[conditionName].value = value?.ToString();
    }

    /// <summary>
    /// Reset all local changes to the parameter. No op if the param does not exist in Firebase.
    /// </summary>
    /// <returns>True if any values were changed.</returns>
    public bool ResetParameter() {
      // Parameter is already in default state.
      if (defaultValue.value != null &&
          LocalValue.value == defaultValue.value &&
          localConditionalValues.Count == 0) {
        return false;
      }

      LocalValue.value = defaultValue.value;
      localConditionalValues.Clear();
      return true;
    }

    public RemoteConfigParameter CreateUploadData() {
      if (LocalValue.value == null) {
        return null;
      }
      var newParam = new RemoteConfigParameter {
        Key = Key,
        defaultValue = LocalValue ?? defaultValue
      };

      // Add any previous conditional values.
      newParam.conditionalValues = new Dictionary<string, RemoteConfigValue>(conditionalValues);
      // Change/remove any alterations in local conditional values.
      foreach (var kv in localConditionalValues) {
        if (kv.Value?.value == null) {
          newParam.conditionalValues.Remove(kv.Key);
        } else {
          newParam.conditionalValues[kv.Key] = new RemoteConfigValue {
            value = kv.Value.value
          };
        }
      }
      return newParam;
    }
  }

  /// <summary>
  /// A condition as defined in the firebase console.
  /// </summary>
  [Serializable]
  public class RemoteConfigCondition {
    /// <summary>
    /// Name of the condition.
    /// </summary>
    public string name;
    /// <summary>
    /// String representation of the logical condition expression.
    /// </summary>
    public string expression;
    /// <summary>
    /// Color of the condition as shown in the console.
    /// </summary>
    public string tagColor;

    /// <summary>
    /// Convenience operator to convert condition to a string (its name).
    /// </summary>
    /// <param name="value">Condition to convert.</param>
    public static implicit operator string(RemoteConfigCondition condition) => condition?.name;
  }

  /// <summary>
  /// Metadata about the last version of the Remote Config.
  /// </summary>
  [Serializable]
  public class VersionInfo {
    public string versionNumber;
    public string updateTime;
    public FirebaseUser endUser;
    public string updateOrigin;
    public string updateType;
  }

  /// <summary>
  /// User details of the last person to update Remote Config.
  /// </summary>
  [Serializable]
  public class FirebaseUser {
    public string email;
  }
}

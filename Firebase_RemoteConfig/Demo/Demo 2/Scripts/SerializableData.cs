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

namespace Firebase.ConfigAutoSync.Demo
{
  /// <summary>
  /// A simple Serializable data container.
  /// Used to demonstrate how data container fields interact with RemoteConfigSyncBehaviour.
  /// </summary>
  [Serializable]
  public class SerializableData
  {
    public string String;
    public int Int;
    public double Double;
    public bool Bool;

    public SerializableData() {
    }

    public SerializableData(
        string publicString,
        int publicInt,
        double publicDouble,
        bool publicBool) {
      String = "nested " + publicString;
      Int = publicInt;
      Double = publicDouble;
      Bool = publicBool;
    }
  }

  /// <summary>
  /// A subclass of SerializableData with a nested data object.
  /// Used to demonstrate how data container fields interact with
  /// RemoteConfigSyncBehaviour.IncludeSubFields.
  /// </summary>
  [Serializable]
  public class DoubleNestedSerializableData : SerializableData {
    public SerializableData DoubleNested;

    public DoubleNestedSerializableData() : base() {
    }

    public DoubleNestedSerializableData(
        string publicString,
        int publicInt,
        double publicDouble,
        bool publicBool) : base(publicString, publicInt, publicDouble, publicBool) {
      DoubleNested = new SerializableData(
        "double " + publicString,
        publicInt + 1,
        publicDouble + 1f,
        publicBool);
    }
  }
}

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

using UnityEngine;

namespace Firebase.ConfigAutoSync.Demo {
  /// <summary>
  /// A demo component demonstrating how the RemoteConfigSyncBehaviour interacts with
  /// the [RemoteConfigSync] and [RemoteConfigSkipSync] attributes.
  /// </summary>
  public class SyncComponent : MonoBehaviour {
    private static readonly string noSyncString = "DO NOT SYNC";
    private static readonly int noSyncInt = -1;
    private static readonly double noSyncDouble = -1.1;
    private static readonly bool noSyncBool = false;

    // ***** RemoteConfigSync-TAGGED FIELDS ***** //
    // These fields have the attribute [RemoteConfigSync]. Therefor, as long as the
    // GameObject also has the RemoteConfigSyncBehaviour Component attached, these fields
    // will be candidates to sync to RemoteConfig.
    [RemoteConfigSync] public string String = "field";
    [RemoteConfigSync] public int Int = 1;
    [RemoteConfigSync] public double Double = 1.1;
    [RemoteConfigSync] public bool Bool = true;

    [RemoteConfigSync]
    public DoubleNestedSerializableData NestedObject =
        new DoubleNestedSerializableData("field", 2, 2.1, true);

    // ***** UNIQUE KEY FIELDS ***** //
    // These fields include a unique key parameter in the RemoteConfigSync attribute.
    // They will be synced with the given strings as their keys instead of the
    // key determined by the path from the parent GameObject's RemoteConfigSyncBehaviour
    // and field name.
    [RemoteConfigSync("UniqueString")] public string UniqueString = "unique field";
    [RemoteConfigSync("UniqueInt")] public int UniqueInt = 4;
    [RemoteConfigSync("UniqueDouble")] public double UniqueDouble = 4.1;
    [RemoteConfigSync("UniqueBool")] public bool UniqueBool = true;
    [RemoteConfigSync("UniqueNestedObject")]
    public DoubleNestedSerializableData UniqueNestedObject =
        new DoubleNestedSerializableData("unique field", 5, 5.1, true);
    // ***** END UNIQUE KEY FIELDS ***** //
    // ***** END RemoteConfigSync-TAGGED FIELDS ***** //



    // ***** NON-RemoteConfigSync-TAGGED FIELDS ***** //
    // These fields do not have the [RemoteConfigSync] attribute. Therefor, they will only be
    // synced if the GameObject's attached RemoteConfigSyncBehaviour.SyncAllFields == true.
    public string NoTagString = "no-tag field";
    public int NoTagInt = 6;
    public double NoTagDouble = 6.1;
    public bool NoTagBool = true;

    public DoubleNestedSerializableData NoTagNestedObject =
        new DoubleNestedSerializableData("no-tag field", 7, 7.1, true);
    // ***** END NON-RemoteConfigSync-TAGGED FIELDS ***** //



    // ***** RemoteConfigSkipSync-TAGGED FIELDS ***** //
    // These fields are tagged with the [RemoteConfigSkipSync] attribute. Therefor, they are never
    // synced, even if the GameObject's RemoteConfigSyncBehaviour.SyncAllFields == true.
    [RemoteConfigSkipSync] public string NoSyncString = noSyncString;
    [RemoteConfigSkipSync] public int NoSyncInt = noSyncInt;
    [RemoteConfigSkipSync] public double NoSyncDouble = noSyncDouble;
    [RemoteConfigSkipSync] public bool NoSyncBool = noSyncBool;
    [RemoteConfigSkipSync]
    public SerializableData NoSyncNestedObject =
        new SerializableData(noSyncString, noSyncInt, noSyncDouble, noSyncBool);
    // ***** END RemoteConfigSkipSync-TAGGED FIELDS ***** //

    // These fields are used at runtime to demonstrate how new GameObjects created during play
    // that have the RemoteConfigSyncBehaviour Component attached are synced at creation time.
    public SyncComponent SyncAllPrefab;
    public SyncComponent SyncFieldsPrefab;

    private void Start() {
      // Create GameObjects at runtime to demonstrate how new Objects created
      // after game starts are synced to RemoteConfig.
      if (SyncAllPrefab) {
        var newSyncAllGameObject = Instantiate(SyncAllPrefab, transform);
        newSyncAllGameObject.name = SyncAllPrefab.name;
      }
      if (SyncFieldsPrefab) {
        var newSyncFieldsGameObject = Instantiate(SyncFieldsPrefab, transform);
        newSyncFieldsGameObject.name = SyncFieldsPrefab.name;
      }
    }
  }
}

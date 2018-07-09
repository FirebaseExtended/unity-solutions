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

using System.Linq;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// Base class for sync elements, both groups (objects containing sync fields), sync fields, and
  /// unmapped parameters.
  /// </summary>
  public abstract class SyncElement : VisualElement {
    protected static readonly string syncToggleClassName = "sync-toggle";

    protected static RemoteConfigData rcData => SyncDataManager.CurrentData;

    /// <summary>
    /// The RemoteConfig parameter linked to this sync element.
    /// </summary>
    public RemoteConfigParameter Param;

    /// <summary>
    /// The sync target (or group) found by SyncTargetManager linked to this visual element.
    /// </summary>
    protected SyncItem syncItem;

    /// <summary>
    /// Level of indentation to use for this element.
    /// </summary>
    protected readonly int indentLevel = 0;

    /// UI elements

    /// <summary>
    /// The toggle button for enabling/disabling syncing this field (or its elements).
    /// </summary>
    public Toggle SyncToggle;

    public SyncElement() : base() {
      AddToClassList("sync-item");
    }

    /// <summary>
    /// Constructor for sync targets and groups found on GameObject resources/assets.
    /// </summary>
    /// <param name="syncItem">The sync target/group associated with this visual element.</param>
    public SyncElement(SyncItem syncItem) : this() {
      this.syncItem = syncItem;
      Param = rcData.GetOrCreateParameter(syncItem.FullKeyString);
      name = syncItem.FullKeyString;
      indentLevel = syncItem.FullKey.Count - 1;
    }

    /// <summary>
    /// No-target constructor for parameters without an associated sync target/group in the
    /// current scene or global assets.
    /// </summary>
    /// <param name="param">The parameter associated with this visual element.</param>
    protected SyncElement(RemoteConfigParameter param) {
      Param = param;
      name = param.Key;
    }

    /// <summary>
    /// Create a checkbox that enables/disables syncing the target or its elements.
    /// </summary>
    /// <param name="startChecked">True if the checkbox should start checked.</param>
    /// <returns>The Toggle element created.</returns>
    protected Toggle CreateSyncToggle(bool startChecked) {
      var syncToggle = new Toggle() {
        name = (syncItem?.FullKeyString ?? Param.Key) + "-toggle",
        value = startChecked
      };
      syncToggle.AddToClassList(syncToggleClassName);
      syncToggle.RegisterValueChangedCallback(UpdateSyncChoiceValueChanged);
      return syncToggle;
    }

    /// <summary>
    /// Create a label and checkbox that enables/disables syncing the target or its elements.
    /// </summary>
    /// <param name="enabled">True if the checkbox should start checked.</param>
    /// <returns>A TemplateContainer VisualElement containing the checkbox and label.</returns>
    protected Toggle CreateSyncCheckboxAndLabel(bool enabled) {
      // Create the container element for the checkbox and label.
      var syncContainer = new TemplateContainer();
      SyncToggle = CreateSyncToggle(enabled);
      SyncToggle.AddToClassList("row");
      SyncToggle.AddToClassList("column");
      SyncToggle.AddToClassList("indent-" + indentLevel);
      var syncLabel = new Label(syncItem?.Key ?? Param.Key);
      // Add the label as a child to the Toggle, so that they share a click callback, and so
      // the label is positioned after the checkbox visually.
      SyncToggle.Children().First().Add(syncLabel);
      SyncToggle.tooltip = syncItem?.FullKeyString ?? Param.Key;
      return SyncToggle;
    }

    /// <summary>
    /// Callback when the sync toggle value is changed.
    /// </summary>
    /// <param name="evt">The change event.</param>
    protected void UpdateSyncChoiceValueChanged(ChangeEvent<bool> evt) {
      UpdateSyncChoice(evt.newValue);
    }

    /// <summary>
    /// Implemented in child classes to respond as appropriate to sync toggle changes.
    /// </summary>
    /// <param name="newValue">The new value to sync/not sync.</param>
    public abstract void UpdateSyncChoice(bool newValue);
  }
}

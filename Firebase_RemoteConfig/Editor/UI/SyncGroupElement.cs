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
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// A VisualElement that represents a top-level Component or Serializable data object found when
  /// searching for sync targets. This allows for a hierarchy view of targets in the editor which
  /// can be collapsed for easy viewing.
  ///
  /// This element is not mapped to a RemoteConfig parameter itself, but rather its key is a prefix
  /// to its child SyncElements (which can themselves be SyncGroupElements or SyncTypElements).
  /// </summary>
  public class SyncGroupElement : SyncElement {
    /// <summary>
    /// Accessor to get the syncItem as a SyncTargetContainer.
    /// </summary>
    private SyncTargetContainer container => syncItem as SyncTargetContainer;

    /// <summary>
    /// Constructor which initializes UI elements for the given SyncTargetContainer as well as its
    /// children, indenting nested sync targets as appropriate.
    /// </summary>
    public SyncGroupElement(SyncItem syncItem) : base(syncItem) {
      if (!(syncItem is SyncTargetContainer)) {
        Debug.LogWarning(
          $"Cannot create SyncGroupElement from SyncTarget {syncItem.FullKeyString}");
        return;
      }

      // The top-level group (the SyncTargetContainer of all the discovered SyncTargets) has a
      // different appearance than nested SyncTargetContainers.
      VisualElement topLevelContainer = syncItem.FullKeyString.Length == 0 ?
          CreateTopLevelGroupElement() :
          CreateNestedGroupElement();

      // For each SyncItem child of this container, add a SyncTargetElement or SyncGroupElement
      // child element with an increased indent.
      // List SyncTargets before nested SyncTargetContainers for visual clarity.
      foreach (var kv in container.Items.OrderByDescending(target => target.Value is SyncTarget)) {
        SyncItem child = kv.Value;
        SyncElement childSyncElement = null;
        if (child is SyncTarget) {
          var target = child as SyncTarget;
          if (target.Field.FieldType == typeof(bool)) {
            childSyncElement = new SyncTypeElement<bool, Toggle>(target);
          } else if (target.Field.FieldType == typeof(double)) {
            childSyncElement = new SyncTypeElement<double, DoubleField>(target);
          } else if (target.Field.FieldType == typeof(int)) {
            childSyncElement = new SyncTypeElement<int, IntegerField>(target);
          } else if (target.Field.FieldType == typeof(string)) {
            childSyncElement = new SyncTypeElement<string, TextField>(target);
          } else {
            Debug.Log(
                $"Invalid type for sync target {target.FullKeyString}: {target.Field.FieldType}.");
            continue;
          }
        } else if (child is SyncTargetContainer) {
          childSyncElement = new SyncGroupElement(child);
        }
        topLevelContainer.Add(childSyncElement);
      }

      if (syncItem.FullKeyString.Length == 0) {
        return;
      }

      // For non-top-level SyncGroupElements, add a "Sync All" toggle, enabled if all descendents
      // are synced.
      // Start unchecked if any sync Toggles in tree are unchecked.
      // Add listener to all sync Toggle descendents to update this Sync All Toggle as appropriate.
      var descendentSyncToggles = this
          .Query<SyncTypeElement>()
          .ToList()
          .Select(el => el.SyncToggle)
          .ToList();
      descendentSyncToggles.ForEach(el => {
        el.RegisterValueChangedCallback(UpdateSyncAllToggle);
      });
      unsyncedDescendents = descendentSyncToggles
          .Where(el => !el.value)
          .ToList();
      CreateSyncAllToggle(topLevelContainer, unsyncedDescendents.Count == 0);
    }

    private List<Toggle> unsyncedDescendents;

    /// <summary>
    /// Used for the top-level SyncTargetContainer which contains all discovered SyncTargets.
    /// Creates a header box with "Sync Targets" and uses this element as the top level UI
    /// element for all created children.
    /// </summary>
    /// <returns>This VisualElement, to contain all created UI child elements.</returns>
    private VisualElement CreateTopLevelGroupElement() {
      var box = new Box();
      box.AddToClassList("headers");
      box.AddToClassList("row");
      var header = new Label("Sync Targets");
      box.Add(header);
      this.Add(box);
      return this;
    }

    /// <summary>
    /// Used for nested SyncTargetContainers, such as Components with
    /// RemoteConfigSyncBehaviour on the same GameObject, or Serializable data objects tagged with
    /// [RemoteConfigSync] (or where RemoteConfigSyncBehaviour.SyncAllFields == true).
    ///
    /// Also, the SyncToggle for a SyncGroupElement is given its own row with the label "Sync All",
    /// and custom behaviour below in <see cref="UpdateSyncChoice"/>
    /// </summary>
    /// <returns>
    /// A Foldout element which will contain child UI elements created. The Foldout is useful for
    /// minimizing groups of fields in the Editor view.
    /// </returns>
    private VisualElement CreateNestedGroupElement() {
      var foldout = new Foldout {
        text = syncItem.Key
      };
      var foldoutToggle = foldout.Q<Toggle>();
      foldoutToggle.AddToClassList("indent-" + indentLevel);
      foldoutToggle.tooltip = container.FullKeyString;
      Add(foldout);
      return foldout;
    }

    /// <summary>
    /// Callback when a descendent sync Toggle changes. Update this group's Sync All Toggle to
    /// reflect whether all descendent targets are selected.
    /// </summary>
    /// <param name="evt">Change event from descendent sync Toggle.</param>
    private void UpdateSyncAllToggle(ChangeEvent<bool> evt) {
      Toggle toggle = (Toggle)evt.target;
      if (evt.newValue) {
        unsyncedDescendents.Remove(toggle);
      } else {
        unsyncedDescendents.Add(toggle);
      }
      this.SyncToggle.SetValueWithoutNotify(unsyncedDescendents.Count == 0);
    }

    /// <summary>
    /// Create a non-top-level SyncGroupElement's "Sync All" toggle.
    /// </summary>
    /// <param name="uiContainer">Foldout that will contain the SyncToggle.</param>
    /// <param name="startChecked">Whether the Sync All Toggle should start checked.</param>
    private void CreateSyncAllToggle(VisualElement uiContainer, bool startChecked) {
      SyncToggle = CreateSyncToggle(startChecked);
      SyncToggle.AddToClassList("indent-" + (indentLevel + 1));
      SyncToggle.AddToClassList("column");
      SyncToggle.text = "Sync All";
      SyncToggle.tooltip = "Select or Deselect all child elements.";
      uiContainer.Insert(0, SyncToggle);
    }

    /// <summary>
    /// When a SyncGroupElement's toggle is clicked, it enables/disables all descendents of the
    /// associated SyncTargetContainer.
    /// </summary>
    /// <param name="newValue">New enabled/disabled value.</param>
    public override void UpdateSyncChoice(bool newValue) {
      this.Query<SyncTypeElement>().ForEach(el => {
        el.SyncToggle.value = newValue;
      });
    }
  }
}

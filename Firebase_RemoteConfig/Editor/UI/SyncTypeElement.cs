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
using UnityEngine;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor {
  /// <summary>
  /// A typed SyncElement for SyncTarget fields, without generic types so that SyncTypeElements
  /// can be defined without including generic types
  /// (e.g. in VisualElement.Query<SyncTypeElement>()).
  /// </summary>
  public abstract class SyncTypeElement : SyncElement {

    /// <summary>
    /// Constructor for SyncTargets found on GameObject resources/assets.
    /// </summary>
    /// <param name="syncItem">The sync target/group associated with this visual element.</param>
    protected SyncTypeElement(SyncItem syncItem) : base(syncItem) { }

    /// <summary>
    /// No-target constructor for parameters without an associated SyncTarget in the
    /// current scene or global assets.
    /// </summary>
    /// <param name="param">The parameter associated with this visual element.</param>
    protected SyncTypeElement(RemoteConfigParameter param) : base(param) {
    }

    /// <summary>
    /// Resets the RemoteConfig parameter to its value on the remote server.
    /// </summary>
    public abstract void ResetParameter();

    /// <summary>
    /// Sync this field from the value of its local SyncTarget.
    /// </summary>
    /// <param name="conditionName">Optional condition name from which to sync.</param>
    public abstract void SyncFromLocal(string conditionName);

    /// <summary>
    /// Sync the current value of this field to its local SyncTarget.
    /// </summary>
    /// <param name="conditionName">Optional condition name to which to sync.</param>
    public abstract void SyncToLocal(string conditionName);
  }

  /// <summary>
  /// A SyncTypeElement that includes generic types for its SyncTarget type (a string, int, double,
  /// or bool) and the type of VisualElement that must be created to allow the user to edit said
  /// type (a TextField, IntegerField, DoubleField, or Toggle, respectively).
  /// Using generic types allows most of the logic to be defined here instead of on the individual
  /// SyncTypeElement child classes.
  /// </summary>
  /// <typeparam name="T">The field type of the sync target, such as string or bool.</typeparam>
  /// <typeparam name="F">A BaseField editor input field that controls fields of type
  /// <typeparamref name="T"/></typeparam>
  public class SyncTypeElement<T, F> : SyncTypeElement where F : BaseField<T>, new() {
    /// Class names for styling concerns.
    protected const string dirtyClassName = "dirty";
    protected const string rowClassName = "row";
    protected const string columnClassName = "column";
    protected const string defaultClassName = "default";
    protected const string conditionFieldClassName = "condition";
    protected const string conditionToggleClassName = "condition-toggle";
    protected const string syncClassSuffix = "-sync";

    /// <summary>
    /// Accessor to get the SyncItem as a SyncTarget.
    /// </summary>
    private SyncTarget target => syncItem as SyncTarget;

    /// <summary>
    /// Constructor for SyncTargets found on GameObject resources/assets.
    /// </summary>
    /// <param name="syncItem">The sync target/group associated with this visual element.</param>
    public SyncTypeElement(SyncItem syncItem) : base(syncItem) {
      InitElement();
    }

    /// <summary>
    /// No-target constructor for parameters without an associated sync target/group in the
    /// current scene or global assets.
    /// </summary>
    /// <param name="param">The parameter associated with this visual element.</param>
    protected SyncTypeElement(RemoteConfigParameter param) : base(param) {
      InitElement();
    }

    /// <summary>
    /// Called after constructor (shared by SyncTypeElements and SyncGroupElements) to initialize
    /// field-specific aspects of the UI element.
    /// </summary>
    private void InitElement() {
      var enabled = Param != null && Param.existsOnServer;
      Add(CreateSyncCheckboxAndLabel(enabled));
      AddToClassList(rowClassName);
      name = Param.Key;

      var field = CreateField();

      // Add conditional value fields & checkboxes.
      foreach (var condition in rcData.conditions) {
        CreateField(condition);
      }

      // Add a button to reset the whole row to its server values.
      var resetButton = new Button(ResetParameter) {
        text = "Reset"
      };
      resetButton.AddToClassList("reset-button");
      // Register Reset callback on MouseDownEvent for Linux editor.
#if UNITY_EDITOR && !UNITY_EDITOR_WIN && !UNITY_EDITOR_OSX
      resetButton.RegisterCallback<MouseDownEvent>(evt => ResetParameter());
#endif
      Add(resetButton);
    }

    /// <summary>
    /// Resets the RemoteConfig parameter to its value on the remote server.
    /// </summary>
    public override void ResetParameter() {
      if (!Param.ResetParameter()) {
        return;
      }

      SyncToggle.value = Param.existsOnServer;


      if (TryParse(Param.defaultValue.value, out var defaultValue)) {
        this.Q<F>(null, defaultClassName).value = defaultValue;
      }
      foreach (var condition in rcData.conditions) {
        if (Param.HasConditionalValue(condition) &&
            TryParse(Param.GetValue(condition.name, true).value, out var condValue)) {
          this.Query<F>(null, condition.name).ForEach(field => field.value = condValue);
          this.Q<Toggle>(null, condition.name + syncClassSuffix).value = true;
        } else {
          this.Q<Toggle>(null, condition.name + syncClassSuffix).value = false;
        }
      }
      this.Query(null, dirtyClassName).ForEach(el => el.RemoveFromClassList(dirtyClassName));
    }

    /// <summary>
    /// Sync this field from the value of its local SyncTarget.
    /// </summary>
    /// <param name="conditionName">Optional condition name from which to sync.</param>
    public override void SyncFromLocal(string conditionName) {
      var localValue = (T)target.Value;

      // If no condition name, set this target's default value from local.
      if (string.IsNullOrWhiteSpace(conditionName)) {
        this.Q<F>(null, defaultClassName).value = localValue;
        return;
      }

      var conditionBool = this.Q<Toggle>(null, conditionName + syncClassSuffix);

      if (!TryParse(Param.GetValue(conditionName).value, out var currentValue)) {
        // Failed to parse current condition value, set to target value.
        conditionBool.value = true;
        this.Q<F>(null, conditionName).value = localValue;
        return;
      }

      // If local value is the same as current condition value, no need to do anything.
      if (localValue == null) {
        if (currentValue == null) {
          return;
        }
      } else if (localValue.Equals(currentValue)) {
        return;
      }

      // Enable the condition Toggle and set both the parameter's conditional value
      // and the editor field's value.
      conditionBool.value = true;
      Param.SetValue(localValue, conditionName);
      var field = this.Q<F>(null, conditionName);
      field.value = localValue;
    }


    /// <summary>
    /// Sync the current value of this parameter's field to its local SyncTarget.
    /// </summary>
    /// <param name="conditionName">Optional condition name to which to sync.</param>
    public override void SyncToLocal(string conditionName) {
      var paramValue = Param.GetValue(conditionName).value;
      if (!TryParse(paramValue, out var syncValue)) {
        Debug.LogWarning($"Illegal value type for {Param.Key} in RemoteConfig: {paramValue}.");
        return;
      }

      target.Value = syncValue;
    }

    /// <summary>
    /// For synced fields, updating the sync choice means enabling/disabling the syncing of the
    /// associated parameter. When disabling, this also means disabling all the UI elements in
    /// the row, such as TextFields and condition Toggles.
    /// </summary>
    /// <param name="newValue">New value of the sync Toggle.</param>
    public override void UpdateSyncChoice(bool newValue) {
      if (newValue) {
        // If param is previously synced, when enabling reset the default value back to its
        // synced value.
        // Otherwise set it to the SyncTarget's value.
        if (Param.existsOnServer) {
          Param.SetValue(Param.GetValue(null, true)?.value ?? target.Value);
        } else {
          Param.SetValue(target.Value);
        }
      } else {
        // When disabling, unset the Param's default value.
        Param.UnsetValue();
      }

      UpdateDirtyState(newValue);
    }

    /// <summary>
    /// Updates the appearance of this element to be highlighted if the local value differs from
    /// the value in Remote Config.
    /// </summary>
    /// <param name="syncToServer">Whether the param should be synced to Remote Config.</param>
    protected void UpdateDirtyState(bool syncToServer) {
      bool syncStateDirty = syncToServer != Param.existsOnServer;
      EnableInClassList(dirtyClassName, syncStateDirty);

      // Enable/disable fields in this row as appropriate.
      if (Param.LocalValue != null) {
        this.Q(null, defaultClassName).SetEnabled(true);
        foreach (var condition in rcData.conditions) {
          this.Q<Toggle>(null, condition.name + syncClassSuffix).SetEnabled(true);
        }
      } else {
        this.Query<F>(null, conditionFieldClassName).ForEach(field => field.SetEnabled(false));
        this.Query<Toggle>(null, conditionToggleClassName).ForEach(field => {
          field.SetEnabled(false);
          field.value = false;
        });
      }
    }

    /// <summary>
    /// Creates a <typeparamref name="F"/> to control the parameter input field.
    /// </summary>
    /// <param name="condition">Condition associated with this UI element, if any.</param>
    /// <returns>The newly created <typeparamref name="F"/>.</returns>
    private F CreateField(RemoteConfigCondition condition = null) {
      // Create container VisualElement with row and column class names for horizontal/vertical
      // alignment styling.
      var container = new TemplateContainer();
      container.AddToClassList(rowClassName);
      container.AddToClassList(columnClassName);

      var field = new F();
      var hasCondition = condition != null && Param.HasConditionalValue(condition);
      var localValue = Param.GetValue(condition);

      if (condition != null) {
        // Add condition enable/disable Toggle, with class names allowing it to be found with
        // `VisualElement.Query()` and `VisualElement.Q()`.
        var condCheckbox = new Toggle();
        condCheckbox.AddToClassList("flex-0");
        condCheckbox.AddToClassList(condition.name + syncClassSuffix);
        condCheckbox.AddToClassList(conditionToggleClassName);
        condCheckbox.SetEnabled(Param.existsOnServer);
        condCheckbox.RegisterCallback<ChangeEvent<bool>, string>(OnConditionToggleChanged, condition.name);
        condCheckbox.value = Param.HasConditionalValue(condition);
        container.Add(condCheckbox);

        // Adjust condition field based on condition status.
        field.AddToClassList(condition.name);
        field.AddToClassList(conditionFieldClassName);
        field.SetEnabled(hasCondition);
      } else {
        field.AddToClassList(defaultClassName);
      }

      // Since all RemoteConfigParameters are strings by default, get the parameter's value in
      // expected field type. First try local value, then the SyncTarget's value, then the default
      // for the given type.
      var parseSuccess = TryParse(localValue?.value, out var parsedValue);
      if (parseSuccess) {
        field.value = parsedValue;
      } else if (target != null && TryParse(target.Value?.ToString(), out var targetValue)) {
        field.value = targetValue;
      } else {
        field.value = default;
      }

      field.RegisterCallback<ChangeEvent<T>, string>(OnValueChanged, condition?.name);
      if (!Param.IsInSync(condition)) {
        container.AddToClassList(dirtyClassName);
      }
      container.Add(field);

      // Add the container with the field (and Toggle for conditionals) to this VisualElement.
      Add(container);
      return field;
    }

    /// <summary>
    /// Callback when a condition is enabled/disabled for this sync target. When Toggle is checked,
    /// enable the field allowing for editing. When unchecked, disable the editing field and set
    /// its visual content to the default value to indicate that will be used.
    /// </summary>
    /// <param name="evt">Change event of Toggle checked/unchecked.</param>
    /// <param name="conditionName">Name of the RemoteConfigCondition this Toggle controls.</param>
    private void OnConditionToggleChanged(ChangeEvent<bool> evt, string conditionName) {
      var enabled = evt.newValue;

      // Enable/disable the condition field based on new Toggle value.
      F conditionField = (evt.target as VisualElement).parent.Q<F>(null, conditionName);
      conditionField.SetEnabled(enabled);

      // If the condition is enabled, get the local or remote conditional value.
      // If disabled, get the default value for the field.
      dynamic newConditionValue;
      if (enabled) {
        TryParse(Param.GetValue(conditionName).value, out newConditionValue);
        Param.SetValue(newConditionValue, conditionName);
      } else {
        Param.UnsetValue(conditionName);
        if (!TryParse(Param.GetValue().value, out newConditionValue)) {
          TryParse(target.Value?.ToString(), out newConditionValue);
        }
      }

      // Set the UI field editor to the conditional value.
      conditionField.value = newConditionValue;

      // Determine if this param/condition state is in sync with what's on the remote server.
      var isInSync = Param.IsInSync(conditionName);

      // Enable or disable the "dirty" class on the field to show changed fields.
      ((Toggle)evt.target).parent.EnableInClassList(dirtyClassName, !isInSync);
      conditionField.parent.EnableInClassList(dirtyClassName, !isInSync);
    }

    /// <summary>
    /// Callback when a condition is enabled/disabled for this sync target. When Toggle is checked,
    /// enable the field allowing for editing. When unchecked, disable the editing field and set
    /// its visual content to the default value to indicate that will be used.
    /// </summary>
    /// <param name="evt">Change event of the input BaseField.</param>
    /// <param name="conditionName">RemoteConfig condition for the target field, if any.</param>
    private void OnValueChanged(ChangeEvent<T> evt, string conditionName) {
      // Set the new local value for the condition.
      if (conditionName == null || Param.HasConditionalValue(conditionName)) {
        Param.SetValue(evt.newValue.ToString(), conditionName);
      }

      // Determine if the new value is the same as the synced one and style accordingly.
      ((VisualElement)evt.target)
          .parent
          .EnableInClassList(dirtyClassName, !Param.IsInSync(conditionName));

      if (conditionName == null) {
        // For all conditions for which this key has no value, update the UI to reflect.
        foreach (var condition in rcData.conditions) {
          if (!Param.HasConditionalValue(condition)) {
            this.Query<F>(null, condition.name).ForEach(f => {
              f.value = evt.newValue;
              f.EnableInClassList(dirtyClassName, !Param.IsInSync(condition));
            });
          }
        }
      }
    }

    /// <summary>
    /// Parse the string input into an out variable of type T, if possible.
    /// </summary>
    /// <param name="input">String input to parse into expected element.</param>
    /// <param name="outvar">Output value in expected type.</param>
    /// <returns>True if parsing was successful.</returns>
    public bool TryParse(string input, out dynamic outvar) {
      if (typeof(T) == typeof(bool)) {
        var r = bool.TryParse(input, out var outT);
        outvar = outT;
        return r;
      }
      if (typeof(T) == typeof(double)) {
        var r = double.TryParse(input, out var outT);
        outvar = outT;
        return r;
      }
      if (typeof(T) == typeof(int)) {
        var r = int.TryParse(input, out var outT);
        outvar = outT;
        return r;
      }
      if (typeof(T) == typeof(string)) {
        outvar = input;
        return !string.IsNullOrWhiteSpace(input);
      }
      outvar = null;
      return false;
    }
  }
}

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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Firebase.ConfigAutoSync.Editor
{
  /// <summary>
  /// This Editor Window displays all the discovered SyncTargets in a hierarchical view,
  /// and allows the user to control which ones are synced to Remote Config, with default
  /// and conditional values displayed as an easy-to-parse grid, including highlighting of
  /// changed values.
  /// </summary>
  public class SyncWindow : EditorWindow
  {
    private const string stylePath =
        "Assets/firebase-unity-solutions/Firebase_RemoteConfig/Editor/Resources/sync_styles.uss";
    private const string headersClassName = "headers";
    private const string columnClassName = "column";
    private const string rowClassName = "row";
    private const string defaultConditionName = "default";

    /// <summary>
    /// The most recently retrieved RemoteConfigData. Setting this can trigger the window to
    /// re-sync and rerender.
    /// </summary>
    public static RemoteConfigData RemoteConfigData;

    /// <summary>
    /// The most recently discovered local SyncTargets, under a top-level SyncTargetContainer.
    /// Automatically searches for new targets the first time it's requested, and setting
    /// this can trigger the window to re-sync and rerender.
    /// </summary>
    public static SyncTargetContainer SyncTargets;

    /// <summary>
    /// Existing instance of this window. If set, will react to changes to SyncTargets and
    /// RemoteConfigData.
    /// </summary>
    public static SyncWindow Instance;

    /// <summary>
    /// RemoteConfig parameters whose keys do not map to any local SyncTarget.
    /// </summary>
    private static List<RemoteConfigParameter> unmappedParams = new List<RemoteConfigParameter>();

    /// <summary>
    /// Indicates the RemoteConfigData has been retrieved successfully.
    /// </summary>
    private static bool ready = false;

    // Singleton UI Elements
    private Label messageField;
    private Box headerContainer;
    private ScrollView valueScrollView;
    private Box buttonContainer;
    private Button downloadButton;
    private Button uploadButton;
    private Button resetChangesButton;
    private TemplateContainer topLevelElement;
    private SyncGroupElement topLevelSyncTarget;

    // State tracking fields.
    private float lastTabWidth;
    private float columnSize;
    private bool retrievingRemoteConfigData;

    /// <summary>
    /// Static method to open or show this window.
    /// </summary>
    [MenuItem("Window/Firebase/Remote Config/Sync")]
    private static void ShowSyncMenu(MenuCommand command) {
      OpenWindow();
    }

    /// <summary>
    /// Open window as a public method, so it can be triggered by other classes.
    /// </summary>
    public static void OpenWindow() {
      GetWindow<SyncWindow>(typeof(SceneView));
    }

    /// <summary>
    /// If the RemoteConfigSyncWindow is actively displayed, re-search scene and project hierarchy
    /// for SyncTargets. Setting new SyncTargets triggers rerender if SyncTargets and
    /// RemoteConfigData are both non-null.
    /// </summary>
    public static void RefreshSyncTargets() {
      if (Instance != null) {
        var syncObjects = Resources.FindObjectsOfTypeAll<RemoteConfigSyncBehaviour>();
        SyncTargets = SyncTargetManager.FindTargets(syncObjects);
        Instance.UpdateUI();
      }
    }

    /// <summary>
    /// Register callbacks and make first call to RemoteConfigDataManager, and initialize the UI.
    /// </summary>
    public void OnEnable() {
      Instance = this;
      titleContent = new GUIContent("Remote Config Sync");

      // Initialize UI state if necessary.
      if (rootVisualElement.childCount == 0) {
        InitializeUI();
      }
      retrievingRemoteConfigData = false;

      SyncDataManager.DataRetrieved += RemoteConfigDataUpdatedCallback;
      SyncDataManager.DataUploaded += RemoteConfigDataUpdatedCallback;
      SyncDataManager.RetrieveError += RemoteConfigErrorCallback;
      if (File.Exists(SyncDataManager.LocalFilePath)) {
        var localContent = File.ReadAllText(SyncDataManager.LocalFilePath);
        RemoteConfigData = RemoteConfigData.Deserialize(localContent);
        SyncDataManager.CurrentData = RemoteConfigData;
        ready = true;
        UpdateUI();
      } else if (!EditorApplication.isPlayingOrWillChangePlaymode) {
        GetRemoteConfigData();
      }
    }

    public void OnDisable() {
      SyncDataManager.DataRetrieved -= RemoteConfigDataUpdatedCallback;
      SyncDataManager.DataUploaded -= RemoteConfigDataUpdatedCallback;
      SyncDataManager.RetrieveError -= RemoteConfigErrorCallback;
    }

    /// <summary>
    /// Rerender window when the project changes.
    /// </summary>
    private void OnProjectChange() => RefreshSyncTargets();

    /// <summary>
    /// Rerender window when something in the hierarchy changes.
    /// </summary>
    private void OnHierarchyChange() => RefreshSyncTargets();

    /// <summary>
    /// Run on EditorApplication.update. Checks to see if the window width has changed drastically
    /// since last render, and re-sizes the columns accordingly.
    /// </summary>
    private void Update() {
      if (RemoteConfigData != null && Mathf.Abs(position.width - lastTabWidth) > 50) {
        ResizeColumns();
        lastTabWidth = position.width;
      }
    }

    /// <summary>
    /// When the window is first shown, create and organize all the top-level UI elements.
    /// </summary>
    private void InitializeUI() {
      var root = rootVisualElement;

      messageField = new Label();

      headerContainer = new Box();
      headerContainer.AddToClassList(headersClassName);
      headerContainer.AddToClassList("top-headers");
      headerContainer.AddToClassList(rowClassName);

      valueScrollView = new ScrollView();
      valueScrollView.AddToClassList("value-container");

      buttonContainer = new Box();
      buttonContainer.AddToClassList("button-container");
      buttonContainer.AddToClassList(rowClassName);

      // In Windows & Mac editor, providing a callback on Button creation works a expected.
      uploadButton = new Button(() => SyncDataManager.UpdateRemoteConfigAsync(
          RemoteConfigData.CreateUploadData())) {
        text = "Upload to Remote Config"
      };

      downloadButton = new Button(() => SyncDataManager.GetRemoteConfigDataAsync()) {
        text = "Sync from Remote Config"
      };

      resetChangesButton = new Button(ResetLocalChanges) {
        text = "Reset All"
      };

      // Register callbacks on MouseDownEvent for Linux editor.
#if UNITY_EDITOR && !UNITY_EDITOR_WIN && !UNITY_EDITOR_OSX
      downloadButton.RegisterCallback<MouseDownEvent>(
          evt => SyncDataManager.GetRemoteConfigDataAsync());
      uploadButton.RegisterCallback<MouseDownEvent>(evt => SyncDataManager
          .UpdateRemoteConfigAsync(RemoteConfigData.CreateUploadData()));
      resetChangesButton.RegisterCallback<MouseDownEvent>(evt => ResetLocalChanges());
#endif

      // Until first Remote Config data sync, only show Sync From button.
      buttonContainer.Add(downloadButton);

      root.Add(headerContainer);
      root.Add(valueScrollView);
      root.Add(buttonContainer);
      // Add target style to root element.
      root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath));
    }

    /// <summary>
    /// Initiates a call to retrieve RemoteConfig data, unless such a call is already in progress.
    /// </summary>
    private void GetRemoteConfigData() {
      if (retrievingRemoteConfigData) {
        return;
      }

      retrievingRemoteConfigData = true;
      SyncDataManager.GetRemoteConfigDataAsync();
    }

    /// <summary>
    /// When a GameObject or Asset is selected which maps to a section in the target hierarchy,
    /// scroll to that section in the window.
    /// </summary>
    private void OnSelectionChange() {
      if (!ready) {
        return;
      }

      // If an asset with the RemoteConfigSyncBehaviour Component was selected,
      // get its key prefix and scroll to the element with that name, if it exists.
      var syncBehaviour = Selection.activeGameObject?.GetComponent<RemoteConfigSyncBehaviour>();
      if (syncBehaviour == null) {
        return;
      }
      string keyPrefix = null;
      if (syncBehaviour.PrefixSource != PrefixSource.Component) {
        keyPrefix = syncBehaviour.GetKeyPrefix(syncBehaviour);
      } else {
        // For GameObjects where the PrefixSource is determined by the Component name,
        // just find the first component name which maps to a section.
        var componentNames = syncBehaviour
            .GetComponents<MonoBehaviour>()
            .Select(comp => comp.GetType().Name);
        keyPrefix = componentNames.First(compName => SyncTargets.Items.ContainsKey(compName));
      }
      if (keyPrefix == null) {
        return;
      }

      // Find and scroll to the element, if it exists.
      var scrollToElement = topLevelElement.Q<VisualElement>(keyPrefix);
      if (scrollToElement != null) {
        valueScrollView.scrollOffset = new Vector2();
        var worldOffset = scrollToElement.worldBound.position;
        var localOffset = valueScrollView.WorldToLocal(worldOffset);

        valueScrollView.scrollOffset = new Vector2(0, localOffset.y);
      }
    }

    /// <summary>
    /// Call when SyncTargets or RemoteConfigData are modified.
    /// If both are set, proceed to render them in the window.
    /// Otherwise display a message explaining what data is needed.
    /// </summary>
    public void UpdateUI() {
      rootVisualElement.Clear();
      if (RemoteConfigData == null) {
        rootVisualElement.Add(messageField);
        messageField.text = "Retrieving Remote Config...";
        GetRemoteConfigData();
        return;
      }
      if (SyncTargets == null) {
        rootVisualElement.Add(messageField);
        messageField.text = "Collecting local asset sync fields...";
        RefreshSyncTargets();
        return;
      }

      rootVisualElement.Add(headerContainer);
      rootVisualElement.Add(valueScrollView);
      rootVisualElement.Add(buttonContainer);

      // With both RemoteConfigData and SyncTargets, identify unmapped RemoteConfig parameters
      // and link SyncTargets to their Parameters (or create params for those without them).
      unmappedParams.Clear();
      // Remove any unsynced+unmapped params leftover from previous renders. This can happen if
      // a new Component is created and then deleted, or SyncAllFields toggles are switched on
      // and back off again, etc.
      var unsynced = RemoteConfigData.parameters.Values.Where(v => !v.existsOnServer).ToList();
      foreach (var param in unsynced) {
        RemoteConfigData.parameters.Remove(param.Key);
      }
      foreach (var param in RemoteConfigData.parameters.Values) {
        var target = SyncTargets.Find(param.Key);
        if (target == null) {
          unmappedParams.Add(param);
        }
      }
      var itemsToSet = new List<SyncItem> {
        SyncTargets
      };
      while (itemsToSet.Count > 0) {
        var item = itemsToSet[0];
        itemsToSet.RemoveAt(0);
        if (item is SyncTargetContainer) {
          foreach (var child in (item as SyncTargetContainer).Items.Values) {
            itemsToSet.Add(child);
          }
        }
      }

      RenderParameters();
    }

    /// <summary>
    /// Once RemoteConfigData and SyncTargets are retrieved, render them in the ScrollView.
    /// First show unmapped parameters in a list, ending in a TextField+Button to add a new
    /// unmapped parameter.
    /// Then show all the discovered SyncTargets in hierarchy view, and highlight which ones
    /// are not synced with RemoteConfig.
    /// </summary>
    public void RenderParameters() {
      InitHeaders();
      var offset = valueScrollView.scrollOffset;
      valueScrollView.Clear();
      topLevelElement = new TemplateContainer();
      valueScrollView.Add(topLevelElement);

      // Render unmapped targets, if any.
      if (unmappedParams.Count > 0) {
        var unmappedParamsSection = new TemplateContainer();

        // Create Unmapped Parameters header section.
        var unmappedParamsHeader = new Box();
        unmappedParamsHeader.AddToClassList(headersClassName);
        var unmappedLabel = new Label("Unmapped Parameters");
        unmappedParamsHeader.Add(unmappedLabel);
        unmappedParamsSection.Add(unmappedParamsHeader);
        foreach (var param in unmappedParams.OrderBy(p => p.Key)) {
          var unmappedTarget = new UnmappedSyncElement(param);
          unmappedParamsSection.Add(unmappedTarget);
        }

        // Add a section with TextField and Button to create a new unmapped Parameter.
        var newKeyContainer = new TemplateContainer();
        newKeyContainer.AddToClassList(columnClassName);
        newKeyContainer.AddToClassList(rowClassName);
        var newUnmappedParamText = "New Unmapped Param";
        var newKeyField = new TextField {
          value = newUnmappedParamText
        };
        newKeyContainer.Add(newKeyField);

        var newUnmappedButton = new Button(() => {
          if (string.IsNullOrWhiteSpace(newKeyField.value)) {
            Debug.LogWarning("Cannot create parameter with null/whitespace key.");
            return;
          }
          if (RemoteConfigData.parameters.ContainsKey(newKeyField.value)) {
            Debug.LogWarning($"A parameter with key {newKeyField.value} already exists.");
            return;
          }
          var newParam = RemoteConfigData.GetOrCreateParameter(newKeyField.value);
          var newUnmappedTarget = new UnmappedSyncElement(newParam);
          // Insert the new unmapped key at the end of the unmapped keys list.
          var index = unmappedParamsSection.IndexOf(newKeyContainer);
          unmappedParamsSection.Insert(index, newUnmappedTarget);
          newKeyField.value = newUnmappedParamText;
          // Apply column sizing to newly created SyncTargetElement.
          newUnmappedTarget
              .Query(null, columnClassName)
              .ForEach(col => col.style.minWidth = col.style.maxWidth = columnSize);
        });
        newUnmappedButton.text = "+";
        newUnmappedButton.AddToClassList("flex-0");
        newKeyContainer.Add(newUnmappedButton);
        unmappedParamsSection.Add(newKeyContainer);
        topLevelElement.Add(unmappedParamsSection);
      }

      // Create a SyncGroupElement for the top-level SyncTargetContainer. SyncGroupElement and
      // the various SyncTypeElement classes handle the logic for creating the hierarchy UI.
      topLevelSyncTarget = new SyncGroupElement(SyncTargets);
      topLevelElement.Add(topLevelSyncTarget);

      // Below the ScrollView area, show a set of buttons to sync to/from RC and reset local changes.
      buttonContainer.Clear();
      buttonContainer.Add(uploadButton);
      buttonContainer.Add(downloadButton);
      buttonContainer.Add(resetChangesButton);

      // Reset UI by scrolling to previous scroll position and sizing the newly created columns.
      valueScrollView.scrollOffset = offset;
      lastTabWidth = position.width;
      ResizeColumns();
    }

    /// <summary>
    /// Reset all local changes to parameters.
    /// </summary>
    private void ResetLocalChanges() {
      topLevelSyncTarget.Query<SyncTypeElement>().ForEach(el => {
        el.ResetParameter();
      });
    }

    /// <summary>
    /// Callback when an error is triggered by RemoteConfigDataManager. Update display/log error
    /// message and provide a button to set Google Credentials if that is the problem.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="errorArgs">ErrorEventArgs with details of the failure.</param>
    private void RemoteConfigErrorCallback(object sender, ErrorArgs errorArgs) {
      var output = $"{errorArgs.Cause.Message}\n{errorArgs.Cause.StackTrace}";
      Debug.LogError(output);
      rootVisualElement.Clear();
      messageField.text = output;
      rootVisualElement.Add(messageField);
      if (errorArgs.InvalidCredentials) {
        rootVisualElement.Add(new Button(() => CredentialsWindow.OpenWindow()) {
          text = "Set Google Credential file."
        });
      }
      ready = false;
      retrievingRemoteConfigData = false;
    }

    /// <summary>
    /// Callback when new RemoteConfig data is successfully uploaded. Update the UI to match
    /// the new server state.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="data">Newly synced data.</param>
    private void RemoteConfigDataUpdatedCallback(object sender, RemoteConfigData data) {
      ready = true;
      retrievingRemoteConfigData = false;
      RemoteConfigData = data;
      UpdateUI();
    }

    /// <summary>
    /// Resize all the elements within this tree with the class "column" to be a consistent
    /// width. If we relied on Flex styling instead of this, the columns would be at the mercy of
    /// flex to determine how they lined up vertically based on the elements' content.
    /// </summary>
    private void ResizeColumns() {
      var columns = 2 + RemoteConfigData.conditions.Count;
      columnSize = (position.width - 100) / columns - 10;
      // Update the column widths to be consistent between rows
      // based on the longest entries' proportional widths.
      var columnItems = rootVisualElement.Query(null, columnClassName).ToList();
      columnItems.ForEach(col => col.style.minWidth = col.style.maxWidth = columnSize);
    }

    /// <summary>
    /// Create headers for the parameter keys, default values, and optionally any conditions.
    /// </summary>
    private void InitHeaders() {
      if (RemoteConfigData == null) {
        return;
      }
      headerContainer.Clear();
      CreateHeader("Key", createButtons: false).AddToClassList("flex-1");
      CreateHeader("Default");
      foreach (var condition in RemoteConfigData.conditions) {
        CreateHeader(condition.name, condition.name);
      }
    }

    /// <summary>
    /// Create a single header UI element.
    /// </summary>
    /// <param name="label">Text label for the header.</param>
    /// <param name="conditionName">Name of condition for the header, if applicable.</param>
    /// <param name="createButtons">If true, create "sync from/to" buttons for the column.</param>
    private Label CreateHeader(
        string label,
        string conditionName = defaultConditionName,
        bool createButtons = true) {
      var container = new TemplateContainer();
      container.AddToClassList(columnClassName);

      var header = new Label(label);
      container.Add(header);

      if (createButtons) {
        var buttonContainer = new TemplateContainer();
        buttonContainer.AddToClassList(rowClassName);
        buttonContainer.AddToClassList("flex-1");
        buttonContainer.AddToClassList("justify");
        var syncFromButton = new Button(() => SyncFromLocal(conditionName)) {
          text = "Set From Local",
          tooltip = "Set values for this column from local asset values.",
          name = conditionName
        };
        buttonContainer.Add(syncFromButton);

        var syncToButton = new Button(() => SyncToLocal(conditionName)) {
          text = "Set To Local",
          tooltip = "Set values on local assets from this column."
        };
        buttonContainer.Add(syncToButton);
        container.Add(buttonContainer);
      }

      headerContainer.Add(container);
      return header;
    }

    /// <summary>
    /// Sync the parameter values for the given column from local SyncTarget values.
    /// </summary>
    /// <param name="conditionName">Name of condition, or "default" for default value.</param>
    private void SyncFromLocal(string conditionName) {
      if (conditionName == defaultConditionName) {
        conditionName = null;
      }
      topLevelSyncTarget.Query<SyncTypeElement>()
          .ForEach(el => el.SyncFromLocal(conditionName));
    }

    /// <summary>
    /// Sync the parameter values for the given column to local SyncTarget values.
    /// </summary>
    /// <param name="conditionName">Name of condition, or "default" for default value.</param>
    private void SyncToLocal(string conditionName) {
      topLevelSyncTarget.Query<SyncTypeElement>()
          .ForEach(el => el.SyncToLocal(conditionName));
    }
  }
}

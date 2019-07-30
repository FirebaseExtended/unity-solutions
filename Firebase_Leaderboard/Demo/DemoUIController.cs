/**
  Copyright 2018 Google LLC

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

using Firebase;
using Firebase.Database;
using Firebase.Unity;
using Firebase.Unity.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Firebase.Leaderboard.Demo {
  /// <summary>
  /// This class controls the UI in the demo scene, responding to events and updating
  /// elements of the UI as events occur.
  /// </summary>
  public class DemoUIController : MonoBehaviour {
    /// <summary>
    /// When adding batch scores, occasionally populate the UserIDInput and ScoreInput
    /// with one of the scores generated. For display purposes only.
    /// </summary>
    private static int DisplayRandomScoreInterval = 100;

    /// <summary>
    /// Input field for User ID, when creating a score manually.
    /// </summary>
    public InputField UserIDInput;

    /// <summary>
    /// Input field for the User display name, when creating a score manually.
    /// </summary>
    public InputField UsernameInput;

    /// <summary>
    /// Input field for score value, when creating a score manually.
    /// </summary>
    public InputField ScoreInput;

    /// <summary>
    /// Button to add a score manually.
    /// </summary>
    public Button AddScoreButton;

    // Label for the user's top score section. Updated to match the content of UserIDInput.
    public Text UserScoreLabel;

    /// <summary>
    /// Text field updated when LeaderboardController.UserScoreUpdated is triggered.
    /// </summary>
    public Text UserScoreText;

    /// <summary>
    /// Text field to display a message to user (e.g. "Scores added: {num}")
    /// </summary>
    public Text MessageText;

    /// <summary>
    /// Input field for the number of scores to retrieve when getting top scores.
    /// </summary>
    public InputField NumScoresInput;

    /// <summary>
    /// Container to place the top score UI elements after loading top scores.
    /// </summary>
    public RectTransform ScoreContainer;

    /// <summary>
    /// Prefab UI object to populate with a single top score entry data after loading.
    /// </summary>
    public GameObject TopScorePrefab;

    /// <summary>
    /// When the clear scores button is pressed, show this panel for user to confirm they
    /// want to delete all the top scores.
    /// </summary>
    public GameObject ConfirmClearPanel;

    /// <summary>
    /// Button to clear all the top scores. Only available if the user has enabled Editor access.
    /// </summary>
    public GameObject ClearScoresButton;

    /// <summary>
    /// The maximum number of scores that can be retrieved in a single request.
    /// </summary>
    public int MaxRetrievableScores;

    /// <summary>
    /// Track the LeaderboardController component attached to this GameObject.
    /// </summary>
    private LeaderboardController leaderboard;

    /// <summary>
    /// List of top score prefabs created in the ScoreContainer scroll view.
    /// </summary>
    private List<GameObject> scoreObjects = new List<GameObject>();

    /// <summary>
    /// Used to add random score values to the DB.
    /// </summary>
    private System.Random random = new System.Random();

    /// <summary>
    /// Tracks the number of scores added when adding in bulk,
    /// so as to know when to re-enable controls.
    /// </summary>
    private int scoresAdded;

    /// <summary>
    /// Helpful index when referring to the Text components in a top score prefab.
    /// </summary>
    private enum TopScoreElement {
      Username = 1,
      Timestamp = 2,
      Score = 3
    }

    /// <summary>
    /// When the game starts, find the LeaderboardController component attached to this GameObject,
    /// subscribe to its events (ScoreAdded/TopScoresUpdated) with the callback functions, and
    /// disable it until we have created the top score prefabs.
    /// Finally, start a CR to create MaxRetrievableScores top score prefabs and disable them.
    /// </summary>
    private void Start() {
      leaderboard = GetComponent<LeaderboardController>();
      NumScoresInput.text = leaderboard.ScoresToRetrieve.ToString();
      leaderboard.FirebaseInitialized += OnInitialized;
      leaderboard.ScoreAdded += ScoreAdded;
      leaderboard.TopScoresUpdated += UpdateScoreDisplay;
      leaderboard.UserScoreUpdated += UpdateUserScoreDisplay;
      leaderboard.enabled = false;
      ToggleControls(false);

      // If Editor Auth is not enabled, scores cannot be cleared.
      if (!leaderboard.EditorAuth) {
        ClearScoresButton.SetActive(false);
      }

      MessageText.text = "Preparing Leaderboard UI...";

      StartCoroutine(CreateTopScorePrefabs());
    }

    /// <summary>
    /// Controls are disabled until Firebase is initialized.
    /// </summary>
    private void OnInitialized(object sender, EventArgs args) {
      MessageText.text = "";
      ToggleControls(true);
    }

    /// <summary>
    /// Unregister the event listeners when this object is destroyed.
    /// </summary>
    private void OnDestroy() {
      leaderboard.FirebaseInitialized -= OnInitialized;
      leaderboard.ScoreAdded -= ScoreAdded;
      leaderboard.TopScoresUpdated -= UpdateScoreDisplay;
      leaderboard.UserScoreUpdated -= UpdateUserScoreDisplay;
    }

    /// <summary>
    /// When a new score is added, update the message text with the total # of scores
    /// added since startup.
    /// </summary>
    private void ScoreAdded(object sender, UserScoreArgs args) {
      scoresAdded++;
      MessageText.text = "Scores Added: " + scoresAdded;
    }

    /// <summary>
    /// When a new list of top scores is received from LeaderboardController,
    /// update the contents of the top score ScrollView with prefabs containing
    /// the user ID, timestamp, and score. Keep the prefabs around so we can
    /// re-use them when retrieving more scores in the future, disabling any past
    /// the number requested.
    /// </summary>
    private void UpdateScoreDisplay(object sender, TopScoreArgs args) {
      var topScores = args.TopScores;
      for (var i = 0; i < Math.Min(topScores.Count, scoreObjects.Count); i++) {
        var score = topScores[i];
        var scoreObject = scoreObjects[i];
        scoreObject.SetActive(true);
        var textElements = scoreObject.GetComponentsInChildren<Text>();
        textElements[(int)TopScoreElement.Username].text =
            String.IsNullOrEmpty(score.Username) ? score.UserID : score.Username;
        textElements[(int)TopScoreElement.Timestamp].text = score.ShortDateString;
        textElements[(int)TopScoreElement.Score].text = score.Score.ToString();
      }
      // Turn off extra scores if there are any.
      for (var i = topScores.Count; i < scoreObjects.Count; i++) {
        scoreObjects[i].SetActive(false);
      }
    }

    /// <summary>
    /// Callback for when a particular user's score is retrieved.
    /// </summary>
    private void UpdateUserScoreDisplay(object sender, UserScoreArgs args) {
      if (args.Score == null) {
        UserScoreText.text = "none";
        return;
      }
      UserScoreText.text = args.Score.Score.ToString();
      // UpdateUserScoreDisplay is called after AddScore button is clicked manually, or for
      // the very last score added when adding a random number of score records.
      // If subscribed to ScoreAdded, remove the subscription until the next button press or
      // batch call.
      leaderboard.ScoreAdded -= UpdateUserScoreDisplay;
    }

    /// <summary>
    /// Called on Start. Instantiates MaxRetrievableScores instances of the TopScores prefab.
    /// </summary>
    private IEnumerator CreateTopScorePrefabs() {
      // Verify that top score prefab has 3 Text components in children:
      // one for user ID, timestamp, and score.
      var textElements = TopScorePrefab.GetComponentsInChildren<Text>();
      var topScoreElementValues = Enum.GetValues(typeof(TopScoreElement));
      var lastTopScoreElementValue =
          (int)topScoreElementValues.GetValue(topScoreElementValues.Length - 1);
      if (textElements.Length < lastTopScoreElementValue) {
        throw new InvalidOperationException(String.Format(
            "At least {0} Text components must be present on TopScorePrefab. Found {1}",
            lastTopScoreElementValue,
            textElements.Length));
      }
      for (int i = 0; i < MaxRetrievableScores; i++) {
        GameObject scoreObject = Instantiate(TopScorePrefab, ScoreContainer.transform);
        scoreObject.GetComponentInChildren<Text>().text = (i + 1).ToString();
        scoreObjects.Add(scoreObject);
        scoreObject.SetActive(false);
        scoreObject.name = "Top Score Record " + i;
        yield return null;
      }

      // Once prefabs are created, safe to enable LeaderboardController.
      MessageText.text = "Initializing Firebase Database connection...";
      leaderboard.enabled = true;
      leaderboard.GetUserScore(UserIDInput.text);
    }

    /// <summary>
    /// Called by UserIDInput's OnValueChanged. Validates UserID value and propagates to
    /// UserIDScoreInput.
    /// If valid, triggers call to get the new UserID's top score within time frame.
    /// </summary>
    /// <param name="newUserId">New value of UserIDInput.</param>
    public void UserIDInputChanged(string newUserId) {
      if (newUserId.Length == 0) {
        UserScoreLabel.text = "Top score for user :";
        UserScoreText.text = "n/a";
        return;
      }
      UserScoreLabel.text = "Top score for user " + newUserId + ":";
      UserScoreText.text = "...";
      AddScoreButton.interactable = UserIdAndScoreInputValid();
      leaderboard.GetUserScore(newUserId);
    }

    /// <summary>
    /// Called by UserIDInput and ScoreInput fields OnValueChanged. If either doesn't have valid
    /// input, disables the AddScore button.
    /// </summary>
    public void ScoreInputChanged() {
      AddScoreButton.interactable = UserIdAndScoreInputValid();
    }

    /// <summary>
    /// Checks whether UserIDInput and ScoreInput have valid fields.
    /// </summary>
    /// <returns>True if both fields are valid.</returns>
    private bool UserIdAndScoreInputValid() {
      long score;
      return !String.IsNullOrEmpty(UserIDInput.text) && Int64.TryParse(ScoreInput.text, out score);
    }

    /// <summary>
    /// Called when NumScoresInput value changes. Clamps the value from 1 to MaxRetrievableScores.
    /// </summary>
    public void CapRetrievableScores() {
      int value;
      if (Int32.TryParse(NumScoresInput.text, out value)) {
        NumScoresInput.text = Mathf.Clamp(value, 1, MaxRetrievableScores).ToString();
        leaderboard.ScoresToRetrieve = value;
      } else {
        NumScoresInput.text = "";
      }
    }

    /// <summary>
    /// Called specifically when the Add Scores button is pressed. Adds the score values
    /// from UserIDInput and ScoreInput, and then updates the top scores display.
    /// </summary>
    public void AddScore() {
      leaderboard.ScoreAdded += UpdateUserScoreDisplay;
      AddScore(UserIDInput.text, UsernameInput.text, int.Parse(ScoreInput.text));
    }

    /// <summary>
    /// Called by AddScore() and by AddRandomScores(). Invokes AddScore from the
    /// LeaderboardController, but does not update the top scores display on its own.
    /// </summary>
    /// <param name="userId">User ID for whom to add a score.</param>
    /// <param name="score">Score to add.</param>
    public void AddScore(string userId, string username, int score) {
      leaderboard.AddScore(userId, username, score);
    }

    /// <summary>
    /// Called by the Add Scores buttons, adding 1, 10, 100, 1,000, or 10,000 top scores.
    /// </summary>
    /// <param name="num">The number of random scores to add.</param>
    public void AddRandomScores(int num) {
      StartCoroutine(AddRandomScoresCR(num));
    }

    /// <summary>
    /// Disables or enables all the Selectable components found underneath this GameObject.
    /// </summary>
    /// <param name="enable">Whether to enable the controls. True by default.</param>
    private void ToggleControls(bool enable = true) {
      foreach (var control in GetComponentsInChildren<Selectable>()) {
        control.interactable = enable;
      }
    }

    /// <summary>
    /// Coroutine that adds [num] random scores to the database. Disables the controls beforehand
    /// and re-enables them afterwards. Occasionally updates the UserIDInput and ScoreInput with
    /// a sample of one of the scores being generated.
    /// </summary>
    /// <param name="num">The number of random scores to add.</param>
    private IEnumerator AddRandomScoresCR(int num) {
      if (num <= 0) {
        yield break;
      }
      ToggleControls(false);
      string userId = null;
      var score = 0;
      scoresAdded = 0;
      for (var i = 0; i < num; i++) {
        userId = System.Guid.NewGuid().ToString().Substring(0, 6);
        score = random.Next(10000);
        AddScore(userId, userId, score);
        if (i % DisplayRandomScoreInterval == 0) {
          UserIDInput.text = UsernameInput.text = userId;
          ScoreInput.text = score.ToString();
          yield return null;
        }
      }
      UserIDInput.text = userId;
      ScoreInput.text = score.ToString();
      while (scoresAdded < num) {
        yield return null;
      }
      // Update the user score display for the last score generated.
      UserScoreLabel.text = "Top score for user " + UserIDInput.text + ":";
      leaderboard.GetUserScore(UserIDInput.text);
      ToggleControls();
    }

    /// <summary>
    /// Shows or hides the dialogue asking if the user is sure they want to clear all scores.
    /// Called when the Clear Scores button is clicked, and hidden when either the Confirm or
    /// Cancel buttons are clicked.
    /// </summary>
    /// <param name="enable">Whether to enable the confirm clear panel.</param>
    public void ToggleClearScoresConfirm(bool enable) {
      ConfirmClearPanel.SetActive(enable);
    }

    /// <summary>
    /// Called when Confirm is clicked from within ConfirmClearPanel. Waits for the
    /// LeaderboardController to be in the Initialized state, then removes the
    /// AllScoreDataPath from the database.
    /// </summary>
    public void ClearScores() {
      ToggleClearScoresConfirm(false);
      FirebaseInitializer.Initialize(status => {
        if (status != DependencyStatus.Available) {
          MessageText.text = "Failed to initialize Firebase Database. DependencyStatus: " + status;
          return;
        }
        leaderboard.dbref.Child(leaderboard.AllScoreDataPath).RemoveValueAsync().ContinueWith(
          task => {
            if (task.Exception != null) {
              throw task.Exception;
            }
          });
      });
    }

    /// <summary>
    /// Called when the Online checkbox is toggled. Sets the leaderboard to be online/offline.
    /// </summary>
    /// <param name="online">Whether the Leaderboard should be online.</param>
    public void SetLeaderboardOnline(bool online) {
      if (online) {
        leaderboard.GoOnline();
      } else {
        leaderboard.GoOffline();
      }
    }
  }
}

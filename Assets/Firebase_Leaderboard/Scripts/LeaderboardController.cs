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
using Firebase.Unity.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Firebase.Leaderboard {
  public class LeaderboardController : MonoBehaviour {
    /// <summary>
    /// Use a service account to authenticate requests while in the Unity Editor. This
    /// allows you to clear all the scores and add scores for any user ID without validating
    /// authentication.
    ///
    /// This is only necessary after changing rules from allowing public access to read/write.
    /// See https://firebase.google.com/docs/database/unity/start#setting_up_public_access.
    /// </summary>
    public bool EditorAuth = true;

    /// <summary>
    /// Location of Editor P12 file. This must be in a base folder called "Editor Default
    /// Resources." See instructions at
    /// https://firebase.google.com/docs/database/unity/start#optional_editor_setup_for_restricted_access.
    /// </summary>
    public string EditorP12FileName;

    /// <summary>
    /// Service account email used for Editor authentication after generating the P12 key file.
    /// </summary>
    public string EditorServiceAccountEmail;

    /// <summary>
    /// Password created along with P12 key file in authentication process.
    /// </summary>
    public string EditorP12Password;

    /// <summary>
    /// Path to store all scores in Firebase Database.
    /// </summary>
    public string AllScoreDataPath = "all_scores";

    /// <summary>
    /// Subscribe to be notified when the database has been initialized.
    /// </summary>
    public event EventHandler FirebaseInitialized;

    /// <summary>
    /// Subscribe to be notified when a score is successfully added by AddScore.
    /// </summary>
    public event EventHandler<UserScoreArgs> ScoreAdded;

    /// <summary>
    /// Subscribe to be notified when a user's score is retrieved as triggered by GetUserScore.
    /// </summary>
    public event EventHandler<UserScoreArgs> UserScoreUpdated;

    /// <summary>
    /// Subscribe to be notified when the top scores are retrieved as triggered by GetTopScores.
    /// </summary>
    public event EventHandler<TopScoreArgs> TopScoresUpdated;

    /// <summary>
    /// (Readonly) Contains the last set of user top scores requested.
    /// </summary>
    public List<UserScore> TopScores;

    /// <summary>
    /// The number of scores to retrieve/monitor from the database.
    /// </summary>
    [SerializeField]
    [HideInInspector]
    private int ScoresToRetrieveInternal = 20;
    public int ScoresToRetrieve {
      get {
        return ScoresToRetrieveInternal;
      }
      set {
        UpdateRetrievalParams(value, EndTime, Interval);
      }
    }

    /// <summary>
    /// The end of the time frame (in Epoch seconds) in which to query top scores.
    /// If < 0, will query scores up to the present, and will automatically update
    /// whenever a new score comes in that makes it into the top ScoresToRetrieve.
    /// </summary>
    [SerializeField]
    [HideInInspector]
    private long EndTimeInternal;
    public long EndTime {
      get {
        return EndTimeInternal;
      }
      set {
        UpdateRetrievalParams(ScoresToRetrieve, value, Interval);
      }
    }

    /// <summary>
    /// The time span (in Epoch seconds) ending at EndTime in which to look for top
    /// scores. For example, to get scores in the past week, set EndTime to -1, and
    /// set Interval to 60 * 60 * 24 * 7 (seconds in one week).
    /// If < 0, will get all scores up to EndTime.
    /// </summary>
    [SerializeField]
    [HideInInspector]
    private long IntervalInternal;
    public long Interval {
      get {
        return IntervalInternal;
      }
      set {
        UpdateRetrievalParams(ScoresToRetrieve, EndTime, value);
      }
    }

    /// <summary>
    /// The EndTime value in Epoch seconds, converted to a DateTime.
    /// If EndTime <= 0, EndDate is the present.
    /// </summary>
    private DateTime EndDate {
      get {
        return EndTime <= 0L ? DateTime.UtcNow : new DateTime(EndTime * TimeSpan.TicksPerSecond);
      }
    }

    /// <summary>
    /// The EndTime value in Epoch seconds minus the Interval value, converted to a DateTime.
    /// If Interval <= 0, StartDate is the start of Epoch time.
    /// </summary>
    private DateTime StartDate {
      get {
        return Interval <= 0L ?
          new DateTime(0L) :
          EndDate.Subtract(new TimeSpan(Interval * TimeSpan.TicksPerSecond));
      }
    }

    /// <summary>
    /// True when there are any async calls in progress.
    /// </summary>
    public bool TasksProcessing {
      get {
        return readyToInitialize || gettingTopScores || gettingUserScore || addingUserScore;
      }
    }
    // These bools are used to control and monitor the state of the Leaderboard and its
    // asynchronous calls.
    private bool readyToInitialize;
    private bool gettingTopScores;
    private bool gettingUserScore;
    private bool addingUserScore;
    private bool initialized;
    private bool getTopScoresCallQueued;
    private bool getUserScoreCallQueued;
    private bool refreshScores;

    internal DatabaseReference dbref;
    private Query currentNewScoreQuery;
    private bool sendScoreAddedEvent;
    private bool sendUserScoreEvent;
    private bool sendUpdateTopScoresEvent;
    private UserScoreArgs userScoreArgs;
    private Dictionary<string, UserScore> userScores = new Dictionary<string, UserScore>();

    /// <summary>
    /// Called on GameObject start.
    /// Initialize the Firebase connection via FirebaseInitializer.
    /// </summary>
    private void Start() {
      TopScores = new List<UserScore>();
      FirebaseInitializer.Initialize(dependencyStatus => {
        if (dependencyStatus == DependencyStatus.Available) {
          Debug.Log("Firebase database ready.");
          readyToInitialize = true;
        } else {
          Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
        }
      });
    }

    /// <summary>
    /// Make sure to de-queue event listeners when a component is disabled.
    /// </summary>
    private void OnDisable() {
      if (currentNewScoreQuery != null) {
        currentNewScoreQuery.ChildAdded -= OnScoreAdded;
      }
    }

    /// <summary>
    /// Called once per frame.
    /// LeaderboardController's initialization and events are queued in its Database callback
    /// methods and then triggered in Update, because Firebase Database uses asynchronous
    /// threads in "ContinueWith" when results are retrieved, but many Unity functions such
    /// as modifying UI elements can only be called from the main thread.
    /// </summary>
    private void Update() {
      if (!initialized) {
        if (readyToInitialize) {
          FirebaseApp app = FirebaseApp.DefaultInstance;
          if (EditorAuth) {
            app.SetEditorP12FileName(EditorP12FileName);
            app.SetEditorServiceAccountEmail(EditorServiceAccountEmail);
            app.SetEditorP12Password(EditorP12Password);
          }

          if (app.Options.DatabaseUrl != null) {
            app.SetEditorDatabaseUrl(app.Options.DatabaseUrl);
          }

          dbref = FirebaseDatabase.DefaultInstance.RootReference;
          initialized = true;
          RefreshScores();
          readyToInitialize = false;
          if (FirebaseInitialized != null) {
            FirebaseInitialized(this, null);
          }
        }
        return;
      }
      if (refreshScores) {
        RefreshScores();
        return;
      }
      if (sendScoreAddedEvent) {
        sendScoreAddedEvent = false;
        if (ScoreAdded != null) {
          ScoreAdded(this, userScoreArgs);
        }
        return;
      }
      if (sendUserScoreEvent) {
        sendUserScoreEvent = false;
        if (UserScoreUpdated != null) {
          UserScoreUpdated(this, userScoreArgs);
        }
        return;
      }
      if (sendUpdateTopScoresEvent) {
        sendUpdateTopScoresEvent = false;
        if (TopScoresUpdated != null) {
          TopScoresUpdated(this, new TopScoreArgs {
            TopScores = TopScores,
            StartDate = StartDate,
            EndDate = EndDate
          });
        }
        return;
      }
    }

    /// <summary>
    /// Resets the current TopScores and retrieves a new set based on the current
    /// ScoresToRetrieve, EndTime, and Interval fields.
    /// </summary>
    public void RefreshScores() {
      if (initialized) {
        userScores.Clear();
        TopScores.Clear();
        GetInitialTopScores(Int64.MaxValue);
      }
    }

    /// <summary>
    /// Retrieve a single user's top score in a given time frame (default: top all time score).
    /// </summary>
    /// <param name="userId">The unique ID of the user.</param>
    public void GetUserScore(string userId) {
      if (!initialized && !getUserScoreCallQueued) {
        Debug.LogWarning(
            "GetUserScore called before Firebase initialized. Waiting for initialization...");
        getUserScoreCallQueued = true;
        StartCoroutine(GetUserScoreWhenInitialized(userId));
        return;
      }
      if (getUserScoreCallQueued) {
        Debug.LogWarning("Still waiting for initialization...");
        return;
      }

      gettingUserScore = true;
      // Validate start and end times or use default values (Epoch time and now, respectively).
      var startTS = StartDate.Ticks / TimeSpan.TicksPerSecond;
      var endTS = EndDate.Ticks / TimeSpan.TicksPerSecond;

      // Get user scores within time frame, then sort by score to find the highest one.
      dbref.Child(AllScoreDataPath)
          .OrderByChild(UserScore.UserIDPath)
          .StartAt(userId)
          .EndAt(userId)
          .GetValueAsync().ContinueWith(task => {
            if (task.Exception != null) {
              throw task.Exception;
            }
            if (!task.IsCompleted) {
              return;
            }
            if (task.Result.ChildrenCount == 0) {
              userScoreArgs = new UserScoreArgs {
                Message = String.Format("No scores for User {0}", userId)
              };
            } else {
              // Find the User's scores within the time range.
              var scores = ParseValidUserScoreRecords(task.Result, startTS, endTS).ToList();
              if (scores.Count() == 0) {
                userScoreArgs = new UserScoreArgs {
                  Message = String.Format("No scores for User {0} within time range ({1} - {2})",
                      userId,
                      startTS,
                      endTS)
                };
              } else {
                var orderedScores =scores.OrderBy(score => score.Score);
                userScoreArgs = new UserScoreArgs {
                  Score = orderedScores.Last()
                };
              }
            }
            gettingUserScore = false;
            sendUserScoreEvent = true;
          });
    }

    /// <summary>
    /// Add a new score for the user at the given timestamp (default Now).
    /// </summary>
    /// <param name="userId">User ID for whom to add the new score.</param>
    /// <param name="score">The score value.</param>
    /// <param name="timestamp">
    ///   The timestamp the score was achieved, in Epoch seconds. If <= 0, current time is used.
    /// </param>
    [Obsolete("User AddScore(userId, username, score, timestamp) instead.")]
    public void AddScore(string userId, int score, long timestamp = -1L) {
      AddScore(userId, userId, score, timestamp);
    }

    /// <summary>
    /// Add a new score for the user at the given timestamp (default Now).
    /// </summary>
    /// <param name="userId">User ID for whom to add the new score.</param>
    /// <param name="username">Username to display for the score.</param>
    /// <param name="score">The score value.</param>
    /// <param name="timestamp">
    ///   The timestamp the score was achieved, in Epoch seconds. If <= 0, current time is used.
    /// </param>
    public void AddScore(string userId, string username, int score, long timestamp = -1L) {
      if (timestamp <= 0) {
        timestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
      }
      var scoreOb = new UserScore(userId, username, score, timestamp);
      var scoreDict = scoreOb.ToDictionary();

      addingUserScore = true;

      var newEntry = dbref.Child(AllScoreDataPath).Push();
      newEntry.SetValueAsync(scoreDict).ContinueWith(task => {
        if (task.Exception != null) {
          Debug.LogWarning("Exception adding score: " + task.Exception);
        }
        if (!task.IsCompleted) {
          return;
        }
        userScoreArgs = new UserScoreArgs {
          Score = scoreOb
        };
        sendScoreAddedEvent = true;
        addingUserScore = false;
      });
    }

    /// <summary>
    /// Have the local DB go offline, storing new entries locally
    /// until a connection is re-established.
    /// </summary>
    public void GoOffline() {
      FirebaseDatabase.DefaultInstance.GoOffline();
    }

    /// <summary>
    /// Re-establish connection to the FirebaseDatabase.
    /// </summary>
    public void GoOnline() {
      FirebaseDatabase.DefaultInstance.GoOnline();
    }

    /// <summary>
    /// Called when the ScoresToRetrieve, EndTime, or Interval fields are modified.
    /// Bounds the fields to appropriate values, and triggers a call to refresh the top scores if
    /// any of the parameters have changed.
    /// </summary>
    /// <param name="numToRetrieve">New ScoresToRetrieve value.</param>
    /// <param name="endTime">New EndTime value.</param>
    /// <param name="interval">New Interval value.</param>
    private void UpdateRetrievalParams(int numToRetrieve, long endTime, long interval) {
        var newScoresToRetrieve = Math.Max(0, Math.Min(numToRetrieve, 100));
        var newEndTime = endTime < 0L ? 0L : endTime;
        var newInterval = interval < 0L ? 0L : interval;
        if (newInterval > endTime && endTime != 0) {
          newInterval = endTime;
        }

        refreshScores = newScoresToRetrieve != ScoresToRetrieveInternal ||
            newEndTime != EndTimeInternal ||
            newInterval != IntervalInternal;
        ScoresToRetrieveInternal = newScoresToRetrieve;
        EndTimeInternal = newEndTime;
        IntervalInternal = newInterval;
    }

    /// <summary>
    /// Callback when a score record is added with a score high enough for a spot on the
    /// leaderboard.
    /// </summary>
    private void OnScoreAdded(object sender, ChildChangedEventArgs args) {
      var score = new UserScore(args.Snapshot);

      // Verify that score is within start/end times, and isn't already in TopScores.
      if (TopScores.Contains(score)) {
        return;
      }

      if (EndTime > 0 || Interval > 0) {
        var EndTimeInternal = EndTime > 0 ?
            EndTime :
            (DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond);
        var startTime = Interval > 0 ? EndTimeInternal - Interval : 0;
        if (score.Timestamp > EndTimeInternal || score.Timestamp < startTime) {
          return;
        }
      }

      // Don't add if the same user already has a higher score.
      // If the same user has a lower score, remove it.
      var existingScore = TopScores.Find(s => s.UserID == score.UserID);
      if (existingScore != null) {
        if (existingScore.Score > score.Score) {
          return;
        }
        TopScores.Remove(existingScore);
      }
      if (TopScores.Any(s => s.UserID == score.UserID)) {
        return;
      }

      TopScores.Add(score);
      TopScores = TopScores.OrderByDescending(s => s.Score).Take(ScoresToRetrieve).ToList();
      sendUpdateTopScoresEvent = true;
    }

    /// <summary>
    /// Callback when a score record is removed from the database.
    /// </summary>
    private void OnScoreRemoved(object sender, ChildChangedEventArgs args) {
      var score = new UserScore(args.Snapshot);
      if (TopScores.Contains(score)) {
        TopScores.Remove(score);
        RefreshScores();
      }
    }

    /// <summary>
    /// When the LeaderboardController is enabled, it retrieves a list of the top
    /// ScoresToRetrieve scores in the requested time frame (EndTime - Interval -> EndTime),
    /// by getting batches of the top ScoresToRetrieve score records, filtering out ones outside
    /// the requested time span and duplicates for the same user.
    /// </summary>
    /// <param name="batchEnd">Where the next page should start (ordered by score).</param>
    private void GetInitialTopScores(long batchEnd) {
      gettingTopScores = true;
      var startTS = StartDate.Ticks / TimeSpan.TicksPerSecond;
      var endTS = EndDate.Ticks / TimeSpan.TicksPerSecond;
      dbref.Child(AllScoreDataPath)
          .OrderByChild("score")
          .EndAt(batchEnd)
          .LimitToLast(ScoresToRetrieve)
          .GetValueAsync()
          .ContinueWith(task => {
            if (task.Exception != null) {
              throw task.Exception;
            } else if (!task.IsCompleted) {
              return;
            }

            if (task.Result.ChildrenCount == 0) {
              // No scores left to retrieve.
              SetTopScores(userScores);
              return;
            }
            var scores = ParseValidUserScoreRecords(task.Result, startTS, endTS);
            foreach (var userScore in scores) {
              if (!userScores.ContainsKey(userScore.UserID)) {
                userScores[userScore.UserID] = userScore;
              }
              if (userScores.Count == ScoresToRetrieve) {
                SetTopScores(userScores);
                return;
              }
            }

            // Until we have found ScoresToRetrieve unique user scores or run out of
            // scores to retrieve, get another page of score records by ending the next batch
            // (ordered by score) at the lowest score found so far.
            var lastScore = task.Result.Children.First().Child("score").GetRawJsonValue();
            long score;
            var nextEndAt = Int64.TryParse(lastScore, out score) ?
                score - 1 :
                Math.Max(0, batchEnd - 1);
            GetInitialTopScores(nextEndAt);
          });
    }

    /// <summary>
    /// Parses a DataSnapshot of score records into UserScore objects, and filters out records
    /// whose timestamp does not fall between startTS and endTS.
    /// </summary>
    /// <param name="snapshot">DataSnapshot record of user scores.</param>
    /// <param name="startTS">Earliest valid timestamp of a user score to retrieve.</param>
    /// <param name="endTS">Latest valid timestamp of a user score to retrieve.</param>
    /// <returns>IEnumerable of valid UserScore objects.</returns>
    private IEnumerable<UserScore> ParseValidUserScoreRecords(
        DataSnapshot snapshot,
        long startTS,
        long endTS) {
      return snapshot.Children
          .Select(scoreRecord => new UserScore(scoreRecord))
          .Where(score => score.Timestamp > startTS && score.Timestamp <= endTS)
          .Reverse();
    }

    /// <summary>
    /// When finished retrieving as many valid user scores that can be found given the
    /// ScoresToRetrieve, EndTime, and Interval constraints, this method stores the scores found
    /// and queues an invocation of TopScoresUpdated on the next Update call.
    /// </summary>
    /// <param name="userScores">The valid top scores found, mapped to their user IDs.</param>
    private void SetTopScores(Dictionary<string, UserScore> userScores) {
      TopScores.Clear();
      // Reset top scores and unsubscribe from OnScoreAdded if already listening.
      if (currentNewScoreQuery != null) {
        currentNewScoreQuery.ChildAdded -= OnScoreAdded;
        currentNewScoreQuery.ChildRemoved -= OnScoreRemoved;
      }
      TopScores.AddRange(userScores.Values.OrderByDescending(score => score.Score));
      // Subscribe to any score added that is greater than the lowest current top score.
      currentNewScoreQuery = dbref.Child(AllScoreDataPath).OrderByChild("score");
      if (TopScores.Count > 0) {
        currentNewScoreQuery = currentNewScoreQuery.StartAt(TopScores.Last().Score);
      }
      // If the end date is now, subscribe to future score added events.
      if (EndTime <= 0) {
        currentNewScoreQuery.ChildAdded += OnScoreAdded;
      }
      currentNewScoreQuery.ChildRemoved += OnScoreRemoved;
      // Send the event with the current Top Scores now.
      sendUpdateTopScoresEvent = true;
      gettingTopScores = false;
    }

    /// <summary>
    /// Coroutine that waits for Firebase to be initialized before re-triggering a call
    /// to get a user's top score.
    /// </summary>
    /// <param name="userId">The user ID whose top score should be retrieved.</param>
    private IEnumerator GetUserScoreWhenInitialized(string userId) {
      while (!initialized) {
        yield return null;
      }
      getUserScoreCallQueued = false;
      GetUserScore(userId);
    }
  }
}

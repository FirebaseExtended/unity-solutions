using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Firebase.Leaderboard.Editor {
  /// <summary>
  /// This custom Editor class adds controls to set and modify the
  /// EndTime and Interval properties on the LeaderboardController.
  /// </summary>
  [CustomEditor(typeof(LeaderboardController))]
  public class LeaderboardControllerEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      var controller = target as LeaderboardController;

      // String field for the score data path in Firebase Realtime Database.
      var dataPath = EditorGUILayout.TextField("Score Data Path", controller.AllScoreDataPath);
      if (dataPath != controller.AllScoreDataPath) {
        controller.AllScoreDataPath = dataPath;
      }

      // Int text field for how many scores to retrieve.
      var numScores = Mathf.Clamp(
          EditorGUILayout.IntField("Scores To Retrieve", controller.ScoresToRetrieve), 1, 100);
      if (numScores != controller.ScoresToRetrieve) {
        controller.ScoresToRetrieve = numScores;
      }

      var lowestFirst = EditorGUILayout.Toggle("Low Scores Better", controller.LowestFirst);
      if (lowestFirst != controller.LowestFirst) {
        controller.LowestFirst = lowestFirst;
      }

      // Label explaining the time frame from which the controller will look for scores.
      GUILayout.BeginHorizontal();
      GUILayout.Label("Get scores from: ");
      GUILayout.Label(GetTimeSpanString(controller.EndTime, controller.Interval));
      GUILayout.EndHorizontal();

      // Long text fields for the EndTime and Interval fields.
      // Add some helpful buttons to set the fields to specific values.
      var newEndTime = EditorGUILayout.LongField("End Time", controller.EndTime);
      if (newEndTime != controller.EndTime) {
        controller.EndTime = newEndTime;
      }
      GUILayout.BeginHorizontal();
      GUILayout.BeginVertical();
      if (GUILayout.Button("Now")) {
        controller.EndTime = 0L;
      }
      newEndTime = TimeButtonGroup(controller.EndTime, GetDateTime(controller.EndTime), true);
      if (newEndTime != controller.EndTime) {
        controller.EndTime = newEndTime;
      }
      GUILayout.EndVertical();

      GUILayout.BeginVertical();
      if (GUILayout.Button("Midnight")) {
        var now = DateTime.UtcNow;
        var midnight = now.Subtract(new TimeSpan(now.Hour, now.Minute, now.Second));
        controller.EndTime = midnight.Ticks / TimeSpan.TicksPerSecond;
      }
      newEndTime = TimeButtonGroup(controller.EndTime, GetDateTime(controller.EndTime), false);
      if (newEndTime != controller.EndTime) {
        controller.EndTime = newEndTime;
      }
      GUILayout.EndVertical();
      GUILayout.EndHorizontal();

      var newInterval = EditorGUILayout.LongField("Interval", controller.Interval);
      if (newInterval != controller.Interval) {
        controller.Interval = newInterval;
      }
      GUILayout.BeginHorizontal();
      GUILayout.BeginVertical();
      if (GUILayout.Button("All Time")) {
        controller.Interval = 0L;
      }
      newInterval = TimeButtonGroup(controller.Interval, GetDateTime(controller.Interval), true);
      if (newInterval != controller.Interval) {
        controller.Interval = newInterval;
      }
      GUILayout.EndVertical();

      GUILayout.BeginVertical();
      if (GUILayout.Button("Same Day")) {
        var endDate = GetDateTime(controller.EndTime);
        var sameDayInterval = new TimeSpan(
            endDate.Hour, endDate.Minute, endDate.Second).Ticks / TimeSpan.TicksPerSecond;
        if (sameDayInterval > 0) {
          controller.Interval = sameDayInterval;
        } else {
          // If endTime is midnight, use previous day.
          controller.Interval = 60 * 60 * 24;
        }
      }
      newInterval = TimeButtonGroup(controller.Interval, GetDateTime(controller.Interval), false);
      if (newInterval != controller.Interval) {
        var endDate = GetDateTime(controller.EndTime);
        // Interval can't be greater than EndTime - can't look into negative time.
        // This likely just means the EndTime is 0.
        if (newInterval > endDate.Ticks / TimeSpan.TicksPerSecond) {
          newInterval -= endDate.Ticks / TimeSpan.TicksPerSecond;
        }
        controller.Interval = newInterval;
        if (controller.EndTime == 0) {

        }
      }
      GUILayout.EndVertical();
      GUILayout.EndHorizontal();
    }

    private long TimeButtonGroup(long toSet, DateTime current, bool minus = false) {
      var minusStr = minus ? "-" : "+";
      var minusMod = minus ? -1 : 1;
      try {
        if (GUILayout.Button(minusStr + "1 Minute")) {
          return current.AddMinutes(minusMod).Ticks / TimeSpan.TicksPerSecond;
        }
        if (GUILayout.Button(minusStr + "1 Hour")) {
          return current.AddHours(minusMod).Ticks / TimeSpan.TicksPerSecond;
        }
        if (GUILayout.Button(minusStr + "1 Day")) {
          return current.AddDays(minusMod).Ticks / TimeSpan.TicksPerSecond;
        }
        if (GUILayout.Button(minusStr + "1 Week")) {
          return current.AddDays(minusMod * 7).Ticks / TimeSpan.TicksPerSecond;
        }
        if (GUILayout.Button(minusStr + "1 Month")) {
          return current.AddMonths(minusMod).Ticks / TimeSpan.TicksPerSecond;
        }
      } catch (ArgumentOutOfRangeException) {
        // Caused when trying to go below DateTime(0). Return 0 instead.
        return 0L;
      }
      return toSet;
    }

    private string GetDateString(DateTime date) {
      return date.ToShortDateString() + " " + date.ToShortTimeString();
    }

    private string GetTimeSpanString(long endTime, long interval) {
      if (endTime == 0 && interval == 0) {
        return "All Time";
      }
      var result = "";
      var endDate = endTime > 0 ?
          new DateTime(endTime * TimeSpan.TicksPerSecond) :
          DateTime.UtcNow;
      if (interval == 0) {
        result += GetDateString(new DateTime(0L));
      } else {
        var timespan = new TimeSpan(interval * TimeSpan.TicksPerSecond);
        result += GetDateString(endDate.Subtract(timespan));
      }
      result += " - " + GetDateString(endDate);
      return result;
    }

    private DateTime GetDateTime(long time) {
      return time > 0 ? new DateTime(time * TimeSpan.TicksPerSecond) : DateTime.UtcNow;
    }
  }
}

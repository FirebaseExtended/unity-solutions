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

using System;
using System.Collections.Generic;
using Firebase.Database;
using UnityEngine;

namespace Firebase.Leaderboard {
  /// <summary>
  /// Represents a single user score record kept in FirebaseDatabase. By default a user score
  /// contains a timestamp, score value, and the User's unique ID. You may modify this class
  /// to add fields, but if you remove or change any of the three default fields, you will
  /// need to update the logic in LeaderboardController to match.
  /// </summary>
  [Serializable]
  public class UserScore {
    public static string UserIDPath = "user_id";
    public static string UsernamePath = "username";
    public static string ScorePath = "score";
    public static string TimestampPath = "timestamp";
    public static string OtherDataPath = "data";

    public string UserID;
    public string Username;
    public long Score;
    public long Timestamp;
    public Dictionary<string, object> OtherData;

    /// <summary>
    /// Timestamp is stored as a long value, seconds since Epoch time. This property
    /// outputs the score record date as a nice string in the format
    /// "dd/mm/yyyy hh:mm (AM|PM)"
    /// </summary>
    public string ShortDateString {
      get {
        var scoreDate = new DateTimeOffset(
            new DateTime(Timestamp * TimeSpan.TicksPerSecond, DateTimeKind.Utc)).LocalDateTime;
        return scoreDate.ToShortDateString() + " " + scoreDate.ToShortTimeString();
      }
    }

    /// <summary>
    /// If record contains required score fields, create a new UserScore from it.
    /// If not, return null.
    /// </summary>
    /// <param name="record">The score record object from Firebase Database.</param>
    public static UserScore CreateScoreFromRecord(DataSnapshot record) {
      if (record == null) {
        Debug.LogWarning("Null DataSnapshot record in UserScore.CreateScoreFromRecord.");
        return null;
      }
      if (record.Child(UserIDPath).Exists && record.Child(ScorePath).Exists &&
          record.Child(TimestampPath).Exists) {
        return new UserScore(record);
      }
      Debug.LogWarning("Invalid record format in UserScore.CreateScoreFromRecord.");
      return null;
    }

    /// <summary>
    /// Construct a UserScore manually from a User ID, score value, and timestamp.
    /// </summary>
    /// <param name="userId">The user's unique ID.</param>
    /// <param name="score">The score value.</param>
    /// <param name="timestamp">The timestamp the score was achieved.</param>
    [Obsolete("User UserScore(userId, username, score, timestamp) instead.")]
    public UserScore(string userId, long score, long timestamp) {
      UserID = userId;
      Username = userId;
      Score = score;
      Timestamp = timestamp;
    }

    /// <summary>
    /// Construct a UserScore manually from a User ID, score value, and timestamp.
    /// </summary>
    /// <param name="userId">The user's unique ID.</param>
    /// <param name="username">The user's display name.</param>
    /// <param name="score">The score value.</param>
    /// <param name="timestamp">The timestamp the score was achieved.</param>
    /// <param name="otherData">Miscellaneous data to store with the score object.</param>
    public UserScore(
        string userId,
        string username,
        long score,
        long timestamp,
        Dictionary<string, object> otherData=null) {
      UserID = userId;
      Username = username;
      Score = score;
      Timestamp = timestamp;
      OtherData = otherData;
    }

    /// <summary>
    /// Reconstruct a UserScore from a DataSnapshot record retrieved from Firebase Database.
    /// </summary>
    /// <param name="record">The score record object from Firebase Database.</param>
    private UserScore(DataSnapshot record) {
      UserID = record.Child(UserIDPath).Value.ToString();
      if (record.Child(UsernamePath).Exists) {
        Username = record.Child(UsernamePath).Value.ToString();
      }
      long score;
      if (Int64.TryParse(record.Child(ScorePath).Value.ToString(), out score)) {
        Score = score;
      } else {
        Score = Int64.MinValue;
      }
      long timestamp;
      if (Int64.TryParse(record.Child(TimestampPath).Value.ToString(), out timestamp)) {
        Timestamp = timestamp;
      }
      if (record.Child(OtherDataPath).Exists && record.Child(OtherDataPath).HasChildren) {
        OtherData = new Dictionary<string, object>();
        foreach (var datum in record.Child(OtherDataPath).Children) {
          OtherData[datum.Key] = datum.Value;
        }
      }
    }

    /// <summary>
    /// Convert the UserScore to a Dictionary<string, object>. Used to upload a new score
    /// to Firebase Database, which accepts a Dictionary as a valid score record.
    /// </summary>
    /// <returns>A Dictionary of key string to value objects.</returns>
    public Dictionary<string, object> ToDictionary() {
      return new Dictionary<string, object>() {
        {UserIDPath, UserID},
        {UsernamePath, Username},
        {ScorePath, Score},
        {TimestampPath, Timestamp},
        {OtherDataPath, OtherData}
      };
    }

    public override string ToString() {
      return String.Format("UserID: {0}, Score: {1}, Timestamp: {2}", UserID, Score, ShortDateString);
    }

    public override int GetHashCode() {
      // Multiply field hashcodes by primes to ensure uniqueness.
      return UserID.GetHashCode() * 17 + Score.GetHashCode() * 31 + Timestamp.GetHashCode() * 47;
    }

    public override bool Equals(object obj) {
      if (this == obj) {
        return true;
      }
      var other = obj as UserScore;
      if (other == null) {
        return false;
      }
      return this.UserID == other.UserID && this.Score == other.Score && this.Timestamp == other.Timestamp;
    }
  }
}

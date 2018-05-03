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

namespace Firebase.Leaderboard {
  /// <summary>
  /// An EventArgs class used for LeaderboardController.UserScoreUpdated to notify listeners
  /// when the set of Top Scores has been updated.
  /// </summary>
  public class TopScoreArgs : EventArgs {
    public DateTime StartDate;
    public DateTime EndDate;
    public List<UserScore> TopScores;
  }
}

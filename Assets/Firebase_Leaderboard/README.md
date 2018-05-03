# Cross Platform Leaderboard using Firebase Database

_Copyright (c) 2018 Google Inc. All rights reserved._

This solution makes it easy to incorporate a cross-platform leaderboard into your Unity&reg;
projects. You can follow the steps here to add a LeaderboardController to your own project, or take
a look at the [Demo Scene][demo-scene] to see one in action right away!

[demo-scene]: /Assets/Firebase_Leaderboard/Demo/DemoScene.unity

# Contents

1. [Requirements](#requirements)
1. [Setup](#setup)
   1. [Optional: Setup editor restricted access](#optional--setup-editor-restricted-access)
1. [Usage](#usage)
   1. [Setting up the LeaderboardController](#setting-up-the-leaderboardcontroller)
   1. [Using LeaderboardController EventHandlers](#using-leaderboardcontroller-eventhandlers)
   1. [Retrieving scores](#retrieving-scores)
   1. [Adding a new user score](#adding-a-new-user-score)
1. [What's Next](#whats-next)
   1. [Security and Authorization](#security-and-authorization)
   1. [Data Validation](#data-validation)

# Requirements

* A Unity&reg; project with at least full .NET 2.0 Api level enabled. To enable .NET 2.0, go to
  Edit -> Project Settings -> Player -> Other Settings and change `Api Compatibility Level` to
  `.NET 2.0.
* A Firebase project. You can create a new Firebase project using the
  [firebase console][firebase-console].

[firebase-console]: https://console.firebase.google.com/

# Setup

* Follow the [Setup steps][firebase-setup] for using Firebase Database with Unity&reg;.
* Set up [public access][public-access] to your database. Note that this leaves your database open
  to the public, so you will want to configure rules before releasing your game.

## Optional: Setup editor restricted access

If you choose to use rules that disallow public access, you will need to [configure the SDK to use
  a service account][restricted-access] to run in the Unity&reg; Editor.
* Copy the rule text found in [firebase-db-rules.txt][firebase-db-rules].
* Go to the [firebase console][firebase-console], select your project, choose `Database` and click
  on the `Rules` tab. Paste the contents of `firebase-db-rules.txt`.

[firebase-setup]: https://firebase.google.com/docs/database/unity/start?authuser=0#setup
[public-access]: https://firebase.google.com/docs/database/unity/start?authuser=0#setting_up_public_access
[restricted-access]: https://firebase.google.com/docs/database/unity/start?authuser=0#optional_editor_setup_for_restricted_access
[firebase-db-rules]: /Assets/Firebase_Leaderboard/firebase-db-rules.txt

# Usage

## Setting up the LeaderboardController

* If you haven't already, copy all the `.cs` source files from
  [Firebase_Leaderboard/Scripts][scripts-folder] to your project.
* Add the `LeaderboardController` MonoBehaviour to a GameObject in
  your scene.
  * It is advisable to disable the GameObject with the LeaderboardController component attached
    when the player is not viewing the Leaderboard, as leaving it enabled will cause the
    LeaderboardController to continue to listen for new score records being added to the DB.
* Fill out the fields on the controller as appropriate.
  * `EditorAuth`: Enable this and fill out the following optional parameters if you have set up
    [restricted editor authentication][restricted-access] for your project.
    * `EditorP12FileName`: The location of the P12 key file downloaded when restricted access was
      set up.
    * `EditorServiceAccountEmail`: The service account that has access to your database. This can
      found on the cloud console's [IAM control][cloud-iam].
    * `EditorP12Password`: The password generated for restricted access.
  * `AllScoreDataPath`: If you wish to use a different path to keep all the user score data (for
    example, if you are already using Firebase DB for other data and want to use a longer path),
    you can change that here.
    * Note that if you do, you will also need to update the path in the rules text if you set up
      editor restricted access.

[scripts-folder]: /Assets/Firebase_Leaderboard/Scripts
[cloud-iam]: https://console.cloud.google.com/iam-admin/iam

## Using LeaderboardController EventHandlers

Because Firebase Database calls are asynchronous, the functions that retrieve and add data to the
DB do not return their results directly. Instead, you subscribe to `EventHandler`s that trigger
when a result is completed.

There are three events that fire when the LeaderboardController completes various actions.

* `ScoreAdded`: Called when a new user score is saved to the DB.
* `UserScoreUpdated`: Called when the current user's top score is retrieved.
* `TopScoresUpdated`: Called when a set of all users' top scores is retrieved.

## Retrieving scores

### Top Scores

Retrieving top scores is as simple as specifying the `NumScoresToRetrieve`, `EndTime`, and
`Interval` fields on the LeaderboardController. When the component is enabled, and Firebase is
initialized, it will automatically retrieve the top scores from the requested time frame. If
EndTime is the present (0), it will also listen for any new scores that would displace any of the
current top scores, and send TopScoresUpdated events accordingly.

For example, if you retrieved the top 5 scores, and the score values are 15, 20, 22, 24, and 31,
then the LeaderboardController sends 1 TopScoresUpdated event with those scores when it is enabled.
Then, if a new score that is > 15 is added while the component is enabled, it will send a new
TopScoresUpdated event with the updated list.

### User Scores

If you want to retrieve the top score for a particular user, call `GetUserScore` and provide the
user's unique ID. When the asynchronous call is complete, the `UserScoreUpdated` event is invoked.

## Adding a new user score

To add a new user score, simply call `AddScore` with the following fields:

* `userId`: The unique ID of the user for whom to add a score, based on whatever authentication
  method ([Firebase Auth][firebase-auth] or otherwise) you prefer.
* `score`: The new score (as an integer).
* `timestamp` (default now): The time the score was achieved.

[firebase-auth]: https://firebase.google.com/docs/auth/unity/start

# What's Next

## Security and Authorization

The rules provided in [firebase-db-rules.txt][firebase-db-rules] are pretty limited, and don't
offer any validation that user's are not manipulating other scores in `all_scores` data. A
malicious user could potentially fake their own high score, or even delete other user's scores!

With some build platforms, this may not be a danger, as the code to find the database URL may be
obfuscated or hidden. However, you will likely want to protect this data with more limited write
rules, limiting what data a user can manipulate via authentication.

The easiest way to do this is to incorporate [Firebase Authentication][firebase-auth], as
Firebase Database has the built-in [`auth` variable][auth-var] that contains a user's uid. If you
do use Firebase Authentication, you can replace the rules text for your database with the text
in [firebase-db-auth-rules.txt][firebase-db-auth-rules]. This limits new score data writes to
entries whose `user_id` attribute matches the `auth.uid`. Remember that you must pass the
Firebase Auth UID to the `AddScore` as the user's ID, or the writes will fail!

[auth-var]: https://firebase.google.com/docs/database/security/user-security#section-variable
[firebase-db-auth-rules]: /Assets/Firebase_Leaderboard/firebase-db-auth-rules.txt

## Data Validation

Using Firebase Auth to restrict write access to user's own records will prevent users from being
deleting or manipulating other users' records, but it won't stop a malicious user from manipulating
their own score data. Again, the likelihood of this happening depends on the build platform.

One common method of securing against this vulnerability is to obfuscate the score in some way,
upload the obfuscated value to the Firebase Database, and then have a server-side or serverless
process that you control read and respond to that data to calculate the score.

This could be as simple as hashing the score value and de-hashing via a
[Cloud Function][cloud-functions], which then updates the Firebase DB score value for the user.

Or, instead of uploading the score value to the DB, you could instead upload some data about the
final game state, and then have a server-side process read that state, compute a score from it,
and update the score value in kind.

[cloud-functions]: https://firebase.google.com/docs/functions/

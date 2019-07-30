# Push-free updates using Firebase Remote Config

_Copyright (c) 2019 Google LLC All rights reserved._

This solution utilizes Firebase’s Remote Config tool to allow Unity developers to modify their
game’s behavior in real time, without requiring a fresh build & release cycle.

Once you've introduced this solution to your project, syncing field values between Remote Config
and GameObjects in your game is as easy as adding a single Component and clicking a few buttons.
You can even set values right from the Unity Editor, without even opening the Firebase console!

# Contents

1. [Requirements](#requirements)
1. [Setup](#setup)
   1. [Create a Firebase project](#create-a-firebase-project)
   1. [Download a google-credentials.json file](#download-a-google-credentials.json-file)
   1. [Add the FirebaseRemoteConfigAutoSync Unity package](#add-the-firebaseremoteconfigautosync-unity-package)
1. [Patroller Demo Scene](#patroller-demo-scene)
   1. [Using the custom editor](#using-the-custom-editor)
   1. [Refreshing values during play](#refreshing-values-during-play)
1. [Using RemoteConfigSyncBehaviour](#using-remoteconfigsyncbehaviour)
   1. [Naming parameters](#naming-parameters)
   1. [Target selection parameters](#target-selection-parameters)
1. [What's Next](#whats-next)

# Requirements

* A Unity&reg; project with .NET 4.x Api level enabled. To enable .NET 4.x, go to
  Edit -> Project Settings -> Player -> Configuration and change `Api Compatibility Level` to
  `.NET 4.x`.

[firebase-console]: https://console.firebase.google.com/

# Setup

## Create a Firebase project.

* If you don't already have a Firebase project, follow the [Setup steps][firebase-setup] to create
  one.
* Download the Remote Config SDK from the `Add a Firebase Unity SDK` step.

## Download a google-credentials.json file

* Go to the [service accounts page][service-accounts] on the Cloud console.
* Click the `Actions` dropdown from the `firebase-adminsdk` service account created when you first
  created your Firebase project.
* Select `Create key` to download a `google-credentials.json` file.
* Place the `google-credentials.json` file somewhere in your Project's Assets directory.
  * Important: Do not place this file anywhere below any `Resources` or `Streaming Assets` folders.
    This credential file contains authentication details to enable the Unity editor to call the
    Remote Config REST api, and is not intended to be included in a build binary. The base-level
    `Assets` directory is a good place for this, but any path without either of those strings
    will do.

[firebase-setup]: https://firebase.google.com/docs/unity/setup
[service-accounts]: https://console.cloud.google.com/iam-admin/serviceaccounts

## Add the FirebaseRemoteConfigAutoSync Unity package

* Download the latest FirebaseRemoteConfigAutoSync unitypackage file from the
  [current-build](/current-build) directory.
* Import the unitypackage file at Assets -> Import Package -> Custom Package.
* Open the Remote Config Credentials editor window at Window -> Firebase -> Remote Config ->
  Credentials.
* Drag your `google-credentials.json` file from your Assets to the `Google Credential File` field.

# Patroller Demo Scene

Open [Demo Scene 1][demo-scene-1]. This is a simple little scene that generates a "patroller"
object which will patrol between a set of points.

Select the `Patrol` prefab under Firebase_RemoteConfig/Demo/Demo 1/Prefabs.

Aside from the built-in Unity components like `Transform` and `Mesh Renderer`, the first Component
in the Inspector window is `RemoteConfigSyncBehaviour`. This Component tells the Auto-Sync tool
that this GameObject's other non-built-in Components (in this case, `PatrolBehaviour`) should be
scanned for sync targets, and defines how to locate and name them.

Remote Config can sync values of the String, integer, double, or boolean primitive types.

In this example, this object's `PatrolBehaviour` field called `Start Point` will map to the Remote
Config parameter `PatrolBehaviour__StartPoint`. However, the names generated can be modified with a
few fields in RemoteConfigSyncBehaviour. See
[Using RemoteConfigSyncBehaviour](#using-remoteconfigsyncbehaviour) for more details.

[sync-behaviour-script]: /Firebase_RemoteConfig/Scripts/RemoteConfigSyncBehaviour.cs
[demo-scene-1]: /Firebase_RemoteConfig/Demo/Demo%201/Demo%201%20Scene.unity

## Using the custom editor

With the `Patrol` prefab selected in the Project view, make sure the `RemoteConfigSyncBehaviour`
component is enabled and `Sync All Fields` is checked.

Open the custom Remote Config browser at `Window` -> `Firebase` -> `Remote Config` -> `Sync`.

Here you should see a list of `Unmapped Parameters` (empty unless you have used Remote Config
before) and a list of `Sync Targets`.

The top group under `Sync Targets` should share the name `PatrolBehaviour`. It should contain
a field called `Sync All`, and fields for each of its matching primitive fields in
[`PatrolBehaviour.cs`][patrol-behaviour].

Click `Sync All` under `PatrolBehaviour`. All its sub-fields should not be highlighted and their
checkboxes enabled.

Click `Upload to Remote Config`. Note the fields are no longer highlighted, but still checked.

Open the [Firebase console][firebase-console], select your project, and go to the `Remote Config`
tool under the `Grow` section.

Observe that the 4 primitive fields in `PatrolBehaviour` now have parameters with the prefix
`PatrolBehaviour__`.

In the Unity Editor, click Play. Observe that an empty scene with 2 buttons appears. Click
`Spawn Patrol`. A 3D sphere should appear and begin to patrol between 4 points in the scene.

This patrol object's field values should match both its default values and the values in Remote
Config: starting from a point on the left, and patrolling through all 4 points at a speed of 2
and size of 1.

Now let's see Remote Config in action. Stop the scene and return to the `Remote Config Sync`
editor.

In the `Remote Config Sync` editor window (NOT the Unity Inspector tab) and change the values
to these:
* `StartPoint`: 1
* `NumPoints`: 2
* `Speed`: 5
* `Size`: .5

Note that they each appear highlighted as you change them from their values in Remote Config.

Click Upload to Remote Config and wait for them to no longer be highlighted.

Before we start the scene, let's confirm that the Prefab in the game itself has not changed.

Select the `Patrol` prefab again and note it still has its initial values.

Refresh the Firebase Console in your browser and confirm it has the new values we set.

Now click Play in the Unity Editor. When the scene starts, click `Spawn Patrol`. Observe that the
patroller now starts on the right, patrols only 2 points, is half the size it once was and
notably faster.

This is because when a GameObject with `RemoteConfigSyncBehaviour` is created or cloned, it checks
its sync target fields against Remote Config parameters, and uses those values (when found) instead
of its defaults.

[patrol-behaviour]: /Firebase_RemoteConfig/Demo%201/Scripts/PatrolBehaviour.cs

## Refreshing values during play

With the game still playing, return to the Remote Config Sync scene and changes the values to:
* `StartPoint`: 2
* `NumPoints`: 3
* `Speed`: 1
* `Size`: 2

Click `Upload to Remote Config`. When it's done, return to the Game tab.

Click to spawn another patrol.

What happened? This new patrol has the same behavior as the last one!

This is because during gameplay, the static class managing sync targets will retrieve Remote Config
values on startup, but won't refresh those values unless explicitly told to do so.

Click `Refresh Values`, and then spawn another patroller. Note that the new patroller has the
newer values, while the existing ones retain their values when they were created. To see how this
is working, you can examine the [`PatrolController`][patrol-controller] script. It forces the
`SyncTargetManager` to get refreshed Remote Config parameters when `Refresh Values` is clicked.

[patrol-controller]: /Firebase_RemoteConfig/Demo%201/Scripts/PatrolController.cs

# Using RemoteConfigSyncBehaviour

The [`RemoteConfigSyncBehaviour`][sync-behaviour-script] script is the Component you attach to
GameObjects in your scene (and prefabs) whose fields you want to sync to and from Remote Config.
Fields that are synced in this way are called "sync targets."

`RemoteConfigSyncBehaviour` has several custom fields that modify the way it locates and names
its sync targets.

## Naming parameters

* `Prefix Source`: By default, the first section of a sync target's prefix is the `Component` it
  is attached to. This allows cloned copies of a prefab (whose names are modified with "(Clone)"
  when they are created) to sync to the same value. But this isn't always the desired behavior.
  Setting this field to `Game Object` uses the name of the GameObject instead of the Component
  (in this case, it would be `Sync Demo Object`). Or, select `Custom` to define a static prefix
  (or no prefix at all).
  * `Key Prefix`: This field only appears when `Prefix Source` is set to `Custom`. Use it to
    define the static prefix desired.

## Target selection parameters

The other two fields on RemoteConfigSyncBehaviour control the discovery of its sync targets.

By default, only fields with the [`RemoteConfigSync`][remote-config-sync] attribute are synced.
This allows fine-grained control over which fields should and should not be synced with Remote
Config.

`[RemoteConfigSync]` can be applied to the primitive types that Remote Config supports, or to
Serializable data objects which contain fields of those types (and so on).

When a Serializable object has the `[RemoteConfigSync]` attribute, its individual fields do not
need the `[RemoteConfigSync]` attribute themselves.

* `Sync All Fields`: If this RemoteConfigSyncBehaviour field is enabled, then ALL primitive
  types found under this GameObject's Components are valid sync targets, regardless of whether they
  have the `[RemoteConfigSync]` attribute or not. When using this setting, you can tell Auto-Sync
  to specifically skip a field with the [`RemoteConfigSkipSync`][skip-sync] attribute.
  * `Include Sub Fields`: This field only appears if `Sync All Fields` is checked. For efficiency
    reasons, even with `Sync All Fields` enabled Auto-Sync still won't go digging through complex
    object children that don't have the `[RemoteConfigSync]` attribute unless `Include Sub Fields`
    is also checked. This tells Auto-Sync to look through this Object's entire field hierarchy for
    sync targets, letting nothing stop it except for `[RemoteConfigSkipSync]`.

_As a rule of thumb: If you want to specify which fields to sync, leave `Sync All Fields` false and_
_use the `[RemoteConfigSync]` attribute on those fields. If you want to specify which fields NOT to_
_include, check `Sync All Fields` and specify fields to skip with `[RemoteConfigSkipSync]`._

To see how these fields modify the sync targets found by the `Remote Config Sync` window, open
[`Demo Scene 2`][demo-scene-2] and select `Demo Sync Object` in the Hierarchy tab. Try selecting
different combinations of `RemoteConfigSyncBehaviour` and observing how it changes the items in the
`Remote Config Sync` tab.

[remote-config-sync]: /Firebase_RemoteConfig/Scripts/RemoteConfigSyncAttribute.cs
[skip-sync]: /Firebase_RemoteConfig/Scripts/RemoteConfigSkipSyncAttribute.cs
[demo-scene-2]: /Firebase_RemoteConfig/Demo/Demo%202/Demo%202%20Scene.unity

# What's Next

The custom editor has several other capabilities as well. It can be used to control/view different
values for parameters based on any [Remote Config Conditions][conditions] you've created.

There are also buttons in the default and condition tabs of the `Remote Config Sync` window to set
all the local targets from Remote Config, or to set that column's sync values from the local
values. You can experiment with this by changing the values on local targets (like the `Patrol`
prefab in Demo Scene 1, or the `Sync Demo Object` in Demo Scene 2), and then clicking
`Set From Local` in the given column, or vise versa.

Check out the other solutions in [Firebase Unity Solutions][unity-solutions] for even more tools to
use Firebase in your Unity project!

[conditions]: https://firebase.google.com/docs/remote-config/parameters
[unity-solutions]: https://github.com/FirebaseExtended/unity-solutions

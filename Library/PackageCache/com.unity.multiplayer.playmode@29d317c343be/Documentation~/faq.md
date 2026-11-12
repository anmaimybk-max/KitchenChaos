# Frequently asked questions

## My Virtual Players aren't synchronized anymore, what can I do?

One potential cause of synchronization issues between the main Editor and Virtual Players in the Multiplayer Play Mode package is corruption within the `Library/VP` folder, which stores their data. If you suspect this is the case, try the following steps to resolve it:

- Close any active Virtual Player instances.
- Close the Unity Editor.
- Delete the `Library/VP folder` in your Unity project.
- Restart the Unity Editor.

The Multiplayer Play Mode package will automatically recreate the Library/VP folder. The Virtual Player data will then be regenerated either when you re-enable the Virtual Players through the Multiplayer Play Mode Window or when you launch a scenario containing Editor instances.

## How do I use the Refresh Virtual Project folder option?

> [!NOTE]
> This functionality is available in Unity 6.6 or later.

The Refresh Virtual Project folder option provides a quick way to clear all Editor instance's library folders without manually deleting files. This is useful when troubleshooting synchronization issues or unexpected behavior with Editor instances.

To use this option:

1. Ensure all Editor instances are stopped. This option is disabled when any Editor instance is active or launching.
2. Open the **Active Scenario** window (**Window** > **Play Mode** > **Active Scenario**).
3. Select the **More options (⋮)** menu in the **Active Scenario** window.
4. Select **Refresh Virtual Project folder**.

The next time you launch a scenario, Unity will automatically regenerate the Editor instances.

## How can I identify the currently running player instance from the code?

### For the main Editor instance

To find out if the current player is the main Editor or not, you can use the the [`IsMainEditor`](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@latest?subfolder=/api/Unity.Multiplayer.Playmode.CurrentPlayer.html#Unity_Multiplayer_Playmode_CurrentPlayer_IsMainEditor) property from the [`CurrentPlayer`](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@latest?subfolder=/api/Unity.Multiplayer.Playmode.CurrentPlayer.html) class.

### For Virtual Players

Several arguments are passed to an Editor instance when it's launched, including the `-name` argument. To find out which Editor instance is currently running, you can parse the launch arguments from the Editor instance. Look for the `-name` argument: the value should be `Player1` (for the main Editor) or `Player2`, `Player3`, or `Player4` (for Virtual Players).

### For local instances

Multiplayer Play Mode doesn't have a generic API that identifies which player is currently running. You can achieve parity with the main Editor instance by using the same `-name` argument as part of the advanced configuration for the local instance. It will be up to you to choose a name and parse it from the code.

### Sample code to parse the `-name` argument

One way to parse the `-name` argument is by using the `System.Environment.GetCommandLineArgs()` method to get the list of the launch arguments and then search through that list.

C# example:

```csharp
using System;

public class Example : MonoBehaviour
{
    var arguments = Environment.GetCommandLineArgs();
    var playerName = arguments[arguments.IndexOf("-name") + 1];
    if(playerName == "Player2")
    {
        // Do something
    }
}

```

## My code assumes the uniqueness of the Editor and adding additional players breaks it. How can I fix it?

The best way to fix this is to update your code to check if the current player is the main Editor or not. You can use the `CurrentPlayer.IsMainEditor` property to check if the current player is the main Editor.

## What's the best way to assign a multiplayer role such as client, server, or client and server (host) to an instance?

The best way to assign a multiplayer role is to use the [Dedicated Server package](play-mode-dedicated-server.md). The use of tags for this purpose isn't recommended, because tags don't scale to local instances.

## I have issues running a scenario on my Android device, what can I do?

Issues with Android platforms often happen when the generated APK doesn't match the platform CPU architecture, or if the scripting backend isn't supported by your Android platform.

### Example with Google Pixel phones

1. Go to **Project Settings** > **Player** > **Android** > **Configuration**.
2. Ensure the selected scripting backend is `IL2CPP` rather than `MONO`.
3. Ensure the target architecture is ARM64.

For other Android-based platforms, refer to the corresponding platform documentation to see what architecture and scripting backend options are recommended.

## What to do when I see the error `Build profile needs to match the current platform`?

To fix the `Build Profile needs to match the current platform` error, follow these steps:

1. Open the Build Profile window (**File** > **Build Profiles**).
2. In the **Platforms** list, select the platform that matches the platform that the Unity Editor is running on.

## I'm using Netcode for Entities and my subscenes don't load correctly on Editor instances

When working with Netcode for Entities, you may encounter issues if you enter Play Mode, start hosting, and then launch additional Editor instances. The subscenes might fail to load properly on the newly launched instances.

To work around this issue, use one of the following approaches:

-  Launch Editor instances before entering Play Mode: Start all Editor instances first, then enter Play Mode and begin hosting on the main Editor.
- Use a Play Mode Scenario: Configure a scenario that launches all instances and enters Play Mode together. This ensures all instances are ready before hosting begins.
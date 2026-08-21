# cloudx-unity

Our complete CloudX Unity SDK integration guide is available on our docs site, [https://docs.cloudx.io/en/unity/integration](https://docs.cloudx.io/en/unity/integration).

[Click here](https://github.com/cloudx-io/cloudx-unity/releases/latest) to download the latest `.unitypackage` Github release.

## Demo app

This repository is also a runnable Unity demo project. It shows a working CloudX integration for
banner, MREC, interstitial and rewarded ads.

Requirements:

- Unity 6 LTS `6000.0.60f1` (see `ProjectSettings/ProjectVersion.txt`)
- iOS: Xcode
- Android: the Unity Android Build Support module

The demo ships with CloudX demo dashboard IDs so it runs without an account. To point it at your own
CloudX app:

1. Replace the app key and ad unit IDs in `Assets/Scripts/DemoConfig.cs`.
2. Set the bundle identifier registered for that app in Unity under
   Project Settings > Player > Identification (`ProjectSettings/ProjectSettings.asset`).

For iOS device builds also set your own Signing Team ID under
Project Settings > Player > Signing. It ships empty on purpose.

Bid requests are authorized per app key and bundle identifier, so both have to match your dashboard
app or the SDK gets no fill.

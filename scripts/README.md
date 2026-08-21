# Internal build scripts

These two scripts are internal CloudX tooling for developing and QA-ing the demo app. They are
deliberately not referenced from the repository README: publishers build this project from the Unity
Editor like any other Unity project, and nothing here is required to do that.

They are macOS-only: the iOS script drives `xcrun` and `xcodebuild`, the Android script drives `adb`,
and both play a completion sound with `afplay`. Each assumes a checkout of this repository with a
connected device.

## build-and-run-android.sh

Exports a Gradle project from Unity in batch mode, builds the APK with the bundled Gradle wrapper,
then installs and launches it over `adb`.

| Flag | Effect |
| --- | --- |
| `-r`, `--release` | Release build instead of development |
| `-u`, `--unity PATH` | Override the Unity executable |
| `-h`, `--help` | Usage |

Requires `adb` on `PATH` and one connected device. The launch target comes from
`ProjectSettings/ProjectSettings.asset`, so it follows the bundle identifier rather than a literal.
`Assets/CloudXSdk/Plugins/Android/cloudx-unity-bridge.aar` must be present; this script never
rebuilds it.

Output: `build/android-project/launcher/build/outputs/apk/{debug,release}/launcher-*.apk`

## build-and-run-ios.sh

Exports an Xcode project from Unity in batch mode (the SDK post-processor runs `pod install`), builds
with `xcodebuild`, then installs and launches. The bundle identifier is read back from the built app,
not assumed.

| Flag | Effect |
| --- | --- |
| `-r`, `--release` | Release build instead of development |
| `-d`, `--device` | Build for a connected device instead of a simulator |
| `-o`, `--open-xcode` | Export and open the workspace in Xcode, then stop |
| `-u`, `--unity PATH` | Override the Unity executable |
| `-h`, `--help` | Usage |

Requires Xcode and `xcbeautify` (`brew install xcbeautify`), which the script pipes `xcodebuild`
through and refuses to run without unless you pass `--open-xcode`.

It defaults to a simulator, but the project ships configured for the iOS device SDK, so a bare
invocation fails to find a destination. Use `--device`, or switch Target SDK to Simulator SDK in
Player Settings first.

Output: `build/ios-build/DerivedData/Build/Products/Debug-iphoneos/CloudXDemoUnity.app`

Known flake: `xcodebuild` occasionally fails with `'CloudXCore/CLXArbiterBid.h' file not found`
because UnityFramework compiles before `CloudXCore.framework` finishes copying into DerivedData.
Re-running the script succeeds; nothing in the project needs changing.

## Unity resolution

Both scripts read the editor version from `ProjectSettings/ProjectVersion.txt` and look for
`/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`. Override with `--unity` or
the `UNITY_EDITOR_PATH` environment variable. Everything is written under `build/`, which is
gitignored.

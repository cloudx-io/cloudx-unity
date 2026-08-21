# Internal build scripts

Internal tooling for building and running the demo app during development. The repository README does
not reference these: publishers build this project from the Unity Editor.

macOS only. Both write to `build/`, which is gitignored.

## build-and-run-android.sh

Exports a Gradle project from Unity, builds the APK, then installs and launches it over `adb`.

| Flag | Effect |
| --- | --- |
| `-r`, `--release` | Release build instead of development |
| `-u`, `--unity PATH` | Override the Unity executable |
| `-h`, `--help` | Usage |

Needs `adb` on `PATH` and one connected device. The launch target comes from
`ProjectSettings/ProjectSettings.asset`, so it follows the bundle identifier.

## build-and-run-ios.sh

Exports an Xcode project from Unity, builds with `xcodebuild`, then installs and launches.

| Flag | Effect |
| --- | --- |
| `-r`, `--release` | Release build instead of development |
| `-d`, `--device` | Build for a connected device instead of a simulator |
| `-o`, `--open-xcode` | Export and open the workspace in Xcode, then stop |
| `-u`, `--unity PATH` | Override the Unity executable |
| `-h`, `--help` | Usage |

Needs Xcode and `xcbeautify` (`brew install xcbeautify`), except with `--open-xcode`. The project is
configured for the device SDK, so use `--device` unless you switch Target SDK in Player Settings.

## Unity resolution

Both read the editor version from `ProjectSettings/ProjectVersion.txt` and look for it under
`/Applications/Unity/Hub/Editor`. Override with `--unity` or `UNITY_EDITOR_PATH`.

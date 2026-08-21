# CloudX SDK - Editor Scripts

This folder contains Unity Editor-only code that is excluded from runtime builds.

## Files

- `CloudXPostProcessiOS.cs` - iOS post-build processing (automatic pod install, dynamic framework embedding)
- `CloudXDependencies.xml` - iOS/Android dependency configuration for EDM4U
- `AndroidBuilder.cs` - Command-line Android build automation
- `PackageExporter.cs` - Unity package export utility

## iOS Setup

The iOS build process is fully automated. When you build for iOS from Unity, the post-process script:

1. Runs `pod install` automatically if dependencies are missing
2. Adds the CloudX SKAdNetwork identifiers while preserving existing entries
3. Detects and embeds all dynamic frameworks from CocoaPods
4. Configures Swift support for dependencies that require it

**Build Steps:**
1. Build from Unity (**File > Build Settings > iOS > Build**)
2. Open the generated `.xcworkspace` in Xcode
3. Build and run from Xcode

### Code Signing

Configure code signing in Unity Player Settings (not hardcoded in scripts):

1. Open **Edit > Project Settings > Player**
2. Select the **iOS** tab
3. Under **Other Settings > Identification**:
   - Set **Bundle Identifier** to your app's bundle ID
4. Under **Other Settings > Signing**:
   - Set **Signing Team ID** to your Apple Developer Team ID
   - Enable **Automatically Sign**

### EDM4U Settings for Meta Adapter

If using `CloudXMetaAdapter`, configure EDM4U to avoid the FBAudienceNetwork static library crash:

1. Open **Assets > External Dependency Manager > iOS Resolver > Settings**
2. **Uncheck** "Add use_frameworks! to Podfile"
3. **Uncheck** "Always add the main target to Podfile"

This prevents the Unity-iPhone target from being added to the Podfile, avoiding duplicate linking of static libraries.

**Why is this needed?**

Meta's FBAudienceNetwork is a static library. When both Unity-iPhone and UnityFramework targets are in the Podfile, CocoaPods links static libraries into both binaries, causing runtime crashes:

```
FBFinalClassViolationException: FBAdSettings is a final class...
```

By removing Unity-iPhone from the Podfile, static libraries only link to UnityFramework, avoiding the crash.

## Android Setup

Android dependencies are managed via EDM4U. No additional configuration is typically required.

## What is the Editor Folder?

Unity's [special folder convention](https://docs.unity3d.com/Manual/SpecialFolders.html) - any folder named `Editor` has these properties:

1. **Editor-Only Compilation**: Code here can use the `UnityEditor` namespace
2. **Excluded from Builds**: Not included in APK/IPA/standalone builds
3. **Development Tools**: Build scripts, custom inspectors, editor utilities

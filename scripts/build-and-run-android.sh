#!/bin/bash

# CloudX Unity Build and Run Script
# Builds and runs the CloudX demo app on a connected Android device
#
# Usage: ./scripts/build-and-run.sh [options]
#
# Options:
#   -r, --release       Build release APK (default: development)
#   -u, --unity PATH    Override Unity executable path
#   -h, --help          Show this help message
#
# Exit codes:
#   0 - Success
#   1 - Error

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Default configuration
RELEASE=false
UNITY_PATH=""

# Get repo root
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Read Unity version from project settings
UNITY_VERSION=$(grep "m_EditorVersion:" "$REPO_ROOT/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
DEFAULT_UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"

# =============================================================================
# Helper Functions
# =============================================================================

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

show_help() {
    sed -n '2,/^$/p' "$0" | sed 's/^# //' | sed 's/^#//'
    exit 0
}

# =============================================================================
# Argument Parsing
# =============================================================================

while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--release)
            RELEASE=true
            shift
            ;;
        -u|--unity)
            UNITY_PATH="$2"
            shift 2
            ;;
        -h|--help)
            show_help
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# =============================================================================
# Resolve Unity Path
# =============================================================================

resolve_unity_path() {
    if [[ -n "$UNITY_PATH" ]]; then
        echo "$UNITY_PATH"
    elif [[ -n "$UNITY_EDITOR_PATH" ]]; then
        echo "$UNITY_EDITOR_PATH"
    else
        echo "$DEFAULT_UNITY"
    fi
}

UNITY_EXECUTABLE=$(resolve_unity_path)
BUNDLED_AAR="$REPO_ROOT/Assets/CloudXSdk/Plugins/Android/cloudx-unity-bridge.aar"
GRADLE_WRAPPER="$REPO_ROOT/gradlew"

if [[ ! -x "$UNITY_EXECUTABLE" ]]; then
    print_error "Unity executable not found at: $UNITY_EXECUTABLE"
    print_info "Use --unity PATH to specify Unity location, or set UNITY_EDITOR_PATH environment variable"
    exit 1
fi

if [[ ! -f "$BUNDLED_AAR" ]]; then
    print_error "Bundled Android AAR not found at: $BUNDLED_AAR"
    print_info "This script does not rebuild the AAR. It must already be present."
    exit 1
fi

if [[ ! -x "$GRADLE_WRAPPER" ]]; then
    print_error "Gradle wrapper not found or not executable at: $GRADLE_WRAPPER"
    exit 1
fi

# =============================================================================
# Determine build method
# =============================================================================

if [[ "$RELEASE" == true ]]; then
    BUILD_METHOD="CloudX.Editor.AndroidBuilder.ExportRelease"
    BUILD_TYPE="Release"
    GRADLE_TASK="assembleRelease"
    APK_PATH="build/android-project/launcher/build/outputs/apk/release/launcher-release.apk"
else
    BUILD_METHOD="CloudX.Editor.AndroidBuilder.ExportDevelopment"
    BUILD_TYPE="Development"
    GRADLE_TASK="assembleDebug"
    APK_PATH="build/android-project/launcher/build/outputs/apk/debug/launcher-debug.apk"
fi

print_info "Build type: $BUILD_TYPE"
print_info "Build method: $BUILD_METHOD"

# =============================================================================
# Step 1: Unity Export to Gradle Project
# =============================================================================

LOG_FILE="$REPO_ROOT/build/unity-export-$(date +%Y%m%d-%H%M%S).log"
mkdir -p "$REPO_ROOT/build"

print_info "Step 1: Exporting Gradle project from Unity..."
print_info "Unity: $UNITY_EXECUTABLE"
print_info "Project: $REPO_ROOT"
print_info "Log file: $LOG_FILE"

# Capture start time
START_TIME=$(date +%s)

"$UNITY_EXECUTABLE" \
    -batchmode \
    -nographics \
    -projectPath "$REPO_ROOT" \
    -executeMethod "$BUILD_METHOD" \
    -buildTarget Android \
    -quit \
    -logFile "$LOG_FILE" \
    2>&1 || {
        EXIT_CODE=$?
        print_error "Unity export failed with exit code: $EXIT_CODE"
        print_error "Check log file for details: $LOG_FILE"

        # Show last 30 lines of log for quick debugging
        if [[ -f "$LOG_FILE" ]]; then
            echo ""
            print_warning "Last 30 lines of Unity log:"
            tail -30 "$LOG_FILE"
        fi

        exit $EXIT_CODE
    }

# Calculate Unity export time
UNITY_END_TIME=$(date +%s)
UNITY_TIME=$((UNITY_END_TIME - START_TIME))
print_success "Unity export completed in ${UNITY_TIME}s"

# =============================================================================
# Step 2: Build APK with Gradle
# =============================================================================

GRADLE_PROJECT="$REPO_ROOT/build/android-project"

if [[ ! -d "$GRADLE_PROJECT" ]]; then
    print_error "Gradle project not found at: $GRADLE_PROJECT"
    exit 1
fi

print_info "Step 2: Building APK with Gradle wrapper..."

"$GRADLE_WRAPPER" -p "$GRADLE_PROJECT" "$GRADLE_TASK" || {
    print_error "Gradle build failed"
    exit 1
}

# Calculate Gradle build time
GRADLE_END_TIME=$(date +%s)
GRADLE_TIME=$((GRADLE_END_TIME - UNITY_END_TIME))
print_success "Gradle build completed in ${GRADLE_TIME}s"

# =============================================================================
# Step 3: Install and Launch
# =============================================================================

OUTPUT_FILE="$REPO_ROOT/$APK_PATH"

if [[ ! -f "$OUTPUT_FILE" ]]; then
    print_error "APK not found at: $OUTPUT_FILE"
    exit 1
fi

print_info "Step 3: Installing and launching app..."
adb install -r "$OUTPUT_FILE" || {
    print_error "Failed to install APK"
    exit 1
}

# Launch the app. The package comes from Player Settings, not a literal, so this
# keeps working after you point the sample at your own bundle identifier.
# tr strips the CR a Windows checkout leaves behind; the || true keeps a missing
# or unreadable settings file on the warning path below instead of tripping set -e.
ANDROID_PACKAGE=$(awk '/^  applicationIdentifier:/{f=1;next} f&&/^    Android:/{print $2;exit} f&&/^  [A-Za-z]/{exit}' \
    "$REPO_ROOT/ProjectSettings/ProjectSettings.asset" 2>/dev/null | tr -d '\r' || true)

LAUNCHED=true
if [[ -z "$ANDROID_PACKAGE" ]]; then
    print_warning "Could not read the Android package from ProjectSettings; skipping launch"
    LAUNCHED=false
elif ! adb shell am start -n "$ANDROID_PACKAGE/com.unity3d.player.UnityPlayerActivity"; then
    print_warning "Failed to launch $ANDROID_PACKAGE (may need manual start)"
    LAUNCHED=false
fi

# Calculate total build time
END_TIME=$(date +%s)
TOTAL_TIME=$((END_TIME - START_TIME))
TOTAL_TIME_MIN=$((TOTAL_TIME / 60))
TOTAL_TIME_SEC=$((TOTAL_TIME % 60))

FILE_SIZE=$(ls -lh "$OUTPUT_FILE" | awk '{print $5}')
print_success "Build completed successfully!"
print_success "Unity export: ${UNITY_TIME}s | Gradle build: ${GRADLE_TIME}s | Total: ${TOTAL_TIME_MIN}m ${TOTAL_TIME_SEC}s"
print_success "APK: $OUTPUT_FILE ($FILE_SIZE)"
if [[ "$LAUNCHED" == true ]]; then
    print_success "App launched on device"
fi

# Return to repo root
cd "$REPO_ROOT"

# Play completion sound on macOS
if [[ "$(uname)" == "Darwin" ]]; then
    afplay /System/Library/Sounds/Ping.aiff 2>/dev/null &
fi

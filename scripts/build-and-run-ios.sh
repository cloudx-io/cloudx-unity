#!/bin/bash

# CloudX Unity Build and Run Script for iOS
# Builds and runs the CloudX demo app on a connected iOS device or simulator
#
# Usage: ./scripts/build-and-run-ios.sh [options]
#
# Options:
#   -r, --release       Build release (default: development)
#   -d, --device        Build for device instead of simulator
#   -o, --open-xcode    Export the Xcode project, open the workspace in Xcode, and stop
#   -u, --unity PATH    Override Unity executable path
#   -h, --help          Show this help message
#
# Requirements:
#   - Xcode and xcodebuild
#   - xcbeautify: brew install xcbeautify
#   - Connected iOS device or running simulator
#
# Exit codes:
#   0 - Success
#   1 - Error

set -e
set -o pipefail

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Default configuration
RELEASE=false
SIMULATOR=true
OPEN_XCODE=false
UNITY_PATH=""

# Get repo root
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Read Unity version from project settings
UNITY_VERSION=$(grep "m_EditorVersion:" "$REPO_ROOT/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
DEFAULT_UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"

# Xcode project settings
XCODE_WORKSPACE="$REPO_ROOT/build/ios-project/Unity-iPhone.xcworkspace"
SCHEME="Unity-iPhone"

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

ensure_xcbeautify_available() {
    if command -v xcbeautify &> /dev/null; then
        return 0
    fi

    print_error "Required tool 'xcbeautify' was not found on PATH."
    print_info "This script pipes xcodebuild output through xcbeautify so Xcode failures remain readable."
    print_info "Install it with: brew install xcbeautify"
    print_info "To export the Xcode project without building it, run this script with --open-xcode."
    exit 1
}

resolve_bundle_id() {
    local app_path="$1"

    if [[ -z "$app_path" || ! -d "$app_path" ]]; then
        return 1
    fi

    defaults read "$app_path/Info.plist" CFBundleIdentifier 2>/dev/null
}

find_booted_ios_simulator() {
    xcrun simctl list devices available | grep "Booted" | grep -E "iPhone|iPad" | head -1 | grep -oE "[A-F0-9-]{36}" || true
}

find_available_iphone_simulator() {
    xcrun simctl list devices available | grep "iPhone" | head -1 | grep -oE "[A-F0-9-]{36}" || true
}

find_available_ipad_simulator() {
    xcrun simctl list devices available | grep "iPad" | head -1 | grep -oE "[A-F0-9-]{36}" || true
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
        -d|--device)
            SIMULATOR=false
            shift
            ;;
        -o|--open-xcode)
            OPEN_XCODE=true
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

if [[ ! -x "$UNITY_EXECUTABLE" ]]; then
    print_error "Unity executable not found at: $UNITY_EXECUTABLE"
    print_info "Use --unity PATH to specify Unity location, or set UNITY_EDITOR_PATH environment variable"
    exit 1
fi

# =============================================================================
# Determine build configuration
# =============================================================================

if [[ "$RELEASE" == true ]]; then
    BUILD_METHOD="CloudX.Editor.iOSBuilder.ExportRelease"
    BUILD_TYPE="Release"
    CONFIGURATION="Release"
else
    BUILD_METHOD="CloudX.Editor.iOSBuilder.ExportDevelopment"
    BUILD_TYPE="Development"
    CONFIGURATION="Debug"
fi

if [[ "$SIMULATOR" == true ]]; then
    DESTINATION="generic/platform=iOS Simulator"
    SDK="iphonesimulator"
    ARCH="arm64"
else
    DESTINATION="generic/platform=iOS"
    SDK="iphoneos"
    ARCH="arm64"
fi

print_info "Build type: $BUILD_TYPE"
print_info "Target: $(if [[ "$SIMULATOR" == true ]]; then echo "Simulator"; else echo "Device"; fi)"

if [[ "$OPEN_XCODE" == false ]]; then
    ensure_xcbeautify_available
fi

SIMULATOR_ID=""
if [[ "$SIMULATOR" == true ]]; then
    SIMULATOR_ID=$(find_booted_ios_simulator)

    if [[ -z "$SIMULATOR_ID" ]]; then
        print_info "No simulator running. Booting an available iPhone simulator..."
        SIMULATOR_ID=$(find_available_iphone_simulator)
        if [[ -z "$SIMULATOR_ID" ]]; then
            print_warning "No iPhone simulator found. Falling back to an available iPad simulator..."
            SIMULATOR_ID=$(find_available_ipad_simulator)
        fi
        if [[ -z "$SIMULATOR_ID" ]]; then
            print_error "No suitable iOS simulator found"
            exit 1
        fi
        xcrun simctl boot "$SIMULATOR_ID"
        open -a Simulator
        xcrun simctl bootstatus "$SIMULATOR_ID" -b
    fi

    DESTINATION="id=$SIMULATOR_ID"
    print_info "Simulator destination: $DESTINATION"
fi

# =============================================================================
# Step 1: Unity Export to Xcode Project
# =============================================================================

LOG_FILE="$REPO_ROOT/build/unity-export-ios-$(date +%Y%m%d-%H%M%S).log"
mkdir -p "$REPO_ROOT/build"

print_info "Step 1: Exporting Xcode project from Unity..."
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
    -buildTarget iOS \
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

if [[ "$OPEN_XCODE" == true ]]; then
    if [[ ! -d "$XCODE_WORKSPACE" ]]; then
        print_error "Xcode workspace not found at: $XCODE_WORKSPACE"
        exit 1
    fi

    print_info "Force killing Xcode so the freshly exported workspace opens cleanly..."
    pkill -9 -x Xcode || true
    sleep 1

    print_info "Opening Xcode workspace and stopping after export..."
    open -a Xcode "$XCODE_WORKSPACE"
    print_success "Opened Xcode workspace: $XCODE_WORKSPACE"
    exit 0
fi

# =============================================================================
# Step 2: Build with Xcode
# =============================================================================

if [[ ! -d "$XCODE_WORKSPACE" ]]; then
    print_error "Xcode workspace not found at: $XCODE_WORKSPACE"
    print_info "Run 'pod install' in build/ios-project first"
    exit 1
fi

print_info "Step 2: Building with xcodebuild..."

BUILD_DIR="$REPO_ROOT/build/ios-build"
ARCHIVE_PATH="$BUILD_DIR/CloudXDemo.xcarchive"
APP_PATH="$BUILD_DIR/CloudXDemo.app"

mkdir -p "$BUILD_DIR"

if [[ "$SIMULATOR" == true ]]; then
    # Build for simulator (no signing required)
    xcodebuild \
        -workspace "$XCODE_WORKSPACE" \
        -scheme "$SCHEME" \
        -configuration "$CONFIGURATION" \
        -sdk "$SDK" \
        -destination "$DESTINATION" \
        -derivedDataPath "$BUILD_DIR/DerivedData" \
        ONLY_ACTIVE_ARCH=NO \
        CODE_SIGNING_ALLOWED=NO \
        build \
        | xcbeautify || {
            print_error "xcodebuild failed"
            exit 1
        }

    # Find the simulator app from this build configuration only.
    BUILT_APP=$(find "$BUILD_DIR/DerivedData/Build/Products/$CONFIGURATION-$SDK" -maxdepth 1 -name "*.app" -type d | head -1)
else
    # Build for device (requires signing)
    xcodebuild \
        -workspace "$XCODE_WORKSPACE" \
        -scheme "$SCHEME" \
        -configuration "$CONFIGURATION" \
        -sdk "$SDK" \
        -derivedDataPath "$BUILD_DIR/DerivedData" \
        -allowProvisioningUpdates \
        build \
        | xcbeautify || {
            print_error "xcodebuild failed"
            exit 1
        }

    # Find the device app from this build configuration only.
    BUILT_APP=$(find "$BUILD_DIR/DerivedData/Build/Products/$CONFIGURATION-$SDK" -maxdepth 1 -name "*.app" -type d | head -1)
fi

if [[ -z "$BUILT_APP" || ! -d "$BUILT_APP" ]]; then
    print_error "Built app not found"
    exit 1
fi

BUNDLE_ID=$(resolve_bundle_id "$BUILT_APP")
if [[ -z "$BUNDLE_ID" ]]; then
    print_error "Failed to read CFBundleIdentifier from built app: $BUILT_APP"
    exit 1
fi

# Calculate Xcode build time
XCODE_END_TIME=$(date +%s)
XCODE_TIME=$((XCODE_END_TIME - UNITY_END_TIME))
print_success "Xcode build completed in ${XCODE_TIME}s"
print_info "Resolved bundle ID from built app: $BUNDLE_ID"

# =============================================================================
# Step 3: Install and Launch
# =============================================================================

print_info "Step 3: Installing and launching app..."

if [[ "$SIMULATOR" == true ]]; then
    # Install and launch on simulator
    xcrun simctl install "$SIMULATOR_ID" "$BUILT_APP" || {
        print_error "Failed to install on simulator"
        exit 1
    }

    xcrun simctl launch "$SIMULATOR_ID" "$BUNDLE_ID" || {
        print_warning "Failed to launch app (may need manual start)"
    }
else
    # Install and launch on a connected physical device using Xcode's device tooling.
    #
    # The Identifier column devicectl prints is a CoreDevice UUID (8-4-4-4-12),
    # not the ECID-style UDID Xcode shows, so matching that shape is what
    # separates it from the name, hostname and model columns - all of which can
    # contain spaces, which rules out positional field splitting. "connected"
    # may be the last column on the row, hence the end-of-line alternative.
    DEVICE_ID=""
    DEVICE_COUNT=0
    while IFS= read -r device_line; do
        if [[ "$device_line" == *"iPhone"* || "$device_line" == *"iPad"* ]] &&
            [[ "$device_line" =~ ([[:xdigit:]]{8}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{12})[[:space:]]+connected([[:space:]]|$) ]]; then
            DEVICE_COUNT=$((DEVICE_COUNT + 1))
            if [[ -z "$DEVICE_ID" ]]; then
                DEVICE_ID="${BASH_REMATCH[1]}"
            fi
        fi
    done < <(xcrun devicectl list devices)
    if [[ -z "$DEVICE_ID" ]]; then
        print_error "No connected physical iOS device found"
        exit 1
    fi
    if [[ "$DEVICE_COUNT" -gt 1 ]]; then
        print_warning "$DEVICE_COUNT connected devices; using $DEVICE_ID"
    fi

    xcrun devicectl device install app --device "$DEVICE_ID" "$BUILT_APP" || {
        print_error "Failed to install on device"
        exit 1
    }

    xcrun devicectl device process launch --device "$DEVICE_ID" --terminate-existing "$BUNDLE_ID" || {
        print_error "Failed to launch app on device"
        exit 1
    }
fi

# Calculate total build time
END_TIME=$(date +%s)
TOTAL_TIME=$((END_TIME - START_TIME))
TOTAL_TIME_MIN=$((TOTAL_TIME / 60))
TOTAL_TIME_SEC=$((TOTAL_TIME % 60))

print_success "Build completed successfully!"
print_success "Unity export: ${UNITY_TIME}s | Xcode build: ${XCODE_TIME}s | Total: ${TOTAL_TIME_MIN}m ${TOTAL_TIME_SEC}s"
print_success "App: $BUILT_APP"
print_success "App launched on $(if [[ "$SIMULATOR" == true ]]; then echo "simulator"; else echo "device"; fi)"

# Return to repo root
cd "$REPO_ROOT"

# Play completion sound on macOS
if [[ "$(uname)" == "Darwin" ]]; then
    afplay /System/Library/Sounds/Ping.aiff 2>/dev/null &
fi

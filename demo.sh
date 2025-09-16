#!/bin/bash

# AutoClick Demo Script
# This script demonstrates the basic usage of AutoClick

set -e

echo "AutoClick Demo Script"
echo "===================="

# Navigate to the AutoClick directory
cd "$(dirname "$0")/AutoClick"

echo "1. Building the application..."
dotnet build

echo -e "\n2. Generating sample configuration..."
dotnet run -- generate-config

echo -e "\n3. Creating template directories..."
mkdir -p templates/stages templates/buttons

echo -e "\n4. Testing ADB device detection..."
echo "Available ADB devices:"
adb devices 2>/dev/null || echo "ADB not found or no devices connected"

echo -e "\n5. Testing help command..."
dotnet run -- --help

echo -e "\n6. Testing dry run (no devices required)..."
dotnet run -- run --dry-run --debug || echo "Expected: No devices found"

echo -e "\nDemo completed!"
echo "Next steps:"
echo "1. Connect Android devices via ADB"
echo "2. Add template images to templates/ directory"
echo "3. Configure actions in config.json"
echo "4. Run: dotnet run -- run"

# Clean up demo config
rm -f config.json
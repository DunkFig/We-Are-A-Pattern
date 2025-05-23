#!/bin/bash
sleep 2 

# Change to the directory of this script
cd "$(dirname "$0")"

echo "🌀 Launching Serial Bridge and MIDI Sender..."

# Launch the serial bridge
echo "🔌 Starting Serial Bridge..."
sleep 5 && python3 serial_bridge.py &

# Launch the MIDI sender
echo "🎹 Starting MIDI Sender..."
sleep 5 && midi_udp_server.py &

# Optional: Wait for a moment to ensure background services spin up
sleep 5

# Launch Unity Build if in deployed mode
if [ "$1" = "build" ]; then
    echo "🎮 Launching Unity Build..."
    ./Build/MyUnityApp.x86_64
else
    echo "🧪 Launching in TEST MODE – Unity is expected to run separately."
    echo "If you want to launch the build, run: ./launch_installation.sh build"
fi

# serial_bridge.py
# This script listens for UDP messages from Unity and forwards them to the Arduino via Serial

import serial
import socket
import sys

# === CONFIGURATION ===
UDP_IP = "127.0.0.1"
UDP_PORT = 9000
SERIAL_PORT = "/dev/cu.usbmodem14201"  # <-- Adjust for Linux
BAUD_RATE = 115200

# === SETUP ===
try:
    ser = serial.Serial(SERIAL_PORT, BAUD_RATE)
    print(f"✅ Serial connection opened on {SERIAL_PORT} @ {BAUD_RATE} baud")
except serial.SerialException as e:
    print(f"❌ Failed to open serial port: {e}")
    sys.exit(1)

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))
print(f"📡 Listening for UDP messages on {UDP_IP}:{UDP_PORT}")

# === MAIN LOOP ===
try:
    while True:
        data, addr = sock.recvfrom(1024)
        try:
            decoded = data.decode("utf-8").strip()
            print(f"➡️  Forwarding to serial: {decoded}")
            ser.write((decoded + "\n").encode())
        except Exception as e:
            print(f"⚠️  Error sending to serial: {e}")
except KeyboardInterrupt:
    print("🔌 Shutting down bridge...")
finally:
    ser.close()
    sock.close()

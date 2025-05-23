import socket
import time
import rtmidi
import serial
import sys

# === CONFIGURATION ===
UDP_IP = "127.0.0.1"
UDP_PORT = 9000
SERIAL_PORT = "/dev/cu.usbmodem144101"  # Adjust for your system
BAUD_RATE = 115200

# === MIDI SETUP ===
midiout = rtmidi.MidiOut()
ports = midiout.get_ports()

print("🎛️ Available MIDI Output Ports:")
for i, port in enumerate(ports):
    print(f"{i}: {port}")

if ports:
    midiout.open_port(0)
    print(f"🎹 MIDI output opened on: {ports[0]}")
else:
    midiout.open_virtual_port("UDP MIDI Bridge")
    print("⚠️ No MIDI hardware ports found; using virtual port")

# === SERIAL SETUP ===
try:
    ser = serial.Serial(SERIAL_PORT, BAUD_RATE)
    print(f"✅ Serial connected on {SERIAL_PORT} @ {BAUD_RATE} baud")
except serial.SerialException as e:
    print(f"❌ Failed to open serial port: {e}")
    sys.exit(1)

# === UDP SETUP ===
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))
print(f"📡 Listening on UDP {UDP_IP}:{UDP_PORT}")

# === MAIN LOOP ===
try:
    while True:
        data, addr = sock.recvfrom(1024)
        try:
            decoded = data.decode("utf-8").strip()

            if decoded.startswith("MIDI"):
                _, note, velocity = decoded.split()
                note = int(note)
                velocity = int(velocity)

                midiout.send_message([0x90, note, velocity])
                print(f"🎵 MIDI ON: {note} velocity {velocity}")
                time.sleep(0.1)
                midiout.send_message([0x80, note, 0])

            else:
                print(f"➡️ Serial → {decoded}")
                ser.write((decoded + "\n").encode())

        except Exception as e:
            print(f"⚠️ Error handling message: {e}")

except KeyboardInterrupt:
    print("\n🔌 Exiting gracefully...")

finally:
    ser.close()
    sock.close()
    print("✅ Clean shutdown.")

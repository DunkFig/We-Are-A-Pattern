import socket
import time
import rtmidi

# Setup MIDI out
midiout = rtmidi.MidiOut()
ports = midiout.get_ports()

print("🎛️ Available MIDI Output Ports:")
for i, port in enumerate(ports):
    print(f"{i}: {port}")

# Open first available port
if ports:
    midiout.open_port(0)
else:
    midiout.open_virtual_port("UDP MIDI Bridge")
    print("⚠️ No hardware ports found; using virtual port.")

# Setup UDP
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(('127.0.0.1', 9000))

print("🎹 MIDI UDP Server listening on port 9000...")

while True:
    data, _ = sock.recvfrom(1024)
    try:
        msg = data.decode('utf-8').strip()
        if msg.startswith("MIDI"):
            _, note, velocity = msg.split()
            note = int(note)
            velocity = int(velocity)

            # Send Note On
            midiout.send_message([0x90, note, velocity])
            print(f"🎵 Note ON: {note} Velocity {velocity}")

            # Simple delay before sending Note Off
            time.sleep(0.1)
            midiout.send_message([0x80, note, 0])
    except Exception as e:
        print(f"⚠️ Error: {e}")

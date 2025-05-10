#include <Adafruit_MCP4728.h>
#include <Wire.h>

Adafruit_MCP4728 mcp;
char buffer[64];
int index = 0;

void setup() {
  Serial.begin(115200);
  while (!Serial) { delay(10); }

  if (!mcp.begin()) {
    Serial.println("Failed to find MCP4728 chip");
    while (1) { delay(10); }
  }

  for (int ch = 0; ch < 4; ch++) mcp.setChannelValue((MCP4728_channel_t)ch, 2048);
}

void loop() {
  while (Serial.available()) {
    char c = Serial.read();

    if (c == '\n') {
      buffer[index] = '\0';

      int v[4] = {0, 0, 0, 0};
      int parsed = sscanf(buffer, "%d %d %d %d", &v[0], &v[1], &v[2], &v[3]);
      if (parsed == 4) {
        mcp.setChannelValue(MCP4728_CHANNEL_A, constrain(v[0], 0, 4095));
        mcp.setChannelValue(MCP4728_CHANNEL_B, constrain(v[1], 0, 4095));
        mcp.setChannelValue(MCP4728_CHANNEL_C, constrain(v[2], 0, 4095));
        mcp.setChannelValue(MCP4728_CHANNEL_D, constrain(v[3], 0, 4095));
      }

      index = 0;
    }
    else if (index < sizeof(buffer) - 1) {
      buffer[index++] = c;
    }
  }
}

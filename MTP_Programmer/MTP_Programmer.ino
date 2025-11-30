/**
 * @file MTP_Programmer.ino
 * @author Jucimar A. Cabral
 * @date 2025
 * @brief Arduino Firmware for SLG46824 Multi-Programmer.
 *
 * This firmware acts as a bridge between the C# WPF application and up to 4
 * Renesas SLG46824 chips. It implements a custom serial protocol to perform I2C
 * operations (Ping, Erase, Write, Read) on multiple channels using software
 * I2C.
 *
 * @copyright MIT License
 *
 * Copyright (c) 2025 Jucimar A. Cabral
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#include "SoftI2C.h"

// Pin Definitions
#define VDD_PIN 10 ///< Pin to control VDD for all chips (Power Cycle)

// I2C Channels
SoftI2C ch1(2, 3); ///< Channel 1: SDA=2, SCL=3
SoftI2C ch2(4, 5); ///< Channel 2: SDA=4, SCL=5
SoftI2C ch3(6, 7); ///< Channel 3: SDA=6, SCL=7
SoftI2C ch4(8, 9); ///< Channel 4: SDA=8, SCL=9

SoftI2C *channels[] = {
    NULL, &ch1, &ch2, &ch3,
    &ch4}; ///< Array of pointers to SoftI2C objects (1-indexed)
int addresses[] = {0, 0, 0, 0, 0}; ///< Array to store detected I2C addresses

#define NVM_CONFIG 0x02 ///< Control code lower bits for NVM operation

/**
 * @brief Arduino Setup function.
 * Initializes Serial, VDD pin, and I2C channels.
 */
void setup() {
  Serial.begin(115200);
  pinMode(VDD_PIN, OUTPUT);
  digitalWrite(VDD_PIN, HIGH); // Power ON

  ch1.begin();
  ch2.begin();
  ch3.begin();
  ch4.begin();
}

/**
 * @brief Arduino Loop function.
 * Handles incoming serial commands in a non-blocking way.
 */
void loop() {
  static String inputString = "";
  while (Serial.available()) {
    char inChar = (char)Serial.read();
    if (inChar == '\n') {
      processCommand(inputString);
      inputString = "";
    } else if (inChar != '\r') {
      inputString += inChar;
    }
  }
}

/**
 * @brief Process received serial command.
 * @param cmd The command string.
 */
void processCommand(String cmd) {
  cmd.trim();
  if (cmd.length() == 0)
    return;

  char type = cmd.charAt(0);
  int space1 = cmd.indexOf(' ');
  int chIdx = 0;

  if (space1 > 0) {
    chIdx = cmd.substring(space1 + 1).toInt();
  }

  if (type == 'I') {
    // Identify: I <dummy>
    Serial.println("MTP_PROGRAMMER_V1");
  } else if (type == 'P') {
    // Ping: P <ch>
    SoftI2C *i2c = channels[chIdx];
    bool found = false;
    int foundAddr = 0;

    // Scan common SLG addresses (0-15)
    for (int addr = 0; addr < 16; addr++) {
      i2c->start();
      if (i2c->writeByte((addr << 3) << 1)) { // Control Code: Slave Addr << 3
        i2c->stop();
        found = true;
        foundAddr = addr;
        break;
      }
      i2c->stop();
    }

    if (found) {
      addresses[chIdx] = foundAddr;
      Serial.print("ACK ");
      Serial.println(foundAddr); // Return found address
    } else {
      Serial.println("NACK");
    }
  } else if (type == 'E') {
    // Erase: E <ch> <page_hex>
    int space2 = cmd.indexOf(' ', space1 + 1);
    int page = strtol(cmd.substring(space2 + 1).c_str(), NULL, 16);

    if (erasePage(chIdx, page))
      Serial.println("ACK");
    else
      Serial.println("NACK");
  } else if (type == 'W') {
    // Write: W <ch> <page_hex> <data_hex>
    int space2 = cmd.indexOf(' ', space1 + 1);
    int space3 = cmd.indexOf(' ', space2 + 1);

    int page = strtol(cmd.substring(space2 + 1, space3).c_str(), NULL, 16);
    String dataStr = cmd.substring(space3 + 1);

    uint8_t data[16];
    for (int i = 0; i < 16; i++) {
      char byteStr[3];
      byteStr[0] = dataStr.charAt(i * 2);
      byteStr[1] = dataStr.charAt(i * 2 + 1);
      byteStr[2] = 0;
      data[i] = strtol(byteStr, NULL, 16);
    }

    if (writePage(chIdx, page, data))
      Serial.println("ACK");
    else
      Serial.println("NACK");
  } else if (type == 'R') {
    // Read: R <ch> <page_hex>
    int space2 = cmd.indexOf(' ', space1 + 1);
    int page = strtol(cmd.substring(space2 + 1).c_str(), NULL, 16);

    readPage(chIdx, page);
  } else if (type == 'X') {
    // Reset: X
    digitalWrite(VDD_PIN, LOW);
    delay(500);
    digitalWrite(VDD_PIN, HIGH);
    Serial.println("ACK");
  }
}

/**
 * @brief Poll for ACK from the device to check if write/erase is complete.
 * @param chIdx Channel index.
 * @return true if ACK received within timeout, false otherwise.
 */
bool ackPolling(int chIdx) {
  SoftI2C *i2c = channels[chIdx];
  int addr = addresses[chIdx];
  int control_code = (addr << 3) | NVM_CONFIG;

  unsigned long start = millis();
  while (millis() - start < 500) { // 500ms timeout
    i2c->start();
    if (i2c->writeByte(control_code << 1)) {
      i2c->stop();
      return true; // ACK received
    }
    i2c->stop();
    delay(1);
  }
  return false; // Timeout
}

/**
 * @brief Erase a page of NVM.
 * @param chIdx Channel index.
 * @param page Page address (0-15).
 * @return true if successful, false otherwise.
 */
bool erasePage(int chIdx, int page) {
  SoftI2C *i2c = channels[chIdx];
  int addr = addresses[chIdx];
  int control_code = (addr << 3) | NVM_CONFIG;

  i2c->start();
  if (!i2c->writeByte(control_code << 1)) {
    i2c->stop();
    return false;
  }
  if (!i2c->writeByte(0xE3)) {
    i2c->stop();
    return false;
  } // Erase OpCode
  if (!i2c->writeByte(0x80 | page)) {
    i2c->stop();
    return false;
  } // 0x80 for NVM
  i2c->stop();

  return ackPolling(chIdx);
}

/**
 * @brief Write a page of NVM.
 * @param chIdx Channel index.
 * @param page Page address (0-15).
 * @param data Pointer to 16 bytes of data.
 * @return true if successful, false otherwise.
 */
bool writePage(int chIdx, int page, uint8_t *data) {
  SoftI2C *i2c = channels[chIdx];
  int addr = addresses[chIdx];
  int control_code = (addr << 3) | NVM_CONFIG;

  i2c->start();
  if (!i2c->writeByte(control_code << 1)) {
    i2c->stop();
    return false;
  }
  if (!i2c->writeByte(page << 4)) {
    i2c->stop();
    return false;
  } // Page Address

  for (int i = 0; i < 16; i++) {
    if (!i2c->writeByte(data[i])) {
      i2c->stop();
      return false;
    }
  }
  i2c->stop();

  return ackPolling(chIdx);
}

/**
 * @brief Read a page of NVM.
 * @param chIdx Channel index.
 * @param page Page address (0-15).
 */
void readPage(int chIdx, int page) {
  SoftI2C *i2c = channels[chIdx];
  int addr = addresses[chIdx];
  int control_code = (addr << 3) | NVM_CONFIG;

  // Set Pointer
  i2c->start();
  if (!i2c->writeByte(control_code << 1)) {
    i2c->stop();
    Serial.println("ERR_CONN");
    return;
  }
  if (!i2c->writeByte(page << 4)) {
    i2c->stop();
    Serial.println("ERR_ADDR");
    return;
  }
  i2c->stop();

  // Read
  i2c->start();
  if (!i2c->writeByte((control_code << 1) | 1)) {
    i2c->stop();
    Serial.println("ERR_READ_START");
    return;
  }

  for (int i = 0; i < 16; i++) {
    uint8_t b = i2c->readByte(i < 15); // ACK for all except last
    if (b < 0x10)
      Serial.print("0");
    Serial.print(b, HEX);
  }
  i2c->stop();
  Serial.println();
}

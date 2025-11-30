/**
 * @file SoftI2C.h
 * @author Jucimar A. Cabral
 * @date 2025
 * @brief Software I2C Library for Arduino.
 *
 * This library implements a software-based I2C (Bit-Banging) interface,
 * allowing the creation of multiple independent I2C channels on any digital
 * pins.
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

#ifndef SoftI2C_h
#define SoftI2C_h

#include <Arduino.h>

/**
 * @class SoftI2C
 * @brief Class for Software I2C communication.
 */
class SoftI2C {
private:
  uint8_t _sdaPin; ///< Pin number for SDA
  uint8_t _sclPin; ///< Pin number for SCL

public:
  /**
   * @brief Constructor for SoftI2C.
   * @param sdaPin Pin number for SDA.
   * @param sclPin Pin number for SCL.
   */
  SoftI2C(uint8_t sdaPin, uint8_t sclPin) {
    _sdaPin = sdaPin;
    _sclPin = sclPin;
  }

  /**
   * @brief Initialize the I2C pins.
   */
  void begin() {
    pinMode(_sdaPin, OUTPUT);
    pinMode(_sclPin, OUTPUT);
    digitalWrite(_sdaPin, HIGH);
    digitalWrite(_sclPin, HIGH);
  }

  /**
   * @brief Generate I2C Start condition.
   */
  void start() {
    digitalWrite(_sdaPin, HIGH);
    digitalWrite(_sclPin, HIGH);
    delayMicroseconds(5);
    digitalWrite(_sdaPin, LOW);
    delayMicroseconds(5);
    digitalWrite(_sclPin, LOW);
  }

  /**
   * @brief Generate I2C Stop condition.
   */
  void stop() {
    digitalWrite(_sdaPin, LOW);
    digitalWrite(_sclPin, HIGH);
    delayMicroseconds(5);
    digitalWrite(_sdaPin, HIGH);
    delayMicroseconds(5);
  }

  /**
   * @brief Write a byte to the I2C bus.
   * @param data The byte to write.
   * @return true if ACK received, false otherwise.
   */
  bool writeByte(uint8_t data) {
    for (uint8_t i = 0; i < 8; i++) {
      if (data & 0x80)
        digitalWrite(_sdaPin, HIGH);
      else
        digitalWrite(_sdaPin, LOW);
      data <<= 1;
      delayMicroseconds(5);
      digitalWrite(_sclPin, HIGH);
      delayMicroseconds(5);
      digitalWrite(_sclPin, LOW);
    }

    // Read ACK
    pinMode(_sdaPin, INPUT);
    digitalWrite(_sclPin, HIGH);
    delayMicroseconds(5);
    bool ack = !digitalRead(_sdaPin);
    digitalWrite(_sclPin, LOW);
    pinMode(_sdaPin, OUTPUT);
    return ack;
  }

  /**
   * @brief Read a byte from the I2C bus.
   * @param ack true to send ACK, false to send NACK (for last byte).
   * @return The byte read.
   */
  uint8_t readByte(bool ack) {
    uint8_t data = 0;
    pinMode(_sdaPin, INPUT);
    for (uint8_t i = 0; i < 8; i++) {
      data <<= 1;
      digitalWrite(_sclPin, HIGH);
      delayMicroseconds(5);
      if (digitalRead(_sdaPin))
        data |= 1;
      digitalWrite(_sclPin, LOW);
      delayMicroseconds(5);
    }
    pinMode(_sdaPin, OUTPUT);

    // Send ACK/NACK
    if (ack)
      digitalWrite(_sdaPin, LOW);
    else
      digitalWrite(_sdaPin, HIGH);

    delayMicroseconds(5);
    digitalWrite(_sclPin, HIGH);
    delayMicroseconds(5);
    digitalWrite(_sclPin, LOW);
    digitalWrite(_sdaPin, HIGH); // Release SDA

    return data;
  }
};

#endif

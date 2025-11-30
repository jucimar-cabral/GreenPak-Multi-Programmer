/**
 * @file ArduinoInterface.cs
 * @author Jucimar A. Cabral
 * @date 2025
 * @brief Handles serial communication with the Arduino programmer.
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
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MTP_Programmer_App
{
    /**
     * @class ArduinoInterface
     * @brief Manages the serial connection and command protocol with the Arduino.
     */
    public class ArduinoInterface
    {
        private SerialPort _serialPort;

        /**
         * @brief Checks if the serial port is connected and open.
         */
        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        /**
         * @brief Connects to the Arduino on the specified port.
         * @param portName The COM port name (e.g., "COM3").
         * @param baudRate The baud rate (default 115200).
         * @throws Exception If connection fails or handshake fails.
         */
        public void Connect(string portName, int baudRate = 115200)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();

            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.ReadTimeout = 3000;
            _serialPort.WriteTimeout = 3000;
            _serialPort.DtrEnable = true; // Enable DTR
            _serialPort.RtsEnable = true; // Enable RTS
            _serialPort.Open();
            
            // Wait for Arduino reset
            Thread.Sleep(2500);

            // Handshake with retries
            int retries = 3;
            while (retries > 0)
            {
                try 
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write("I 1\n"); // Explicit newline
                    
                    string response = null;
                    try {
                        response = _serialPort.ReadLine();
                        while (string.IsNullOrWhiteSpace(response))
                        {
                            response = _serialPort.ReadLine();
                        }
                    } catch (TimeoutException) {
                        // Ignore timeout on retry
                    }

                    if (response != null && response.Trim() == "MTP_PROGRAMMER_V1")
                    {
                        return; // Success
                    }
                }
                catch (Exception)
                {
                    // Ignore errors during retry
                }
                
                retries--;
                if (retries > 0) Thread.Sleep(500);
            }

            _serialPort.Close();
            throw new Exception($"Device on {portName} did not respond with correct ID after 3 attempts.");
        }

        /**
         * @brief Disconnects the serial port.
         */
        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
        }

        /**
         * @brief Sends a command to the Arduino and waits for a response.
         * @param command The command string.
         * @return The response string.
         * @throws Exception If timeout occurs.
         */
        public string SendCommand(string command)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            _serialPort.DiscardInBuffer();
            _serialPort.Write(command + "\n"); // Explicit newline

            try
            {
                // Read response until we get a meaningful line or timeout
                // The Arduino might echo, so we might need to filter
                string response = _serialPort.ReadLine();
                while (string.IsNullOrWhiteSpace(response))
                {
                    response = _serialPort.ReadLine();
                }
                return response.Trim();
            }
            catch (TimeoutException)
            {
                throw new Exception("Timeout waiting for response");
            }
        }

        /**
         * @brief Pings the SLG46824 on the specified channel.
         * @param channel The channel number (1-4).
         * @return true if the device responds with ACK, false otherwise.
         */
        public bool Ping(int channel)
        {
            string resp = SendCommand($"P {channel}");
            return resp.StartsWith("ACK");
        }

        /**
         * @brief Erases a page of NVM on the specified channel.
         * @param channel The channel number (1-4).
         * @param page The page address (0-15).
         * @return true if successful, false otherwise.
         */
        public bool ErasePage(int channel, int page)
        {
            string resp = SendCommand($"E {channel} {page:X}");
            return resp == "ACK";
        }

        /**
         * @brief Writes a page of NVM on the specified channel.
         * @param channel The channel number (1-4).
         * @param page The page address (0-15).
         * @param data The 16-byte data array.
         * @return true if successful, false otherwise.
         */
        public bool WritePage(int channel, int page, byte[] data)
        {
            if (data.Length != 16) throw new ArgumentException("Page data must be 16 bytes");
            string hexData = BitConverter.ToString(data).Replace("-", "");
            string resp = SendCommand($"W {channel} {page:X} {hexData}");
            return resp == "ACK";
        }

        /**
         * @brief Reads a page of NVM from the specified channel.
         * @param channel The channel number (1-4).
         * @param page The page address (0-15).
         * @return The 16-byte data array read from the device.
         * @throws Exception If read fails.
         */
        public byte[] ReadPage(int channel, int page)
        {
            string resp = SendCommand($"R {channel} {page:X}");
            // Response should be hex string
            if (resp.StartsWith("ERR")) throw new Exception($"Read Error: {resp}");
            
            // Parse hex string
            return StringToByteArray(resp);
        }

        /**
         * @brief Resets (Power Cycles) the target devices via the Arduino.
         */
        public void Reset()
        {
            if (!IsConnected) return;
            SendCommand("X");
            Thread.Sleep(500); // Wait for power cycle
        }

        /**
         * @brief Converts a hex string to a byte array.
         * @param hex The hex string.
         * @return The byte array.
         */
        private static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}

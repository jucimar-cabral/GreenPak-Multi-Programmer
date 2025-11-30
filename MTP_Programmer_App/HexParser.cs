/**
 * @file HexParser.cs
 * @author Jucimar A. Cabral
 * @date 2025
 * @brief Parser for Intel Hex files.
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
using System.IO;
using System.Collections.Generic;

namespace MTP_Programmer_App
{
    /**
     * @class HexParser
     * @brief Static class for parsing Intel Hex files.
     */
    public static class HexParser
    {
        /**
         * @brief Parses an Intel Hex file and extracts the NVM data (256 bytes).
         * @param filePath The path to the .hex file.
         * @return A byte array containing the 256 bytes of NVM data.
         * @throws Exception If parsing fails or data is invalid.
         */
        public static byte[] Parse(string filePath)
        {
            byte[] buffer = new byte[256];
            for (int i = 0; i < 256; i++) buffer[i] = 0xFF; // Fill with 0xFF

            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                if (!line.StartsWith(":")) continue;

                // Parse line
                int byteCount = Convert.ToInt32(line.Substring(1, 2), 16);
                int address = Convert.ToInt32(line.Substring(3, 4), 16);
                int recordType = Convert.ToInt32(line.Substring(7, 2), 16);

                if (recordType == 0) // Data Record
                {
                    string dataPart = line.Substring(9, byteCount * 2);
                    for (int i = 0; i < byteCount; i++)
                    {
                        if (address + i < 256)
                        {
                            buffer[address + i] = Convert.ToByte(dataPart.Substring(i * 2, 2), 16);
                        }
                    }
                }
            }
            return buffer;
        }
    }
}

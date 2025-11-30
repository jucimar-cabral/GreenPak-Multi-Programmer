# SLG46824 Multi-Programmer

## Overview
This system allows programming up to 4 Renesas SLG46824/6 chips simultaneously using an Arduino Uno R4 Minima as a bridge.

## Hardware Setup
Connect the SLG46824/6 chips to the Arduino Uno R4 Minima as follows:

| Channel | SDA Pin | SCL Pin |
| :--- | :--- | :--- |
| **Channel 1** | 2 | 3 |
| **Channel 2** | 4 | 5 |
| **Channel 3** | 6 | 7 |
| **Channel 4** | 8 | 9 |

**Common Connections:**
-   **VDD Control**: Pin 10 (Connect to VDD of all chips to allow power cycling).
-   **GND**: Connect common Ground.

## Software Setup

### 1. Arduino Firmware
1.  Open `MTP_Programmer.ino` in Arduino IDE.
2.  Ensure `SoftI2C.h` is in the same folder.
3.  Select Board: **Arduino Uno R4 Minima**.
4.  Upload the sketch.

### 2. C# Application
1.  Open `MTP_Programmer.sln` in Visual Studio.
2.  Build and Run `MTP_Programmer_App`.

## Usage Guide

1.  **Connect**:
    -   Select the Arduino COM port from the dropdown.
    -   Click **Connect**.
2.  **Load Firmware**:
    -   You can now select a different `.hex` file for each channel.
    -   Click the **...** button next to each channel to load its firmware.
    -   The checkbox enables/disables the channel.
3.  **Program**:
    -   Click **Write NVM**.
    -   The app will program each enabled channel with its specific firmware.
    -   **Note**: Verification for Page 0F (Address 0x0F0-0x0FF) is relaxed. It will only check the last byte (0xA5) to avoid false failures due to reserved bits.
5.  **Verify**:
    -   Click **Verify**.
    -   The app will read back the NVM from each chip and compare it with the loaded file.
    -   Success or Failure will be reported in the log.

## Troubleshooting
### Connection Issues
-   **Handshake**: The app sends `I 1\n` and expects `MTP_Programmer_V1`. If this fails, check the COM port and baud rate (115200).
-   **Reset**: The app now automatically power cycles the target chips (via Pin 10) after programming to ensure the new NVM data is loaded.
-   **Verification Failures**:
    -   If verification fails, ensure the connections are short and stable.
    -   The app uses ACK polling to ensure data is written correctly.
    -   Page 0F verification is relaxed (only checks last byte).
-   **Verify Failed**: Check for noise on I2C lines. Reduce wire length.

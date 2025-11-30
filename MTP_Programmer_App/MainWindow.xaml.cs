/**
 * @file MainWindow.xaml.cs
 * @author Jucimar A. Cabral
 * @date 2025
 * @brief Main Window logic for the MTP Programmer Application.
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

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MTP_Programmer_App
{
    /**
     * @class MainWindow
     * @brief Interaction logic for MainWindow.xaml.
     */
    public partial class MainWindow : Window
    {
        private ArduinoInterface _arduino = new ArduinoInterface();
        private Dictionary<int, byte[]> _channelFirmware = new Dictionary<int, byte[]>();

        /**
         * @brief Constructor. Initializes components and refreshes COM ports.
         */
        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();
        }

        /**
         * @brief Refreshes the list of available COM ports.
         */
        private void RefreshPorts()
        {
            cbPorts.Items.Clear();
            foreach (var port in SerialPort.GetPortNames())
            {
                cbPorts.Items.Add(port);
            }
            if (cbPorts.Items.Count > 0) cbPorts.SelectedIndex = 0;
        }

        /**
         * @brief Event handler for Refresh button click.
         */
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        /**
         * @brief Event handler for Connect/Disconnect button click.
         */
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_arduino.IsConnected)
                {
                    _arduino.Disconnect();
                    btnConnect.Content = "Connect";
                    btnProgram.IsEnabled = false;
                    btnVerify.IsEnabled = false;
                    Log("Disconnected.");
                }
                else
                {
                    if (cbPorts.SelectedItem == null) return;
                    _arduino.Connect(cbPorts.SelectedItem.ToString());
                    btnConnect.Content = "Disconnect";
                    UpdateButtons();
                    Log($"Connected to {cbPorts.SelectedItem}.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /**
         * @brief Updates the enabled state of Program and Verify buttons.
         */
        private void UpdateButtons()
        {
            bool anyFileLoaded = _channelFirmware.Count > 0;
            bool connected = _arduino.IsConnected;
            btnProgram.IsEnabled = connected && anyFileLoaded;
            btnVerify.IsEnabled = connected && anyFileLoaded;
        }

        /**
         * @brief Loads a hex file for a specific channel.
         * @param channel The channel number (1-4).
         * @param txtBox The TextBox to display the file path.
         */
        private void LoadFileForChannel(int channel, TextBox txtBox)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Hex files (*.hex)|*.hex|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                txtBox.Text = openFileDialog.FileName;
                try
                {
                    byte[] data = HexParser.Parse(openFileDialog.FileName);
                    if (_channelFirmware.ContainsKey(channel))
                        _channelFirmware[channel] = data;
                    else
                        _channelFirmware.Add(channel, data);
                        
                    Log($"Loaded firmware for Ch {channel}: {data.Length} bytes.");
                    UpdateButtons();
                }
                catch (Exception ex)
                {
                    Log($"Error parsing hex for Ch {channel}: {ex.Message}");
                    if (_channelFirmware.ContainsKey(channel)) _channelFirmware.Remove(channel);
                }
            }
        }

        private void btnBrowseCh1_Click(object sender, RoutedEventArgs e) => LoadFileForChannel(1, txtFileCh1);
        private void btnBrowseCh2_Click(object sender, RoutedEventArgs e) => LoadFileForChannel(2, txtFileCh2);
        private void btnBrowseCh3_Click(object sender, RoutedEventArgs e) => LoadFileForChannel(3, txtFileCh3);
        private void btnBrowseCh4_Click(object sender, RoutedEventArgs e) => LoadFileForChannel(4, txtFileCh4);

        /**
         * @brief Event handler for Program button click.
         * Programs selected channels with loaded firmware.
         */
        private async void btnProgram_Click(object sender, RoutedEventArgs e)
        {
            if (!_arduino.IsConnected) return;

            btnProgram.IsEnabled = false;
            btnVerify.IsEnabled = false;
            Log("Starting programming...");

            try
            {
                await Task.Run(() =>
                {
                    int[] channels = GetSelectedChannels();
                    foreach (var ch in channels)
                    {
                        if (!_channelFirmware.ContainsKey(ch))
                        {
                            LogAsync($"Skipping Channel {ch}: No firmware loaded.");
                            continue;
                        }

                        LogAsync($"Programming Channel {ch}...");
                        
                        // 1. Ping
                        if (!_arduino.Ping(ch))
                        {
                            LogAsync($"Channel {ch} not responding. Skipping.");
                            continue;
                        }

                        // 2. Erase & Write Pages
                        byte[] firmware = _channelFirmware[ch];
                        for (int page = 0; page < 16; page++)
                        {
                            LogAsync($"Ch {ch}: Erasing Page {page}...");
                            if (!_arduino.ErasePage(ch, page)) throw new Exception($"Failed to erase Ch {ch} Page {page}");
                            
                            LogAsync($"Ch {ch}: Writing Page {page}...");
                            byte[] pageData = new byte[16];
                            Array.Copy(firmware, page * 16, pageData, 0, 16);
                            
                            if (!_arduino.WritePage(ch, page, pageData)) throw new Exception($"Failed to write Ch {ch} Page {page}");
                        }
                        LogAsync($"Channel {ch} Programmed Successfully.");
                    }

                    LogAsync("Power Cycling Targets...");
                    _arduino.Reset();
                    Thread.Sleep(500); // Extra wait
                });
                Log("Programming Complete.");
            }
            catch (Exception ex)
            {
                Log($"Programming Error: {ex.Message}");
            }
            finally
            {
                UpdateButtons();
            }
        }

        /**
         * @brief Event handler for Verify button click.
         * Verifies programmed data against loaded firmware.
         */
        private async void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            if (!_arduino.IsConnected) return;

            btnProgram.IsEnabled = false;
            btnVerify.IsEnabled = false;
            Log("Starting verification...");

            try
            {
                await Task.Run(() =>
                {
                    int[] channels = GetSelectedChannels();
                    foreach (var ch in channels)
                    {
                        if (!_channelFirmware.ContainsKey(ch))
                        {
                            LogAsync($"Skipping Channel {ch}: No firmware loaded.");
                            continue;
                        }

                        LogAsync($"Verifying Channel {ch}...");
                        byte[] firmware = _channelFirmware[ch];
                        
                        for (int page = 0; page < 16; page++)
                        {
                            byte[] readData = _arduino.ReadPage(ch, page);
                            byte[] expectedData = new byte[16];
                            Array.Copy(firmware, page * 16, expectedData, 0, 16);

                            if (!readData.SequenceEqual(expectedData))
                            {
                                if (page == 15)
                                {
                                    // Special handling for Page 0F
                                    // Only check the last byte (0xA5)
                                    if (readData[15] == expectedData[15])
                                    {
                                        LogAsync($"Warning: Ch {ch} Page {page} mismatch ignored (reserved bits). Last byte matched.");
                                        continue;
                                    }
                                }
                                throw new Exception($"Verification Failed Ch {ch} Page {page}\nExpected: {BitConverter.ToString(expectedData)}\nRead:     {BitConverter.ToString(readData)}");
                            }
                        }
                        LogAsync($"Channel {ch} Verified Successfully.");
                    }
                });
                Log("Verification Complete.");
            }
            catch (Exception ex)
            {
                Log($"Verification Error: {ex.Message}");
            }
            finally
            {
                UpdateButtons();
            }
        }

        /**
         * @brief Gets the list of selected channels from the UI checkboxes.
         * @return Array of selected channel numbers.
         */
        private int[] GetSelectedChannels()
        {
            var list = new System.Collections.Generic.List<int>();
            Dispatcher.Invoke(() =>
            {
                if (chkCh1.IsChecked == true) list.Add(1);
                if (chkCh2.IsChecked == true) list.Add(2);
                if (chkCh3.IsChecked == true) list.Add(3);
                if (chkCh4.IsChecked == true) list.Add(4);
            });
            return list.ToArray();
        }

        /**
         * @brief Logs a message to the UI log text box.
         * @param message The message to log.
         */
        private void Log(string message)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss}: {message}\n");
            txtLog.ScrollToEnd();
        }

        /**
         * @brief Logs a message asynchronously from a background thread.
         * @param message The message to log.
         */
        private void LogAsync(string message)
        {
            Dispatcher.Invoke(() => Log(message));
        }
    }
}
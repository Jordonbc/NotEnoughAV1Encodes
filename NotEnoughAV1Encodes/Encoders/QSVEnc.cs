﻿using System.IO;
using System.Windows;

namespace NotEnoughAV1Encodes.Encoders
{
    class QSVEnc : IEncoder
    {
        public string GetCommand()
        {
            // Get MainWindow instance to access UI elements
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;

            string settings = "-f yuv4mpegpipe - | " +
                    "\"" + Path.Combine(Directory.GetCurrentDirectory(), "Apps", "qsvenc", "QSVEncC64.exe") + "\" --y4m -i -";

            // Codec
            settings += " --codec av1";

            // Quality / Bitrate Selection
            string quality = mainWindow.ComboBoxQualityModeQSVAV1.SelectedIndex switch
            {
                0 => " --cqp " + mainWindow.SliderQualityQSVAV1.Value,
                1 => " --icq " + mainWindow.SliderQualityQSVAV1.Value,
                2 => " --vbr " + mainWindow.TextBoxBitrateQSVAV1.Text,
                3 => " --cbr " + mainWindow.TextBoxBitrateQSVAV1.Text,
                _ => ""
            };

            // Preset
            settings += quality + " --quality " + mainWindow.GenerateQuickSyncEncoderSpeed();

            // Bit-Depth
            settings += " --output-depth ";
            settings += mainWindow.ComboBoxVideoBitDepthLimited.SelectedIndex switch
            {
                0 => "8",
                1 => "10",
                _ => "8"
            };

            // Output Colorspace
            settings += " --output-csp ";
            settings += mainWindow.ComboBoxColorFormat.SelectedIndex switch
            {
                0 => "i420",
                1 => "i422",
                2 => "i444",
                _ => "i420"
            };

            return settings;
        }
    }
}

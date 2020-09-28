﻿using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;

namespace NotEnoughAV1Encodes
{
    class SmallFunctions
    {
        private static ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();

        public static void WriteToFileThreadSafe(string text, string path)
        {
            //Some smaller Blackmagic, so parallel Workers won't deadlock files
            // Set Status to Locked
            _readWriteLock.EnterWriteLock();
            try
            {
                // Append text to the file
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(text);
                    sw.Close();
                }
            }
            finally
            {
                // Release lock
                _readWriteLock.ExitWriteLock();
            }
        }

        public static class Cancel
        {
            //Public Cancel boolean
            public static bool CancelAll = false;
        }

        public static void KillInstances()
        {
            //Kills all aomenc and ffmpeg instances
            try
            {
                foreach (var process in Process.GetProcessesByName("aomenc")) { process.Kill(); }
                foreach (var process in Process.GetProcessesByName("rav1e")) { process.Kill(); }
                foreach (var process in Process.GetProcessesByName("SvtAv1EncApp")) { process.Kill(); }
                foreach (var process in Process.GetProcessesByName("ffmpeg")) { process.Kill(); }
            }
            catch { }
        }

        public static void PlayFinishedSound()
        {
            SoundPlayer playSound = new SoundPlayer(Properties.Resources.finished);
            playSound.Play();
        }

        public static void Logging(string log)
        {
            if (MainWindow.logging)
            {
                DateTime starttime = DateTime.Now;
                checkCreateFolder(Path.Combine(Directory.GetCurrentDirectory(), "Logging"));
                WriteToFileThreadSafe(starttime.ToString() + " : " + log, Path.Combine(Directory.GetCurrentDirectory(), "Logging", "program.log"));
            }
        }

        public static void ExecuteFfmpegTask(string ffmpegCommand)
        {
            //Run ffmpeg command
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                WorkingDirectory = MainWindow.ffmpegPath,
                Arguments = ffmpegCommand
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        //--------------------------- Video Information ---------------------------

        public static void CountVideoChunks()
        {
            MainWindow.videoChunks = Directory.GetFiles(Path.Combine(MainWindow.tempPath, "Chunks"), "*mkv", SearchOption.AllDirectories).Select(x => Path.GetFileName(x)).ToArray();
            MainWindow.videoChunksCount = MainWindow.videoChunks.Count();
            //Removes all chunks from chunklist which are in encoded.log
            if (MainWindow.resumeMode == true)
            {
                bool fileExist = File.Exists(Path.Combine(MainWindow.tempPath, "encoded.log"));
                if (fileExist)
                {
                    foreach (string line in File.ReadLines(Path.Combine(MainWindow.tempPath, "encoded.log")))
                    {
                        MainWindow.videoChunks = MainWindow.videoChunks.Where(s => s != line).ToArray();
                    }
                    MainWindow.videoChunksCount = MainWindow.videoChunks.Count();
                }
            }
        }

        public static void GetChunksFrameCount(string PathTemp)
        {
            int frameCount = 0;
            foreach(var files in Directory.GetFiles(Path.Combine(MainWindow.tempPath, "Chunks"), "*mkv", SearchOption.AllDirectories))
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "cmd.exe",
                        WorkingDirectory = MainWindow.ffmpegPath,
                        Arguments = "/C ffmpeg.exe -i " + '\u0022' + files + '\u0022' + " -hide_banner -loglevel 32 -map 0:v:0 -c copy -f null -",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();
                string stream = process.StandardError.ReadToEnd();
                process.WaitForExit();
                string tempStream = stream.Substring(stream.LastIndexOf("frame="));
                string data = getBetween(tempStream, "frame=", "fps=");
                frameCount += int.Parse(data);
            }
            MainWindow.frameCountChunks = frameCount;
            Logging("Total Frame Count Chunks: " + frameCount);
        }

        public static void GetSourceFrameCount(string videoInput)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    WorkingDirectory = MainWindow.ffmpegPath,
                    Arguments = "/C ffmpeg.exe -i " + '\u0022' + videoInput + '\u0022' + " -hide_banner -loglevel 32 -map 0:v:0 -c copy -f null -",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
            process.Start();
            string stream = process.StandardError.ReadToEnd();
            process.WaitForExit();
            string tempStream = stream.Substring(stream.LastIndexOf("frame="));
            string data = getBetween(tempStream, "frame=", "fps=");
            MainWindow.frameCountSource = int.Parse(data);
            Logging("Total Frame Count Source: " + data);
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                int Start, End;
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }

            return "";
        }

        //-------------------------------------------------------------------------
        //--------------------------------- Checks --------------------------------

        public static void checkCreateFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        public static bool CheckVideoOutput()
        {
            if (File.Exists(MainWindow.videoOutput)) { File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "UnfinishedJobs", MainWindow.fileName + ".xml")); return true; } else { MessageBox.Show("No Output File found!"); return false; }
        }

        public static bool CheckAudioOutput()
        {
            if (MainWindow.audioEncoding)
            {
                if (File.Exists(Path.Combine(MainWindow.tempPath, "AudioEncoded", "audio.mkv")))
                { return true; }
                else { return false; }
            }
            else { return true; }
        }

        public static bool CheckSubtitleOutput()
        {
            if (MainWindow.subtitleEncoding && MainWindow.subtitleHardcoding == false)
            {
                if (File.Exists(Path.Combine(MainWindow.tempPath, "Subtitles", "subtitle.mkv")))
                { return true; }
                else { return false; }
            }
            else { return true; }
        }

        public static bool CheckFileFolder()
        {
            try
            {
                if (!Directory.EnumerateFiles(Path.Combine(MainWindow.tempPath, "Chunks")).Any())
                { return true; }
                else { return false; }
            }
            catch { return true; }

        }

        public static void checkDependeciesStartup()
        {
            bool ffmpegExists, ffprobeExists, mkvmergeExists;
            ffmpegExists = File.Exists(MainWindow.ffmpegPath + "\\ffmpeg.exe");
            ffprobeExists = File.Exists(MainWindow.ffprobePath + "\\ffprobe.exe");
            mkvmergeExists = File.Exists(MainWindow.mkvToolNixPath + "\\mkvmerge.exe");
            if (ffmpegExists == false || ffprobeExists == false)
            {
                if (MessageBox.Show("Could not find ffmpeg or ffprobe! \n\nOpen dependency installer? \n\nFor manual installation, place ffmpeg and ffprobe in: \n" + Directory.GetCurrentDirectory() + "\\Apps\\ffmpeg\\ \n\nEncoders can be placed in \\Apps\\Encoder\\. \n\nAlternatively all dependencies can be placed in the root directory next to the exe.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (MainWindow.found7z)
                    {
                        DownloadDependencies egg = new DownloadDependencies(false);
                        egg.ShowDialog();
                        MainWindow.setEncoderPath();
                    }
                    else { MessageBoxes.Message7zNotFound(); }
                }
            }
            if (mkvmergeExists != true)
            {
                if (MessageBox.Show("Could not find mkvmerge!\n\nIt should have been included with NEAV1E.\n\nOpen mkvtoolnix download site?\n\nYou have three options now:\n1. Download and Install MKVToolNix (it's free!)\n2. Download the portable MKVToolNix version and place mkvmerge.exe in the root directory next to the exe, or under /Apps/mkvtoolnix/ \n3. Place the mkvmerge.exe in the PATH environment.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Process.Start("https://www.fosshub.com/MKVToolNix.html");
                }
            }
        }

        public static bool checkDependencies(string encoder)
        {
            bool av1encoderexists = false, ffmpegExists, ffprobeExists, mkvmergeExists;
            ffmpegExists = File.Exists(MainWindow.ffmpegPath + "\\ffmpeg.exe");
            ffprobeExists = File.Exists(MainWindow.ffprobePath + "\\ffprobe.exe");
            mkvmergeExists = File.Exists(MainWindow.mkvToolNixPath + "\\mkvmerge.exe");
            switch (encoder)
            {
                case "aomenc": av1encoderexists = File.Exists(MainWindow.aomencPath + "\\aomenc.exe"); break;
                case "rav1e": av1encoderexists = File.Exists(MainWindow.rav1ePath + "\\rav1e.exe"); break;
                case "svt-av1": av1encoderexists = File.Exists(MainWindow.svtav1Path + "\\SvtAv1EncApp.exe"); break;
                case "libaom": av1encoderexists = ffmpegExists; break;
                case "libvpx-vp9": av1encoderexists = ffmpegExists; break;
                default: break;
            }
            if (ffmpegExists && ffprobeExists && av1encoderexists) { return true; }
            else
            {
                MessageBox.Show("Could not find all dependencies: \nffmpeg found: " + ffmpegExists + " \nffprobe found: " + ffprobeExists + " \nmkvmerge found: " + mkvmergeExists + " \n" + encoder + " found: " + av1encoderexists, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
        }

        //-------------------------------------------------------------------------
        //----------------------------------- IO ----------------------------------

        public static void DeleteChunkFolderContent()
        {
            try
            {
                //Deletes all Files in & above Chunk Folder
                DirectoryInfo tmp = new DirectoryInfo(Path.Combine(MainWindow.tempPath, "Chunks"));
                DirectoryInfo tmp2 = new DirectoryInfo(MainWindow.tempPath);
                foreach (FileInfo file in tmp.GetFiles()) { file.Delete(); }
                foreach (FileInfo file in tmp2.GetFiles()) { file.Delete(); }
                if (Directory.Exists(Path.Combine(MainWindow.tempPath, "Subitles")))
                    Directory.Delete(Path.Combine(MainWindow.tempPath, "Subitles"), true);
                if (Directory.Exists(Path.Combine(MainWindow.tempPath, "AudioEncoded")))
                    Directory.Delete(Path.Combine(MainWindow.tempPath, "AudioEncoded"), true);
            }
            catch { }

        }
        //I don't know why I have two delete temp functions... For now I won't touch
        public static void DeleteTempFiles()
        {
            try
            {
                DirectoryInfo tmp = new DirectoryInfo(MainWindow.tempPath);
                tmp.Delete(true);
            }
            catch (IOException ex) { MessageBox.Show("Could not delete all files: " + ex.Message); }
        }

        public static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        public static string GetFullPathWithOutName(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return path;
            }
            return null;
        }

        public static void DeleteLogFile()
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Logging", "program.log")))
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "Logging", "program.log"));
        }

        public static void Check7zExtractor()
        {
            if (File.Exists(@"C:\Program Files\7-Zip\7zG.exe")) { MainWindow.found7z = true; }
        }

        public static string getFilename(string videoInput)
        {
            //Mostly only used for knowing the name, for temp folder naming
            return Path.GetFileNameWithoutExtension(videoInput);
        }

        public static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }
        //-------------------------------------------------------------------------

        public static double FractionToDouble(string fraction)
        {
            //Converts the Video Framerate from a fraction to a double value for later eta calculation
            if (double.TryParse(fraction, out double result))
            {
                return result;
            }
            string[] split = fraction.Split(new char[] { ' ', '/' });
            if (split.Length == 2 || split.Length == 3)
            {
                if (int.TryParse(split[0], out int a) && int.TryParse(split[1], out int b))
                {
                    if (split.Length == 2)
                    {
                        return (double)a / b;
                    }
                    if (int.TryParse(split[2], out int c))
                    {
                        return a + (double)b / c;
                    }
                }
            }
            return 24;
        }
    }
}

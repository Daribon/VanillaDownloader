using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MonoTorrent.Client;
using VanillaDownloader.Annotations;
using VanillaDownloader.Utils;
using VanillaDownloader.Views;
using StormLibSharp;

namespace VanillaDownloader.Models
{
    class TorrentModel : INotifyPropertyChanged
    {
        private TorrentManager manager;
        private MainModel parent;
        private PeerDetailsViewModel peerDetailsViewModel;

        private string downloadsPath = Path.Combine(Environment.CurrentDirectory, "Downloads");

        public string Name => Path.GetFileName(MainModel.PatchPath);
        public TorrentState State => manager.State;

        public double Progress => manager.PartialProgress / 100.0;
        public double ProgressBar => manager.PartialProgress;
        public string DownloadSpeed => $"Down Speed: {FormatSpeed(manager.Monitor.DownloadRate)}";
        public string UploadSpeed => $"Up Speed: {FormatSpeed(manager.Monitor.UploadRate)}";
        public string EstimatedTimeLeft => $"ETA: {CalculateEstimatedTimeLeft()}";
        public string DataUsage => $"{FormatDataUsage(manager.Monitor.DataBytesReceived)} ↓ / {FormatDataUsage(manager.Monitor.DataBytesSent)} ↑";
        public string ProgressFile =>$"{(FileSizeMB * manager.PartialProgress / 100.0):0.00} MB / {FileSizeMB:0.00} MB";
        public bool IsComplete => manager.PartialProgress >= 100;
        public double FileSizeMB => FileSizeBytes / (1024.0 * 1024.0);

        public long FileSizeBytes
        {
            get
            {
                var fileName = Path.GetFileName(MainModel.PatchPath);
                var file = manager.Files.FirstOrDefault(f => f.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                return file?.Length ?? 0; throw new FileNotFoundException("The file does not exist in the torrent.", fileName);
            }
        }

        private string FormatSpeed(double rate)
        {
            if (rate < 1024) // Less than 1 KiB/s
            {
                return $"{rate:0.00} B/sec"; // Display in bytes per second
            }
            else if (rate < 1024 * 1024) // Less than 1 MiB/s
            {
                return $"{rate / 1024.0:0.00} KiB/sec"; // Display in kibibytes per second
            }
            else // 1 MiB/s or more
            {
                return $"{rate / (1024.0 * 1024.0):0.00} MiB/sec"; // Display in mebibytes per second
            }
        }

        private string FormatDataUsage(double bytes)
        {
            if (bytes < 1024) // Less than 1 B
            {
                return $"{bytes:0.00} B"; // Display in bytes
            }
            else if (bytes < 1024 * 1024) // Less than 1 MiB
            {
                return $"{bytes / 1024.0:0.00} KiB"; // Display in kibibytes
            }
            else if (bytes < 1024 * 1024 * 1024) // Less than 1 GiB
            {
                return $"{bytes / (1024.0 * 1024.0):0.00} MiB"; // Display in mebibytes
            }
            else // 1 GiB or more
            {
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.00} GiB"; // Display in gibibytes
            }
        }


        public Command CancelCommand { get; }
        public Command FinishedCommand { get; }
        public Command ShowPeerDetailsCommand { get; }
        public Command OpenDlFolderCommand { get; }

        public PeerDetailsViewModel PeerDetailsViewModel
        {
            get => peerDetailsViewModel;
            set
            {
                if (peerDetailsViewModel != value)
                {
                    peerDetailsViewModel = value;
                    OnPropertyChanged();
                }
            }
        }

        public TorrentModel(TorrentManager manager, MainModel parent)
        {
            this.parent = parent;
            this.manager = manager;
            this.manager.TorrentStateChanged += (sender, args) =>
            {
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsComplete));
            };

            PeerDetailsViewModel = new PeerDetailsViewModel();

            CancelCommand = new Command(async () => await Cancel());
            FinishedCommand = new Command(async () => await Finished());
            ShowPeerDetailsCommand = new Command(async () => await ShowPeerDetailsAsync());
            OpenDlFolderCommand = new Command(async () => await OpenDlFolder());

        StartUpdatingPeerDetails();
        }

        private async Task UpdatePeerDetails()
        {
            try
            {
                var peers = await manager.GetPeersAsync();

                if (peers != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PeerDetailsViewModel.Peers.Clear();

                        foreach (var peer in peers)
                        {
                            PeerDetailsViewModel.Peers.Add(new PeerDetailsViewModel
                            {
                                Host = peer?.Uri?.Host,
                                Port = peer?.Uri?.Port ?? 0,
                                DownloadSpeed = FormatSpeed(peer?.Monitor?.DownloadRate ?? 0),
                                UploadSpeed = FormatSpeed(peer?.Monitor?.UploadRate ?? 0),
                                DownloadedData = FormatDataUsage(peer?.Monitor?.DataBytesReceived ?? 0),
                                UploadedData = FormatDataUsage(peer?.Monitor?.DataBytesSent ?? 0),
                                PiecesRequested = peer?.AmRequestingPiecesCount ?? 0,
                                ClientApp = peer.ClientApp.ToString(),
                                PiecesReceived = peer?.PiecesReceived ?? 0
                            });
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("No peers found.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving and processing peers: {ex.Message}");
            }
        }

        public async Task ShowPeerDetailsAsync()
        {
            await UpdatePeerDetails();

            var peerDetailsWindow = new PeerDetailsWindow
            {
                DataContext = PeerDetailsViewModel
            };

            peerDetailsWindow.Show();
        }

        public async Task Start()
        {
            await manager.StartAsync();
        }

        public Task Cancel()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        public async Task OpenDlFolder()
        {
            try
            {
                await Task.Run(() => Process.Start(downloadsPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening downloads folder: {ex.Message}");
            }
        }

        public async Task Finished()
        {
            if (IsComplete)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var exePath = MainModel.PatchPath;
                        var zipPath = MainModel.PatchPath;
                        var mpqPath = Path.Combine(downloadsPath, "wow-patch.mpq");
                        var bnupdatePath = Path.Combine(downloadsPath, "BNUpdate.exe");
                        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;

                        ExtractMpq(exePath, zipPath, mpqPath);
                        ExtractBnUpdate(mpqPath, bnupdatePath);
                        MoveFile(mpqPath, "wow-patch.mpq");
                        MoveFile(bnupdatePath, "BNUpdate.exe");
                        StartBnUpdate(exeDirectory);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"An error occurred: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("Torrent is not complete yet.");
            }
        }

        private void ExtractMpq(string exePath, string zipPath, string mpqPath)
        {
            if (!File.Exists(mpqPath))
            {
                if (File.Exists(exePath))
                {
                    ExtractMpqFromExe(exePath, mpqPath);
                    Debug.WriteLine($"Extracted MPQ to {mpqPath}");
                }
                else if (File.Exists(zipPath))
                {
                    ExtractMpqFromZip(zipPath, mpqPath);
                    Debug.WriteLine($"Extracted MPQ from ZIP to {mpqPath}");
                }
                else
                {
                    Debug.WriteLine("Neither EXE nor ZIP file found.");
                }
            }
            else
            {
                Debug.WriteLine($"MPQ file already exists at {mpqPath}, skipping extraction.");
            }
        }

        private void ExtractBnUpdate(string mpqPath, string bnupdatePath)
        {
            if (File.Exists(mpqPath))
            {
                if (!File.Exists(bnupdatePath))
                {
                    ExtractFileFromMpq(mpqPath, "BNUpdate.exe", bnupdatePath);
                }
                else
                {
                    Debug.WriteLine("BNUpdate.exe already exists, skipping extraction.");
                }

                ModifyPatchCmd(mpqPath);
            }
            else
            {
                Debug.WriteLine($"MPQ file {mpqPath} does not exist.");
            }
        }

        private static void MoveFile(string sourcePath, string destinationPath)
        {
            if (File.Exists(sourcePath) && !File.Exists(destinationPath))
            {
                File.Move(sourcePath, destinationPath);
                Debug.WriteLine($"Moved {Path.GetFileName(sourcePath)} to {destinationPath}");
            }
        }

        private void StartBnUpdate(string exeDirectory)
        {
            if (Process.GetProcessesByName("BNUpdate").Length == 0)
            {
                Process.Start(Path.Combine(exeDirectory, "BNUpdate.exe"));
            }
        }

        private void ExtractMpqFromZip(string zipPath, string mpqPath)
        {
            try
            {
                using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Open))
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                {
                    // Find the MPQ file in the ZIP archive
                    var mpqEntry = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".mpq", StringComparison.OrdinalIgnoreCase));

                    if (mpqEntry != null)
                    {
                        using (var entryStream = mpqEntry.Open())
                        using (var mpqFileStream = new FileStream(mpqPath, FileMode.Create))
                        {
                            entryStream.CopyTo(mpqFileStream);
                            Debug.WriteLine($"MPQ file extracted successfully to: {mpqPath}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MPQ file not found in the ZIP archive.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while extracting MPQ from ZIP: {ex.Message}");
            }
        }

        public void RefreshProgress()
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(ProgressBar));
            OnPropertyChanged(nameof(DownloadSpeed));
            OnPropertyChanged(nameof(UploadSpeed));
            OnPropertyChanged(nameof(EstimatedTimeLeft));
            OnPropertyChanged(nameof(DataUsage));
            OnPropertyChanged(nameof(ProgressFile));
        }

        private string CalculateEstimatedTimeLeft()
        {
            if (IsComplete || manager.Monitor.DownloadRate <= 0)
            {
                return "∞";  // Return infinity if the torrent is complete or download speed is zero or unknown
            }

            var remainingBytes = (long)((1 - manager.Progress / 100.0) * FileSizeBytes);
            var timeLeft = TimeSpan.FromSeconds(remainingBytes / manager.Monitor.DownloadRate);

            return $"{timeLeft.Hours:D2}:{timeLeft.Minutes:D2}:{timeLeft.Seconds:D2}";
        }

        private void ExtractMpqFromExe(string exePath, string mpqPath)
        {
            byte[] mpqSignature = new byte[] { 0x4D, 0x50, 0x51, 0x1A, 0x20, 0x00, 0x00, 0x00 }; // MPQ\x1A \x00\x00\x00

            try
            {
                using (FileStream exeStream = new FileStream(exePath, FileMode.Open, FileAccess.Read))
                {
                    long fileLength = exeStream.Length;
                    long position = 0;
                    bool found = false;

                    while (position < fileLength - mpqSignature.Length)
                    {
                        exeStream.Position = position;
                        byte[] buffer = new byte[mpqSignature.Length];
                        exeStream.Read(buffer, 0, mpqSignature.Length);

                        if (buffer.SequenceEqual(mpqSignature))
                        {
                            found = true;
                            break;
                        }

                        position++;
                    }

                    if (found)
                    {
                        using (FileStream mpqStream = new FileStream(mpqPath, FileMode.Create, FileAccess.Write))
                        {
                            exeStream.Position = position;
                            exeStream.CopyTo(mpqStream);
                        }

                        Debug.WriteLine("MPQ file extracted successfully.");
                    }
                    else
                    {
                        Debug.WriteLine("MPQ signature not found in the executable.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while extracting MPQ: {ex.Message}");
            }
        }

        private void ExtractFileFromMpq(string mpqPath, string fileName, string outputPath)
        {
            try
            {
                using (MpqArchive archive = new MpqArchive(mpqPath, FileAccess.Read))
                {
                    archive.ExtractFile(fileName, outputPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting {fileName} from MPQ: {ex.Message}");
            }
        }

        private void ModifyPatchCmd(string mpqPath)
        {
            string patchCmdPath = Path.Combine(downloadsPath, "patch.cmd");

            try
            {
                // Extract patch.cmd from the MPQ archive
                ExtractFileFromMpq(mpqPath, "patch.cmd", patchCmdPath);

                // Read and modify the content
                string content = File.ReadAllText(patchCmdPath, Encoding.ASCII);
                content = content.Replace("FileVersionLessThan", "FileVersionNotEqualTo");

                // Write the modified content back to file
                File.WriteAllText(patchCmdPath, content, Encoding.ASCII);

                using (var archive = new MpqArchive(mpqPath, FileAccess.ReadWrite))
                {
                    try
                    {
                        // Attempt to remove the old patch.cmd from the archive
                        archive.RemoveFile("patch.cmd");
                        Debug.WriteLine("Old patch.cmd removed successfully.");

                        // Add the modified patch.cmd to the archive
                        archive.AddFileFromDiskWithCompression(patchCmdPath, "patch.cmd", MpqCompressionTypeFlags.MPQ_COMPRESSION_ZLIB);
                        Debug.WriteLine("patch.cmd added successfully.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"An error occurred while modifying the MPQ archive: {ex.Message}");
                    }
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"IO Exception: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Debug.WriteLine($"Unauthorized Access Exception: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                // Clean up temporary files
                if (File.Exists(patchCmdPath))
                {
                    try
                    {
                        File.Delete(patchCmdPath);
                        Debug.WriteLine("Temporary patch.cmd file deleted.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete temporary patch.cmd file: {ex.Message}");
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void StartUpdatingPeerDetails()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    RefreshProgress();
                    await UpdatePeerDetails();
                    await Task.Delay(1000);
                }
            });
        }
    }
}
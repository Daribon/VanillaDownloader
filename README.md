# Vanilla Downloader

**VanillaDownloader** is a torrent client utilizing the MonoTorrent library, specifically designed for downloading and processing vanilla patches. It provides a user-friendly interface for tracking torrent progress and efficiently managing downloaded files.

## Features

- **Torrent Tracking**: Monitor the state, progress, and speed of torrent downloads.
- **Peer Details**: View detailed information about peers connected to the torrent, including their download/upload speeds and data usage.
- **Automatic Processing**: Automate the processing of files after a torrent download completes, such as extracting and modifying files.

## How It Works

1. **Torrent Download**: Torrents can be downloaded by using magnet links, `.torrent` files, or URLs from the web. The files contained in the torrents can be downloaded normally or through web-seeding.
2. **Once Torrent is Finished**:
   - **From Executable**: Attempt to extract the MPQ file from the executable file.
   - **From ZIP Archive**: Attempt to extract the MPQ file from the ZIP archive.
   - **From MPQ**: Extract specific files (`BNUpdate.exe` and `patch.cmd`) from MPQ archives.
3. **Processing**: After the torrent download is complete, the program automatically processes the extracted files. It modifies `patch.cmd` as needed to ensure compatibility with vanilla installations (e.g., applying a 1.7.0 patch to a 1.12.1 client) and finally runs `BNUpdate.exe` to apply the patch.

## Dependencies

- **MonoTorrent**: For torrent management and monitoring.
- **StormLibSharp**: C# wrappers for Ladislav Zezula's StormLib.
- **StormLib**: For handling MPQ archives.
using System;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;

namespace VanillaDownloader.Models
{
    class MainModel
    {
        public const string downloadsFolder = "./Downloads";
        public static readonly string PatchPath = Path.Combine(downloadsFolder, null); // Set to "Torrent/path/to/patch/to/download"
        public static readonly string magnetLink = null; // Set to "magnet:?xt=urn:btih:YOUR_MAGNET_LINK_HASH&dn=NAME_OF_TORRENT" to use
        public static readonly string embeddedTorrent = null; // Set to "VanillaDownloader.Resources.NAME_OF_TORRENT" to use
        public static readonly string torrentUrl = null; // Set to "https://YOUR_OWN_TORRENT_LINK.torrent" to use

        ClientEngine engine;
        Timer timer;

        public ObservableCollection<TorrentModel> Torrents { get; } = new ObservableCollection<TorrentModel>();

        public MainModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                Debug.WriteLine(resourceName);
            }

            InitializeEngine();
            InitializeTimer();

            // Add torrent from magnet link, embedded torrent file, or URL on startup asynchronously
            Task.Run(async () => await AddTorrent().ConfigureAwait(false)).Wait(); // Wait for it to finish adding
        }

        private void InitializeEngine()
        {
            var settingBuilder = new EngineSettingsBuilder
            {
                AllowPortForwarding = true,
                AutoSaveLoadDhtCache = true,
                AutoSaveLoadFastResume = false,
            };

            var engineSettings = settingBuilder.ToSettings();
            engine = new ClientEngine(engineSettings);
        }

        private void InitializeTimer()
        {
            timer = new Timer(
                RefreshProgress,
                null,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(50));
        }

        private async Task AddTorrent()
        {
            if (!string.IsNullOrEmpty(magnetLink))
            {
                await AddTorrentFromMagnetLink(magnetLink);
            }
            else if (!string.IsNullOrEmpty(embeddedTorrent))
            {
                await AddTorrentFromEmbeddedResource(embeddedTorrent);
            }
            else if (!string.IsNullOrEmpty(torrentUrl))
            {
                await AddTorrentFromUrl(torrentUrl);
            }
            else
            {
                Debug.WriteLine("No torrent source specified.");
            }
        }

        private async Task AddTorrentFromMagnetLink(string magnetLink)
        {
            // Parse the magnet link
            var magnet = MagnetLink.Parse(magnetLink);

            // Extract InfoHashes from the magnet link
            var infoHashes = magnet.InfoHashes;

            // Check if the torrent is already in the engine
            if (engine.Contains(infoHashes))
                return;

            // Add the torrent to the engine
            var manager = await engine.AddAsync(magnet, downloadsFolder, new TorrentSettings());

            // Process the torrent
            await ProcessTorrent(manager);
        }

        private async Task AddTorrentFromEmbeddedResource(string resourceName)
        {
            // Load the torrent from the embedded resource
            var torrent = await LoadTorrentFromEmbeddedResourceAsync(resourceName);

            // Check if the torrent is already in the engine
            if (engine.Contains(torrent.InfoHashes))
                return;

            // Add the torrent to the engine
            var manager = await engine.AddAsync(torrent, downloadsFolder, new TorrentSettings());

            // Process the torrent
            await ProcessTorrent(manager);
        }

        private async Task<Torrent> LoadTorrentFromEmbeddedResourceAsync(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("The embedded torrent resource could not be found.", resourceName);
                }

                return await Torrent.LoadAsync(stream);
            }
        }

        private async Task AddTorrentFromUrl(string url)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var torrent = await Torrent.LoadAsync(stream);

                    // Check if the torrent is already in the engine
                    if (engine.Contains(torrent.InfoHashes))
                        return;

                    // Add the torrent to the engine
                    var manager = await engine.AddAsync(torrent, downloadsFolder, new TorrentSettings());

                    // Process the torrent
                    await ProcessTorrent(manager);
                }
            }
        }

        private async Task ProcessTorrent(TorrentManager manager)
        {
            // Log available files with exact path
            Debug.WriteLine("Available files:");
            foreach (var file in manager.Files)
            {
                Debug.WriteLine($"File: {file.Path} (Size: {file.Length / 1024 / 1024} MB)");
            }

            // Set specific file priority
            foreach (var file in manager.Files)
            {
                if (file.Path.EndsWith(Path.GetFileName(PatchPath), StringComparison.OrdinalIgnoreCase))
                {
                    await manager.SetFilePriorityAsync(file, Priority.Highest); // Download this file
                    Debug.WriteLine($"Setting priority for {file.Path} to High.");
                }
                else
                {
                    await manager.SetFilePriorityAsync(file, Priority.DoNotDownload); // Skip these files
                    Debug.WriteLine($"Setting priority for {file.Path} to DoNotDownload.");
                }
            }

            // Create a TorrentModel and add it to the collection
            var model = new TorrentModel(manager, this);
            Torrents.Add(model);

            // Start the model
            await model.Start();

            // Optional: Verify priority settings after starting
            Debug.WriteLine("Priority settings applied:");
            foreach (var file in manager.Files)
            {
                Debug.WriteLine($"{file.Path}: {file.Priority}");
            }
        }

        void RefreshProgress(object state)
        {
            foreach (var manager in Torrents)
                manager.RefreshProgress();
        }
    }
}
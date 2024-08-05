using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VanillaDownloader.Models
{
    public class PeerDetailsViewModel : INotifyPropertyChanged
    {
        private string host;
        private int port;
        private string downloadSpeed;
        private string uploadSpeed;
        private string downloadedData;
        private string uploadedData;
        private int piecesRequested;
        private string clientApp;
        private int piecesReceived;
        private ObservableCollection<PeerDetailsViewModel> peers;

        public string Host { get => host; set { host = value; OnPropertyChanged(); } }
        public int Port { get => port; set { port = value; OnPropertyChanged(); } }
        public string DownloadSpeed { get => downloadSpeed; set { downloadSpeed = value; OnPropertyChanged(); } }
        public string UploadSpeed { get => uploadSpeed; set { uploadSpeed = value; OnPropertyChanged(); } }
        public string DownloadedData { get => downloadedData; set { downloadedData = value; OnPropertyChanged(); } }
        public string UploadedData { get => uploadedData; set { uploadedData = value; OnPropertyChanged(); } }
        public int PiecesRequested { get => piecesRequested; set { piecesRequested = value; OnPropertyChanged(); } }
        public string ClientApp { get => clientApp; set { clientApp = value; OnPropertyChanged(); } }
        public int PiecesReceived { get => piecesReceived; set { piecesReceived = value; OnPropertyChanged(); } }

        public ObservableCollection<PeerDetailsViewModel> Peers
        {
            get => peers;
            set { peers = value; OnPropertyChanged(); }
        }

        public PeerDetailsViewModel()
        {
            Peers = new ObservableCollection<PeerDetailsViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
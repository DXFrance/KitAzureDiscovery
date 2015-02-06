using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using Newtonsoft.Json;

namespace WPFClient
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        #region fields
        private FilterInfoCollection _videoDevices;
        private VideoCaptureDevice _videoSource;
        private MotionDetector _motionDetector;
        private readonly SynchronizationContext _synchronizationContext;
        private List<FilterInfo> _devices;
        private FilterInfo _selectedDevice;
        private BitmapImage _currentImage;
        private bool _bitmapToUpload;
        private readonly DispatcherTimer _timer;
        private string _information;
        private ICommand _startOrStopCommand;
        private string _azureSiteUrl;
        private BitmapImage _buttonImage;
        #endregion

        public MainWindowViewModel()
        {
            _synchronizationContext = SynchronizationContext.Current;
            _timer = new DispatcherTimer();
            _timer.Tick += OnUploadCapture;
            _timer.Interval = new TimeSpan(0, 0, 0, Properties.Settings.Default.Duration);
            AzureSiteUrl = Properties.Settings.Default.AzureSiteUrl;
            ButtonImage = Application.Current.Resources["StartImage"] as BitmapImage;
        }

        //-----------------public properties
        /// <summary>
        /// Liste des caméras connectées à l'ordinateur
        /// </summary>
        public List<FilterInfo> Devices
        {
            get { return _devices; }
            set
            {
                _devices = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Caméra utilisée pour la capture
        /// </summary>
        public FilterInfo SelectedDevice
        {
            get { return _selectedDevice; }
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage CurrentImage
        {
            get { return _currentImage; }
            set
            {
                _currentImage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Texte affiché en haut à droite
        /// </summary>
        public string Information
        {
            get { return _information; }
            set
            {
                _information = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Url racine du site Azure ou l'on upload les images
        /// </summary>
        public string AzureSiteUrl
        {
            get { return _azureSiteUrl; }
            set
            {
                _azureSiteUrl = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Image du bouton pour lancer ou arrêter la capture
        /// </summary>
        public BitmapImage ButtonImage
        {
            get { return _buttonImage; }
            set
            {
                _buttonImage = value;
                OnPropertyChanged();
            }
        }

        public ICommand StartOrStopCommand
        {
            get { return _startOrStopCommand ?? (_startOrStopCommand = new RelayCommand(StartOrStop)); }
        }

        //-----------------public methods
        /// <summary>
        /// Charge la liste des WebCams connectés à l'ordinateur
        /// </summary>
        public void InitializeWebCamList()
        {
            SelectedDevice = null;
            Devices = null;

            _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_videoDevices.Count == 0)
                return;

            Devices = _videoDevices.Cast<FilterInfo>().ToList();
            SelectedDevice = Devices[0];
        }

        //-----------------private methods
        /// <summary>
        /// Démarre ou arrête la capture
        /// </summary>
        private void StartOrStop()
        {
            if (SelectedDevice == null)
                return;

            if (_videoSource != null && _videoSource.IsRunning)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        private void Start()
        {
            CloseVideoSource();
            ButtonImage = Application.Current.Resources["StopImage"] as BitmapImage;
            // le détecteur de mouvement
            _motionDetector = new MotionDetector(
                new TwoFramesDifferenceDetector
                {
                    DifferenceThreshold = 15,
                    SuppressNoise = true
                },
                new BlobCountingObjectsProcessing
                {
                    HighlightColor = Color.Red,
                    HighlightMotionRegions = true,
                    MinObjectsHeight = 10,
                    MinObjectsWidth = 10
                });

            _videoSource = new VideoCaptureDevice(SelectedDevice.MonikerString);
            _videoSource.NewFrame += OnNewFrameReceived;
            _videoSource.Start();
            Information = "Capture démarrée";
            _timer.IsEnabled = true;
        }

        private void Stop(string moreInfos = null)
        {
            _bitmapToUpload = false;
            CloseVideoSource();
            ButtonImage = Application.Current.Resources["StartImage"] as BitmapImage;
            Information = "Capture arrêtée";
            if (moreInfos != null)
                Information = string.Format("{0} : {1}", Information, moreInfos);

            _timer.IsEnabled = false;
        }

        /// <summary>
        /// Tente d'uploader la capture sur le serveur
        /// </summary>
        ///<remarks>Valide l'uri saisie</remarks>
        private async void OnUploadCapture(object sender, EventArgs e)
        {
            if (CurrentImage == null || !_bitmapToUpload) return;

            Uri siteUri;
            if (!Uri.TryCreate(AzureSiteUrl, UriKind.Absolute, out siteUri))
            {
                Information = "Url du site Azure non valide !";
                return;
            }
            var uri = new Uri("/Images/UploadImage", UriKind.Relative);
            var azureWebSiteUri = new Uri(siteUri, uri);

            Information = "Sauvegarde en cours...";
            _timer.Stop();

            // conversion du bitmapImage en bitmap jpeg
            var bitmapDatas = GetBitmapDatas();
            if (bitmapDatas == null)
            {
                Information = "";
                _bitmapToUpload = false;
                return;
            }

            var request = (HttpWebRequest) WebRequest.Create(azureWebSiteUri);
            request.Method = "POST";

            // traitement de la requête : ajoute le tableau de byte de l'image capturée
            Stream requestStream = null;
            try
            {
                requestStream = await Task.Factory.FromAsync<Stream>(
                    request.BeginGetRequestStream,
                    request.EndGetRequestStream, null);

                await requestStream.WriteAsync(bitmapDatas, 0, bitmapDatas.Length);

            }
            catch (WebException ex)
            {
                Stop(ex.Message);
                return;
            }
            finally
            {
                if (requestStream != null)
                    requestStream.Dispose();
            }
            // traitement de la réponse : récupère la réponse
            //          enregistre la fréquence de l'upload retournée par le serveur Azure
            //          enregistre l'url du site Azure (pour éviter de le ressaisir à chaque fois)

            WebResponse response = null;
            try
            {
                response = await Task.Factory.FromAsync<WebResponse>(
                    request.BeginGetResponse,
                    request.EndGetResponse, null);
                Stream responseStream = response.GetResponseStream();
                if (responseStream == null)
                {
                    Stop("Impossible de récupérer la réponse du serveur Azure");
                    return;
                }
                using (var reader = new StreamReader(responseStream))
                {
                    var serialiser = new JsonSerializer();
                    var configuration = (AppConfiguration) serialiser.Deserialize(reader, typeof (AppConfiguration));

                    if (Properties.Settings.Default.Duration != configuration.Duration)
                    {
                        Information = string.Format("Changement de la fréquence de téléchargement : {0}s",
                            configuration.Duration);
                        Properties.Settings.Default.Duration = configuration.Duration;

                        _timer.Interval = new TimeSpan(0, 0, 0, Properties.Settings.Default.Duration);
                    }
                    Properties.Settings.Default.AzureSiteUrl = siteUri.ToString();
                    Properties.Settings.Default.Save();

                    _timer.Start();
                }
                Information = "";
                _bitmapToUpload = false;
            }
            catch (WebException ex)
            {
                Stop(ex.Message);
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
        }

        private byte[] GetBitmapDatas()
        {
            var bitmap = BitmapConverter.ToJpegBitmap(CurrentImage);
            if (bitmap == null)
                return null;

            return (byte[])new ImageConverter().ConvertTo(bitmap, typeof(byte[]));
        }

        private void CloseVideoSource()
        {
            if (_motionDetector != null) _motionDetector.Reset();
            if (_videoSource == null) return;
            if (!_videoSource.IsRunning) return;
            _videoSource.SignalToStop();
            _videoSource = null;
        }

        /// <summary>
        /// A chaque frame capturée, mets à jour l'image si un mouvement a été détecté
        /// </summary>
        private void OnNewFrameReceived(object sender, NewFrameEventArgs eventArgs)
        {
            var img = (Bitmap) eventArgs.Frame.Clone();
            var motionLevel = _motionDetector.ProcessFrame(img);

            if (CurrentImage == null)
            {
                _synchronizationContext.Post(
                    o =>
                    {
                        CurrentImage = BitmapConverter.ToBitmapImage(img);
                    }, null);
            }
            // vous pouvez jouer sur ce chiffre pour éviter de détecter des petits mouvements
            if (motionLevel < .005f) return;

            _bitmapToUpload = true;
            _synchronizationContext.Post(o =>
            {
                Information = "Mouvement détecté !";
                CurrentImage = BitmapConverter.ToBitmapImage(img);
            }, null);
        }

        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            // objets non managés
            CloseVideoSource();
            if (disposing)
            {
                // on n'a pas d'objets managés à supprimer mais au cas ou par la suite...
            }
        }
        ~MainWindowViewModel()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

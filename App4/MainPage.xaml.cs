using Windows.UI.Xaml.Controls;
using Microsoft.ProjectOxford.Face;
using Windows.Media;
using Windows.Graphics.Imaging;
using Windows.Media.MediaProperties;
using Windows.Media.Capture;
using System.Threading.Tasks;
using System;
using Windows.System.Threading;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.ProjectOxford.Face.Contract;
using System.ComponentModel;
using Windows.Media.FaceAnalysis;
using System.Collections.Generic;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using System.Threading;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App4
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region ClasesInicializadasPrincipal
        private VideoEncodingProperties videoProperties;
        private MediaCapture mediaCapture;
        private ThreadPoolTimer frameProcessingTimer;
        const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Bgra8;
        public InMemoryRandomAccessStream ms;
        
        IList<DetectedFace> faces = null;
        private FaceTracker faceTracker;
        private ScenarioState currentState;
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        private readonly SolidColorBrush fillBrushText = new SolidColorBrush(Windows.UI.Colors.GreenYellow);
        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private readonly double lineThickness = 2.0;
        private SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1);
        public string IdentidadEncontrada = "";

        public string PersonId
        {
            get
            {
                return _personId;
            }

            set
            {
                _personId = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("PersonId"));
                }
            }
        }

        #endregion ClasesInicializadasPrincipal

        #region Campos
        private static string sampleGroupId = Guid.NewGuid().ToString();
        private ObservableCollection<Face> _faces = new ObservableCollection<Face>();
        private ObservableCollection<Person> _persons = new ObservableCollection<Person>();
        private string _personId;
        private int _maxConcurrentProcesses;

        public int MaxImageSize
        {
            get
            {
                return 300;
            }
        }
        #endregion Campos

        #region Constructor Main Page
        public MainPage()
        {


            this.InitializeComponent();
            this.currentState = ScenarioState.Idle;
            App.Current.Suspending += this.OnSuspending;
            _maxConcurrentProcesses = 4;



        }
        #endregion Constructor

        public string GroupId
        {
            get
            {
                return "2f30c8b9-216e-4b74-9294-6b93d9cda686";
            }

            set
            {
                sampleGroupId = value;
            }
        }

        //private async void btnTomarFoto_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        //{
        //    using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
        //    {

        //    }
        //}
        private void CameraStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            IdentidadEncontrada = "";
            if (this.currentState == ScenarioState.Streaming)
            {
                //this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
                this.ChangeScenarioState(ScenarioState.Idle);
                btnIniciarStream.Content = "Iniciar Streamming";
            }
            else
            {
                //this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
                this.ChangeScenarioState(ScenarioState.Streaming);
                btnIniciarStream.Content = "Parar Streamming";
            }
        }
        private void MediaCapture_CameraStreamFailed(MediaCapture sender, object args)
        {
            // MediaCapture is not Agile and so we cannot invoke its methods on this caller's thread
            // and instead need to schedule the state change on the UI thread.
            var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ChangeScenarioState(ScenarioState.Idle);
            });
        }
        private async Task<bool> StartWebcamStreaming()
        {
            bool successful = true;
            try
            {

                this.mediaCapture = new MediaCapture();
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = StreamingCaptureMode.Video;
                await mediaCapture.InitializeAsync(settings);
                this.mediaCapture.Failed += this.MediaCapture_CameraStreamFailed;

                var deviceController = mediaCapture.VideoDeviceController;
                this.videoProperties = deviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                this.CamPreview.Source = this.mediaCapture;
                await mediaCapture.StartPreviewAsync();

                TimeSpan timerInterval = TimeSpan.FromMilliseconds(66);
                this.frameProcessingTimer = Windows.System.Threading.ThreadPoolTimer.CreatePeriodicTimer(new Windows.System.Threading.TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);
            }
            catch (System.UnauthorizedAccessException)
            {
                // If the user has disabled their webcam this exception is thrown; provide a descriptive message to inform the user of this fact.
                //this.rootPage.NotifyUser("Webcam is disabled or access to the webcam is disabled for this app.\nEnsure Privacy Settings allow webcam usage.", NotifyType.ErrorMessage);
                successful = false;
            }
            catch (Exception ex)
            {
                //this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                successful = false;
            }
            return successful;

        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (this.faceTracker == null)
            {

                this.faceTracker = await FaceTracker.CreateAsync();
            }
        }

        private void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (this.currentState == ScenarioState.Streaming)
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                try
                {
                    this.ChangeScenarioState(ScenarioState.Idle);
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }
        private async void ShutdownWebCam()
        {
            if (this.frameProcessingTimer != null)
            {
                this.frameProcessingTimer.Cancel();
            }

            if (this.mediaCapture != null)
            {
                if (this.mediaCapture.CameraStreamState == Windows.Media.Devices.CameraStreamState.Streaming)
                {
                    try
                    {
                        await this.mediaCapture.StopPreviewAsync();
                    }
                    catch (Exception)
                    {
                        ;   // Since we're going to destroy the MediaCapture object there's nothing to do here
                    }
                }
                this.mediaCapture.Dispose();
            }

            this.frameProcessingTimer = null;
            this.CamPreview.Source = null;
            this.mediaCapture = null;

        }
        private async void ChangeScenarioState(ScenarioState newState)
        {
            // Disable UI while state change is in progress
            switch (newState)
            {
                case ScenarioState.Idle:

                    this.ShutdownWebCam();

                    this.VisualizationCanvas.Children.Clear();
                    this.currentState = newState;
                    break;

                case ScenarioState.Streaming:

                    if (!await this.StartWebcamStreaming())
                    {
                        this.ChangeScenarioState(ScenarioState.Idle);
                        break;
                    }

                    this.VisualizationCanvas.Children.Clear();
                    this.currentState = newState;

                    break;
            }
        }
        public SoftwareBitmapSource softwareBitmapSource = new SoftwareBitmapSource();

        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {
            if (this.currentState != ScenarioState.Streaming)
            {
                return;
            }

            // If a lock is being held it means we're still waiting for processing work on the previous frame to complete.
            // In this situation, don't wait on the semaphore but exit immediately.
            if (!frameProcessingSemaphore.Wait(0))
            {
                return;
            }


            try
            {
                const BitmapPixelFormat InputPixelFormat1 = BitmapPixelFormat.Nv12;


                using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat1, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
                {
                    var valor = await this.mediaCapture.GetPreviewFrameAsync(previewFrame);

                    
                    if (FaceDetector.IsBitmapPixelFormatSupported(previewFrame.SoftwareBitmap.BitmapPixelFormat))
                    {
                        var previewFrameSize = new Windows.Foundation.Size(previewFrame.SoftwareBitmap.PixelWidth, previewFrame.SoftwareBitmap.PixelHeight);
                        var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                        {
                            var caraNuevaValor = "";
                            this.SetupVisualization(previewFrameSize, faces);


                        //this.imagenCompletar.Source = bitmpatSRC;
                        //bitmpatSRC.SetBitmapAsync(previewFrameBMO);
                    });

                        faces = await this.faceTracker.ProcessNextFrameAsync(previewFrame);
                        if(faces.Count != 0 && IdentidadEncontrada == "")
                        {
                            string[] nombre = null;
                            int contador = 0;
                            foreach(var caraEncontrad in faces)
                            {
                                nombre[contador] = await ObtenerIdentidad();
                                contador += 1;
                            }
                            
                        }

                    }
                    else
                    {
                        throw new System.NotSupportedException("PixelFormat '" + InputPixelFormat.ToString() + "' is not supported by FaceDetector");
                    }


                    
                }
            }
            catch (Exception ex)
            {
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                });
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }

        }
        private async Task<string> ObtenerIdentidad()
        {
            byte[] arrayImage;
            var PersonName = "";

            try
            {
                const BitmapPixelFormat InputPixelFormat1 = BitmapPixelFormat.Bgra8;

                using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat1, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
                {
                    var valor = await this.mediaCapture.GetPreviewFrameAsync(previewFrame);

                    SoftwareBitmap softwareBitmapPreviewFrame = valor.SoftwareBitmap;

                    Size sizeCrop = new Size(softwareBitmapPreviewFrame.PixelWidth, softwareBitmapPreviewFrame.PixelHeight);
                    Point point = new Point(0, 0);
                    Rect rect = new Rect(0, 0, softwareBitmapPreviewFrame.PixelWidth, softwareBitmapPreviewFrame.PixelHeight);
                    var arrayByteData = await EncodedBytes(softwareBitmapPreviewFrame, BitmapEncoder.BmpEncoderId);

                    SoftwareBitmap softwareBitmapCropped = await CreateFromBitmap(softwareBitmapPreviewFrame, (uint)softwareBitmapPreviewFrame.PixelWidth, (uint)softwareBitmapPreviewFrame.PixelHeight, arrayByteData);
                    SoftwareBitmap displayableImage = SoftwareBitmap.Convert(softwareBitmapCropped, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    arrayImage = await EncodedBytes(displayableImage, BitmapEncoder.BmpEncoderId);
                    var nuevoStreamFace = new MemoryStream(arrayImage);

                    var ignored1 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        softwareBitmapSource.SetBitmapAsync(displayableImage);

                        imagenCamaraWeb.Source = softwareBitmapSource;

                    });

                    string subscriptionKey = "[Your Key]";
                    string subscriptionEndpoint = "[Your Subscription EndPoint]";
                    var faceServiceClient = new FaceServiceClient(subscriptionKey, subscriptionEndpoint);

                    try
                    {
                        

                        // using (var fsStream = File.OpenRead(sampleFile))
                        // {

                        var faces = await faceServiceClient.DetectAsync(nuevoStreamFace);

                        
                        var resultadoIdentifiacion = await faceServiceClient.IdentifyAsync(faces.Select(ff => ff.FaceId).ToArray(), largePersonGroupId: this.GroupId);

                        for (int idx = 0; idx < faces.Length; idx++)
                        {
                            // Update identification result for rendering

                            var res = resultadoIdentifiacion[idx];
                            
                            if (res.Candidates.Length > 0)
                            {
                                var nombrePersona = await faceServiceClient.GetPersonInLargePersonGroupAsync(GroupId, res.Candidates[0].PersonId);
                                PersonName = nombrePersona.Name.ToString();
                                var ignored2 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    
                                    txtResult.Text = nombrePersona.Name.ToString();
                                    IdentidadEncontrada = txtResult.Text;
                                    

                                });
                            }
                            else
                            {
                                txtResult.Text = "Unknown";
                            }
                        }
                        //}

                    }
                    catch (Exception ex)
                    {
                        var error = ex.Message.ToString();
                    }
                }
            }catch(Exception ex)
            {
                var mensaje = ex.Message.ToString();
            }
            return PersonName;

        }


        private enum ScenarioState
        {

            Idle,
            Streaming
        }
        private void SetupVisualization(Windows.Foundation.Size framePizelSize, IList<DetectedFace> foundFaces)
        {
            this.VisualizationCanvas.Children.Clear();



            double actualWidth = this.VisualizationCanvas.ActualWidth;
            double actualHeight = this.VisualizationCanvas.ActualHeight;

            if (this.currentState == ScenarioState.Streaming && foundFaces != null && actualWidth != 0 && actualHeight != 0)
            {
                double widthScale = framePizelSize.Width / actualWidth;
                double heightScale = framePizelSize.Height / actualHeight;

                int i = 0;
                foreach (DetectedFace face in foundFaces)
                {

                    Rectangle box = new Rectangle();
                    
                    box.Width = (int)face.FaceBox.Width / (int)widthScale;
                    box.Height = (int)(face.FaceBox.Height / heightScale);
                    box.Fill = this.fillBrush;
                    box.Stroke = this.lineBrush;
                    box.StrokeThickness = this.lineThickness;
                    box.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale), 0, 0);
                    this.VisualizationCanvas.Children.Add(box);

                    TextBlock texto = new TextBlock();
                    texto.Text = IdentidadEncontrada;
                    texto.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale)-15, 0, 0);
                    texto.Foreground = this.lineBrush;
                    this.VisualizationCanvas.Children.Add(texto);


                }

            }

        }


        private async void btnTomarFoto_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            txtResult.Text = "";

           // await ProcessCurrentVideoFrame();
        }

        public ObservableCollection<Face> TargetFaces
        {
            get
            {
                return _faces;
            }
        }

        private async Task<SoftwareBitmap> CreateFromBitmap(SoftwareBitmap softwareBitmap, uint width, uint heigth, byte[] data)
        {
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
               

                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;

                var ancho = width * (0.2);
                var alto = heigth * (0.2);


                encoder.BitmapTransform.ScaledWidth = (uint)ancho;
                encoder.BitmapTransform.ScaledHeight = (uint)alto;

                await encoder.FlushAsync();

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                return await decoder.GetSoftwareBitmapAsync(softwareBitmap.BitmapPixelFormat, softwareBitmap.BitmapAlphaMode);
            }
        }
        private async Task<byte[]> EncodedBytes(SoftwareBitmap soft, Guid encoderId)
        {
            byte[] array = null;

            // First: Use an encoder to copy from SoftwareBitmap to an in-mem stream (FlushAsync)
            // Next:  Use ReadAsync on the in-mem stream to get byte[] array

            using (ms = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, ms);
                encoder.SetSoftwareBitmap(soft);

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception ex) { return new byte[0]; }

                array = new byte[ms.Size];
                await ms.ReadAsync(array.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
            }
            return array;
        }

        public event PropertyChangedEventHandler PropertyChanged;


        public ObservableCollection<Person> Persons
        {
            get
            {
                return _persons;
            }
        }

        

    }
}

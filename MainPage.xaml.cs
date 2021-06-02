using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SelfieUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StorageFile storeFile;  
        private IRandomAccessStream stream;
        private MediaCapture mediaCapture;
        private bool isPreviewing;
        private DisplayRequest displayRequest = new DisplayRequest();

        private async Task StartPreviewAsync()
        {
            try
            {
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessageToUser("The app was denied access to the camera");
                return;
            }

            try
            {
                PreviewControl.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }
        }

        private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs e)
        {
            if (e.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                ShowMessageToUser("The Camera Preview can't be displayed because another app is using it");
            }
            else if (e.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await StartPreviewAsync();
                });
            }
        }

        private async Task CleanupCameraAsync()
        {
            if (mediaCapture != null)
            {
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PreviewControl.Source = null;
                    if (displayRequest != null)
                    {
                        displayRequest.RequestRelease();
                    }

                    mediaCapture.Dispose();
                    mediaCapture = null;
                });
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await CleanupCameraAsync();
        }

        public MainPage()
        {
            InitializeComponent();

            Application.Current.Suspending += Application_Suspending;
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanupCameraAsync();
                deferral.Complete();
            }
        }

        private async void ShowMessageToUser(string message)
        {
            MessageDialog dialog = new MessageDialog(message, "Warning");
            await dialog.ShowAsync();
        }

        private async void StartPreviewBtn_Click(object sender, RoutedEventArgs e) => await StartPreviewAsync();

        private async void SavePictureBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //CameraCaptureUI cameraCaptureUI = new CameraCaptureUI();
                var myPictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
                StorageFile file = await myPictures.SaveFolder.CreateFileAsync("photo.jpg", CreationCollisionOption.GenerateUniqueName);

                using (var captureStream = new InMemoryRandomAccessStream())
                {
                    await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

                    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var decoder = await BitmapDecoder.CreateAsync(captureStream);
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                        var properties = new BitmapPropertySet
                        {
                            {
                                "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) 
                            }
                        };
                        await encoder.BitmapProperties.SetPropertiesAsync(properties);

                        await encoder.FlushAsync();
                    }
                }
            }
            catch(Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            
        }

        //private async Task GetVideoDevicesAsync()
        //{
        //    DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        //    foreach (var device in devices) CameraComboBox.Items.Add(new CameraItem { Device = device });
        //}

        //private async void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    await CleanupCameraAsync();
        //    var camItem = (CameraItem)CameraComboBox.SelectedItem;
        //    await StartPreviewAsync(camItem.Device.Id);
        //}
    }
}
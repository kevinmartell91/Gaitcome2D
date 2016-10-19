using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using Gaitcome2D;
using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using BytescoutImageToVideo;
using Gaitcome2D.AviFileWrapper;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;

namespace Gaitcome2D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// =>    Get-ChildItem .\ -include bin,obj,bld,Backup,_UpgradeReport_Files,Debug,Release,ipch -Recurse | foreach ($_) { remove-item $_.fullname -Force -Recurse }
        ///       run this on power shell in order to delete unimportant files to commit
        /// </summary>
        int numCameras;
        int FPS;
        bool isRecording;
        bool isPlaying;
        string saveVideoPath;
        string readVideoPath;
        string testMultiImagesFolder;
        string cameraFolder;
        //int imageQualityWriter;
        int contFrames;
        DispatcherTimer timerRecording = new DispatcherTimer();
        List<ImageSource> lstImgSour;
        private bool userIsDraggingSlider = false;

        // paid alternative
        ImageToVideo converter;

        bool cam0, cam1;

        #region imagePlayer

        DispatcherTimer timerImagePlayer = new DispatcherTimer();

        bool isImagePlayerDataLoaded;
        int imagePlayerValue;
        int imagePlayerSpeed;
        double DEFAULT_SPEED_SLIDER_VALUE;
        string[] files;

        #endregion

        #region Image processing Gait analysis

         Image<Bgr, byte> infraredBgrImage;
         Image<Bgr, byte> colorImage; 

        Image<Bgr, byte> infraredImgCpy;
        Image<Gray, byte> grayImg;
        Image<Gray, byte> binaryImg;

        int blobCount;
        int countFrames;
        List<List<System.Drawing.PointF>> markersHistory;
        bool isDrawingAxis;
        string readImagePath;
        bool isLeftSagittalPlane;
        bool isCapturingAngles;

        #endregion

        #region Record TAB

        bool isRecordingPathSelected;
        string recordingVideoPath;
        int indexInfraredCamera;


        #endregion


        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
            timerRecording.Tick += new EventHandler(recording_Timer_Tick);
            //timer.Interval = 

            numCameras = 0;
            FPS = 75;
            //imageQualityWriter = 30;    // 0 - 100 
            isRecording = false;        //true or false
            isPlaying = false;          //true or false
            readVideoPath = @"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi";
            saveVideoPath = @"D:\Projects\Gaitcom\testMultiImages\";
            testMultiImagesFolder = @"D:\Projects\Gaitcom\testMultiImages\";
            cameraFolder = @"cam";
            cam0 = cam1 = false;
            contFrames = 0;

            // paid alternative
            converter = new ImageToVideo();

            ImageSourceConverter c = new ImageSourceConverter();
            lstImgSour = new List<ImageSource>();


            #region imagePlayer

            timerImagePlayer.Tick += new EventHandler(imagePlayer_Timer_Tick);
            setTimerIntervalImagePlayer();

            isImagePlayerDataLoaded = false;
            imagePlayerValue = 0;

            DEFAULT_SPEED_SLIDER_VALUE = 0.6;

            #endregion


            #region Image processing Gait analysis

            infraredImgCpy = null;
            grayImg = null;

            blobCount = 0;
            countFrames = 0;
            markersHistory = new List<List<PointF>>();
            isDrawingAxis = true;
            readImagePath = "";
            isLeftSagittalPlane = false;
            isCapturingAngles = false;

            #endregion

            #region Record TAB

            isRecordingPathSelected = false;
            recordingVideoPath = "";
            indexInfraredCamera = 0;

            #endregion

        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            #region Initialize Cleye cameras resources

            // Query for number of connected cameras
            numCameras = CLEyeCameraDevice.CameraCount;
            if (numCameras == 0)
            {
                System.Windows.MessageBox.Show("Could not find any PS3Eye cameras!");
                return;
            }
            output.Items.Add(string.Format("Found {0} CLEyeCamera devices", numCameras));
            // Show camera's UUIDs
            for (int i = 0; i < numCameras; i++)
            {
                output.Items.Add(string.Format("CLEyeCamera #{0} UUID: {1}", i + 1, CLEyeCameraDevice.CameraUUID(i)));
                tbxResultsPelvis.Text = CLEyeCameraDevice.CameraUUID(i).ToString();
            }
            // Create cameras, set some parameters and start capture
            if (numCameras >= 1)
            {
                cameraImage0.Device.Create(CLEyeCameraDevice.CameraUUID(0));
                //cameraImage0.Device.Rotation = 300;
                //cameraImage0.Device.HorizontalFlip = true;
                cam0 = true;
            }
            if (numCameras == 2)
            {
                cameraImage1.Device.Create(CLEyeCameraDevice.CameraUUID(1));
                //cameraImage1.Device.Rotation = 300;
                cameraImage1.Device.AutoGain = true;
                cameraImage1.Device.Gain = 80;
            
                cameraImage1.Device.AutoExposure = true;
                cameraImage1.Device.Exposure = 10;

                cameraImage1.Device.AutoWhiteBalance = true;
                cameraImage1.Device.WhiteBalanceRed = 4;
                cameraImage1.Device.WhiteBalanceGreen = 4;
                cameraImage1.Device.WhiteBalanceBlue = 4;

                cam1 = true;
            }
            #endregion

            indexInfraredCamera =  getIndexInfraredCamera();
            allCamerasConnectedStart();
            checkConnectedCameras();

        }

        private int getIndexInfraredCamera()
        {
            for (int i = 0; i < numCameras; i++)
            {
                if (CLEyeCameraDevice.CameraUUID(i).ToString() == "39f05022-8525-cde4-c19c-746220fec2e3")
                {
                    return i;
                }
            }
            return -1;
        }

        private void checkConnectedCameras()
        {
            if (cam0) ckbCamera00.IsChecked = true;
            if (cam1) ckbCamera01.IsChecked = true;

        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            #region closing cameras initialized

            if (numCameras >= 1)
            {
                cameraImage0.Device.Stop();
                cameraImage0.Device.Destroy();
            }
            if (numCameras == 2)
            {
                cameraImage1.Device.Stop();
                cameraImage1.Device.Destroy();
            }

            #endregion
        }

        private void recording_Timer_Tick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                allCamerasWriteJpeg(recordingVideoPath + "\\" + cameraFolder);
                //cameraImage0.Device.BitmapSource.DownloadCompleted += objImage_DownloadCompleted;
            }

            //update the timestamp of timeline video
            if (isPlaying && (Media.Source != null) && (Media.NaturalDuration.HasTimeSpan) && (!userIsDraggingSlider))
            {
                sliProgress.Minimum = 0;
                sliProgress.Maximum = Media.NaturalDuration.TimeSpan.TotalSeconds;
                sliProgress.Value = Media.Position.TotalSeconds;
            }
        }

        private void imagePlayer_Timer_Tick(object sender, EventArgs e)
        {
            if (imagePlayerValue <= sliImageProgress.Maximum)
            {
                updateImageFrame(imagePlayerValue);
                sliImageProgress.Value = imagePlayerValue;
                imagePlayerValue++;
            }
            else
            {
                timerImagePlayer.Stop();
                //imagePlayerValue = 0;
            }
        }

        private void setTimerIntervalImagePlayer()
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerSpeed = (int)(FPS * SpeedSlider.Value);
                timerImagePlayer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / imagePlayerSpeed);
            }
        }


        #region WriteJpeg not used
        //private void WriteJpeg(string fileName, int quality, BitmapSource bmp)
        //{
        //    //WORKS but with the PS3 EYE camera's frecuency is not syncronized. vy decrasing th image quality
        //    //the tproblem is fixed

        //    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        //    BitmapFrame outputFrame = BitmapFrame.Create(bmp);
        //    encoder.Frames.Add(outputFrame);
        //    encoder.QualityLevel = quality;

        //    using (FileStream file = File.OpenWrite(fileName))
        //    {
        //        encoder.Save(file);
        //    }

        //}
        #endregion

        //private void objImage_DownloadCompleted(object sender, EventArgs e)
        //{
        //    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        //    Guid photoID = System.Guid.NewGuid();
        //    String photolocation = isRecordingPathSelected + "\\" + photoID.ToString() + ".jpg";  //file name 

        //    encoder.Frames.Add(BitmapFrame.Create((BitmapImage)sender));

        //    using (var filestream = new FileStream(photolocation, FileMode.Create))
        //        encoder.Save(filestream);
        //} 

        public Bitmap bitmapSourceToBitmap(BitmapSource source)
        {
            Bitmap bmp = new Bitmap(
              source.PixelWidth,
              source.PixelHeight,
              System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            BitmapData data = bmp.LockBits(
              new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size),
              ImageLockMode.WriteOnly,
              System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            source.CopyPixels(
              Int32Rect.Empty,
              data.Scan0,
              data.Height * data.Stride,
              data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        public Bitmap ConvertToBitmap(string fileName)
        {
            Bitmap bitmap;
            using (Stream bmpStream = System.IO.File.Open(fileName, System.IO.FileMode.Open))
            {
                System.Drawing.Image image = System.Drawing.Image.FromStream(bmpStream);

                bitmap = new Bitmap(image);

            }
            return bitmap;
        }

        //paid dll
        public void createVideoFromImages()
        {
            // Activate the component
            converter.RegistrationName = "demo";
            converter.RegistrationKey = "demo";

            // Enable transition effects for the first and last slide
            converter.UseInEffectForFirstSlide = true;
            converter.UseOutEffectForLastSlide = true;

            // Add images and set slide durations and transition effects
            Slide slide;
            for (int i = 0; i < contFrames - 3; i++)
            {
                slide = converter.AddImageFromFileName(testMultiImagesFolder + cameraFolder + "01" + @"\img" + i + ".jpg");
                //slide.InEffect = TransitionEffectType.teFade;
                //slide.OutEffect = TransitionEffectType.teFade;
                slide.Duration = 10;//1000/FPS; 
            }

            // Set output video size
            converter.OutputWidth = 640;
            converter.OutputHeight = 480;

            // Set output video file name
            converter.OutputVideoFileName = saveVideoPath + "colorVideo.avi";

            // Run the conversion
            converter.RunAndWait();

            // Open the result video file in default webm player
            Process.Start(saveVideoPath + "colorVideo.avi");

        }

        #region AviFile Wrapper methods
        /* Source
         * http://www.codeproject.com/Articles/7388/A-Simple-C-Wrapper-for-the-AviFile-Library 
         */
        public void getFrameByIndex(int index)
        {
            AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\test2.avi", true);
            VideoStream aviStream = aviManager.GetVideoStream();
            aviStream.GetFrameOpen();
            aviStream.GetBitmap(index).Save(testMultiImagesFolder + "imgTest.jpg");
            aviStream.GetFrameClose();
            aviManager.Close();
        }

        public void getAllFramesFromVideo()
        {
            AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\test2.avi", true);
            VideoStream stream = aviManager.GetVideoStream();
            stream.GetFrameOpen();

            String path = @"D:\Projects\Gaitcom\testCreateFolder\cam0\";
            for (int n = 0; n < stream.CountFrames; n++)
            {
                //stream.ExportBitmap(n, path + n.ToString() + ".bmp");
                stream.GetBitmap(n).Save(path + "img" + n.ToString() + ".jpg");
            }

            stream.GetFrameClose();
        }

        public AviManager CopyToVideo(int startAtSecond, int stopAtSecond)
        {
            #region this code create copy of avi video
            //public AviManager CopyToVideo(int startAtSecond, int stopAtSecond)
            //{
            //    String newFileName = @"D:\Projects\Gaitcom2D\testMultiImages\testAviFilesImages\aviFileWapperTest.avi";

            //    AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi", true);

            //    AviManager newFile = new AviManager(newFileName, false);

            //    try
            //    {
            //        //copy video stream

            //        VideoStream videoStream = aviManager.GetVideoStream();

            //        int startFrameIndex =
            //             (int)videoStream.FrameRate * startAtSecond;
            //        int stopFrameIndex =
            //             (int)videoStream.FrameRate * stopAtSecond;

            //        videoStream.GetFrameOpen();
            //        Bitmap bmp = videoStream.GetBitmap(startFrameIndex);

            //        VideoStream newStream = newFile.AddVideoStream(
            //             false,
            //             videoStream.FrameRate,
            //             bmp);

            //        for (int n = startFrameIndex + 1;
            //                     n <= stopFrameIndex; n++)
            //        {
            //            bmp = videoStream.GetBitmap(n);
            //            newStream.AddFrame(bmp);
            //        }
            //        videoStream.GetFrameClose();

            //        //copy audio stream

            //        AudioStream waveStream = aviManager.GetWaveStream();

            //        Avi.AVISTREAMINFO streamInfo =
            //                         new Avi.AVISTREAMINFO();
            //        Avi.PCMWAVEFORMAT streamFormat =
            //                         new Avi.PCMWAVEFORMAT();
            //        int streamLength = 0;
            //        IntPtr ptrRawData = waveStream.GetStreamData(
            //            ref streamInfo,
            //            ref streamFormat,
            //            ref streamLength);

            //        int startByteIndex = waveStream.CountSamplesPerSecond
            //               * startAtSecond
            //               * waveStream.CountBitsPerSample / 8;

            //        int stopByteIndex = waveStream.CountSamplesPerSecond
            //               * stopAtSecond
            //               * waveStream.CountBitsPerSample / 8;

            //        ptrRawData =
            //          new IntPtr(ptrRawData.ToInt32() + startByteIndex);

            //        byte[] rawData =
            //          new byte[stopByteIndex - startByteIndex];
            //        Marshal.Copy(ptrRawData, rawData, 0, rawData.Length);

            //        streamInfo.dwLength = rawData.Length;
            //        streamInfo.dwStart = 0;

            //        IntPtr unmanagedRawData =
            //              Marshal.AllocHGlobal(rawData.Length);
            //        Marshal.Copy(rawData, 0, unmanagedRawData,
            //                                     rawData.Length);

            //        newFile.AddAudioStream(unmanagedRawData,
            //              streamInfo,
            //              streamFormat,
            //              rawData.Length);

            //    }
            //    catch (Exception ex)
            //    {
            //        newFile.Close();
            //        //throw ex;
            //    }
            //    return newFile;
            //}
            #endregion

            String newFileName = @"D:\Projects\Gaitcom2D\testMultiImages\testAviFilesImages\aviFileWapperTest.avi";

            AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi", true);

            AviManager newFile = new AviManager(newFileName, false);

            try
            {
                //copy video stream

                //VideoStream videoStream = GetVideoStream();

                int startFrameIndex = startAtSecond;
                int stopFrameIndex = stopAtSecond;

                Bitmap bmp = ConvertToBitmap(testMultiImagesFolder + cameraFolder + "01\\" + "img" + 0 + ".jpg");

                VideoStream newStream = newFile.AddVideoStream(
                     false,
                     FPS,
                     bmp);


                for (int n = startFrameIndex + 1; n <= stopFrameIndex; n++)
                {
                    bmp = ConvertToBitmap(testMultiImagesFolder + cameraFolder + "01\\" + "img" + n + ".jpg");
                    newStream.AddFrame(bmp);
                }

                //copy audio stream

                //AudioStream waveStream = aviManager.GetWaveStream();

                //Avi.AVISTREAMINFO streamInfo =
                //                 new Avi.AVISTREAMINFO();
                //Avi.PCMWAVEFORMAT streamFormat =
                //                 new Avi.PCMWAVEFORMAT();
                //int streamLength = 0;
                //IntPtr ptrRawData = waveStream.GetStreamData(
                //    ref streamInfo,
                //    ref streamFormat,
                //    ref streamLength);

                //int startByteIndex = waveStream.CountSamplesPerSecond
                //       * startAtSecond
                //       * waveStream.CountBitsPerSample / 8;

                //int stopByteIndex = waveStream.CountSamplesPerSecond
                //       * stopAtSecond
                //       * waveStream.CountBitsPerSample / 8;

                //ptrRawData =
                //  new IntPtr(ptrRawData.ToInt32() + startByteIndex);

                //byte[] rawData =
                //  new byte[stopByteIndex - startByteIndex];
                //Marshal.Copy(ptrRawData, rawData, 0, rawData.Length);

                //streamInfo.dwLength = rawData.Length;
                //streamInfo.dwStart = 0;

                //IntPtr unmanagedRawData =
                //      Marshal.AllocHGlobal(rawData.Length);
                //Marshal.Copy(rawData, 0, unmanagedRawData,
                //                             rawData.Length);

                //newFile.AddAudioStream(unmanagedRawData,
                //      streamInfo,
                //      streamFormat,
                //      rawData.Length);

            }
            catch (Exception ex)
            {
                newFile.Close();
                //throw ex;
            }

            return newFile;
        }

        #endregion

        #region Record TAB methods

        private void btnRecordingSavePath_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecordingPathSelected)
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog();

                DialogResult result = fbd.ShowDialog();

                if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    recordingVideoPath = fbd.SelectedPath;
                    //files = Directory.GetFiles(fbd.SelectedPath);

                    //Create new directories, if not exist, per each camera connected
                    createCameraFolders();
                    isRecordingPathSelected = true;

                }
            }

        }

        private void createCameraFolders()
        {
            for (int i = 0; i < numCameras; i++)
            {
                // If directory does not exist, create it. 
                string newCameraPath = recordingVideoPath+ "\\" + cameraFolder + i;
                if (!Directory.Exists(newCameraPath))
                {
                    Directory.CreateDirectory(newCameraPath);
                }
            }
        }


        private bool emptyFilesInCameraFolders()
        {
            string [] folders;
            for (int i = 0; i < numCameras; i++)
            {
                string pathDirectory = recordingVideoPath + "\\" + cameraFolder + i.ToString();
                if (Directory.Exists(pathDirectory))
                {
                    folders = Directory.GetFiles(pathDirectory);
                    if (folders.Length > 0)
                    {
                        return false;
                    }
                }
               
            }
            return true;
        }


        #endregion

        private void allCamerasWriteJpeg(string path)
        {
            if (indexInfraredCamera == 0 || indexInfraredCamera == -1)
            {
                if (cam0)
                    bitmapSourceToBitmap(cameraImage0.Device.BitmapSource).Save(path + "0\\" + "img" + contFrames + ".jpg", ImageFormat.Jpeg);
                if (cam1)
                    bitmapSourceToBitmap(cameraImage1.Device.BitmapSource).Save(path + "1\\" + "img" + contFrames + ".jpg", ImageFormat.Jpeg);
            }
            else if (indexInfraredCamera == 1)
            {
                if (cam0)
                    bitmapSourceToBitmap(cameraImage0.Device.BitmapSource).Save(path + "1\\" + "img" + contFrames + ".jpg", ImageFormat.Jpeg);
                if (cam1)
                    bitmapSourceToBitmap(cameraImage1.Device.BitmapSource).Save(path + "0\\" + "img" + contFrames + ".jpg", ImageFormat.Jpeg);
            }
            contFrames++;
        }

        private void allCamerasConnectedStop()
        {
            if (cam0) cameraImage0.Device.Stop();
            if (cam1) cameraImage1.Device.Stop();
        }

        private void allCamerasConnectedStart()
        {
            if (cam0) cameraImage0.Device.Start();
            if (cam1) cameraImage1.Device.Start();
        }

        #region Media player methods

        
        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {

            //OpenFileDialog openFileDialog = new OpenFileDialog();
            //openFileDialog.Filter = "Media files (*.mp3;*.mpg;*.mpeg;*.avi;*.wmv)|*.mp3;*.mpg;*.mpeg;*.avi;*.wmv|All files (*.*)|*.*";
            //if (openFileDialog.ShowDialog() == true)
            //{
            //    Media.Source = new Uri(openFileDialog.FileName);
            //    MediaName.Text = openFileDialog.FileName;
            //    readVideoPath = openFileDialog.FileName;

            //}

            #region imagePlayer

            sliImageProgress.Minimum = 0;
            sliImageProgress.Maximum = 0;
            sliImageProgress.Value = 0;

            try
            {
                if (!isImagePlayerDataLoaded)
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog();

                    DialogResult result = fbd.ShowDialog();

                    if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        recordingVideoPath = fbd.SelectedPath;
                        
                        // recordingVideoPath + "\\" + cameraFolder + i.ToString()

                        setImagePlayerValues(recordingVideoPath, 1, fbd.SelectedPath.Length);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Desea abrir otro folder de imagenes", "Advertencia", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                    isImagePlayerDataLoaded = false;
                    btnOpen_Click(sender,e);

                    //save work of therapist

                    //then allow to open other files 
                }
            }
            catch (Exception )
            {
                statusImagePlayerFailed();

            }

            #endregion
        }

        private void setImagePlayerValues(string selectedPath,int flagOpenFrom, int totalFrames)
        {
            
            string msgFrom = "";
            files = Directory.GetFiles(selectedPath);
            if(flagOpenFrom == 0)//Record Tab
            { 
                msgFrom = "Número de frames grabados: "; 
            }
            else if (flagOpenFrom ==1)//Player Tab
            {
                cam0 = false;
                cam1 = false;
                msgFrom = "Número de frames encontrados: ";
                if (Directory.Exists(selectedPath + "\\" + cameraFolder + "0") && (Directory.GetFiles(selectedPath + "\\" + cameraFolder + "0").Length != 0)) cam0 = true;
                if (Directory.Exists(selectedPath + "\\" + cameraFolder + "1") && (Directory.GetFiles(selectedPath + "\\" + cameraFolder + "1").Length != 0)) cam1 = true;
                if(cam0)
                    totalFrames = Directory.GetFiles(selectedPath + "\\" + cameraFolder + "0").Length; //this folder always will have data if one camera is connected at least
            }

            System.Windows.Forms.MessageBox.Show(msgFrom + totalFrames.ToString() + " en el siguiente directorio: " + selectedPath, "Message");
            //readImagePath = selectedPath;

            sliImageProgress.Maximum = totalFrames - 1; //TO DO : delimit the real frames to be readed, if there are more than two recoding in the same folder
            isImagePlayerDataLoaded = true;
            SpeedSlider.Value = DEFAULT_SPEED_SLIDER_VALUE;
            setTimerIntervalImagePlayer();
            statusImagePlayerOpened();
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (Media.Source != null)
            {
                Media.Play();
                timerRecording.Start();//update the timeline numbers
                isPlaying = true;
            }


            #region imagePlayer

            if (isImagePlayerDataLoaded)
            {
                isPlaying = true;
                timerImagePlayer.Start();
            }

            #endregion
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (Media.CanPause)
                Media.Pause();



            #region imagePlayer

            if (isImagePlayerDataLoaded)
            {
                isPlaying = false;
                timerImagePlayer.Stop();
            }

            #endregion

        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {

            if (Media.Source != null)
                Media.Stop();

            #region imagePlayer

            if (isImagePlayerDataLoaded)
            {
                isPlaying = false;
                timerImagePlayer.Stop();

                imagePlayerValue = 0;

                updateImageFrame(imagePlayerValue);
                sliImageProgress.Value = imagePlayerValue;

                statusImagePlayerEnded();
            }

            #endregion
        }

        private void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            if (isRecordingPathSelected)
            {
                if (!isRecording)
                {
                    if (!emptyFilesInCameraFolders())
                    {
                        //here the savepath is changed
                        messageRewriteRecordingFiles(sender, e);
                    }
                    
                    timerRecording.Start();
                    isRecording = true;
                    btnCapture.Content = "Stop";
                }
                else
                {

                    timerRecording.Stop();
                    isRecording = false;
                    btnCapture.Content = "Record";
                    setImagePlayerValues(recordingVideoPath + "\\" + cameraFolder + "0", 0,contFrames);

                    // paid dll 
                    //createVideoFromImages();

                    contFrames = 0;
                }
            }

        }

        private void messageRewriteRecordingFiles(object sender, RoutedEventArgs e)
        {
            DialogResult result = System.Windows.Forms.MessageBox.Show("La ruta seleccionada no esta vacia, posiblemente contenga grabaciones realizadas con anterioridad. ¿Desea sobre escribir los archivos existentes?. De lo contrario, seleccione NO, y determine una nueva ruta de almacenamineto.",
                       "Advertencia", MessageBoxButtons.YesNoCancel,
                       MessageBoxIcon.Warning);

            if (result == System.Windows.Forms.DialogResult.No)
            {
                isRecordingPathSelected = false;
                btnRecordingSavePath_Click(sender, e);
            }
            else if (result == System.Windows.Forms.DialogResult.Yes)
            {
                return;
            }

        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {

            //AviManager am = CopyToVideo(10, imgCont);

            //if (!Media.NaturalDuration.HasTimeSpan) return;

            //var position = Media.Position;
            //if (position.TotalSeconds < 5)
            //    Media.Position = TimeSpan.FromSeconds(0);
            //else
            //    Media.Position = position.Add(TimeSpan.FromSeconds(-5));

            #region imagePlayer

            if (isImagePlayerDataLoaded &&
                (imagePlayerValue > sliImageProgress.Minimum))
            {
                updateImageFrame(--imagePlayerValue);
            }
            #endregion
        }

        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            //if (!Media.NaturalDuration.HasTimeSpan) return;

            //var targetPosition = Media.Position.Add(TimeSpan.FromSeconds(5));
            //if (targetPosition > Media.NaturalDuration.TimeSpan)
            //    Media.Position = Media.NaturalDuration.TimeSpan;
            //else
            //    Media.Position = targetPosition;

            #region imagePlayer

            if (isImagePlayerDataLoaded &&
                imagePlayerValue < sliImageProgress.Maximum)
            {
                timerImagePlayer.Stop();
                updateImageFrame(++imagePlayerValue);
            }

            #endregion
        }

        private void btnDrawReferenceLines_Click(object sender, RoutedEventArgs e)
        {
            #region imagePlayer

            if (isImagePlayerDataLoaded)
            {

                if (!isDrawingAxis)
                {
                    isDrawingAxis = true;
                }
                else
                {
                    isDrawingAxis = false;
                }
                updateImageFrame(imagePlayerValue);

            }

            #endregion
        }

        private void btnForzeInitCameras_Click(object sender, RoutedEventArgs e)
        {
            getAllFramesFromVideo();
            //#region Initialize Cleye cameras resources

            //// Query for number of connected cameras
            //numCameras = CLEyeCameraDevice.CameraCount;
            //if (numCameras == 0)
            //{
            //    System.Windows.MessageBox.Show("Could not find any PS3Eye cameras!");
            //    return;
            //}
            //output.Items.Add(string.Format("Found {0} CLEyeCamera devices", numCameras));
            //// Show camera's UUIDs
            //for (int i = 0; i < numCameras; i++)
            //{
            //    output.Items.Add(string.Format("CLEyeCamera #{0} UUID: {1}", i + 1, CLEyeCameraDevice.CameraUUID(i)));
            //}
            //// Create cameras, set some parameters and start capture
            //if (numCameras >= 1)
            //{
            //    cameraImage0.Device.Create(CLEyeCameraDevice.CameraUUID(0));
            //    cam0 = true;
            //}
            //if (numCameras == 2)
            //{
            //    cameraImage1.Device.Create(CLEyeCameraDevice.CameraUUID(1));
            //    cam1 = true;
            //}
            //#endregion

        }

        private void Speed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Media != null)
            {
                Media.SpeedRatio = SpeedSlider.Value;
            }

            #region imagePlayer

            if (isImagePlayerDataLoaded)
            {
                setTimerIntervalImagePlayer();
            }

            #endregion
        }

        private void Media_MediaEnded(object sender, RoutedEventArgs e)
        {
            Status.Fill = System.Windows.Media.Brushes.Blue;
            isPlaying = false;
        }

        private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Status.Fill = System.Windows.Media.Brushes.Red;
            isPlaying = false;
        }

        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            Status.Fill = System.Windows.Media.Brushes.Green;
            ShowMediaInformation();
        }


        private void ShowMediaInformation()
        {
            var sb = new StringBuilder();

            var duration = Media.NaturalDuration.HasTimeSpan
                ? Media.NaturalDuration.TimeSpan.TotalMilliseconds.ToString(@"#s")
                : "No duration";
            sb.Append(duration);

            if (Media.HasVideo)
            {
                sb.Append(", video");
            }

            if (Media.HasAudio)
            {
                sb.Append(", audio");
            }

            MediaInformation.Text = sb.ToString();
        }

        private void sliProgress_DragStarted(object sender, DragStartedEventArgs e)
        {
            userIsDraggingSlider = true;
        }

        private void sliProgress_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            userIsDraggingSlider = false;
            Media.Position = TimeSpan.FromSeconds(sliProgress.Value);
        }

        private void sliProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //lblProgressStatus.Text = TimeSpan.FromSeconds(sliProgress.Value).ToString(@"hh\:mm\:ss\:ff");
        }

        #endregion

        #region imagePlayer

        private void sliImageProgress_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                isPlaying = false;
                timerImagePlayer.Stop();
            }
            //userIsDraggingSlider = true;
        }

        private void sliImageProgress_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                //timerImagePlayer.Start();
            }
            //userIsDraggingSlider = false;
            //Media.Position = TimeSpan.FromSeconds(sliProgress.Value);
        }

        private void sliImageProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                if (!isPlaying)
                    updateImageFrame(imagePlayerValue);
                //lblImageProgressStatus.Text = TimeSpan.FromSeconds(sliProgress.Value).ToString(@"hh\:mm\:ss\:ff");
            }

        }

        private void updateImageFrame(int imagePlayervalue)
        {
            if (isImagePlayerDataLoaded)
            {
                lblImageProgressStatus.Text = "Frame N°- " + imagePlayervalue.ToString();
                
                // set black frames if there are no Loaded images
                infraredBgrImage = new Image<Bgr, byte>(220, 140, new Bgr(System.Drawing.Color.Black));
                colorImage = new Image<Bgr, byte>(320,240,new Bgr(System.Drawing.Color.Black));
                
                if (cam0) infraredBgrImage = new Image<Bgr, Byte>(recordingVideoPath + "\\" + cameraFolder + @"0\\" + "img" + imagePlayervalue.ToString() + ".jpg");
                if (cam1) colorImage = new Image<Bgr, Byte>(recordingVideoPath + "\\" + cameraFolder + @"1\\" + "img" + imagePlayervalue.ToString() + ".jpg");

                //if (imagePlayervalue % 2 == 0)
                //{
                    ///horizontalCameraConfiguration();
                //}
                //verticalCameraConfigutation();

                if (!isLeftSagittalPlane)
                {
                    infraredBgrImage = infraredBgrImage.Flip(FLIP.HORIZONTAL);
                }

                GaitAnalysis(infraredBgrImage, colorImage, isCapturingAngles);


            }
        }

        private void btnSagittalPlane_Click(Object sender, RoutedEventArgs e)
        {
            if (!isLeftSagittalPlane)
            {
                isLeftSagittalPlane = true;
                btntSagittalPlane.Content = "Izquierda";

            }
            else 
            {
                isLeftSagittalPlane = false;
                btntSagittalPlane.Content = "Derecha";
            }

            imagePlayerValue = (int)sliImageProgress.Value;
            updateImageFrame(imagePlayerValue);
        }

        private void verticalCameraConfigutation()
        {
            // Vertical configuration. Pointing to the patient => IRcam (Top)  &  ColorCam (bottom)
            // OK
            colorImage = colorImage.Rotate(-180, new Bgr(0, 0, 0));
            //colorImage = colorImage.Flip(FLIP.HORIZONTAL);
        }

        private void horizontalCameraConfiguration()
        {
            // Horizontal configuration. Pointing to the patient => IRcam (right)  &  ColorCam (left)
            // OK
            colorImage = colorImage.Rotate(90, new Bgr(0, 0, 0));
            infraredBgrImage = infraredBgrImage.Rotate(-90, new Bgr(0, 0, 0));

        }

        private void sliderMinUmbral_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMinUmbralValue.Text = "MinUmbralValue: " + ((int)sliderMinUmbral.Value).ToString();
                updateImageFrame(imagePlayerValue);
            }
        }

        private void sliderMaxUmbral_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMaxUmbralValue.Text = "MaxUmbralValue: " + ((int)sliderMaxUmbral.Value).ToString();
                updateImageFrame(imagePlayerValue);
            }
        }

        private void sliderMinSizeBlob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMinSizeBlobValue.Text = "MinSizeBlob: " + ((int)sliderMinSizeBlob.Value).ToString();
                updateImageFrame(imagePlayerValue);
            }
        }

        private void sliderMaxSizeBlob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMaxSizeBlobValue.Text = "MaxSizeBlob: " + ((int)sliderMaxSizeBlob.Value).ToString();
                updateImageFrame(imagePlayerValue);
            }
        }

        private void statusImagePlayerEnded()
        {
            Status.Fill = System.Windows.Media.Brushes.Blue;
            isPlaying = false;
        }

        private void statusImagePlayerFailed()
        {
            Status.Fill = System.Windows.Media.Brushes.Red;
            isPlaying = false;
        }

        private void statusImagePlayerOpened()
        {
            Status.Fill = System.Windows.Media.Brushes.Green;
            ShowMediaInformation();
        }

        #endregion

        #region Image processing - Gait Analysis

        private void GaitAnalysis(Image<Bgr, byte> infraBgrImg, Image<Bgr, byte> colorImg, bool isCapturingAngles)
        {
            List<System.Drawing.PointF> lstRetMarkers = FindRetroreFlectiveMarkers(infraBgrImg);
            
            if (lstRetMarkers != null && lstRetMarkers.Count == 7)
            {
                Dictionary<string, PointF> dicMarkers = 
                    LabelingMarkersWithHumanReferencePoints(lstRetMarkers);

                DrawByColorsRetrorefelctiveMarkerLabels(dicMarkers, infraredImgCpy, 4, 2);
                CalculateRetroreflectiveMarkersCentroid(dicMarkers, infraredImgCpy, false);

                CalculatePelvisAngles(dicMarkers, infraredImgCpy, isDrawingAxis,isCapturingAngles);
                CalculateHipAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);
                CalculateKneeAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);
                float lowerPointY = CalculateAnkleAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);

                if (isCapturingAngles)
                {
                    /// before this method i need to have a list(Pair<bool(InitalComtact),angle>) per each unit 
                    /// in order to set the bool IC to TRUE or FLASE and know where IC begins  and end.
                    //InitialContact_AutomaticRecognition(lowerPointY, isCapturingAngles);
                }
               



                markersHistory.Add(lstRetMarkers);
            }
            
            //DrawLinesHoriontal(dicMarkers, infraredImgCpy, true);

            //colorImagePlayer.Source = ToBitmapSource(colorImg);
            //dataImagePlayer.Source = ToBitmapSource(infraredImgCpy);
            //binaryImagePlayer.Source = ToBitmapSource(grayImg);

            binaryImagePlayer.Source = ToBitmapSource(colorImg);
            colorImagePlayer.Source = ToBitmapSource(infraredImgCpy);
            dataImagePlayer.Source = ToBitmapSource(grayImg);

        }

        private void DrawByColorsRetrorefelctiveMarkerLabels(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, float radious,int thickness)
        {
            
            System.Drawing.PointF drawPointF;
            CircleF c;

            /// Calcaneus
            drawPointF = new System.Drawing.PointF(dicMarkers["Calcaneus"].X, dicMarkers["Calcaneus"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.Red), thickness);

            /// Foot
            drawPointF = new System.Drawing.PointF(dicMarkers["Foot"].X, dicMarkers["Foot"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.Yellow), thickness);
            
            /// Ankle
            drawPointF = new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.Orange), thickness);

            /// PosteriorSuperiorIliacSpine
            drawPointF = new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["PosteriorSuperiorIliacSpine"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.HotPink), thickness);

            /// AnteriorSuperiorIliacSpine
            drawPointF = new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.DarkSlateBlue), thickness);

            /// Knee
            drawPointF = new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.Green), thickness);

            /// AnteriorSuperiorIliacSpine
            drawPointF = new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y);
            c = new CircleF(drawPointF, radious);
            bgrImg.Draw(c, new Bgr(System.Drawing.Color.MediumPurple), thickness);
        }

        private List<System.Drawing.PointF> FindRetroreFlectiveMarkers(Image<Bgr, byte> bgrImg)
        {
            Contour<System.Drawing.Point> contours = null;
            List<System.Drawing.PointF> markers = null;

            infraredImgCpy = bgrImg.Copy();
            grayImg = null;

            grayImg = bgrImg.Convert<Gray, Byte>();

            // retrive values from Slider
            grayImg = grayImg.ThresholdBinary(new Gray(sliderMinUmbral.Value), new Gray(sliderMaxUmbral.Value));

            using (MemStorage stor = new MemStorage())
            {
                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                contours = grayImg.FindContours(
                    Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                    Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                    stor);

                markers = new List<System.Drawing.PointF>();
                blobCount = 0;
                for (int i = 0; contours != null; contours = contours.HNext)
                {
                    i++;

                    float markerPosX = contours.GetMinAreaRect().center.X;
                    float markerPosY = contours.GetMinAreaRect().center.Y;

                    MCvBox2D blobBox = contours.GetMinAreaRect();

                    System.Drawing.PointF pointF = new System.Drawing.PointF(markerPosX, markerPosY);


                    if ((contours.Area > Math.Pow(sliderMinSizeBlob.Value, 2)) && 
                        (contours.Area < Math.Pow(sliderMaxSizeBlob.Value, 2)))
                    {
                        blobCount++;

                        //DrawAllBlobsDetected(infraredImgCpy, blobBox);
                        //DrawAllBlobPositions(infraredImgCpy, blobBox);
                        //WriterAllBlobsPositions(blobBox);

                        markers.Add(new System.Drawing.PointF(blobBox.center.X, blobBox.center.Y));

                    } // end if

                } // end for

            } // end mem usage

            return markers;
        }

        private void DrawAllBlobsDetected(Image<Bgr, byte> bgrImg, MCvBox2D blobBox)
        {
            MCvScalar markerColor = new MCvScalar(0, 0, 255);

            CvInvoke.cvCircle(bgrImg,
                              new System.Drawing.Point((int)blobBox.center.X, (int)blobBox.center.Y),
                              1,
                              markerColor, -1,
                              LINE_TYPE.CV_AA,
                              0);

            CircleF c = new CircleF(new System.Drawing.PointF(blobBox.center.X, blobBox.center.Y), blobBox.size.Width / 2);
                                    bgrImg.Draw(c,
                                    new Bgr(System.Drawing.Color.Orange),
                                    1);
        }

        private void DrawAllBlobPositions(Image<Bgr, byte> brgImg, MCvBox2D blobBox)
        {
            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 0.5, 0.5);
            brgImg.Draw("  " + (blobBox.center.X).ToString() + "\n" + (blobBox.center.Y).ToString(), ref f, new System.Drawing.Point((int)blobBox.center.X, (int)blobBox.center.Y), new Bgr(121, 116, 40));
            //original_Frame.Draw(contours.Area.ToString(), ref f, new System.Drawing.Point((int)markerPosX, (int)markerPosY), new Bgr(121, 116, 40));
        }

        private void WriterAllBlobsPositions(MCvBox2D blobBox)
        {
            tbxResultsPelvis.AppendText("{" + blobBox.center.X.ToString() + " - " + blobBox.center.Y.ToString() + "}");
            tbxResultsPelvis.AppendText(Environment.NewLine);
        }

        private void CalculateRetroreflectiveMarkersCentroid(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis)
        {
            if (isDrawAxis)
            {

                float xm = (dicMarkers["AnteriorSuperiorIliacSpine"].X +
                            dicMarkers["PosteriorSuperiorIliacSpine"].X +
                            dicMarkers["Knee"].X +
                            dicMarkers["Ankle"].X +
                            dicMarkers["Foot"].X +
                            dicMarkers["Calcaneus"].X +
                            dicMarkers["Trochanter"].X
                            ) / 7;
                float ym = (dicMarkers["AnteriorSuperiorIliacSpine"].Y +
                          dicMarkers["PosteriorSuperiorIliacSpine"].Y +
                          dicMarkers["Knee"].Y +
                          dicMarkers["Ankle"].Y +
                          dicMarkers["Foot"].Y +
                          dicMarkers["Calcaneus"].Y +
                          dicMarkers["Trochanter"].Y
                          ) / 7;

                System.Drawing.PointF centroidAllMarkers = new System.Drawing.PointF(xm, ym);

                CircleF c = new CircleF(centroidAllMarkers, 10);
                bgrImg.Draw(c,
                    new Bgr(System.Drawing.Color.Aquamarine),
                    2);

            }
        }

        struct PairDistance
        {
            double dist1;
            double dist2;

            public double getDist1()
            {
                return dist1;
            }
            public void setDist1(double _dist1)
            {
                dist1 = _dist1;
            }
            public double getDist2()
            {
                return dist2;
            }
            public void setDist2(double _dist2)
            {
                dist2 = _dist2;
            }

        }

        private Dictionary<string, PointF> LabelingMarkersWithHumanReferencePoints(List<PointF> markers)
        {

            /// Creating a list of pairs where is saved the distances 
            /// between markers. This will facilitate the identification 
            /// of Calcaneus, ankle and foot markers

            int makersIterationLimit = 3;
            List<PairDistance> lstPairDist = new List<PairDistance>();

            for (int i = 0; i < makersIterationLimit; i++)
            {
                PairDistance pair = new PairDistance();
                bool firstDistCalculated = false;

                for (int j = 0; j < makersIterationLimit; j++)
                {
                    //if not the same marker
                    if (i != j)
                    {
                        double distance = CalculateEuclideanDistance(markers[i], markers[j]);

                        if (!firstDistCalculated)
                        {
                            pair.setDist1(distance);
                            firstDistCalculated = true;
                        }
                        else
                        {
                            pair.setDist2(distance);
                        }
                    }
                }
                lstPairDist.Add(pair);
            }


            /// Detecting the ankle and foot marker by minimum sum and maximum sum
            /// of distances, repectively.
            /// 

            double minSum = lstPairDist[0].getDist1() + lstPairDist[0].getDist2();
            double maxSum = minSum;

            int posAnkleMarker = 0;
            int posfootMarker = 0;

            for (int i = 1; i < lstPairDist.Count; i++)
            {
                double sumTemp = lstPairDist[i].getDist1() + lstPairDist[i].getDist2();
                if (minSum > sumTemp)
                {
                    minSum = sumTemp;
                    posAnkleMarker = i;
                }
                if (maxSum < sumTemp)
                {
                    maxSum = sumTemp;
                    posfootMarker = i;
                }
            }

            /// Detecting Calcaneus marker by a simple calculations
            /// 

            int posCalcaneusMarker = -1;

            if (posAnkleMarker + posfootMarker == 3)
            {
                posCalcaneusMarker = 0;
            }
            else
            {
                posCalcaneusMarker = 3 - posfootMarker - posAnkleMarker;
            }

           



            // Detectiting Iliac Spines, in order to do this must be determinates 
            // which of the two last markers of the array are which one ASIS and PSIS

            //assuming that EIPS Marker is in the last position, otherwise, this
            //will be  updated
            int posPosteriorSuperiorIliacSpineMarker = 6;
            int posAnteriorSuperiorIliacSpineMarker = 5;

            if (markers[posPosteriorSuperiorIliacSpineMarker].Y > markers[posAnteriorSuperiorIliacSpineMarker].Y)
            {
                posPosteriorSuperiorIliacSpineMarker = 5;
                posAnteriorSuperiorIliacSpineMarker = 6;
            }

           

            /// labeling the marker by its names
            /// 

            Dictionary<String, PointF> dicJoints = new Dictionary<string, PointF>();

            for (int i = 0; i < markers.Count; i++)
            {
                string keyJoint = "";
                PointF markerPos = new PointF();
                if (i == posAnkleMarker)
                {
                    keyJoint = "Ankle";
                    markerPos.X = markers[posAnkleMarker].X;
                    markerPos.Y = markers[posAnkleMarker].Y;
                }

                if (i == posCalcaneusMarker)
                {
                    keyJoint = "Calcaneus";
                    markerPos.X = markers[posCalcaneusMarker].X;
                    markerPos.Y = markers[posCalcaneusMarker].Y;
                }

                if (i == posfootMarker)
                {
                    keyJoint = "Foot";
                    markerPos.X = markers[posfootMarker].X;
                    markerPos.Y = markers[posfootMarker].Y;
                }

                if (i == 3) //fixed position in the array (decresed ordered by "Y" position )
                {
                    keyJoint = "Knee";
                    markerPos.X = markers[i].X;
                    markerPos.Y = markers[i].Y;
                }

                if (i == 4)
                {
                    keyJoint = "Trochanter";
                    markerPos.X = markers[i].X;
                    markerPos.Y = markers[i].Y;
                }

                if (i == posPosteriorSuperiorIliacSpineMarker)
                {
                    keyJoint = "PosteriorSuperiorIliacSpine";
                    markerPos.X = markers[i].X;
                    markerPos.Y = markers[i].Y;
                }

                if (i == posAnteriorSuperiorIliacSpineMarker)
                {
                    keyJoint = "AnteriorSuperiorIliacSpine";
                    markerPos.X = markers[i].X;
                    markerPos.Y = markers[i].Y;
                }

                dicJoints.Add(keyJoint, markerPos);

            }

            return dicJoints;
        }

        private double CalculateEuclideanDistance(PointF pointF1, PointF pointF2)
        {
            return Math.Sqrt(Math.Pow(pointF1.X - pointF2.X, 2) + Math.Pow(pointF1.Y - pointF2.Y, 2));
        }

        private void CalculateHipAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
        {
            /// calculating longitudinal axis
            LineSegment2DF iliacSpainLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X,dicMarkers["PosteriorSuperiorIliacSpine"].Y),
                new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y)
            );

            LineSegment2DF femurLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y),
                new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y)
            );

            LineSegment2DF verticalAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Knee"].Y),
                new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y)
            );

            /// drawing axis
            if (isDrawAxis)
            {
                bgrImg.Draw(iliacSpainLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                bgrImg.Draw(femurLogAxis, new Bgr(System.Drawing.Color.Blue), 1);
                bgrImg.Draw(verticalAxis, new Bgr(System.Drawing.Color.Yellow), 1);
            }

            /// ankle angle calculation
            double hipAngle;
            if (!isLeftSagittalPlane)// right
            {
                hipAngle = femurLogAxis.GetExteriorAngleDegree(iliacSpainLogAxis) - 90;
            }
            else //left
            {
                hipAngle = iliacSpainLogAxis.GetExteriorAngleDegree(femurLogAxis) - 90;
            }

            //Wiliam requirement, hes method to calculate the hip angles
            //double angleEmguHip = (-1) * femurLogAxis.GetExteriorAngleDegree(verticalAxis);
            //tbxResultsAnkle.AppendText(angleEmguHip.ToString());
            //tbxResultsAnkle.AppendText(Environment.NewLine);

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1, 1);
            bgrImg.Draw(((int)hipAngle).ToString(), 
                ref f, 
                new System.Drawing.Point((int)dicMarkers["AnteriorSuperiorIliacSpine"].X + 10, 
                    (int)dicMarkers["AnteriorSuperiorIliacSpine"].Y), 
                new Bgr(121, 116, 40));

            if (isCapturingAngles)
            {
                tbxResultsHip.AppendText(hipAngle.ToString());
                tbxResultsHip.AppendText(Environment.NewLine);
            }
           
        }

        private void CalculatePelvisAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
        {
            /// calculating longitudinal axis
            LineSegment2DF iliacSpainLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["PosteriorSuperiorIliacSpine"].Y),
                new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y));

            LineSegment2DF horizontalLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y),
                 new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y));

            /// drawing axis
            if (isDrawAxis)
            {
                bgrImg.Draw(iliacSpainLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                bgrImg.Draw(horizontalLogAxis, new Bgr(System.Drawing.Color.Blue), 1);
            }

            /// ankle angle calculation
            double ankleAngle;
            if (!isLeftSagittalPlane)// right
            {
                ankleAngle = 180 - iliacSpainLogAxis.GetExteriorAngleDegree(horizontalLogAxis);
            }
            else //left
            {
                ankleAngle = 180 - horizontalLogAxis.GetExteriorAngleDegree(iliacSpainLogAxis);
            }
            
            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1, 1);
            bgrImg.Draw(((int)ankleAngle).ToString(), 
                ref f, 
                new System.Drawing.Point((int)dicMarkers["PosteriorSuperiorIliacSpine"].X, 
                    (int)dicMarkers["PosteriorSuperiorIliacSpine"].Y), 
                new Bgr(121, 116, 40)
            );

            if (isCapturingAngles)
            {
                tbxResultsPelvis.AppendText(ankleAngle.ToString());
                tbxResultsPelvis.AppendText(Environment.NewLine);
            }
        }

        private void CalculateKneeAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
        {
            /// calculating longitudinal axis
            LineSegment2DF femurLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y),
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y)
            );

            LineSegment2DF tibiaLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y),
                new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y)
            );

            /// drawing axis
            if (isDrawAxis)
            {
                bgrImg.Draw(tibiaLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                bgrImg.Draw(femurLogAxis, new Bgr(System.Drawing.Color.Red), 1);
            }

            double kneeAngle;
            if (!isLeftSagittalPlane)// right
            {
                kneeAngle = femurLogAxis.GetExteriorAngleDegree(tibiaLogAxis);
            }
            else //left
            {
                kneeAngle = tibiaLogAxis.GetExteriorAngleDegree(femurLogAxis);
            }
            
            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1.0, 1.0);
            bgrImg.Draw(((int)kneeAngle).ToString(), 
                ref f, 
                new System.Drawing.Point((int)dicMarkers["Knee"].X - 50, (int)dicMarkers["Knee"].Y), 
                new Bgr(255, 0, 0)
            );

            if (isCapturingAngles)
            {
                tbxResultsKnee.AppendText(Math.Abs(kneeAngle).ToString());
                tbxResultsKnee.AppendText(Environment.NewLine);
            }
        }

        private float CalculateAnkleAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
        {

            /// finding the missing P(x,y) that form a perfect triangle
            /// Source => http://www.freemathhelp.com/forum/threads/82575-need-help-finding-3rd-set-of-coordinates-to-a-right-triangle/page2
            /// 
            float perfecTriangleMissingPointX = dicMarkers["Ankle"].X - (dicMarkers["Knee"].Y - dicMarkers["Ankle"].Y);
            float perfecTriangleMissingPointY = dicMarkers["Ankle"].Y - (dicMarkers["Ankle"].X - dicMarkers["Knee"].X);

            if (!isLeftSagittalPlane)
            {
                perfecTriangleMissingPointX = dicMarkers["Ankle"].X + (dicMarkers["Knee"].Y - dicMarkers["Ankle"].Y);
                perfecTriangleMissingPointY = dicMarkers["Ankle"].Y + (dicMarkers["Ankle"].X - dicMarkers["Knee"].X);
            }

            /// calculating longitudinal axis
            LineSegment2DF tibiaLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y),
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y)
            );

            LineSegment2DF retropieLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["Calcaneus"].X, dicMarkers["Calcaneus"].Y),
                new System.Drawing.PointF(dicMarkers["Foot"].X, dicMarkers["Foot"].Y)
            );

            
            //// Hipothesis -> the lower P(y) from perpendicular axis from tibiaAxis is when INITIAL CONTACT SHOW UP
            double perfecTriangleMissingPointXCorrectionINITIAL_CONTACT_CIO = 0;
            double perfecTriangleMissingPointXCorrection = 0;
            if (!isLeftSagittalPlane)
            {
                perfecTriangleMissingPointXCorrectionINITIAL_CONTACT_CIO = tibiaLogAxis.Length;
                perfecTriangleMissingPointXCorrection = perfecTriangleMissingPointX - dicMarkers["Ankle"].X;
            }

            LineSegment2DF perpendicularTibiaLongAxis = new LineSegment2DF(

                new System.Drawing.PointF(perfecTriangleMissingPointX , perfecTriangleMissingPointY),
                
                /// shows like a kinect Forces on the feet
                //new System.Drawing.PointF(perfecTriangleMissingPointX - (float)perfecTriangleMissingPointXCorrectionINITIAL_CONTACT_CIO, perfecTriangleMissingPointY),
                //new System.Drawing.PointF(perfecTriangleMissingPointX - (float)(2 * perfecTriangleMissingPointXCorrection), perfecTriangleMissingPointY),
                
                
                new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y));

            /// drawing axis
            if (isDrawAxis)
            {
                bgrImg.Draw(tibiaLogAxis, new Bgr(System.Drawing.Color.Yellow), 1);
                bgrImg.Draw(retropieLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                bgrImg.Draw(perpendicularTibiaLongAxis, new Bgr(System.Drawing.Color.Green), 1);
            }

            /// ankle angle calculation
            double ankleAngle;
            
            if (!isLeftSagittalPlane)// right
            {
                ankleAngle = retropieLogAxis.GetExteriorAngleDegree(perpendicularTibiaLongAxis);
            }
            else //left
            {
                ankleAngle = perpendicularTibiaLongAxis.GetExteriorAngleDegree(retropieLogAxis);
            }

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1.0, 1.0);
            bgrImg.Draw(((int)ankleAngle).ToString(), 
                ref f, 
                new System.Drawing.Point((int)dicMarkers["Calcaneus"].X, (int)dicMarkers["Calcaneus"].Y), 
                new Bgr(121, 116, 40)
            );

            if (isCapturingAngles)
            {
                tbxResultsAnkle.AppendText(ankleAngle.ToString());
                tbxResultsAnkle.AppendText(Environment.NewLine);
            }

            //return the P(y) detecting the initial contact
            return perfecTriangleMissingPointY;

        }

        private void DrawLinesHoriontal(List<System.Drawing.PointF> markersF, Image<Bgr, byte> bgrImg)
        {
            for (int i = 0; i < markersF.Count; i++)
            {
                LineSegment2D line1 = new LineSegment2D(new System.Drawing.Point(0, (int)markersF[i].Y),
                    new System.Drawing.Point((int)markersF[i].X, (int)markersF[i].Y));

                bgrImg.Draw(line1, new Bgr(System.Drawing.Color.Red), 1);
            }

            countFrames++;
        }

        private double findAngleNotUsed(Dictionary<string, PointF> markersF)
        {
            double a = Math.Pow(markersF["Knee"].X - markersF["Ankle"].X, 2) + Math.Pow(markersF["Knee"].Y - markersF["Ankle"].Y, 2);
            double b = Math.Pow(markersF["Knee"].X - markersF["Trochanter"].X, 2) + Math.Pow(markersF["Knee"].Y - markersF["Trochanter"].Y, 2);
            double c = Math.Pow(markersF["Trochanter"].X - markersF["Ankle"].X, 2) + Math.Pow(markersF["Trochanter"].Y - markersF["Ankle"].Y, 2);

            return Math.Abs(Math.Acos((a + b - c) / Math.Sqrt(4 * a * b)) * 180 / Math.PI - 180);
        }


        [System.Runtime.InteropServices.DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);
        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
        /// </summary>
        /// <param name="image">The Emgu CV Image</param>
        /// <returns>The equivalent BitmapSource</returns>
        public BitmapSource ToBitmapSource(IImage image)
        {
            using (Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }
        #endregion

    }
}


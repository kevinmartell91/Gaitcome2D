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
using GleamTech;
using GleamTech.VideoUltimate;
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
        string camaraFolder;
        //int imageQualityWriter;
        int imgCont;
        DispatcherTimer timer = new DispatcherTimer();
        List<ImageSource> lstImgSour;
        private bool userIsDraggingSlider = false;

        // paid alternative
        ImageToVideo converter;

        bool cam01, cam02;

        #region imagePlayer

        DispatcherTimer timerImagePlayer = new DispatcherTimer();

        bool isImagePlayerDataLoaded;
        int imagePlayerValue;
        int imagePlayerSpeed;
        double DEFAULT_SPEED_SLIDER_VALUE;
        string[] files;

        #endregion

        #region Image processing Gait analysis

        Image<Bgr, byte> infraredImgCpy;
        Image<Gray, byte> grayImg;
        Image<Gray, byte> binaryImg;

        int blobCount;
        int countFrames;
        List<List<System.Drawing.PointF>> markersHistory;
        List<double> angles;
        bool isDrawAxis;
        string readImagePath;

        #endregion


        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
            timer.Tick += new EventHandler(timer_Tick);
            //timer.Interval = 

            numCameras = 0;
            FPS = 75;
            //imageQualityWriter = 30;    // 0 - 100 
            isRecording = false;        //true or false
            isPlaying = false;          //true or false
            readVideoPath = @"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi";
            saveVideoPath = @"D:\Projects\Gaitcom\testMultiImages\";
            testMultiImagesFolder = @"D:\Projects\Gaitcom\testMultiImages\";
            camaraFolder = @"cam";
            cam01 = cam02 = false;
            imgCont = 0;

            // paid alternative
            converter = new ImageToVideo();

            ImageSourceConverter c = new ImageSourceConverter();
            lstImgSour = new List<ImageSource>();


            #region imagePlayer

            timerImagePlayer.Tick += new EventHandler(timerImagePlayer_Tick);
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
            angles = new List<double>();
            isDrawAxis = true;
            readImagePath = "";

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
            }
            // Create cameras, set some parameters and start capture
            if (numCameras >= 1)
            {
                cameraImage1.Device.Create(CLEyeCameraDevice.CameraUUID(0));
                cam01 = true;
            }
            if (numCameras == 2)
            {
                cameraImage2.Device.Create(CLEyeCameraDevice.CameraUUID(1));
                cam02 = true;
            }
            #endregion

        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            #region closing cameras initialized

            if (numCameras >= 1)
            {
                cameraImage1.Device.Stop();
                cameraImage1.Device.Destroy();
            }
            if (numCameras == 2)
            {
                cameraImage2.Device.Stop();
                cameraImage2.Device.Destroy();
            }

            #endregion
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                allCamerasWriteJpeg();
            }

            //update the timestamp of timeline video
            if (isPlaying && (Media.Source != null) && (Media.NaturalDuration.HasTimeSpan) && (!userIsDraggingSlider))
            {
                sliProgress.Minimum = 0;
                sliProgress.Maximum = Media.NaturalDuration.TimeSpan.TotalSeconds;
                sliProgress.Value = Media.Position.TotalSeconds;
            }

        }

        private void timerImagePlayer_Tick(object sender, EventArgs e)
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
            for (int i = 0; i < imgCont - 3; i++)
            {
                slide = converter.AddImageFromFileName(testMultiImagesFolder + camaraFolder + "01" + @"\img" + i + ".jpg");
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
            AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi", true);
            VideoStream aviStream = aviManager.GetVideoStream();
            aviStream.GetFrameOpen();
            aviStream.GetBitmap(index).Save(testMultiImagesFolder + "imgTest.jpg");
            aviStream.GetFrameClose();
            aviManager.Close();
        }

        public void getAllFramesFromVideo()
        {
            AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi", true);
            VideoStream stream = aviManager.GetVideoStream();
            stream.GetFrameOpen();

            String path = @"D:\Projects\Gaitcom2D\testMultiImages\testAviFilesImages\";
            for (int n = 0; n < stream.CountFrames; n++)
            {
                //stream.ExportBitmap(n, path + n.ToString() + ".bmp");
                stream.GetBitmap(n).Save(path + n.ToString() + ".jpg");
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

                Bitmap bmp = ConvertToBitmap(testMultiImagesFolder + camaraFolder + "01\\" + "img" + 0 + ".jpg");

                VideoStream newStream = newFile.AddVideoStream(
                     false,
                     FPS,
                     bmp);


                for (int n = startFrameIndex + 1; n <= stopFrameIndex; n++)
                {
                    bmp = ConvertToBitmap(testMultiImagesFolder + camaraFolder + "01\\" + "img" + n + ".jpg");
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

        private void allCamerasWriteJpeg()
        {
            if (cam01)
                bitmapSourceToBitmap(cameraImage1.Device.BitmapSource).Save(testMultiImagesFolder + camaraFolder + "01\\" + "img" + imgCont + ".jpg", ImageFormat.Jpeg);
            if (cam02)
                bitmapSourceToBitmap(cameraImage2.Device.BitmapSource).Save(testMultiImagesFolder + camaraFolder + "02\\" + "img" + imgCont + ".jpg", ImageFormat.Jpeg);

            //GetBitmap(cameraImage2.Device.BitmapSource).Save(@"D:\Projects\test_images_from_ps3_02\img" + imgCont + ".jpg", ImageFormat.Jpeg);

            imgCont++;
        }

        private void allCamerasConnectedStop()
        {
            if (cam01) cameraImage1.Device.Stop();
            if (cam02) cameraImage2.Device.Stop();
        }

        private void allCamerasConnectedStart()
        {
            if (cam01) cameraImage1.Device.Start();
            if (cam02) cameraImage2.Device.Start();
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
                        files = Directory.GetFiles(fbd.SelectedPath);
                        System.Windows.Forms.MessageBox.Show("Files found: " + files.Length.ToString() + "  ||  " + "Path:  " + fbd.SelectedPath, "Message");
                        readImagePath = fbd.SelectedPath;

                        sliImageProgress.Maximum = Directory.GetFiles(fbd.SelectedPath).Length - 1;
                        isImagePlayerDataLoaded = true;
                        SpeedSlider.Value = DEFAULT_SPEED_SLIDER_VALUE;
                        setTimerIntervalImagePlayer();
                        statusImagePlayerOpened();
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Desea abrir otro folder de imagenes", "Advertencia", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                    isImagePlayerDataLoaded = false;

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

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (Media.Source != null)
            {
                Media.Play();
                timer.Start();//update the timeline numbers
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
            if (!isRecording)
            {
                allCamerasConnectedStart();
                timer.Start();
                isRecording = true;
            }
            else
            {
                allCamerasConnectedStop();
                timer.Stop();
                isRecording = false;
                // paid dll 
                //createVideoFromImages();

                //imgCont = 0;
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

                if (!isDrawAxis)
                {
                    isDrawAxis = true;
                }
                else
                {
                    isDrawAxis = false;
                }
                updateImageFrame(imagePlayerValue);

            }

            #endregion
        }

        private void btnForzeInitCameras_Click(object sender, RoutedEventArgs e)
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
            }
            // Create cameras, set some parameters and start capture
            if (numCameras >= 1)
            {
                cameraImage1.Device.Create(CLEyeCameraDevice.CameraUUID(0));
                cam01 = true;
            }
            if (numCameras == 2)
            {
                cameraImage2.Device.Create(CLEyeCameraDevice.CameraUUID(1));
                cam02 = true;
            }
            #endregion

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
                //desordered enumeration in reding files by index, change the named files at saving frames moment
                lblImageProgressStatus.Text = "Frame N°- " + imagePlayervalue.ToString();
                Uri framePath = new Uri(testMultiImagesFolder + camaraFolder + "01" + @"\img" + imagePlayervalue.ToString() + ".jpg");
                //Uri framePath = new Uri(files[imagePlayervalue]);
                //imgPlayer.Source = new BitmapImage(framePath);


                //GaitAnalysis(new Image<Bgr, Byte>(framePath.ToString()), false);

                //Image<Bgr, byte> imageGait = new Image<Bgr, Byte>("D:\\Projects\\Gaitcom2D\\testMultiImages\\test_images_from_AVI\\" + imagePlayervalue.ToString() + ".jpg");
                Image<Bgr, byte> infraredImage = new Image<Bgr, Byte>(readImagePath + "\\img" + imagePlayervalue.ToString() + ".jpg");
                Image<Bgr, byte> colorImage = new Image<Bgr, Byte>(testMultiImagesFolder + camaraFolder + "02" + @"\img" + imagePlayervalue.ToString() + ".jpg");



                //if (imagePlayervalue % 2 == 0)
                //{
                //    imageGait = imageGait.Flip(FLIP.HORIZONTAL);
                //}
                GaitAnalysis(infraredImage,colorImage, false);


            }
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

        void readAllFramesFromVideo(string videoSourcePath)
        {
            using (var videoFrameReader = new VideoFrameReader(videoSourcePath))
            {
                var frameIndex = 0;
                var videoDuration = videoFrameReader.Duration.TotalMilliseconds;
                var totalFrames = videoFrameReader.GetEnumerator();
                ImageSourceConverter c = new ImageSourceConverter();

                while (videoFrameReader.Read())
                {
                    ////http://docs.gleamtech.com/videoultimate/html/using-videoultimate-in-a-project.htm
                    //Console.WriteLine("Coded Frame Number: " + videoFrameReader.CurrentFrameNumber);
                    //Console.WriteLine("Frame Index: " + frameIndex++);

                    using (var frame = videoFrameReader.GetFrame()) //Do something with frame
                    {
                        //int frameBegin = 100;
                        //int frameEnd =103;
                        //if (frameBegin <= frameIndex && frameIndex <= frameEnd)
                        if (true)
                        {
                            //videoFrameReader.Duration.ToString(
                            frame.Save(@"D:\Projects\test_images_from_AVI\img" + frameIndex + ".jpg", ImageFormat.Jpeg);
                            //ImageSource imageSource = new BitmapImage(new Uri(@"D:\Projects\test_images_from_ps3_01\img" + frameIndex + ".jpg"));
                            //lstImgSour.Add(imageSource);
                        }
                        frameIndex++;
                    }

                }
            }
        }

        #region Image processing - Gait Analysis

        private void GaitAnalysis(Image<Bgr, byte> infraredImg, Image<Bgr, byte> colorImg, bool captureAngles)
        {
            Contour<System.Drawing.Point> contours = null;
            List<System.Drawing.PointF> markers = null;

            MCvScalar markerColor;

            infraredImgCpy = infraredImg.Copy();
            grayImg = null;

            grayImg = infraredImg.Convert<Gray, Byte>();
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

                    MCvBox2D box = contours.GetMinAreaRect();

                    if ((contours.Area > Math.Pow(sliderMinSizeBlob.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSizeBlob.Value, 2)))
                    {

                        blobCount++;
                        markerColor = new MCvScalar(0, 0, 255);

                        CvInvoke.cvCircle(colorImg,
                            new System.Drawing.Point((int)markerPosX, (int)markerPosY),
                            1,
                            markerColor, -1,
                            LINE_TYPE.CV_AA,
                            0);

                        System.Drawing.PointF pointF = new System.Drawing.PointF(markerPosX, markerPosY);

                        CircleF c = new CircleF(pointF, box.size.Width / 2);
                        colorImg.Draw(c,
                            new Bgr(System.Drawing.Color.Orange),
                            1);

                        markers.Add(pointF);

                        /// Changes August
                        //writerTextMarkerPosition(markerPosX, markerPosY);
                        //showMarkerPosition(markerPosX, markerPosY);

                    } // end if

                } // end for

            } // end mem usage


            if (markers.Count == 7)
            {
                Dictionary<string, PointF> dicMarkers = labelMarkers(markers, infraredImgCpy, captureAngles);
                CalculateKneeAngles(dicMarkers, infraredImgCpy, captureAngles);
                CalculateAnkleAngles(dicMarkers, infraredImgCpy, captureAngles);
                CalculatePelvisAngles(dicMarkers, infraredImgCpy, captureAngles);
                CalculateHipAngles(dicMarkers, infraredImgCpy, captureAngles);
                CalculateCentroid(dicMarkers, infraredImgCpy, captureAngles);

                markersHistory.Add(markers);
            }

            //DrawLinesHoriontal(markers, original_Frame, captureAngles);

            colorImagePlayer.Source = ToBitmapSource(colorImg);
            dataImagePlayer.Source = ToBitmapSource(infraredImgCpy);
            binaryImagePlayer.Source = ToBitmapSource(grayImg);

            //txtBlobCount.Text = blobCount.ToString();


        }

        private void CalculateCentroid(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> original_Frame, bool captureAngles)
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
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Aquamarine),
                2);

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

        private Dictionary<string, PointF> labelMarkers(List<PointF> markers, Image<Bgr, byte> original_Frame, bool captureAngles)
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
                        double distance = calculateEuclideanDistance(markers[i], markers[j]);

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

            /// testing by drawing
            /// 

            System.Drawing.PointF calcaneusPointF = new System.Drawing.PointF(markers[posCalcaneusMarker].X, markers[posCalcaneusMarker].Y);
            System.Drawing.PointF footPointF = new System.Drawing.PointF(markers[posfootMarker].X, markers[posfootMarker].Y);
            System.Drawing.PointF anklePointF = new System.Drawing.PointF(markers[posAnkleMarker].X, markers[posAnkleMarker].Y);

            CircleF c = new CircleF(calcaneusPointF, 10);
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Red),
                2);
            c = new CircleF(anklePointF, 10);
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Orange),
                2);
            c = new CircleF(footPointF, 10);
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Yellow),
                2);


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

            /// testing by drawing
            /// 

            System.Drawing.PointF posteriorPointF = new System.Drawing.PointF(markers[posPosteriorSuperiorIliacSpineMarker].X, markers[posPosteriorSuperiorIliacSpineMarker].Y);
            System.Drawing.PointF anteriorPointF = new System.Drawing.PointF(markers[posAnteriorSuperiorIliacSpineMarker].X, markers[posAnteriorSuperiorIliacSpineMarker].Y);

            c = new CircleF(posteriorPointF, 10);
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Red),
                2);
            c = new CircleF(anteriorPointF, 10);
            original_Frame.Draw(c,
                new Bgr(System.Drawing.Color.Yellow),
                2);


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

        private double calculateEuclideanDistance(PointF pointF1, PointF pointF2)
        {
            return Math.Sqrt(Math.Pow(pointF1.X - pointF2.X, 2) + Math.Pow(pointF1.Y - pointF2.Y, 2));
        }

        private void showMarkerPosition(float markerPosX, float markerPosY)
        {
            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 0.5, 0.5);
            infraredImgCpy.Draw("  " + (markerPosX).ToString() + "\n" + (markerPosY).ToString(), ref f, new System.Drawing.Point((int)markerPosX, (int)markerPosY), new Bgr(121, 116, 40));
            //original_Frame.Draw(contours.Area.ToString(), ref f, new System.Drawing.Point((int)markerPosX, (int)markerPosY), new Bgr(121, 116, 40));
        }

        private void writerTextMarkerPosition(float markerPosX, float markerPosY)
        {
            //tbxResults.AppendText("{" + markerPosX.ToString() + " - " + markerPosY.ToString() + "}");
            //tbxResults.AppendText(Environment.NewLine);
        }

        private void CalculateHipAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> imageToPutData, bool captureAngles)
        {
            /// calculating longitudinal axis
            /// 
            LineSegment2DF iliacSpainLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["PosteriorSuperiorIliacSpine"].Y),
                new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y));

            LineSegment2DF femurLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y),
               new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y));

            /// drawing axis
            /// 
            if (isDrawAxis)
            {
                imageToPutData.Draw(iliacSpainLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                imageToPutData.Draw(femurLogAxis, new Bgr(System.Drawing.Color.Blue), 1);
            }

            /// ankle angle calculation
            double angleEmguAnkle1 = iliacSpainLogAxis.GetExteriorAngleDegree(femurLogAxis) - 90;
            double angleEmguAnkle2 = femurLogAxis.GetExteriorAngleDegree(iliacSpainLogAxis) - 90;



            double angle = findAngle(dicMarkers);
            // double angle = angleEmgu;

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1, 1);
            imageToPutData.Draw(((int)angleEmguAnkle1).ToString(), ref f, new System.Drawing.Point((int)dicMarkers["AnteriorSuperiorIliacSpine"].X + 10, (int)dicMarkers["AnteriorSuperiorIliacSpine"].Y), new Bgr(121, 116, 40));

            if (captureAngles)
                angles.Add(angle);

            tbxResultsHip.AppendText(angleEmguAnkle1.ToString());
            tbxResultsHip.AppendText(Environment.NewLine);
        }

        private void CalculatePelvisAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> imageToPutData, bool captureAngles)
        {
            /// calculating longitudinal axis
            /// 
            LineSegment2DF iliacSpainLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["PosteriorSuperiorIliacSpine"].Y),
                new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y));

            LineSegment2DF horizontalLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["AnteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y),
                 new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["AnteriorSuperiorIliacSpine"].Y));

            /// drawing axis
            /// 
            if (isDrawAxis)
            {
                imageToPutData.Draw(iliacSpainLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                imageToPutData.Draw(horizontalLogAxis, new Bgr(System.Drawing.Color.Blue), 1);
            }
            /// ankle angle calculation
            double angleEmguAnkle1 = 180 - horizontalLogAxis.GetExteriorAngleDegree(iliacSpainLogAxis);
            double angleEmguAnkle2 = 180 - iliacSpainLogAxis.GetExteriorAngleDegree(horizontalLogAxis);

            double angle = findAngle(dicMarkers);
            // double angle = angleEmgu;

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1, 1);
            imageToPutData.Draw(((int)angleEmguAnkle1).ToString(), ref f, new System.Drawing.Point((int)dicMarkers["PosteriorSuperiorIliacSpine"].X, (int)dicMarkers["PosteriorSuperiorIliacSpine"].Y), new Bgr(121, 116, 40));

            if (captureAngles)
                angles.Add(angle);

            tbxResultsPelvis.AppendText(angleEmguAnkle1.ToString());
            tbxResultsPelvis.AppendText(Environment.NewLine);

        }

        private void CalculateKneeAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> imageToPutData, bool captureAngles)
        {

            LineSegment2DF femurLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["Trochanter"].X, dicMarkers["Trochanter"].Y),
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y));
            LineSegment2DF tibiaLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y),
                new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y));

            if (isDrawAxis)
            {
                imageToPutData.Draw(tibiaLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                imageToPutData.Draw(femurLogAxis, new Bgr(System.Drawing.Color.Red), 1);
            }
            /// In order to calculate positive and negative angles use GetExteriorAngleDegree
            /// If pendient is positive, then ExteriorAngleDegree =>  +
            /// If pendient is negative, then ExteriorAngleDegree =>  -
            /// When analizing the left leg multiple the angle by (-1)  to get coherent angles
            /// When analizing the rigth leg multiple the angle by (+1)  to get coherent angles
            /// 
            double angleEmgu1 = tibiaLogAxis.GetExteriorAngleDegree(femurLogAxis);
            double angleEmgu2 = femurLogAxis.GetExteriorAngleDegree(tibiaLogAxis);

            /// Ths method always will calculate the positive  Interior angle
            double angle = findAngle(dicMarkers);
            // double angle = angleEmgu;

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1.0, 1.0);
            imageToPutData.Draw(((int)angleEmgu1).ToString(), ref f, new System.Drawing.Point((int)dicMarkers["Knee"].X - 50, (int)dicMarkers["Knee"].Y), new Bgr(255, 0, 0));
            //original_Frame.Draw(((int)angleEmgu2).ToString(), ref f, new System.Drawing.Point((int)dicMarkers["Knee"].X +10, (int)dicMarkers["Knee"].Y), new Bgr(0, 0, 255));

            if (captureAngles)
                angles.Add(angle);

            //========================== activar un BOOL para empezar a capturar lo angulos y desactivarlo al presionarlo nuevamente 
            //========================== luego guardar la lista de angulos y mostrarlo en la grafica

            //tbxResults.AppendText("line.AddPoint(" + (countFrames * 1.0).ToString() + "," + Math.Abs(angle).ToString() + ");");
            //tbxResults.AppendText(Math.Abs(angle).ToString() + ",");
            tbxResultsKnee.AppendText(Math.Abs(angle).ToString());
            tbxResultsKnee.AppendText(Environment.NewLine);

            /*
            ListBoxItem item = new ListBoxItem();
            item.Content = "AddPoint(" + (countFrames * 1.0).ToString() + "," + angle.ToString() + ");";
            listResults.Items.Add(item);
            */
            countFrames++;
        }

        private void CalculateAnkleAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> imageToPutData, bool captureAngles)
        {
            /// finding the missing P(x,y) that form a perfect triangle
            /// Source => http://www.freemathhelp.com/forum/threads/82575-need-help-finding-3rd-set-of-coordinates-to-a-right-triangle/page2
            /// 
            float px = dicMarkers["Ankle"].X - (dicMarkers["Knee"].Y - dicMarkers["Ankle"].Y);
            float py = dicMarkers["Ankle"].Y - (dicMarkers["Ankle"].X - dicMarkers["Knee"].X);

            /// calculating longitudinal axis
            /// 
            LineSegment2DF tibiaLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y),
                new System.Drawing.PointF(dicMarkers["Knee"].X, dicMarkers["Knee"].Y));

            LineSegment2DF retropieLogAxis = new LineSegment2DF(new System.Drawing.PointF(dicMarkers["Calcaneus"].X, dicMarkers["Calcaneus"].Y),
                new System.Drawing.PointF(dicMarkers["Foot"].X, dicMarkers["Foot"].Y));

            LineSegment2DF perpendicularTibiaLongAxis = new LineSegment2DF(new System.Drawing.PointF(px, py),
                new System.Drawing.PointF(dicMarkers["Ankle"].X, dicMarkers["Ankle"].Y));

            /// drawing axis
            /// 
            if (isDrawAxis)
            {
                imageToPutData.Draw(tibiaLogAxis, new Bgr(System.Drawing.Color.Yellow), 1);
                imageToPutData.Draw(retropieLogAxis, new Bgr(System.Drawing.Color.Red), 1);
                imageToPutData.Draw(perpendicularTibiaLongAxis, new Bgr(System.Drawing.Color.Green), 1);
            }

            /// ankle angle calculation
            double angleEmguAnkle1 = perpendicularTibiaLongAxis.GetExteriorAngleDegree(retropieLogAxis);
            double angleEmguAnkle2 = retropieLogAxis.GetExteriorAngleDegree(perpendicularTibiaLongAxis);


            double angle = findAngle(dicMarkers);
            // double angle = angleEmgu;

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1.0, 1.0);
            imageToPutData.Draw(((int)angleEmguAnkle1).ToString(), ref f, new System.Drawing.Point((int)dicMarkers["Calcaneus"].X, (int)dicMarkers["Calcaneus"].Y), new Bgr(121, 116, 40));

            if (captureAngles)
                angles.Add(angle);

            //========================== activar un BOOL para empezar a capturar lo angulos y desactivarlo al presionarlo nuevamente 
            //========================== luego guardar la lista de angulos y mostrarlo en la grafica

            //tbxResults.AppendText("line.AddPoint(" + (countFrames * 1.0).ToString() + "," + Math.Abs(angle).ToString() + ");");
            //tbxResults.AppendText(Math.Abs(angle).ToString() + ",");
            tbxResultsAnkle.AppendText(angleEmguAnkle1.ToString());
            tbxResultsAnkle.AppendText(Environment.NewLine);

            /*
            ListBoxItem item = new ListBoxItem();
            item.Content = "AddPoint(" + (countFrames * 1.0).ToString() + "," + angle.ToString() + ");";
            listResults.Items.Add(item);
            */
            countFrames++;
        }

        private void DrawLinesHoriontal(List<System.Drawing.PointF> markersF, Image<Bgr, byte> outImg1, bool captureAngles)
        {


            for (int i = 0; i < markersF.Count; i++)
            {
                LineSegment2D line1 = new LineSegment2D(new System.Drawing.Point(0, (int)markersF[i].Y),
                    new System.Drawing.Point((int)markersF[i].X, (int)markersF[i].Y));

                outImg1.Draw(line1, new Bgr(System.Drawing.Color.Red), 1);
            }


            countFrames++;
        }

        private double findAngle(Dictionary<string, PointF> markersF)
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


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
using Gaitcome2D.AviFileWrapper;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;



namespace Gaitcome2D
{
    public partial class MainWindow : Window
    {

        /// <summary>
        /// =>    Get-ChildItem .\ -include packages,bin,obj,bld,Backup,_UpgradeReport_Files,Debug,Release,ipch -Recurse | foreach ($_) { remove-item $_.fullname -Force -Recurse }
        ///       run this on power shell in order to delete unimportant files to commit
        /// </summary>
        /// 

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


        bool cam0, cam1 ,demoData;

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
        bool isDrawingAxis;
        string readImagePath;
        bool isLeftSagittalPlane;
        bool isCapturingAngles;

        List<double> lstHipAngles;
        List<double> lstKneeAngles;
        List<double> lstAnkleAngles;
        List<double> lstPelvisAngles;

        int initialFrameToProcess;
        int finalFrameToProccess;

        bool isDetectingInitialContact;
        List<KeyValuePair<KeyValuePair<int, double>, bool>> lstCandidatesInitialContanct;
        int limMaxIC;
        int indexCandidatesInitialContanct;
        #endregion

        #region Record TAB
        bool isRecordingPathSelected;
        string recordingVideoPath;
        int indexInfraredCamera;
        #endregion

        public MainWindow()
        {
            //InitializeComponent();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
            timerRecording.Tick += new EventHandler(recording_Timer_Tick);

            numCameras = 0;
            FPS = 75;
            isRecording = false;        //true or false
            isPlaying = false;          //true or false
            readVideoPath = @"C:\Users\kevin\Desktop\testCLEYE_75 FPS.avi";
            saveVideoPath = @"D:\Projects\Gaitcom\testMultiImages\";
            testMultiImagesFolder = @"D:\Projects\Gaitcom\testMultiImages\";
            cameraFolder = @"cam";
            cam0 = cam1 = false;
            contFrames = 0;

            demoData = true;

            // paid alternative
            //converter = new ImageToVideo();

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
            lstCandidatesInitialContanct = new List<KeyValuePair<KeyValuePair<int, double>, bool>>();
            isDrawingAxis = true;
            readImagePath = "";
            isLeftSagittalPlane = false;
            isCapturingAngles = false;

            lstHipAngles = new List<double>();
            lstKneeAngles = new List<double>();
            lstAnkleAngles = new List<double>();
            lstPelvisAngles = new List<double>();

            initialFrameToProcess = 0;
            //txbInitialFrame.Text = initialFrameToProcess.ToString();
            finalFrameToProccess = 0;
            //txbLastFrame.Text = finalFrameToProccess.ToString();

            isDetectingInitialContact = false;

            limMaxIC = 3;
            indexCandidatesInitialContanct = 0;

            #endregion

            #region Record TAB

            isRecordingPathSelected = false;
            recordingVideoPath = "";
            indexInfraredCamera = 0;

            #endregion

            #region Plotting TAB




            #endregion

            #region closing cameras initialized

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
                //cameraImage1.Device.AutoGain = true;
                //cameraImage1.Device.Gain = 80;

                //cameraImage1.Device.AutoExposure = true;
                //cameraImage1.Device.Exposure = 10;

                //cameraImage1.Device.AutoWhiteBalance = true;
                //cameraImage1.Device.WhiteBalanceRed = 4;
                //cameraImage1.Device.WhiteBalanceGreen = 4;
                //cameraImage1.Device.WhiteBalanceBlue = 4;

                cam1 = true;
            }
            #endregion

            indexInfraredCamera = GetIndexInfraredCamera();
            allCamerasConnectedStart();
            CheckCamerasConnected();

        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

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
           
        }

        private void Window_Closed_1(object sender, EventArgs e)
        {
            this.Close();
        }

        
        #region [ Record TAB ]

        private void btnForzeInitCameras_Click(object sender, RoutedEventArgs e)
        {
            getAllFramesFromVideo();
        }

        /*
          This functionality is not used any more since EkEN camera replace the PSEYE &
          thrid program extracts the frames from .MOV file(EKEN default video format)
        */
        private void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            if (isRecordingPathSelected)
            {
                if (!isRecording)
                {
                    if (!IsEmptyFilesCameraFolders())
                    {
                        //here the savepath is changed
                        OverwriteRecordingFilesMessage(sender, e);
                    }

                    isRecording = true;
                    btnCapture.Content = "Stop";

                    cameraImage0.Device.isRecording = isRecording;
                    cameraImage1.Device.isRecording = isRecording;

                    cameraImage0.Device.strPath = recordingVideoPath + "\\" + cameraFolder + "0\\img";
                    cameraImage1.Device.strPath = recordingVideoPath + "\\" + cameraFolder + "1\\img";


                }
                else
                {
                    isRecording = false;
                    contFrames = cameraImage1.Device.intCountFrames;

                    cameraImage0.Device.isRecording = isRecording;
                    cameraImage1.Device.isRecording = isRecording;

                    cameraImage0.Device.intCountFrames = 0;
                    cameraImage1.Device.intCountFrames = 0;

                    btnCapture.Content = "Record";
                    UpdateImagePlayerAtributtes(recordingVideoPath + "\\" + cameraFolder + "0", 0, contFrames);
                }
            }

        }

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
                    CreateCameraFolders();
                    isRecordingPathSelected = true;

                }
            }

        }

        private void btnGraficarTest_Click(object sender, EventArgs e)
        {
            if (demoData)
            {
                SimulateData();
            }
                PlottingResults pr1 = new PlottingResults(lstAnkleAngles, lstKneeAngles, lstHipAngles, lstPelvisAngles);
                GridGraphics.DataContext = pr1;

                PlottingResults pr2 = new PlottingResults(lstAnkleAngles, lstKneeAngles, lstHipAngles, lstPelvisAngles);
                ResultsView resultsView = new ResultsView();
                resultsView.DataContext = pr2;
                resultsView.Show();
        }

        private int GetIndexInfraredCamera()
        {
            for (int i = 0; i < numCameras; i++)
            {
                if (CLEyeCameraDevice.CameraUUID(i).ToString() == "39f05022-8525-cde4-c19c-746220fec2e3")
                    return i;
            }
            return -1;
        }

        private void CheckCamerasConnected()
        {
            if (cam0) ckbCamera00.IsChecked = true;
            if (cam1) ckbCamera01.IsChecked = true;

        }
        
       private void CreateCameraFolders()
        {
            for (int i = 0; i < numCameras; i++)
            {
                // If directory does not exist, create it. 
                string newCameraPath = recordingVideoPath + "\\" + cameraFolder + i;
                if (!Directory.Exists(newCameraPath))
                {
                    Directory.CreateDirectory(newCameraPath);
                }
            }
        }

        private bool IsEmptyFilesCameraFolders()
        {
            string[] folders;
            for (int i = 0; i < numCameras; i++)
            {
                string pathDirectory = recordingVideoPath + "\\" + cameraFolder + i.ToString();
                if (Directory.Exists(pathDirectory))
                {
                    folders = Directory.GetFiles(pathDirectory);
                    if (folders.Length > 0)
                        return false;
                }

            }
            return true;
        }

        /*
          This functionality is not used any more since EkEN camera replace the PSEYE &
          thrid program extracts the frames from .MOV file(EKEN default video format)
        */
        private void allCamerasWriteJpeg(string path)
        {
            if (indexInfraredCamera == 0 || indexInfraredCamera == -1)
            {
                if (cam0)
                    bitmapSourceToBitmap(cameraImage0.Device.BitmapSource).Save(path + "0\\" + "img" + contFrames + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                if (cam1)
                    bitmapSourceToBitmap(cameraImage1.Device.BitmapSource).Save(path + "1\\" + "img" + contFrames + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            else if (indexInfraredCamera == 1)
            {
                if (cam0)
                    bitmapSourceToBitmap(cameraImage0.Device.BitmapSource).Save(path + "1\\" + "img" + contFrames + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                if (cam1)
                    bitmapSourceToBitmap(cameraImage1.Device.BitmapSource).Save(path + "0\\" + "img" + contFrames + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            contFrames++;
        }
        
        // Not used bacause is recorded directly to CLEYE Lib (Implemented)
        private void recording_Timer_Tick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                allCamerasWriteJpeg(recordingVideoPath + "\\" + cameraFolder);
                //cameraImage0.Device.BitmapSource.DownloadCompleted += objImage_DownloadCompleted;
            }
         
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
        #endregion


        #region [ Player TAB ]
        
        private void imagePlayer_Timer_Tick(object sender, EventArgs e)
        {
            if (imagePlayerValue <= sliImageProgress.Maximum)
            {
                UpdateFrameToBeProcessed(imagePlayerValue);
                sliImageProgress.Value = imagePlayerValue;
                imagePlayerValue++;

                if (isCapturingAngles && imagePlayerValue > finalFrameToProccess &&
                    finalFrameToProccess != 0)
                {
                    //initialFrameToProcess++; => replaced by ImagePlayerValue
                    timerImagePlayer.Stop();
                    isCapturingAngles = false;
                    btnCaptureAngles.Content = "Capturar ángulos: OFF";
                    indexCandidatesInitialContanct = 0;

                    if (isDetectingInitialContact)
                    {
                        AutomaticInitialContactDetections();

                        // HOT FOX -> there are false IC due to the camera UP and DOWN movements
                        PrintAllGaitCyclesDetected();
                    }
                    else
                    {
                        PrintAllAnglesDetected();
                        btnGraficarTest_Click(sender, e);
                    }
                }
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
                imagePlayerSpeed = (int)(FPS * sliImagePlayerSpeed.Value);
                timerImagePlayer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / imagePlayerSpeed);
            }
        }
        
        private void UpdateImagePlayerAtributtes(string selectedPath, int flagOpenFrom, int totalFrames)
        {
            string msgFrom = "";
            files = Directory.GetFiles(selectedPath);
            switch (flagOpenFrom)//Record Tab
            {
                case 0://Opened from Record Tab
                    msgFrom = "Número de frames grabados: ";
                    break;

                case 1://Opened from Player Tab

                    cam0 = false;
                    cam1 = false;
                    msgFrom = "Número de frames encontrados: ";

                    if (Directory.Exists(selectedPath + "\\" + cameraFolder + "0") &&
                        (Directory.GetFiles(selectedPath + "\\" + cameraFolder + "0").Length != 0))
                        cam0 = true;

                    if (Directory.Exists(selectedPath + "\\" + cameraFolder + "1") &&
                        (Directory.GetFiles(selectedPath + "\\" + cameraFolder + "1").Length != 0))
                        cam1 = true;

                    if (cam0)//this folder always will have data if one camera is connected at least
                        totalFrames = Directory.GetFiles(selectedPath + "\\" + cameraFolder + "0").Length;

                    break;
            }

            System.Windows.Forms.MessageBox.Show(msgFrom + totalFrames.ToString() + " en el siguiente directorio: " + selectedPath, "Message");
            //readImagePath = selectedPath;

            sliImageProgress.Maximum = totalFrames - 1;
            isImagePlayerDataLoaded = true;
            sliImagePlayerSpeed.Value = DEFAULT_SPEED_SLIDER_VALUE;
            setTimerIntervalImagePlayer();
            statusImagePlayerOpened();
        }

        private void OverwriteRecordingFilesMessage(object sender, RoutedEventArgs e)
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

        private void sliImagePlayerSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                setTimerIntervalImagePlayer();
            }
        }

        private void UpdateFrameToBeProcessed(int imagePlayervalue)
        {
            if (isImagePlayerDataLoaded)
            {
                lblImageProgressStatus.Text = "Frame N°- " + (imagePlayervalue + 1).ToString();

                // set black frames if there are no Loaded images
                infraredBgrImage = new Image<Bgr, byte>(220, 140, new Bgr(System.Drawing.Color.Black));
                colorImage = new Image<Bgr, byte>(320, 240, new Bgr(System.Drawing.Color.Black));

                //if (cam0) infraredBgrImage = new Image<Bgr, Byte>(recordingVideoPath + "\\" + cameraFolder + @"0\\" + "img" + imagePlayervalue.ToString() + ".jpg");
                StringBuilder sIndexFourDigits = getStringFourDigitIndex(imagePlayervalue);
                if (cam0) infraredBgrImage = new Image<Bgr, Byte>(recordingVideoPath + "\\" + cameraFolder + @"0\\" + "img " + sIndexFourDigits.ToString() + ".jpg");
                
                if (cam1) colorImage = new Image<Bgr, Byte>(recordingVideoPath + "\\" + cameraFolder + @"1\\" + "img" + imagePlayervalue.ToString() + ".jpg");

                //if (imagePlayervalue % 2 == 0)
                //{
                ///horizontalCameraConfiguration();
                //}
                //verticalCameraConfigutation();

                SimulateRightSagittalPlane();

                ImageProcessToGaitAngles(infraredBgrImage, colorImage, isCapturingAngles);
            }
        }

        private StringBuilder getStringFourDigitIndex(int imagePlayervalue)
        {
            int iCont = 4;// calculate this number from the lenght of the original folder
            int iDigit = imagePlayervalue;
            StringBuilder sIndex = new StringBuilder();

            do {
                iCont--;
                imagePlayervalue /= 10;
            } while (imagePlayervalue > 0);

            for (int i = 0; i < iCont; i++)
            {
                sIndex.Append("0");
            }
            sIndex.Append(iDigit.ToString());

            return sIndex;

        }

       

        private void SimulateRightSagittalPlane()
        {
            if (!isLeftSagittalPlane)
            {
                infraredBgrImage = infraredBgrImage.Flip(FLIP.HORIZONTAL);
            }
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

        
        #region [ Image Player Options : buttons, sliders, status circle]

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
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
                        UpdateImagePlayerAtributtes(recordingVideoPath, 
                                                    1,
                                                    fbd.SelectedPath.Length);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Desea abrir otro folder de imagenes", 
                        "Advertencia",
                        MessageBoxButtons.OKCancel, 
                        MessageBoxIcon.Exclamation);

                    isImagePlayerDataLoaded = false;
                    btnOpen_Click(sender, e);
                    //save work of therapist
                    //then allow to open other files 
                }
            }
            catch (Exception)
            {
                statusImagePlayerFailed();
            }
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                isPlaying = true;
                timerImagePlayer.Start();
                statusImagePlayerOpened();
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                isPlaying = false;
                timerImagePlayer.Stop();
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                isPlaying = false;
                timerImagePlayer.Stop();

                imagePlayerValue = 0;

                UpdateFrameToBeProcessed(imagePlayerValue);
                sliImageProgress.Value = imagePlayerValue;

                statusImagePlayerEnded();
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
                timerImagePlayer.Stop();
                UpdateFrameToBeProcessed(--imagePlayerValue);
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
                UpdateFrameToBeProcessed(++imagePlayerValue);
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
                    btnDrawReferenceLines.Content = "Ejes logitudianles: ON";
                }
                else
                {
                    isDrawingAxis = false;
                    btnDrawReferenceLines.Content = "Ejes logitudianles: OFF";
                }
                UpdateFrameToBeProcessed(imagePlayerValue);

            }

            #endregion
        }

        private void btnSagittalPlane_Click(Object sender, RoutedEventArgs e)
        {
            if (!isLeftSagittalPlane)
            {
                isLeftSagittalPlane = true;
                btntSagittalPlane.Content = "Plano Sagital: IZQUIERDO";

            }
            else
            {
                isLeftSagittalPlane = false;
                btntSagittalPlane.Content = "Plano Sagital: DERECHO";
            }

            imagePlayerValue = (int)sliImageProgress.Value;
            UpdateFrameToBeProcessed(imagePlayerValue);
        }

        private void btnInitialContactDetection_Click(Object sender, RoutedEventArgs e)
        {
            if (!isDetectingInitialContact)
            {
                isDetectingInitialContact = true;
                btnInitialContactDetection.Content = "Deteccion de Contacto inicial : ON";

            }
            else
            {
                isDetectingInitialContact = false;
                btnInitialContactDetection.Content = "Deteccion de Contacto inicial : OFF";
            }

        }

        private void btnCaptureAngles_Click(Object sender, RoutedEventArgs e)
        {
            //pool
            System.Windows.MessageBox.Show(getStringFourDigitIndex(1000).ToString());
            if (!isCapturingAngles)
            {
                if (initialFrameToProcess < finalFrameToProccess)
                {
                    ClearAllResourcesForAllocatingAnatomicalUnitsInformation();
                    isCapturingAngles = true;
                    btnCaptureAngles.Content = "Capturar ángulos: ON";

                    btnPlay_Click(sender, e); // auto play
                    sliImageProgress.Value = Convert.ToInt32(txbInitialFrame.Text);
                }
                else
                {
                    System.Windows.MessageBox.Show("No ha selecionado un segmento del video ", "Atanción",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }

            }
            else
            {
                isCapturingAngles = false;
                btnCaptureAngles.Content = "Capturar ángulos: OFF";
            }

        }

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
                    UpdateFrameToBeProcessed(imagePlayerValue);
                //lblImageProgressStatus.Text = TimeSpan.FromSeconds(sliProgress.Value).ToString(@"hh\:mm\:ss\:ff");
            }

        }

        private void sliImageProgress_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                txbInitialFrame.Text = ((int)sliImageProgress.Value +1).ToString();
                initialFrameToProcess = (int)sliImageProgress.Value;
            }
        }

        private void sliImageProgress_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isImagePlayerDataLoaded)
            {
                finalFrameToProccess = (int)sliImageProgress.Value;
                if (!(initialFrameToProcess < finalFrameToProccess))
                {
                    txbLastFrame.Text = "-";
                }
                else
                {
                    txbLastFrame.Text = ((int)sliImageProgress.Value +1).ToString();
                }
            }
        }

        private void sliMinUmbral_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMinUmbralValue.Text = "MinUmbralValue: " + ((int)sliderMinUmbral.Value).ToString();
                UpdateFrameToBeProcessed(imagePlayerValue);
            }
        }

        private void sliMaxUmbral_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMaxUmbralValue.Text = "MaxUmbralValue: " + ((int)sliderMaxUmbral.Value).ToString();
                UpdateFrameToBeProcessed(imagePlayerValue);
            }
        }

        private void sliMinSizeBlob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMinSizeBlobValue.Text = "MinSizeBlob: " + ((int)sliderMinSizeBlob.Value).ToString();
                UpdateFrameToBeProcessed(imagePlayerValue);
            }
        }

        private void sliMaxSizeBlob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isImagePlayerDataLoaded)
            {
                imagePlayerValue = (int)sliImageProgress.Value;
                txtMaxSizeBlobValue.Text = "MaxSizeBlob: " + ((int)sliderMaxSizeBlob.Value).ToString();
                UpdateFrameToBeProcessed(imagePlayerValue);
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
        }

        #endregion

       
        #region [ Image processing - Gait Analysis ]

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

        private void ImageProcessToGaitAngles(Image<Bgr, byte> infraBgrImg, Image<Bgr, byte> colorImg, bool isCapturingAngles)
        {
            List<System.Drawing.PointF> lstRetMarkers = FindRetroreFlectiveMarkers(infraBgrImg);

            if (lstRetMarkers != null && lstRetMarkers.Count == 7)
            {
                Dictionary<string, PointF> dicMarkers =
                    LabelingMarkersWithHumanReferencePoints(lstRetMarkers);

                DrawByColorsRetrorefelctiveMarkerLabels(dicMarkers, infraredImgCpy, 4, 2);
                CalculateRetroreflectiveMarkersCentroid(dicMarkers, infraredImgCpy, false);

                CalculatePelvisAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);
                CalculateHipAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);
                CalculateKneeAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);
                CalculateAnkleAngles(dicMarkers, infraredImgCpy, isDrawingAxis, isCapturingAngles);

                //markersHistory.Add(lstRetMarkers);
            }


            //colorImagePlayer.Source = ToBitmapSource(colorImg);
            //dataImagePlayer.Source = ToBitmapSource(infraredImgCpy);
            //binaryImagePlayer.Source = ToBitmapSource(grayImg);

            binaryImagePlayer.Source = ToBitmapSource(colorImg);
            colorImagePlayer.Source = ToBitmapSource(infraredImgCpy);
            dataImagePlayer.Source = ToBitmapSource(grayImg);

        }

        private void CalculateHipAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
        {
            /// calculating longitudinal axis
            LineSegment2DF iliacSpainLogAxis = new LineSegment2DF(
                new System.Drawing.PointF(dicMarkers["PosteriorSuperiorIliacSpine"].X, dicMarkers["PosteriorSuperiorIliacSpine"].Y),
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
                lstHipAngles.Add(hipAngle);
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
            double pelvisAngle;
            if (!isLeftSagittalPlane)// right
            {
                pelvisAngle = 180 - iliacSpainLogAxis.GetExteriorAngleDegree(horizontalLogAxis);
            }
            else //left
            {
                pelvisAngle = 180 - horizontalLogAxis.GetExteriorAngleDegree(iliacSpainLogAxis);
            }

            MCvFont f = new MCvFont(FONT.CV_FONT_HERSHEY_COMPLEX, 1, 1);
            bgrImg.Draw(((int)pelvisAngle).ToString(),
                ref f,
                new System.Drawing.Point((int)dicMarkers["PosteriorSuperiorIliacSpine"].X,
                    (int)dicMarkers["PosteriorSuperiorIliacSpine"].Y),
                new Bgr(121, 116, 40)
            );

            if (isCapturingAngles)
            {
                lstPelvisAngles.Add(pelvisAngle);
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
                lstKneeAngles.Add(kneeAngle);
            }
        }

        private void CalculateAnkleAngles(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, bool isDrawAxis, bool isCapturingAngles)
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

                new System.Drawing.PointF(perfecTriangleMissingPointX, perfecTriangleMissingPointY),

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
                lstAnkleAngles.Add(ankleAngle);

                KeyValuePair<int, double> indexAndDouble =
                    new KeyValuePair<int, double>(
                        indexCandidatesInitialContanct,
                    //change this parameter to perfecTriangleMissingPointY when is correct
                        perfecTriangleMissingPointY
                    );

                lstCandidatesInitialContanct.Add(
                    new KeyValuePair<KeyValuePair<int, double>, bool>(indexAndDouble, false));

                indexCandidatesInitialContanct++;
            }

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

        private void DrawByColorsRetrorefelctiveMarkerLabels(Dictionary<string, PointF> dicMarkers, Image<Bgr, byte> bgrImg, float radious, int thickness)
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

        private double CalculateEuclideanDistance(PointF pointF1, PointF pointF2)
        {
            return Math.Sqrt(Math.Pow(pointF1.X - pointF2.X, 2) + Math.Pow(pointF1.Y - pointF2.Y, 2));
        }

        private double FindAngleFromThreePoints(Dictionary<string, PointF> markersF)
        {
            double a = Math.Pow(markersF["Knee"].X - markersF["Ankle"].X, 2) + Math.Pow(markersF["Knee"].Y - markersF["Ankle"].Y, 2);
            double b = Math.Pow(markersF["Knee"].X - markersF["Trochanter"].X, 2) + Math.Pow(markersF["Knee"].Y - markersF["Trochanter"].Y, 2);
            double c = Math.Pow(markersF["Trochanter"].X - markersF["Ankle"].X, 2) + Math.Pow(markersF["Trochanter"].Y - markersF["Ankle"].Y, 2);

            return Math.Abs(Math.Acos((a + b - c) / Math.Sqrt(4 * a * b)) * 180 / Math.PI - 180);
        }

        #region [ Image Format Converters]

        [System.Runtime.InteropServices.DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);
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

        #endregion

        #endregion


        #endregion


        #region [ Results TAB ]

        private void AutomaticInitialContactDetections()
        {
            //Find Initial all INICITAL CONTACTS double sort
            int length = lstCandidatesInitialContanct.Count;

            double maxY = lstCandidatesInitialContanct[0].Key.Value;
            bool isFoundedIC = false;

            int contIC = 0;

            for (int i = 1; i < length; i++)
            {
                //found a fallig
                if (!(lstCandidatesInitialContanct[i].Key.Value > maxY))
                {
                    //    isFoundedIC = false;
                    //}
                    ////found less
                    //else
                    //{
                    if (!isFoundedIC && contIC < limMaxIC)
                    {
                        isFoundedIC = true;

                        KeyValuePair<KeyValuePair<int, double>, bool> newCandidate =
                            new KeyValuePair<KeyValuePair<int, double>, bool>(
                                new KeyValuePair<int, double>(
                                    lstCandidatesInitialContanct[i - 1].Key.Key,
                                    lstCandidatesInitialContanct[i - 1].Key.Value
                                ),
                                true // set IC contact detected to TRUE
                            );

                        lstCandidatesInitialContanct[i - 1] = newCandidate;
                        contIC++;
                    }

                    //if (lstCandidatesInitialContanct[i].Key.Value > maxY  )
                    //    isFoundedIC = false;
                }
                else
                {
                    isFoundedIC = false;
                }
                maxY = lstCandidatesInitialContanct[i].Key.Value;
            }
        }
        
        private int CompareAngles(KeyValuePair<KeyValuePair<int, double>, bool> x, KeyValuePair<KeyValuePair<int, double>, bool> y)
        {
            return y.Key.Value.CompareTo(x.Key.Value);
        }

        private int CompareIndexs(KeyValuePair<KeyValuePair<int, double>, bool> x, KeyValuePair<KeyValuePair<int, double>, bool> y)
        {
            return x.Key.Key.CompareTo(y.Key.Key);
        }

        private void PrintAllGaitCyclesDetected()
        {
            int lstLenght = lstCandidatesInitialContanct.Count;

            int limIC = 0;
            bool isPrintingAngles = false;

            for (int i = 0; i < lstLenght; i++)
            {
                // detect I.C. candidates along 
                if (lstCandidatesInitialContanct[i].Value)
                {
                    tbxResultsHip.AppendText("------------");
                    tbxResultsHip.AppendText(Environment.NewLine);
                    tbxResultsPelvis.AppendText("------------");
                    tbxResultsPelvis.AppendText(Environment.NewLine);
                    tbxResultsKnee.AppendText("------------");
                    tbxResultsKnee.AppendText(Environment.NewLine);
                    tbxResultsAnkle.AppendText("------------");
                    tbxResultsAnkle.AppendText(Environment.NewLine);

                    isPrintingAngles = true;

                    if (!(limIC < limMaxIC))
                        isPrintingAngles = false;

                    limIC++;

                }

                if (isPrintingAngles)
                {
                    tbxResultsHip.AppendText(lstHipAngles[i].ToString());
                    tbxResultsHip.AppendText(Environment.NewLine);

                    tbxResultsPelvis.AppendText(lstPelvisAngles[i].ToString());
                    tbxResultsPelvis.AppendText(Environment.NewLine);

                    tbxResultsKnee.AppendText(lstKneeAngles[i].ToString());
                    tbxResultsKnee.AppendText(Environment.NewLine);

                    tbxResultsAnkle.AppendText(lstAnkleAngles[i].ToString());
                    tbxResultsAnkle.AppendText(Environment.NewLine);
                }
            }
        }

        private void PrintAllAnglesDetected()
        {
            int lstLenght = lstCandidatesInitialContanct.Count;

            for (int i = 0; i < lstLenght; i++)
            {
                tbxResultsHip.AppendText(lstHipAngles[i].ToString());
                //tbxResultsHip.AppendText(lstCandidatesInitialContanct[i].Key.Value.ToString());
                tbxResultsHip.AppendText(Environment.NewLine);

                tbxResultsPelvis.AppendText(lstPelvisAngles[i].ToString());
                tbxResultsPelvis.AppendText(Environment.NewLine);

                tbxResultsKnee.AppendText(lstKneeAngles[i].ToString());
                tbxResultsKnee.AppendText(Environment.NewLine);

                tbxResultsAnkle.AppendText(lstAnkleAngles[i].ToString());
                tbxResultsAnkle.AppendText(Environment.NewLine);
            }

        }

        private void ClearAllResourcesForAllocatingAnatomicalUnitsInformation()
        {
            lstHipAngles.Clear();
            lstKneeAngles.Clear();
            lstAnkleAngles.Clear();
            lstPelvisAngles.Clear();

            //lsLstAnkle.ClearSelection();
            //lsLstHip.ClearSelection();
            //lsLstAnkle.ClearSelection();
            //lsLstPelvis.ClearSelection();
        }

        private void SimulateData()
        {
            #region intput angles

            double[] lstkneeangles = { 
13.70632602,
14.25693355,
15.52448901,
16.51486072,
17.52174176,
17.62899552,
17.47182991,
17.87217915,
18.3781601 ,
19.39507432,
19.97474935,
21.18163744,
21.93255951,
22.21751445,
22.19637243,
21.93069074,
22.15353859,
21.92811507,
21.32897342,
20.89153327,
20.41619865,
19.93657606,
19.38596596,
19.02235987,
18.50483351,
17.8907502 ,
17.19938406,
16.93688551,
16.48110613,
16.15487458,
15.70243802,
15.85617708,
15.48434699,
14.83603953,
15.03592024,
14.82570722,
14.3942139 ,
14.00626204,
14.13115169,
14.07627847,
14.32117694,
14.30735712,
14.06475696,
14.4790715 ,
14.17656336,
14.86105984,
14.9882223 ,
15.35935986,
15.57538974,
16.35339242,
16.94216447,
17.07406   ,
18.29797422,
18.66074221,
19.82823119,
20.64124306,
21.0372776 ,
22.54354785,
23.7274491 ,
24.96491131,
26.35493257,
27.32763533,
29.58056482,
31.32182572,
32.85896594,
35.27090666,
37.54889874,
40.22772042,
43.16398734,
46.48779822,
49.36824337,
52.37298795,
55.98497137,
58.13248938,
60.99014125,
63.53123734,
65.74783659,
67.53062628,
68.8120082 ,
69.5802922 ,
70.16337877,
69.87250507,
70.13501496,
69.30551355,
69.04139792,
68.80957728,
67.07120752,
65.87462881,
64.27474686,
62.49384799,
60.5797449 ,
57.93762478,
55.46879756,
52.42264464,
49.26391043,
46.07810994,
42.21635459,
39.10196872,
34.48082193,
31.2037531 ,
26.99982442,
23.62013846,
20.6645453 ,
17.71702778,
15.41676547,
13.80201922,
12.51152748,
12.74928749,
12.65705189,
13.14983614,
13.95203887,
14.2288268 ,
13.73966356,
12.62653173,
12.38241732,
12.66945859,
12.54436459,
13.3272386 ,
12.79050519,
13.22421975,
13.43524955,
12.88090998,
13.18914375,
12.44728901,
12.84993863,
12.81999889,
12.06276893,
12.19762352,
11.1799366 ,
10.85193395
                                     };
            double[] lsthipangles = {
30.79402386,
29.43643409,
29.72018591,
31.06302578,
29.31049281,
29.32742454,
29.37988462,
29.23465653,
28.77170903,
27.74189215,
27.72099087,
27.1350595 ,
26.31682652,
24.73871518,
23.40532353,
22.52031781,
21.5803184 ,
19.80594154,
19.51789542,
18.60371381,
17.76400351,
16.54697952,
15.92475974,
15.58781269,
14.80810087,
13.92545876,
13.03537666,
12.17325643,
11.63534334,
11.23685388,
10.41892125,
10.15904062,
8.718623136,
8.699487214,
8.793750311,
7.638342659,
7.160626291,
6.51652182 ,
6.598930696,
5.912973536,
5.614096523,
5.511477095,
4.860763773,
4.506374989,
4.406753893,
4.259369603,
4.295300041,
3.960136523,
3.292398105,
2.689781645,
2.395125183,
2.096342837,
1.79482748 ,
1.619114693,
1.619114268,
1.271669461,
0.9516463625,
1.018723373,
0.8551758274,
0.7990841875,
0.7439472166,
0.8904954587,
1.650916585,
1.580861266,
2.283916414,
2.60682385 ,
2.495158881,
4.26221736 ,
4.987433159,
5.70904797 ,
7.339864252,
8.53993242 ,
10.24241579,
10.98368062,
12.76914087,
14.04846082,
15.6323595 ,
17.02142987,
17.48535735,
19.39041906,
20.79200906,
20.82498046,
23.02396031,
23.5816099 ,
23.98516373,
25.45383476,
26.85152533,
27.49687906,
29.34963093,
29.6829991 ,
29.94946881,
31.40081887,
31.84232127,
32.16540085,
32.3133921 ,
32.36689403,
32.19536066,
32.79033475,
32.45961514,
32.23161543,
30.63097891,
30.37791856,
29.59047939,
29.02696795,
28.30600152,
28.39463484,
27.48674119,
27.40366762,
27.20159371,
26.90820657,
26.44803504,
25.38468542,
25.07548214,
23.9643903 ,
22.6967485 ,
22.19485569,
21.54211746,
20.18772678,
20.2011797 ,
18.79013665,
18.14438536,
17.13760088,
16.51027512,
15.46872238,
14.25979811,
14.14711025,
12.5750738 ,
11.87728485,
10.68551838,
9.686563529
                                      };
            double[] lstankleangles = {
1.12303581  ,
1.806520648 ,
1.418049271 ,
1.767478111 ,
1.25691619  ,
-1.140927406,
-2.940240465,
-3.34282199 ,
-4.33587576 ,
-3.958945534,
-4.127332015,
-3.27280738 ,
-3.399803795,
-1.740559552,
-1.035179622,
-0.9347528904,
-0.09050063046,
0.2689976704,
0.8378789784,
1.181796579 ,
1.783412686 ,
2.278786452 ,
2.32711095  ,
2.174327154 ,
2.874361716 ,
3.080336723 ,
3.375083802 ,
3.256196542 ,
3.156665998 ,
3.498230422 ,
4.261007706 ,
4.304228304 ,
5.198582028 ,
4.9883067   ,
5.602563227 ,
6.003082024 ,
5.695340876 ,
6.24916348  ,
6.54845225  ,
7.509132881 ,
8.009601147 ,
7.976843625 ,
9.15446587  ,
8.851654725 ,
8.953486112 ,
10.03099395 ,
9.691195387 ,
10.7820713  ,
11.28127468 ,
11.48011145 ,
11.99286052 ,
12.00085726 ,
13.01371215 ,
13.30456392 ,
13.34868887 ,
13.74314209 ,
13.4577201  ,
14.11886511 ,
14.00695439 ,
13.41678625 ,
13.02219953 ,
12.1095351  ,
11.55565717 ,
10.45471722 ,
9.126317887 ,
7.422480195 ,
5.605593858 ,
3.891162663 ,
2.138478835 ,
0.6741698087,
-1.600587145,
-3.729343626,
-5.666744933,
-8.267992517,
-9.572081409,
-10.18055082,
-10.12963098,
-8.801526731,
-8.953291338,
-7.255973309,
-6.139007165,
-5.468495626,
-4.037341139,
-2.859198408,
-1.427282437,
-0.2009456467,
2.254468102 ,
0.985420434 ,
2.203329853 ,
2.888109847 ,
5.301708059 ,
6.057592604 ,
6.088945458 ,
5.806114069 ,
4.027199914 ,
4.71199731  ,
4.041946943 ,
3.584646118 ,
2.519413633 ,
1.918002471 ,
1.953972714 ,
2.780276421 ,
2.41000271  ,
2.101814871 ,
1.783806518 ,
2.506786646 ,
2.123661459 ,
3.091383564 ,
2.554361794 ,
2.848422208 ,
2.798624654 ,
0.5207864736,
-1.878250332,
-3.835404495,
-5.215175812,
-5.841785102,
-6.205055038,
-5.592460378,
-6.38940429 ,
-5.199146173,
-4.625218258,
-4.383988111,
-4.116066915,
-3.805689419,
-2.900084669,
-2.159602168,
-2.637913861,
-1.372610366,
-1.620619078,
-0.1049756016
                                      };
            double[] lstpelvisangles = {
                                 5.886005637,
4.661294116,
4.763641691,
6.46982006 ,
4.982419697,
5.194428985,
5.710593053,
5.710593053,
5.599318136,
4.569486006,
4.993744254,
4.484610274,
3.925899102,
4.356975071,
3.778191575,
3.708428315,
3.460189432,
2.374977574,
3.15071253 ,
3.122130396,
3.122130396,
2.849472282,
3.094057949,
3.576334375,
3.576334375,
3.513750433,
3.576334375,
3.366463446,
3.544766431,
3.81407083 ,
3.81407296 ,
3.840953692,
3.306754217,
4.085616476,
4.59425321 ,
4.049579819,
4.367923769,
4.513988704,
5.057248461,
5.013113874,
5.013113874,
5.511477095,
5.463842713,
5.511473271,
6.009002941,
6.061788826,
6.505619063,
6.562698745,
6.505630441,
6.009017308,
6.009005913,
6.340195376,
6.009005913,
6.449534634,
6.449534634,
6.284783336,
6.562698745,
6.449534634,
6.505626274,
6.449534634,
6.394397663,
6.741371943,
6.881723952,
6.394412043,
6.881723952,
6.394397663,
5.686045609,
6.449534634,
6.176794025,
5.511477095,
5.957131185,
5.785558043,
5.511477095,
5.057248461,
5.057248461,
4.553767776,
4.553767776,
4.513984874,
3.544766431,
4.014167978,
3.979382353,
3.01278765 ,
3.715288032,
3.278981464,
2.511363301,
2.511363301,
3.01278765 ,
2.468117914,
3.483274231,
2.986637144,
2.468117914,
3.483267627,
3.483271462,
3.453316295,
3.513750433,
3.483271462,
3.483271462,
3.94518617 ,
4.398701459,
4.436474339,
3.625200177,
3.94518617 ,
3.94518617 ,
3.878524799,
3.878524799,
4.361572623,
3.846021883,
3.814074664,
3.846030205,
3.814074664,
3.561529585,
3.04781737 ,
3.310941548,
3.338868466,
2.815556695,
2.815556695,
2.792702475,
1.877877573,
2.770215728,
1.862621026,
1.832839485,
1.847610142,
1.847610142,
1.81830308 ,
1.132176722,
1.789910608,
1.342620639,
1.342620426,
1.353191936,
1.332216102
                                 };
            #endregion

            for (int i = 0; i < lstpelvisangles.Count(); i++)
            {
                lstPelvisAngles.Add(lstpelvisangles[i]);
                lstAnkleAngles.Add(lstankleangles[i]);
                lstHipAngles.Add(lsthipangles[i]);
                lstKneeAngles.Add(lstkneeangles[i]);
            }
        }

        #endregion

        
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
            //AviManager aviManager = new AviManager(@"C:\Users\kevin\Desktop\FHD0024.avi", true);
            VideoStream stream = aviManager.GetVideoStream();
            stream.GetFrameOpen();

            String path = @"D:\Projects\Gaitcom\testCreateFolder\cam0\";
            for (int n = 0; n < stream.CountFrames; n++)
            {
                //stream.ExportBitmap(n, path + n.ToString() + ".bmp");
                stream.GetBitmap(n).Save(path + "img" + n.ToString() + ".jpg");
            }

            stream.GetFrameClose();
            System.Windows.MessageBox.Show("Image extraction completed!");
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

    }
}


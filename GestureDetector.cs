//------------------------------------------------------------------------------
// <copyright file="GestureDetector.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using Microsoft.Kinect;
    using Microsoft.Kinect.VisualGestureBuilder;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    /// <summary>
    /// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
    /// and updates the associated GestureResultView object with the latest results for the 'Seated' gesture
    /// </summary>
    public class GestureDetector : IDisposable
    {
        /// <summary> Path to the gesture database that was trained with VGB </summary>
        //private readonly string gestureDatabase = @"Database\Seated.gbd";
        private readonly string waveDatabase = @"Database\wave.gbd";

        /// <summary> Name of the discrete gesture in the database that we want to track </summary>
        //private readonly string seatedGestureName = "Seated";
        private readonly string waveGestureName = "Wave";

        //private readonly string waveRightDatabase = @"Database\wave_right.gbd";
        private readonly string waveRightGestureName = "wave_right";

        private readonly string raiseRightHandGestureName = "raise_right_hand";

        private readonly string raiseLeftHandGestureName = "raise_left_hand";

        //Set sleeping time after key gesture having been detected to avoid conflict
        private readonly int sleepingTime = 1000;

        /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
        private VisualGestureBuilderFrameSource vgbFrameSource = null;

        /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
        private VisualGestureBuilderFrameReader vgbFrameReader = null;

        private int waveCount = 0;
        private bool isWaveDetectedStatus = false;

        private int waveRightCount = 0;
        private bool isWaveRightDetectedStatus = true;

        private int bodyIndex = 0;
        //private bool isBodySelected = false;
        private int[] bodiesSelectStatus;

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        [DllImport("user32.dll")]
        static extern byte MapVirtualKey(byte wCode, int wMap);
        /// <summary>
        /// Initializes a new instance of the GestureDetector class along with the gesture frame source and reader
        /// </summary>
        /// <param name="kinectSensor">Active sensor to initialize the VisualGestureBuilderFrameSource object with</param>
        /// <param name="gestureResultView">GestureResultView object to store gesture results of a single body to</param>
        public GestureDetector(KinectSensor kinectSensor, GestureResultView gestureResultView,int[] bodiesSelectStatus)
        {
            this.bodiesSelectStatus = bodiesSelectStatus;
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }

            if (gestureResultView == null)
            {
                throw new ArgumentNullException("gestureResultView");
            }
            
            this.GestureResultView = gestureResultView;
            
            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            this.vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            this.vgbFrameSource.TrackingIdLost += this.Source_TrackingIdLost;

            // open the reader for the vgb frames
            this.vgbFrameReader = this.vgbFrameSource.OpenReader();
            if (this.vgbFrameReader != null)
            {
                this.vgbFrameReader.IsPaused = true;
                this.vgbFrameReader.FrameArrived += this.Reader_GestureFrameArrived;
            }

            // load the 'Seated' gesture from the gesture database
            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(this.waveDatabase))
            {
                // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
                // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
                foreach (Gesture gesture in database.AvailableGestures)
                {
                    if (gesture.Name.Equals(this.waveGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                    if (gesture.Name.Equals(this.waveRightGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                    if (gesture.Name.Equals(this.raiseRightHandGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                    if (gesture.Name.Equals(this.raiseLeftHandGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                }
            }
        }

        /// <summary> Gets the GestureResultView object which stores the detector results for display in the UI </summary>
        public GestureResultView GestureResultView { get; private set; }

        /// <summary>
        /// Gets or sets the body tracking ID associated with the current detector
        /// The tracking ID can change whenever a body comes in/out of scope
        /// </summary>
        public ulong TrackingId
        {
            get
            {
                return this.vgbFrameSource.TrackingId;
            }

            set
            {
                if (this.vgbFrameSource.TrackingId != value)
                {
                    this.vgbFrameSource.TrackingId = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the detector is currently paused
        /// If the body tracking ID associated with the detector is not valid, then the detector should be paused
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return this.vgbFrameReader.IsPaused;
            }

            set
            {
                if (this.vgbFrameReader.IsPaused != value)
                {
                    this.vgbFrameReader.IsPaused = value;
                }
            }
        }

        /// <summary>
        /// Disposes all unmanaged resources for the class
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader objects
        /// </summary>
        /// <param name="disposing">True if Dispose was called directly, false if the GC handles the disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vgbFrameReader != null)
                {
                    this.vgbFrameReader.FrameArrived -= this.Reader_GestureFrameArrived;
                    this.vgbFrameReader.Dispose();
                    this.vgbFrameReader = null;
                }

                if (this.vgbFrameSource != null)
                {
                    this.vgbFrameSource.TrackingIdLost -= this.Source_TrackingIdLost;
                    this.vgbFrameSource.Dispose();
                    this.vgbFrameSource = null;
                }
            }
        }

        /// <summary>
        /// Handles gesture detection results arriving from the sensor for the associated body tracking Id
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (VisualGestureBuilderFrame frame = frameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    // get the discrete gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;

                    if (discreteResults != null)
                    {
                      
                        foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                        {
                            if (gesture.Name.Equals(this.waveGestureName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult result = null;
                                discreteResults.TryGetValue(gesture, out result);
                                
                                if (result != null)
                                {
                                    // update the GestureResultView object with new gesture result values
                                   
                                    if (result.Detected)
                                    {
                                        
                                        if (!isWaveDetectedStatus )
                                        {
                                            waveCount++;
                                            //Console.WriteLine("aaa");
                                            isWaveDetectedStatus = true;
                                            if (this.bodiesSelectStatus[this.GestureResultView.BodyIndex]==1)
                                            {
                                                //Process the activity only while the detected body raise his/her hand recently
                                                Console.WriteLine("["+this.GestureResultView.BodyIndex +"]"+ " Left " + waveCount);
                                                this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, " Left");
                                                ctrlAndPageUpClick();
                                                //Pause detecting to avoid conflict
                                                Thread.Sleep(sleepingTime);
                                            }
                                            
                                        }
                                    }
                                    else
                                    {
                                        if (isWaveDetectedStatus)
                                        {
                                            isWaveDetectedStatus = false;
                                        }
                                    }
                                }
                            }
                            else if (gesture.Name.Equals(this.waveRightGestureName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult rightResult = null;
                                discreteResults.TryGetValue(gesture, out rightResult);

                                if (rightResult != null)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    if (rightResult.Detected)
                                    {
                                        if (!isWaveRightDetectedStatus)
                                        {
                                            waveRightCount++;
                                            
                                            isWaveRightDetectedStatus = true;
                                            if (this.bodiesSelectStatus[this.GestureResultView.BodyIndex] == 1)
                                            {
                                                //Process the activity only while the detected body raise his/her hand recently
                                                Console.WriteLine("[" + this.GestureResultView.BodyIndex + "]"  +" Right " + waveRightCount);
                                                this.GestureResultView.UpdateGestureResult(true, rightResult.Detected, rightResult.Confidence, " Right");
                                                ctrlAndPageDownClick();
                                                //Pause detecting to avoid conflict
                                                Thread.Sleep(sleepingTime);
                                            }
                                           
                                        }
                                    }
                                    else
                                    {
                                        if (isWaveRightDetectedStatus)
                                        {
                                            isWaveRightDetectedStatus = false;
                                        }
                                    }
                                }
                            }
                            else if (gesture.Name.Equals(this.raiseRightHandGestureName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult raiseRightHandResult = null;
                                discreteResults.TryGetValue(gesture, out raiseRightHandResult);

                                if (raiseRightHandResult != null)
                                {
                                   
                                    if (raiseRightHandResult.Detected)
                                    {
                                        if (this.bodiesSelectStatus[this.bodyIndex] == 0)
                                        {
                                            Console.WriteLine("[" + this.GestureResultView.BodyIndex + "]" + " body is detected");
                                            this.GestureResultView.UpdateGestureResult(true, raiseRightHandResult.Detected, raiseRightHandResult.Confidence, "detected ");
                                        }
                                        this.bodyIndex = this.GestureResultView.BodyIndex;
                                        for (int i = 0; i < bodiesSelectStatus.Length; i++)
                                        {
                                            this.bodiesSelectStatus[i] = 0;
                                        }
                                         this.bodiesSelectStatus[this.GestureResultView.BodyIndex] = 1;
                                         
                                        

                                    }
                                }
                            }
                            else if (gesture.Name.Equals(this.raiseLeftHandGestureName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult raiseLeftHandResult = null;
                                discreteResults.TryGetValue(gesture, out raiseLeftHandResult);

                                if (raiseLeftHandResult != null)
                                {
                                    
                                    if (raiseLeftHandResult.Detected)
                                    {
                                        //this.bodyIndex = this.GestureResultView.BodyIndex;
                                        //Release all detectation
                                        if (this.bodiesSelectStatus[this.bodyIndex] == 1)
                                        {
                                            Console.WriteLine("[" + this.GestureResultView.BodyIndex + "]" + " body is  released");
                                            this.GestureResultView.UpdateGestureResult(true, raiseLeftHandResult.Detected, raiseLeftHandResult.Confidence, " released ");
                                        }
                                        for (int i = 0; i < bodiesSelectStatus.Length; i++)
                                        {
                                            this.bodiesSelectStatus[i] = 0;
                                        }
                                        //this.bodiesSelectStatus[this.GestureResultView.BodyIndex] = 1;

                                        
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Press Ctrl and PageUp key in the meanwhile
        /// </summary>
        private void ctrlAndPageUpClick() 
        {
            //按键参数请查看 https://msdn.microsoft.com/zh-cn/library/windows/desktop/dd375731(v=vs.85).aspx
            keybd_event(0x11, 0, 0, 0);//0x11 stands for Ctrl key
            keybd_event(0x21, 0, 0, 0);//0x21 stands for PageUp key

            keybd_event(0x11, 0, 0x0002, 0);
            keybd_event(0x21, 0, 0x0002, 0);
        }
        /// <summary>
        /// Press Ctrl and PageDown key in the meanwhile
        /// </summary>
        private void ctrlAndPageDownClick()
        {
            //按键参数请查看 https://msdn.microsoft.com/zh-cn/library/windows/desktop/dd375731(v=vs.85).aspx
            keybd_event(0x11, 0, 0, 0);
            keybd_event(0x22, 0, 0, 0);

            keybd_event(0x11, 0, 0x0002, 0);
            keybd_event(0x22, 0, 0x0002, 0);
        }

        /// <summary>
        /// Handles the TrackingIdLost event for the VisualGestureBuilderSource object
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            // update the GestureResultView object to show the 'Not Tracked' image in the UI
            this.GestureResultView.UpdateGestureResult(false, false, 0.0f,"");
        }
    }
}

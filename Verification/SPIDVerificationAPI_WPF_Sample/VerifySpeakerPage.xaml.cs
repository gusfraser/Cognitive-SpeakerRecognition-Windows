﻿// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services (formerly Project Oxford): https://www.microsoft.com/cognitive-services
// 
// Microsoft Cognitive Services (formerly Project Oxford) GitHub:
// https://github.com/Microsoft/Cognitive-SpeakerRecognition-Windows
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using Microsoft.ProjectOxford.SpeakerRecognition;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Verification;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SPIDVerificationAPI_WPF_Sample
{
    /// <summary>
    /// Interaction logic for VerifySpeakerPage.xaml
    /// </summary>
    public partial class VerifySpeakerPage : Page
    {
        private string _subscriptionKey;
        private Guid _speakerId = Guid.Empty;
        private WaveIn _waveIn;
        private WaveFileWriter _fileWriter;
        private Stream _stream;
        private SpeakerVerificationServiceClient _serviceClient;
        /// <summary>
        /// Initialization constructor for the verify speaker page
        /// </summary>
        public VerifySpeakerPage()
        {
            InitializeComponent();
            try
            {
                _subscriptionKey = ((MainWindow)Application.Current.MainWindow).SubscriptionKey;
                IsolatedStorageHelper _storageHelper = IsolatedStorageHelper.getInstance();
                string _savedSpeakerId = _storageHelper.readValue(MainWindow.SPEAKER_FILENAME);
                if (_savedSpeakerId != null)
                {
                    _speakerId = new Guid(_savedSpeakerId);
                }

                if (_speakerId == Guid.Empty)
                {
                    ((MainWindow)Application.Current.MainWindow).Log("You need to create a profile and complete enrollments first before verification");
                    recordBtn.IsEnabled = false;
                    stopRecordBtn.IsEnabled = false;
                }
                else
                {
                    initializeRecorder();
                    _serviceClient = new SpeakerVerificationServiceClient(_subscriptionKey);
                    string userPhrase = _storageHelper.readValue(MainWindow.SPEAKER_PHRASE_FILENAME);
                    userPhraseTxt.Text = userPhrase;
                    stopRecordBtn.IsEnabled = false;
                }
            }
            catch (Exception gexp)
            {
                setStatus("Error: " + gexp.Message);
                recordBtn.IsEnabled = false;
                stopRecordBtn.IsEnabled = false;
            }
        }

        /// <summary>
        /// Initialize NAudio recorder instance
        /// </summary>
        private void initializeRecorder()
        {
            _waveIn = new WaveIn();
            _waveIn.DeviceNumber = 0;
            int sampleRate = 16000; // 16 kHz
            int channels = 1; // mono
            _waveIn.WaveFormat = new WaveFormat(sampleRate, channels);
            _waveIn.DataAvailable += waveIn_DataAvailable;
            _waveIn.RecordingStopped += waveSource_RecordingStopped;
        }

        /// <summary>
        /// A listener called when the recording stops
        /// </summary>
        /// <param name="sender">Sender object responsible for event</param>
        /// <param name="e">A set of arguments sent to the listener</param>
        private void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            _fileWriter.Dispose();
            _fileWriter = null;
            _stream.Seek(0, SeekOrigin.Begin);
            //Dispose recorder object
            _waveIn.Dispose();
            initializeRecorder();
            verifySpeaker(_stream);
        }

        /// <summary>
        /// A method that's called whenever there's a chunk of recorded audio is recorded
        /// </summary>
        /// <param name="sender">The sender object responsible for the event</param>
        /// <param name="e">The arguments of the event object</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_fileWriter == null)
            {
                _stream = new IgnoreDisposeStream(new MemoryStream());
                _fileWriter = new WaveFileWriter(_stream, _waveIn.WaveFormat);
            }
            _fileWriter.Write(e.Buffer, 0, e.BytesRecorded);
        }

        /// <summary>
        /// Verifies the speaker by using the audio
        /// </summary>
        /// <param name="audioStream">The audio stream</param>
        private async void verifySpeaker(Stream audioStream)
        {
            try
            {
                setStatus("Verifying..");
                Stopwatch sw = Stopwatch.StartNew();
                Verification response = await _serviceClient.VerifyAsync(audioStream, _speakerId);
                sw.Stop();
                setStatus("Verification Done, Elapsed Time: " + sw.Elapsed);
                statusResTxt.Text = response.Result.ToString();
                confTxt.Text = response.Confidence.ToString();
                if (response.Result == Result.Accept)
                {
                    statusResTxt.Background = Brushes.Green;
                    statusResTxt.Foreground = Brushes.White;
                }
                else
                {
                    statusResTxt.Background = Brushes.Red;
                    statusResTxt.Foreground = Brushes.White;
                }
            }
            catch (VerificationException exception)
            {
                setStatus("Cannot verify speaker: " + exception.Message);
            }
            catch(Exception e)
            {
                setStatus("Error: " + e);
            }
        }

        /// <summary>
        /// A helper method that's used for logging status at the status bar
        /// </summary>
        /// <param name="status">The status to be logged at the status bar</param>
        private void setStatus(string status)
        {
            ((MainWindow)Application.Current.MainWindow).Log(status);
        }

        /// <summary>
        /// Click handler for the stop recording button
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="e">Event arguments object</param>
        private void stopRecordBtn_Click_1(object sender, RoutedEventArgs e)
        {
            recordBtn.IsEnabled = true;
            stopRecordBtn.IsEnabled = false;
            _waveIn.StopRecording();
        }

        /// <summary>
        /// Click handler for the record button
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="e">Event arguments object</param>
        private void recordBtn_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WaveIn.DeviceCount == 0)
                {
                    throw new Exception("Cannot detect microphone.");
                }
                _waveIn.StartRecording();
                recordBtn.IsEnabled = false;
                stopRecordBtn.IsEnabled = true;
                setStatus("Recording...");
            }
            catch (Exception ge)
            {
                setStatus("Error: " + ge.Message);
            }
        }
    }
}

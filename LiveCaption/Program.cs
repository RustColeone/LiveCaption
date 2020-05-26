using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Google.Cloud.Speech.V1;

namespace LiveCaption
{
    class Program
    {
        private WaveFileWriter RecordedAudioWriter = null;
        private WasapiLoopbackCapture CaptureInstance = null;

        public MMDeviceEnumerator AudioEnumerator;
        public MMDeviceCollection AllDevices;
        static void Main(string[] args)
        {
            Program Recording = new Program();
            Console.WriteLine("Looking for devices:");
            int[] MicID = Recording.findAllSpeakers();
            Console.WriteLine("Found:");
            for (int i = 0; i < Recording.AllDevices.Count; i++)
            {
                Console.WriteLine("> "+Recording.AllDevices[i].FriendlyName);
            }
            Console.ReadLine();
            Console.WriteLine("Recording Started");
            Recording.startRecording();
            Console.ReadLine();
            Console.WriteLine("Recording Ended");
            Recording.stopRecording();
            Recording.resampling();
            Recording.GRecognition();
            int TempTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            /*while (!Console.KeyAvailable)
            {
                
                string temp = "";
                for (int i = 0; i < Amplitude; i++)
                {
                    temp += "##";
                }
                Console.WriteLine(temp);
                
                float Amplitude = Recording.currentAmplitude(MicID) * 100;
                Console.WriteLine(Amplitude);
            }*/
        }

        int[] findAllSpeakers()
        {
            AudioEnumerator = new MMDeviceEnumerator();
            AllDevices = AudioEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
            //int[] ids = new int[] { };
            string ids = "";
            string[] TempArrays;
            for (int i = 0; i < AllDevices.Count; i++)
            {
                string tempName = AllDevices[i].FriendlyName;
                if (!(tempName.Contains("Mic") || tempName.Contains("mic")))
                {
                    ids += i+", ";
                }
            }
            TempArrays = ids.Split(new string[] { ", " }, StringSplitOptions.None);
            int[] MicID = new int[TempArrays.Length-1];
            for(int i = 0; i < MicID.Length; i++)
            {
                MicID[i] = Convert.ToInt32(TempArrays[i]);
            }
            return MicID;
        }

        float currentAmplitude(int[] MicID)
        {
            float max = 0;
            for(int i = 0; i < MicID.Length; i++)
            {
                float tempAmplitude = AllDevices[MicID[i]].AudioMeterInformation.MasterPeakValue;
                if (tempAmplitude > max)
                {
                    max = tempAmplitude;
                }
            }
            return max;
        }

        void startRecording()
        {
            // Define the output wav file of the recorded audio
            string outputFilePath = @"system_recorded_audio.wav";

            // Redefine the capturer instance with a new instance of the LoopbackCapture class
            this.CaptureInstance = new WasapiLoopbackCapture();

            // Redefine the audio writer instance with the given configuration
            this.RecordedAudioWriter = new WaveFileWriter(outputFilePath, CaptureInstance.WaveFormat);

            // When the capturer receives audio, start writing the buffer into the mentioned file
            this.CaptureInstance.DataAvailable += (s, a) =>
            {
                this.RecordedAudioWriter.Write(a.Buffer, 0, a.BytesRecorded);
            };

            // When the Capturer Stops
            this.CaptureInstance.RecordingStopped += (s, a) =>
            {
                this.RecordedAudioWriter.Dispose();
                this.RecordedAudioWriter = null;
                CaptureInstance.Dispose();
            };

            // Start recording !
            this.CaptureInstance.StartRecording();
        }
        void stopRecording()
        {
            // Stop recording !
            this.CaptureInstance.StopRecording();
        }

        void resampling()
        {
            this.RecordedAudioWriter.Close();
            int outRate = 16000;
            var inFile = @"system_recorded_audio.wav";
            var outFile = @"resampled.wav";
            bool Done = false;
            using (var reader = new WaveFileReader(inFile))
            {
                var outFormat = new WaveFormat(outRate, 1);
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    WaveFileWriter.CreateWaveFile(outFile, resampler);
                }
            }
        }
        void GRecognition()
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "GOOGLECREDENTIALS.json");
            var speech = SpeechClient.Create();
            var response = speech.Recognize(new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRateHertz = 16000,
                LanguageCode = "en",
            }, RecognitionAudio.FromFile("resampled.wav"));


            string Text = "";

            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    Text = Text + " " + alternative.Transcript;
                }
            }
            Console.WriteLine(Text);
        }
    }
}

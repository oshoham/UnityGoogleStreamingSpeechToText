using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Cloud.Speech.V1;
using Grpc.Core;

namespace GoogleCloudStreamingSpeechToText {
    [Serializable]
    public class TranscriptionEvent : UnityEvent<string> { }

    [RequireComponent(typeof(AudioSource))]
    public class StreamingRecognizer : MonoBehaviour {
        public string microphoneName {
            get => _microphoneName;
            set {
                if (_microphoneName == value) {
                    return;
                }

                _microphoneName = value;
                if (Application.isPlaying && IsListening()) {
                    Restart();
                }
            }
        }

        public bool startOnAwake = true;
        public bool returnInterimResults = true;
        public bool enableDebugLogging = false;
        public UnityEvent onStartListening;
        public UnityEvent onStopListening;
        public TranscriptionEvent onFinalResult = new TranscriptionEvent();
        public TranscriptionEvent onInterimResult = new TranscriptionEvent();

        private bool _initialized = false;
        private bool _listening = false;
        private bool _restart = false;
        private bool _newStreamOnRestart = false;
        private bool _newStream = false;
        [SerializeField] private string _microphoneName;
        private AudioSource _audioSource;
        private CancellationTokenSource _cancellationTokenSource;
        private byte[] _buffer;
        private SpeechClient.StreamingRecognizeStream _streamingCall;
        private List<ByteString> _audioInput = new List<ByteString>();
        private List<ByteString> _lastAudioInput = new List<ByteString>();
        private int _resultEndTime = 0;
        private int _isFinalEndTime = 0;
        private int _finalRequestEndTime = 0;
        private double _bridgingOffset = 0;

        private const string CredentialFileName = "gcp_credentials.json";
        private const double NormalizedFloatTo16BitConversionFactor = 0x7FFF + 0.4999999999999999;
        private const float MicInitializationTimeout = 1;
        private const int StreamingLimit = 290000; // almost 5 minutes

        public void StartListening() {
            if (!_initialized) {
                return;
            }

            StartCoroutine(nameof(RequestMicrophoneAuthorizationAndStartListening));
        }

        public async void StopListening() {
            if (!_initialized || _cancellationTokenSource == null) {
                return;
            }

            try {
                Task whenCanceled = Task.Delay(Timeout.InfiniteTimeSpan, _cancellationTokenSource.Token);

                _cancellationTokenSource.Cancel();

                try {
                    await whenCanceled;
                } catch (TaskCanceledException) {
                    if (enableDebugLogging) {
                        Debug.Log("Stopped.");
                    }
                }
            } catch (ObjectDisposedException) {}
        }

        public bool IsListening() {
            return _listening;
        }

        private void Restart() {
            if (!_initialized) {
                return;
            }

            _restart = true;
            StopListening();
        }

        private void Awake() {
            string credentialsPath = Path.Combine(Application.streamingAssetsPath, CredentialFileName);
            if (!File.Exists(credentialsPath)) {
                Debug.LogError("Could not find StreamingAssets/gcp_credentials.json. Please create a Google service account key for a Google Cloud Platform project with the Speech-to-Text API enabled, then download that key as a JSON file and save it as StreamingAssets/gcp_credentials.json in this project. For more info on creating a service account key, see Google's documentation: https://cloud.google.com/speech-to-text/docs/quickstart-client-libraries#before-you-begin");
                return;
            }

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);

            AudioConfiguration audioConfiguration = AudioSettings.GetConfiguration();

            _buffer = new byte[audioConfiguration.dspBufferSize * 2];

            _audioSource = gameObject.GetComponent<AudioSource>();
            AudioMixer audioMixer = (AudioMixer)Resources.Load("MicrophoneMixer");
            AudioMixerGroup[] audioMixerGroups = audioMixer.FindMatchingGroups("MuteMicrophone");
            if (audioMixerGroups.Length > 0) {
                _audioSource.outputAudioMixerGroup = audioMixerGroups[0];
            }

            string[] microphoneDevices = Microphone.devices;
            if (string.IsNullOrEmpty(_microphoneName) || Array.IndexOf(microphoneDevices, _microphoneName) == -1) {
                _microphoneName = microphoneDevices[0];
            }

            _initialized = true;

            if (startOnAwake) {
                StartListening();
            }
        }

        private void OnDestroy() {
            if (!_initialized) {
                return;
            }

            Microphone.End(_microphoneName);
            _audioSource.Stop();
            _cancellationTokenSource?.Dispose();
        }

        private async void OnAudioFilterRead(float[] data, int channels) {
            if (!_listening) {
                return;
            }

            if (_newStream && _lastAudioInput.Count != 0) {
                // Approximate math to calculate time of chunks
                double chunkTime = StreamingLimit / (double)_lastAudioInput.Count;
                if (!Mathf.Approximately((float)chunkTime, 0)) {
                    if (_bridgingOffset < 0) {
                        _bridgingOffset = 0;
                    }
                    if (_bridgingOffset > _finalRequestEndTime) {
                        _bridgingOffset = _finalRequestEndTime;
                    }
                    int chunksFromMS = (int)Math.Floor(
                        (_finalRequestEndTime - _bridgingOffset) / chunkTime
                    );
                    _bridgingOffset = (int)Math.Floor(
                        (_lastAudioInput.Count - chunksFromMS) * chunkTime
                    );

                    for (int i = chunksFromMS; i < _lastAudioInput.Count; i++) {
                        await _streamingCall.WriteAsync(new StreamingRecognizeRequest() {
                            AudioContent = _lastAudioInput[i]
                        });
                    }
                }
            }
            _newStream = false;

            // convert 1st channel of audio from floating point to 16 bit packed into a byte array
            // reference: https://github.com/naudio/NAudio/blob/ec5266ca90e33809b2c0ceccd5fdbbf54e819568/Docs/RawSourceWaveStream.md#playing-from-a-byte-array
            for (int i = 0; i < data.Length / channels; i++) {
                short sample = (short)(data[i * channels] * NormalizedFloatTo16BitConversionFactor);
                byte[] bytes = BitConverter.GetBytes(sample);
                _buffer[i * 2] = bytes[0];
                _buffer[i * 2 + 1] = bytes[1];
            }

            ByteString chunk = ByteString.CopyFrom(_buffer, 0, _buffer.Length);

            _audioInput.Add(chunk);

            await _streamingCall.WriteAsync(new StreamingRecognizeRequest() { AudioContent = chunk });
        }

        private IEnumerator RequestMicrophoneAuthorizationAndStartListening() {
            while (!Application.HasUserAuthorization(UserAuthorization.Microphone)) {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }

            InitializeMicrophoneAndBeginStream();
        }

        private void InitializeMicrophoneAndBeginStream() {
            if (enableDebugLogging) {
                Debug.Log("Starting...");
            }

            AudioConfiguration audioConfiguration = AudioSettings.GetConfiguration();
            _audioSource.clip = Microphone.Start(_microphoneName, true, 10, audioConfiguration.sampleRate);

            // wait for microphone to initialize
            float timerStartTime = Time.realtimeSinceStartup;
            bool timedOut = false;
            while (!(Microphone.GetPosition(_microphoneName) > 0) && !timedOut) {
                timedOut = Time.realtimeSinceStartup - timerStartTime >= MicInitializationTimeout;
            }

            if (timedOut) {
                Debug.LogError("Unable to initialize microphone.");
                return;
            }

            _audioSource.loop = true;
            _audioSource.Play();

            StreamingMicRecognizeAsync();
        }

        private async Task HandleTranscriptionResponses() {
            while (await _streamingCall.ResponseStream.MoveNext(default)) {
                if (_streamingCall.ResponseStream.Current.Results.Count <= 0) {
                    continue;
                }

                StreamingRecognitionResult result = _streamingCall.ResponseStream.Current.Results[0];
                if (result.Alternatives.Count <= 0) {
                    continue;
                }

                _resultEndTime = (int)((result.ResultEndTime.Seconds * 1000) + (result.ResultEndTime.Nanos / 1000000));

                string transcript = result.Alternatives[0].Transcript.Trim();

                if (result.IsFinal) {
                    if (enableDebugLogging) {
                        Debug.Log("Final: " + transcript);
                    }

                    _isFinalEndTime = _resultEndTime;

                    onFinalResult.Invoke(transcript);
                } else {
                    if (returnInterimResults) {
                        if (enableDebugLogging) {
                            Debug.Log("Interim: " + transcript);
                        }

                        onInterimResult.Invoke(transcript);
                    }
                }
            }
        }

        private async void RestartAfterStreamingLimit() {
            if (_cancellationTokenSource == null) {
                return;
            }
            try {
                await Task.Delay(StreamingLimit, _cancellationTokenSource.Token);

                _newStreamOnRestart = true;

                if (enableDebugLogging) {
                    Debug.Log("Streaming limit reached, restarting...");
                }

                Restart();
            } catch (TaskCanceledException) {}
        }

        private async void StreamingMicRecognizeAsync() {
            SpeechClient speech = SpeechClient.Create();
            _streamingCall = speech.StreamingRecognize();

            AudioConfiguration audioConfiguration = AudioSettings.GetConfiguration();

            // Write the initial request with the config.
            await _streamingCall.WriteAsync(new StreamingRecognizeRequest() {
                StreamingConfig = new StreamingRecognitionConfig() {
                    Config = new RecognitionConfig() {
                        Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                        SampleRateHertz = audioConfiguration.sampleRate,
                        LanguageCode = "en",
                        MaxAlternatives = 1
                    },
                    InterimResults = returnInterimResults,
                }
            });

            _cancellationTokenSource = new CancellationTokenSource();

            Task handleTranscriptionResponses = HandleTranscriptionResponses();

            _listening = true;

            if (!_restart) {
                onStartListening.Invoke();
            }

            if (enableDebugLogging) {
                Debug.Log("Ready to transcribe.");
            }

            RestartAfterStreamingLimit();

            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, _cancellationTokenSource.Token);
            } catch (TaskCanceledException) {
                // Stop recording and shut down.
                if (enableDebugLogging) {
                    Debug.Log("Stopping...");
                }

                _listening = false;

                Microphone.End(microphoneName);
                _audioSource.Stop();

                await _streamingCall.WriteCompleteAsync();
                try {
                    await handleTranscriptionResponses;
                } catch (RpcException) {}

                if (!_restart) {
                    onStopListening.Invoke();
                }

                if (_restart) {
                    _restart = false;
                    if (_newStreamOnRestart) {
                        _newStreamOnRestart = false;

                        _newStream = true;

                        if (_resultEndTime > 0) {
                            _finalRequestEndTime = _isFinalEndTime;
                        }
                        _resultEndTime = 0;

                        _lastAudioInput = null;
                        _lastAudioInput = _audioInput;
                        _audioInput = new List<ByteString>();
                    }
                    StartListening();
                }
            }
        }
    }
}

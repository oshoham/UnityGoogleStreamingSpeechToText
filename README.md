# Unity Google Streaming Speech-to-Text

[![openupm](https://img.shields.io/npm/v/com.oshoham.unity-google-cloud-streaming-speech-to-text?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.oshoham.unity-google-cloud-streaming-speech-to-text/)

## Compatibility

This library has been tested on OS X and Windows 10 in Unity 2019.1.4f1. At this time, I don't have the time and resources to add support for Android, iOS, or other platforms, but I welcome PRs that address this.

## Installation

This library is available as a Unity package hosted on [UPM](https://openupm.com/packages/com.oshoham.unity-google-cloud-streaming-speech-to-text). The easiest way to install it is via the [UPM command-line tool](https://github.com/openupm/openupm-cli#openupm-cli) (requires Node.js version 12 or higher):

```bash
# Install openupm-cli
npm install -g openupm-cli

# Go to your Unity project directory
cd YOUR_UNITY_PROJECT_DIR

# Install the package
openupm add com.oshoham.unity-google-cloud-streaming-speech-to-text
```

## Setup

1. In the Project Settings menu, change Player -> Configuration -> API Compatibility Level to **.NET 4.x**. 
2. Follow step 1 of Google's [Cloud Speech-to-Text Quickstart Guide](https://cloud.google.com/speech-to-text/docs/quickstart-client-libraries#before-you-begin) to:
    1. Set up a GCP Console project.
    2. Enable the Speech-to-Text API for your project.
    3. Create a service account.
    4. Download your service account's private key as a JSON file.
3. Rename your private key JSON file to `gcp_credentials.json`.
4. Place your `gcp_credentials.json` file in a folder called `Assets/StreamingAssets` in your Unity project.

## Usage

In your Unity scene, create a new `GameObject` and attach the `StreamingRecognizer` MonoBehavior to it.

If you want to quickly test that things are working, check the `Enable Debug Logging` option on the `StreamingRecognizer`, then play your scene. You should see some debugging messages appear in the Console, along with a live transcription of any speech audible to your computer's microphone.

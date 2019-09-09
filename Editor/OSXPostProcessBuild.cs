#if UNITY_EDITOR

using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

// Adapted from https://github.com/firebase/quickstart-unity/issues/152#issuecomment-389800268

namespace GoogleCloudStreamingSpeechToText {
    public class OSXPostProcessBuild : MonoBehaviour {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {
            if (target != BuildTarget.StandaloneOSX) {
                return;
            }
            
            string frameworksPath = Path.Combine(pathToBuiltProject, "Contents/Frameworks/MonoEmbedRuntime/osx");
            Directory.CreateDirectory(frameworksPath);
            
            string grpcBundlePath = Path.GetFullPath("Packages/com.oshoham.unity-google-cloud-streaming-speech-to-text/Plugins/Grpc.Core/runtimes/osx/x64/grpc_csharp_ext.bundle");
            FileUtil.CopyFileOrDirectory(grpcBundlePath, Path.Combine(frameworksPath, "libgrpc_csharp_ext.bundle"));
        }
    }
}

#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nekomimi.Daimao
{
    public sealed class Filer
    {
        public const string RequestPath = "filer";

        private enum PathType
        {
            Persistent,
            Tmp,
            AndroidFilesDir,
        }

        private static readonly Dictionary<PathType, string> Path = new Dictionary<PathType, string>()
        {
            { PathType.Persistent, "persistent" },
            { PathType.Tmp, "tmp" },
            { PathType.AndroidFilesDir, "filesdir" },
        };

        private readonly Dictionary<PathType, string> _pathCache = new Dictionary<PathType, string>();

        private async UniTask CachePath()
        {
            await UniTask.SwitchToMainThread();

            _pathCache[PathType.Persistent] = Application.persistentDataPath;
            _pathCache[PathType.Tmp] = Application.temporaryCachePath;

#if !UNITY_EDITOR && UNITY_ANDROID
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var getFilesDir = currentActivity.Call<AndroidJavaObject>("getFilesDir"))
                {
                    _pathCache[PathType.AndroidFilesDir] = getFilesDir.Call<string>("getCanonicalPath");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
#endif
        }

        public async UniTask ServeFile(HttpListenerResponse response, FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            using (var fileStream = fileInfo.OpenRead())
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                await fileStream.CopyToAsync(response.OutputStream);
                fileStream.Close();
            }
        }

        private DirectoryInfo GetDir(PathType pathType, string path)
        {
            var dir = System.IO.Path.Combine(_pathCache[pathType], path);
            var info = new DirectoryInfo(dir);
            return info;
        }
    }
}
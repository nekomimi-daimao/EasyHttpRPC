using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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

        private static readonly Dictionary<PathType, string> PathTable = new Dictionary<PathType, string>()
        {
            { PathType.Persistent, "persistent" },
            { PathType.Tmp, "tmp" },
            { PathType.AndroidFilesDir, "filesdir" },
        };

        private readonly Dictionary<PathType, string> _pathCache = new Dictionary<PathType, string>();

        public async UniTask CachePath()
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

        private static async UniTask ServeFile(HttpListenerResponse response, string file)
        {
            var fileInfo = new FileInfo(file);

            if (!fileInfo.Exists)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            using (var fileStream = fileInfo.OpenRead())
            {
                await fileStream.CopyToAsync(response.OutputStream);
                fileStream.Close();
            }

            response.StatusCode = (int)HttpStatusCode.OK;
        }

        private async UniTask ParseDir(HttpListenerResponse response, string dir)
        {
            var directoryInfo = new DirectoryInfo(dir);

            if (!directoryInfo.Exists)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var builderDir = new StringBuilder();
            foreach (var dirInfo in directoryInfo.EnumerateDirectories())
            {
                builderDir.AppendLine("<a href=`./_REPLACE_`>_REPLACE_</a>".Replace("_REPLACE_", dirInfo.Name));
            }

            var builderFile = new StringBuilder();
            foreach (var fileInfo in directoryInfo.EnumerateFiles())
            {
                builderFile.AppendLine("<a href=`./_REPLACE_`>_REPLACE_</a>".Replace("_REPLACE_", fileInfo.Name));
            }

            using (var streamWriter = new StreamWriter(response.OutputStream))
            {
                // TODO replaced html
                // await streamWriter.WriteAsync(message);
            }

            response.StatusCode = (int)HttpStatusCode.OK;
        }

        public async UniTask Parse(HttpListenerRequest request, HttpListenerResponse response)
        {
            // 1234:filer/persistent/a.txt
            // 1234:filer/persistent/dir/dir2/b.txt
            var param = request.Url.Segments.SkipWhile(s => !s.StartsWith(RequestPath)).Skip(1).ToArray();
            var pair = PathTable.FirstOrDefault(pair => string.Equals(pair.Value, param[0]));

            if (string.IsNullOrEmpty(pair.Value))
            {
                // TODO return top
                return;
            }

            if (!_pathCache.TryGetValue(pair.Key, out var pathType))
            {
                // TODO error response
                return;
            }

            param[0] = pathType;
            var combined = Path.Combine(param);

            if (File.Exists(combined))
            {
                await ServeFile(response, combined);
            }
            else if (Directory.Exists(combined))
            {
                await ParseDir(response, combined);
            }
            else
            {
                // TODO error no such file
            }
        }
    }
}
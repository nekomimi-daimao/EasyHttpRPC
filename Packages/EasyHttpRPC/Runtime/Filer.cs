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

            response.StatusCode = (int)HttpStatusCode.OK;
            using (var fileStream = fileInfo.OpenRead())
            {
                await fileStream.CopyToAsync(response.OutputStream);
                fileStream.Close();
            }
        }

        private static UniTask ServeDir(HttpListenerResponse response, string dir)
        {
            var directoryInfo = new DirectoryInfo(dir);

            if (!directoryInfo.Exists)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return UniTask.CompletedTask;
            }

            var builderDir = new StringBuilder();
            foreach (var dirInfo in directoryInfo.EnumerateDirectories())
            {
                builderDir.AppendLine(
                    "<li><a href='javascript:void(0);' onclick=onLinkDir(arguments[0])>_REPLACE_</a></li>"
                        .Replace("_REPLACE_", dirInfo.Name));
            }

            var builderFile = new StringBuilder();
            foreach (var fileInfo in directoryInfo.EnumerateFiles())
            {
                builderFile.AppendLine(
                    "<li><a href='javascript:void(0);' onclick=onLinkFile(arguments[0])>_REPLACE_</a></li>"
                        .Replace("_REPLACE_", fileInfo.Name));
            }

            return ServeHtml(response,
                GenerateHtml(directoryInfo.FullName, builderDir.ToString(), builderFile.ToString()));
        }

        private static UniTask ServeMessage(HttpListenerResponse response, string message)
        {
            return ServeHtml(response, GenerateHtml(message, null, null));
        }

        private static async UniTask ServeHtml(HttpListenerResponse response, string html)
        {
            using (var streamWriter = new StreamWriter(response.OutputStream))
            {
                await streamWriter.WriteAsync(html);
            }

            response.ContentType = "text/html;";
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        public async UniTask Serve(HttpListenerRequest request, HttpListenerResponse response)
        {
            var param = request.Url.Segments.SkipWhile(s => !s.StartsWith(RequestPath)).Skip(1).ToArray();
            if (!param.Any())
            {
                await ServeMessage(response, null);
                return;
            }

            var pair = PathTable.FirstOrDefault(pair =>
                string.Equals(pair.Value, param[0].Replace("/", string.Empty)));
            // FirstOrDefault, default
            if (string.IsNullOrEmpty(pair.Value))
            {
                await ServeMessage(response, string.Join(string.Empty, param));
                return;
            }

            if (!_pathCache.TryGetValue(pair.Key, out var pathType))
            {
                await ServeMessage(response, "invalid pathType");
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
                await ServeDir(response, combined);
            }
            else
            {
                await ServeMessage(response, "no such path");
            }
        }

        private const string ReplaceMessage = "<!--REPLACE_MESSAGE-->";
        private const string ReplaceDir = "<!--REPLACE_DIR-->";
        private const string ReplaceFile = "<!--REPLACE_FILE-->";

        private static string GenerateHtml(string message, string dir, string file)
        {
            return Html
                .Replace(ReplaceMessage, message)
                .Replace(ReplaceDir, dir)
                .Replace(ReplaceFile, file);
        }

        private const string Html = @"

<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>EasyHttpRPC/Filer</title>
    <script type='text/javascript'>
        function pathChanged() {
            const select = document.getElementById('pathType');
            if (!select.value || select.value.length === 0) {
                return;
            }
            window.location.pathname = `/filer/${select.value}`;
        }

        function back() {
            const current = window.location.pathname;
            window.location.pathname = current.substring(0, current.lastIndexOf('/') + 1);
        }

        function onLinkDir(e) {
            const name = e.target.textContent;
            window.location.pathname += `/${encodeURIComponent(name)}`;
        }

        function onLinkFile(e) {
            const name = e.target.textContent;
            const url = new URL(window.location.href);
            for (let key of url.searchParams.keys()) {
                url.searchParams.delete(key);
            }
            url.href += `/${encodeURIComponent(name)}`;
            const a = document.createElement('a');
            document.body.appendChild(a);
            a.download = name;
            a.href = url.href;
            a.click();
            a.remove();
        }

    </script>
    <style>
        body {
            padding: 20px 40px;
        }
    </style>
</head>
<body>
<h1>EasyHttpRPC/Filer</h1>
<b>
    <!--REPLACE_MESSAGE-->
</b>
<br>
<a href='javascript:void(0);' onclick=back()> ← back </a>
<form>
    <label>path :
        <select id='pathType' onchange='pathChanged()'>
            <option selected value=''>none</option>
            <option value='persistent'>persistent</option>
            <option value='tmp'>tmp</option>
            <option value='getfilesdir'>getfilesdir</option>
        </select>
    </label>
</form>
<hr>
<ul>
    <!--REPLACE_DIR-->
</ul>
<hr>
<ul>
    <!--REPLACE_FILE-->
</ul>
</body>
</html>


";
    }
}
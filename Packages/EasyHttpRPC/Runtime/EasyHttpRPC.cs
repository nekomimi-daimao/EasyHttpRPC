/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2021-2023 NekomimiDaimao
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 * https://gist.github.com/nekomimi-daimao/e5726cde473de30a12273cd827779704
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nekomimi.Daimao
{
    public sealed class EasyHttpRPC
    {
        private HttpListener _httpListener;
        public bool IsListening => _httpListener != null && _httpListener.IsListening;

        private const int PortDefault = 1234;

        /// <summary>
        /// post, this key
        /// </summary>
        public const string PostKey = "post";

        public EasyHttpRPC(CancellationToken cancellationToken, int port = PortDefault)
        {
            if (!HttpListener.IsSupported)
            {
                return;
            }

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($@"http://+:{port}/");
            _httpListener.Start();

            ListeningLoop(_httpListener, cancellationToken).Forget();
        }

        public void Close()
        {
            _httpListener?.Close();
            _httpListener = null;
        }

        private readonly Dictionary<string, Func<NameValueCollection, UniTask<string>>> _functions =
            new Dictionary<string, Func<NameValueCollection, UniTask<string>>>();

        public string[] Registered => _functions.Keys.ToArray();

        /// <summary>
        /// Register function.
        /// <see cref="Func<NameValueCollection, UniTask<string>>"/>
        /// 
        /// <code>
        /// private async UniTask<string> Example(NameValueCollection arg)
        /// {
        ///     var example = arg["example"];
        ///     var result = await SomethingAsync(example);
        ///     return result;
        /// }
        /// </code>
        /// </summary>
        /// <param name="method">treated as lowercase</param>
        /// <param name="func"><see cref="Func<NameValueCollection, UniTask<string>>"/></param>
        /// 
        public void RegisterRPC(string method, Func<NameValueCollection, UniTask<string>> func)
        {
            _functions[method.ToLower()] = func;
        }

        /// <summary>
        /// unregister function
        /// </summary>
        /// <param name="method"></param>
        public void UnregisterRPC(string method)
        {
            _functions.Remove(method);
        }

        private async UniTaskVoid ListeningLoop(HttpListener listener, CancellationToken token)
        {
            token.Register(() => { listener?.Close(); });

            await UniTask.SwitchToThreadPool();

            while (true)
            {
                if (token.IsCancellationRequested || !listener.IsListening)
                {
                    break;
                }

                HttpListenerResponse response = null;
                var statusCode = HttpStatusCode.InternalServerError;

                try
                {
                    string message;

                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    response = context.Response;
                    response.ContentEncoding = Encoding.UTF8;

                    var method = request.RawUrl?.Split('?')[0].Remove(0, 1).ToLower();

                    if (string.IsNullOrEmpty(method))
                    {
                        statusCode = HttpStatusCode.OK;
                        response.ContentType = "text/html;";
                        message = Html;
                    }
                    else if (_functions.TryGetValue(method, out var func))
                    {
                        try
                        {
                            NameValueCollection nv = null;
                            if (string.Equals(request.HttpMethod, HttpMethod.Get.Method))
                            {
                                nv = request.QueryString;
                            }
                            else if (string.Equals(request.HttpMethod, HttpMethod.Post.Method))
                            {
                                string content;
                                using (var reader = new StreamReader(request.InputStream))
                                {
                                    content = await reader.ReadToEndAsync();
                                }

                                nv = new NameValueCollection { [PostKey] = content };
                            }

                            message = await func(nv);
                            statusCode = HttpStatusCode.OK;
                        }
                        catch (Exception e)
                        {
                            message = e.Message;
                        }
                    }
                    else
                    {
                        message = $"non-registered : {method}";
                    }

                    response.StatusCode = (int)statusCode;
                    using (var streamWriter = new StreamWriter(response.OutputStream))
                    {
                        await streamWriter.WriteAsync(message);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // NOP
                }
                finally
                {
                    response?.Close();
                }
            }
        }

        private const string Html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>EasyHttpRPC</title>
    <script type=""text/javascript"">

        async function send() {
            const kv = collect();
            const url = new URL(window.location.origin);
            url.pathname = document.getElementById(""path"").value;
            url.search = new URLSearchParams(kv).toString();
            let result;
            try {
                const request = await fetch(url);
                result = await request.text();
            } catch (err) {
                result = err;
            }
            document.getElementById(""result"").value = result;

            store();
            restoreSelect();
        }

        function add() {
            const kvArray = document.querySelectorAll("".kv"");
            const kv = kvArray[kvArray.length - 1];
            const clone = kv.cloneNode(true);
            const k = clone.querySelector("".k"");
            const v = clone.querySelector("".v"");
            k.value = null;
            v.value = null;
            kv.after(clone);
        }

        function remove() {
            const kvArray = document.querySelectorAll("".kv"");
            if (kvArray.length <= 1) {
                return;
            }

            for (let i = kvArray.length - 1; i >= 0; i--) {
                const kv = kvArray[i];
                const k = kv.querySelector("".k"");
                const v = kv.querySelector("".v"");
                if (k.value || v.value) {
                    continue;
                }
                kv.remove();
                return;
            }
            kvArray[kvArray.length - 1].remove();
        }

        function collect() {
            const co = {};
            const kvArray = document.querySelectorAll("".kv"");
            for (let i = 0; i < kvArray.length; i++) {
                const kv = kvArray[i];
                const k = kv.querySelector("".k"");
                const v = kv.querySelector("".v"");
                if (k.value || v.value) {
                    co[k.value] = v.value;
                }
            }
            return co;
        }

        function clearKeyValue() {
            const kvArray = document.querySelectorAll("".kv"");
            for (let i = 0; i < kvArray.length; i++) {
                const kv = kvArray[i];
                const k = kv.querySelector("".k"");
                const v = kv.querySelector("".v"");
                k.value = null;
                v.value = null;
            }
        }

        // store
        function store() {
            const path = document.getElementById(""path"").value;
            if (!path) {
                return;
            }
            const co = collect();
            localStorage[path] = JSON.stringify(co);
        }

        function restore() {
            const select = document.getElementById(""stored"");
            const selected = select.options[select.selectedIndex];
            const path = document.getElementById(""path"");
            if (!selected.value) {
                path.value = null;
                clearKeyValue();
                return;
            }
            const itemJson = localStorage.getItem(selected.value);
            if (!itemJson) {
                return;
            }
            path.value = selected.value;
            clearKeyValue();
            const item = JSON.parse(itemJson);
            let kvArray = document.querySelectorAll("".kv"");
            const keys = Object.keys(item);
            const diff = keys.length - kvArray.length;
            if (diff > 0) {
                for (let i = 0; i < diff; i++) {
                    add();
                }
                kvArray = document.querySelectorAll("".kv"");
            }

            for (let i = 0; i < keys.length; i++) {
                const value = item[keys[i]];
                const kv = kvArray[i];
                const k = kv.querySelector("".k"");
                const v = kv.querySelector("".v"");
                k.value = keys[i];
                v.value = value;
            }
        }

        function restoreSelect() {
            clearSelect();
            const select = document.getElementById(""stored"");
            Object.keys(localStorage).forEach((key) => {
                const option = new Option(key, key);
                select.add(option);
            });
        }

        function clearSelect() {
            const select = document.getElementById(""stored"");
            for (let i = select.options.length - 1; i >= 1; i--) {
                select.options[i].remove();
            }
        }

        function clearStored() {
            const clear = confirm(""clear stored input?"");
            if (!clear) {
                return;
            }
            localStorage.clear();
            clearSelect();
            clearKeyValue();
            document.getElementById(""path"").value = null;
        }

    </script>

    <style>

        body {
            padding: 20px 40px;
        }

        #path {
            margin: 4px auto;
        }

        .action_container {
            display: flex;
            padding: 4px 8px;
            margin-bottom: 4px;
        }

        .action {
            margin-right: 12px;
        }

        .clear {
            margin-left: auto;
            margin-right: 12px;
        }

        .v {
            width: 20em;
        }

    </style>

</head>
<body onload=""restoreSelect()"">
<h1>EasyHttpRPC</h1>

<form>
    <label>result<br>
        <textarea id=""result"" rows=""6"" cols=""60""> </textarea>
    </label>
    <br>
    <label>restore :
        <select id=""stored"" onchange=""restore()"">
            <option selected value="""">new</option>
        </select>
    </label>
    <br>
    <label>pass :
        <input type=""text"" id=""path"">
    </label>
    <br>
    <div class=""action_container"">
        <button type=""button"" class=""action"" onclick=send()>Send</button>
        <button type=""button"" class=""action"" onclick=add()>Add</button>
        <button type=""button"" class=""action"" onclick=remove()>Remove</button>
        <button type=""button"" class=""clear"" onclick=clearStored()>Clear</button>
    </div>
    <div class=""kv"">
        <label>
            <input type=""text"" class=""k"" placeholder=""key"">
            <input type=""text"" class=""v"" placeholder=""value"">
        </label>
    </div>
</form>

</body>
</html>
";
    }
}

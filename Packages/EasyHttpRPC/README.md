# EasyHttpRPC

simple, http-based RPC for Unity

## install

Window -> Package Manager -> Add package from git url

`https://github.com/nekomimi-daimao/EasyHttpRPC.git?path=/Packages/EasyHttpRPC`

### Defines

`Define Constraints` is as follows.

`UNITY_EDITOR || DEVELOPMENT_BUILD || ALLOW_EHR`

If you want to use it in a release build, define `ALLOW_EHR`.

## Usage

### RPC

#### Add RPC

```csharp
// create rpc
private UniTask<string> Ping(NameValueCollection arg)
{
    var builder = new StringBuilder();
    foreach (var s in EasyHttpRPC.Registered)
    {
        builder.AppendLine(s);
    }

    return UniTask.FromResult(builder.ToString());
}
```

```csharp
// register
EasyHttpRpcHolder.EasyHttpRPC.RegisterRPC(nameof(holder.Ping), holder.Ping);
```

#### Use Browser

`192.168.12.34` → http://192.168.12.34:1234

### Filer

browse and download app files

Click 📁

| key         | path                                                                                                                                |
|-------------|-------------------------------------------------------------------------------------------------------------------------------------|
| persistent  | [Application.persistentDataPath](https://docs.unity3d.com/ja/2023.2/ScriptReference/Application-persistentDataPath.html)            |
| tmp         | [Application.temporaryCachePath](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Application-temporaryCachePath.html) |
| getfilesdir | (Android Only) getFilesDir()                                                                                                        |

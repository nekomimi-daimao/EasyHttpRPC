# EasyHttpRPC

simple, http-based RPC for Unity

## install

Window -> Package Manager -> Add package from git url

`https://github.com/nekomimi-daimao/EasyHttpRPC.git?path=/Packages/EasyHttpRPC`

## Usage

### Add RPC

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

### Use

`192.168.12.34` → http://192.168.12.34:1234

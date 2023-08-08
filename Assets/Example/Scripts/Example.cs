using System;
using System.Collections.Specialized;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Nekomimi.Daimao;
using UnityEngine;

namespace Example.Scripts
{
    public sealed class Example : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody cubeRigidbody;

        private void Start()
        {
            EasyHttpRpcHolder.EasyHttpRPC.RegisterRPC(nameof(Jump), Jump);
            EasyHttpRpcHolder.EasyHttpRPC.RegisterRPC(nameof(ResetCube), ResetCube);
        }

        private async UniTask<string> Jump(NameValueCollection arg)
        {
            await UniTask.SwitchToMainThread();

            var forceRaw = arg["force"];
            if (!float.TryParse(forceRaw, out var force))
            {
                force = 5f;
            }

            cubeRigidbody.AddForce(Vector3.up * force, ForceMode.Impulse);

            return $"{force}";
        }

        private async UniTask<string> ResetCube(NameValueCollection arg)
        {
            await UniTask.SwitchToMainThread();
            cubeRigidbody.velocity = Vector3.zero;
            cubeRigidbody.angularVelocity = Vector3.zero;
            cubeRigidbody.position = Vector3.zero;
            cubeRigidbody.rotation = Quaternion.identity;
            return "reset!";
        }
    }
}
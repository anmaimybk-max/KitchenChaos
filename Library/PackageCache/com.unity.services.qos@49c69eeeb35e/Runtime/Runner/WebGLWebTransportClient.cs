#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;

namespace Unity.Services.Qos.Runner
{
    /// <summary>
    /// Browser WebTransport session backed by Plugins/QosWebTransport.jslib.
    /// </summary>
    class WebGLWebTransportClient : IWebTransportClient
    {
        static readonly Dictionary<int, WebGLWebTransportClient> s_Instances = new Dictionary<int, WebGLWebTransportClient>();
        static bool s_CallbacksRegistered;

        int m_InstanceId = -1;
        TaskCompletionSource<bool> m_ConnectCompletion;

        public event Action<byte[]> DatagramReceived;
        public event Action<Exception> Faulted;

        public Task ConnectAsync(string url)
        {
            RegisterCallbacks();

            m_ConnectCompletion = new TaskCompletionSource<bool>();
            m_InstanceId = QosWebTransportAllocate(url);
            s_Instances[m_InstanceId] = this;

            var result = QosWebTransportConnect(m_InstanceId);
            if (result != 0)
            {
                m_ConnectCompletion.TrySetException(new PlatformNotSupportedException($"WebTransport connect failed with code {result}."));
            }

            return m_ConnectCompletion.Task;
        }

        public void Send(byte[] datagram)
        {
            var result = QosWebTransportSend(m_InstanceId, datagram, datagram.Length);
            if (result != 0)
            {
                throw new InvalidOperationException($"WebTransport send failed with code {result}.");
            }
        }

        public void Dispose()
        {
            if (m_InstanceId >= 0)
            {
                QosWebTransportClose(m_InstanceId);
                s_Instances.Remove(m_InstanceId);
                m_InstanceId = -1;
            }
        }

        static void RegisterCallbacks()
        {
            if (s_CallbacksRegistered)
            {
                return;
            }

            QosWebTransportSetOnOpen(OnOpen);
            QosWebTransportSetOnDatagram(OnDatagram);
            QosWebTransportSetOnError(OnError);
            s_CallbacksRegistered = true;
        }

        delegate void OnOpenCallback(int instanceId);
        delegate void OnDatagramCallback(int instanceId, IntPtr buffer, int length);
        delegate void OnErrorCallback(int instanceId, IntPtr message);

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        static void OnOpen(int instanceId)
        {
            if (s_Instances.TryGetValue(instanceId, out var client))
            {
                client.m_ConnectCompletion?.TrySetResult(true);
            }
        }

        [MonoPInvokeCallback(typeof(OnDatagramCallback))]
        static void OnDatagram(int instanceId, IntPtr buffer, int length)
        {
            if (!s_Instances.TryGetValue(instanceId, out var client))
            {
                return;
            }

            var data = new byte[length];
            Marshal.Copy(buffer, data, 0, length);
            client.DatagramReceived?.Invoke(data);
        }

        [MonoPInvokeCallback(typeof(OnErrorCallback))]
        static void OnError(int instanceId, IntPtr message)
        {
            if (!s_Instances.TryGetValue(instanceId, out var client))
            {
                return;
            }

            var exception = new Exception(Marshal.PtrToStringAuto(message));
            if (!client.m_ConnectCompletion.TrySetException(exception))
            {
                client.Faulted?.Invoke(exception);
            }
        }

        [DllImport("__Internal")]
        static extern void QosWebTransportSetOnOpen(OnOpenCallback callback);

        [DllImport("__Internal")]
        static extern void QosWebTransportSetOnDatagram(OnDatagramCallback callback);

        [DllImport("__Internal")]
        static extern void QosWebTransportSetOnError(OnErrorCallback callback);

        [DllImport("__Internal")]
        static extern int QosWebTransportAllocate(string url);

        [DllImport("__Internal")]
        static extern int QosWebTransportConnect(int instanceId);

        [DllImport("__Internal")]
        static extern int QosWebTransportSend(int instanceId, byte[] buffer, int length);

        [DllImport("__Internal")]
        static extern int QosWebTransportClose(int instanceId);
    }
}
#endif

using System;
using System.Threading.Tasks;

namespace Unity.Services.Qos.Runner
{
    /// <summary>
    /// Minimal WebTransport session used by <see cref="WebTransportQosRunner"/>:
    /// connect, exchange datagrams, close.
    /// </summary>
    interface IWebTransportClient : IDisposable
    {
        /// <summary>
        /// Raised for every datagram received on the session.
        /// </summary>
        event Action<byte[]> DatagramReceived;

        /// <summary>
        /// Raised when the session fails after it was established.
        /// </summary>
        event Action<Exception> Faulted;

        /// <summary>
        /// Opens the session. The returned task completes when the session is
        /// ready and faults when the connection fails or the platform does not
        /// support WebTransport.
        /// </summary>
        Task ConnectAsync(string url);

        /// <summary>
        /// Sends one datagram over the session. Throws when the datagram could
        /// not be handed to the transport.
        /// </summary>
        void Send(byte[] datagram);
    }
}

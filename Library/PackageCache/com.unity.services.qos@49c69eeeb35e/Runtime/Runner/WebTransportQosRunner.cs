using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Qos.Models;

namespace Unity.Services.Qos.Runner
{
    delegate IWebTransportClient WebTransportClientProvider();

    /// <summary>
    /// QoS runner for platforms without UDP sockets (WebGL). Measures latency
    /// and packet loss by exchanging QoS protocol packets as WebTransport
    /// datagrams with servers that advertise an HTTPS endpoint URL. Servers
    /// without a WebTransport endpoint report invalid measurements, which
    /// callers already filter out.
    /// </summary>
    class WebTransportQosRunner : IQosRunner
    {
        internal const int RequestsPerEndpoint = 5;
        internal const int TimeoutMs = 10000;
        const int k_MaxLatencyMs = 60000;
        const byte k_RequestMagic = 0x59;
        const byte k_ResponseMagic = 0x95;
        const string k_RequestTitle = "QoS request";
        const int k_MinResponseLength = 13;

        static readonly IQosMeasurements k_InvalidMeasurements = new QosMeasurements(int.MaxValue, 1f);

        readonly WebTransportClientProvider m_ClientProvider;
        readonly int m_TimeoutMs;

        public WebTransportQosRunner(WebTransportClientProvider clientProvider = null, int timeoutMs = TimeoutMs)
        {
            m_ClientProvider = clientProvider ?? DefaultClientProvider;
            m_TimeoutMs = timeoutMs;
        }

        static IWebTransportClient DefaultClientProvider()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWebTransportClient();
#else
            throw new PlatformNotSupportedException("WebTransportQosRunner is only supported on WebGL.");
#endif
        }

        public async Task<List<Internal.QosResult>> MeasureQosAsync(IList<QosServer> servers)
        {
            var measurements = await MeasureAllAsync(servers.Select(server => server.Endpoints));

            var results = new List<Internal.QosResult>(servers.Count);
            for (var i = 0; i < servers.Count; i++)
            {
                results.Add(new Internal.QosResult
                {
                    Region = servers[i].Region,
                    AverageLatencyMs = measurements[i].AverageLatencyMs,
                    PacketLossPercent = measurements[i].PacketLossPercent
                });
            }

            return results;
        }

        public async Task<List<QosAnnotatedResult>> MeasureQosAsync(IList<QosServiceServer> servers)
        {
            var measurements = await MeasureAllAsync(servers.Select(server => server.Endpoints));

            var results = new List<QosAnnotatedResult>(servers.Count);
            for (var i = 0; i < servers.Count; i++)
            {
                results.Add(new QosAnnotatedResult
                {
                    Region = servers[i].Region,
                    AverageLatencyMs = measurements[i].AverageLatencyMs,
                    PacketLossPercent = measurements[i].PacketLossPercent,
                    Annotations = servers[i].Annotations
                });
            }

            return results;
        }

        public async Task<List<(V2.Models.QosServer, IQosMeasurements)>> MeasureQosV2Async(IList<V2.Models.QosServer> servers)
        {
            var measurements = await MeasureAllAsync(servers.Select(server => server.Endpoints));

            var results = new List<(V2.Models.QosServer, IQosMeasurements)>(servers.Count);
            for (var i = 0; i < servers.Count; i++)
            {
                results.Add((servers[i], measurements[i]));
            }

            return results;
        }

        // Servers are measured concurrently under one time budget, like the
        // Baselib job's single expiry, so one slow or lossy server cannot
        // starve the others.
        Task<IQosMeasurements[]> MeasureAllAsync(IEnumerable<IList<string>> serverEndpoints)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(m_TimeoutMs);
            return Task.WhenAll(serverEndpoints.Select(endpoints => MeasureServerAsync(endpoints, deadline)));
        }

        async Task<IQosMeasurements> MeasureServerAsync(IList<string> endpoints, DateTime deadline)
        {
            var url = FindWebTransportEndpoint(endpoints);
            if (url == null)
            {
                return k_InvalidMeasurements;
            }

            try
            {
                return await MeasureEndpointAsync(url, deadline);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"QoS WebTransport measurement failed for '{url}': {e.Message}");
                return k_InvalidMeasurements;
            }
        }

        // WebTransport endpoints are advertised by QoS discovery as extra
        // scheme-prefixed entries in the endpoints list, alongside the
        // host:port UDP endpoint at index 0.
        internal static string FindWebTransportEndpoint(IList<string> endpoints)
        {
            if (endpoints == null)
            {
                return null;
            }

            foreach (var endpoint in endpoints)
            {
                if (endpoint != null && endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return endpoint;
                }
            }

            return null;
        }

        async Task<IQosMeasurements> MeasureEndpointAsync(string url, DateTime deadline)
        {
            using var client = m_ClientProvider();

            var identifier = (ushort)new System.Random().Next(ushort.MaxValue);
            var seenSequences = 0;
            var responseCount = 0;
            var totalLatency = 0L;
            Exception fault = null;

            client.DatagramReceived += datagram =>
            {
                if (!TryParseResponse(datagram, identifier, out var sequence, out var latency))
                {
                    return;
                }

                var sequenceBit = 1 << sequence;
                if ((seenSequences & sequenceBit) != 0)
                {
                    return;
                }

                seenSequences |= sequenceBit;
                responseCount++;
                totalLatency += latency;
            };
            client.Faulted += e => fault = e;

            var connectTask = client.ConnectAsync(url);
            while (!connectTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                await Task.Yield();
            }

            if (!connectTask.IsCompleted)
            {
                throw new TimeoutException($"WebTransport connection to '{url}' timed out.");
            }

            await connectTask;

            for (byte sequence = 0; sequence < RequestsPerEndpoint; sequence++)
            {
                client.Send(BuildRequest(sequence, identifier));
            }

            while (responseCount < RequestsPerEndpoint && fault == null && DateTime.UtcNow < deadline)
            {
                await Task.Yield();
            }

            if (fault != null && responseCount < RequestsPerEndpoint)
            {
                throw fault;
            }

            if (responseCount == 0)
            {
                return k_InvalidMeasurements;
            }

            return new QosMeasurements(
                (int)(totalLatency / responseCount),
                (RequestsPerEndpoint - responseCount) / (float)RequestsPerEndpoint);
        }

        internal static byte[] BuildRequest(byte sequence, ushort identifier)
        {
            var title = System.Text.Encoding.UTF8.GetBytes(k_RequestTitle);
            var packet = new byte[3 + title.Length + 1 + 2 + 8];
            packet[0] = k_RequestMagic;
            packet[1] = 0x00;
            packet[2] = (byte)(title.Length + 1);
            Buffer.BlockCopy(title, 0, packet, 3, title.Length);

            var offset = 3 + title.Length;
            packet[offset++] = sequence;
            packet[offset++] = (byte)(identifier & 0xFF);
            packet[offset++] = (byte)(identifier >> 8);

            var timestamp = (ulong)NowMs();
            for (var i = 0; i < 8; i++)
            {
                packet[offset + i] = (byte)(timestamp >> (8 * i));
            }

            return packet;
        }

        internal static bool TryParseResponse(byte[] datagram, ushort identifier, out byte sequence, out long latencyMs)
        {
            sequence = 0;
            latencyMs = 0;
            if (datagram == null || datagram.Length < k_MinResponseLength || datagram[0] != k_ResponseMagic)
            {
                return false;
            }

            if ((datagram[1] & 0xF0) != 0 || datagram[2] >= RequestsPerEndpoint)
            {
                return false;
            }

            sequence = datagram[2];

            var responseIdentifier = (ushort)(datagram[3] | (datagram[4] << 8));
            if (responseIdentifier != identifier)
            {
                return false;
            }

            var timestamp = 0UL;
            for (var i = 0; i < 8; i++)
            {
                timestamp |= (ulong)datagram[5 + i] << (8 * i);
            }

            if (timestamp > long.MaxValue)
            {
                return false;
            }

            latencyMs = NowMs() - (long)timestamp;
            return latencyMs >= 0 && latencyMs <= k_MaxLatencyMs;
        }

        static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        readonly struct QosMeasurements : IQosMeasurements
        {
            public QosMeasurements(int averageLatencyMs, float packetLossPercent)
            {
                AverageLatencyMs = averageLatencyMs;
                PacketLossPercent = packetLossPercent;
            }

            public int AverageLatencyMs { get; }
            public float PacketLossPercent { get; }
        }
    }
}

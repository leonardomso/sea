#if UNITY_5_3_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpacetimeDB
{
    /// <summary>
    /// Small standalone transport for Unity targets.
    ///
    /// Unity's embedded ClientWebSocket can connect to some WebSocket servers but
    /// stalls while reading the SpacetimeDB upgrade response. This transport keeps
    /// the SDK protocol-independent and handles the RFC 6455 framing directly.
    /// It is intentionally limited to the frames needed by SpacetimeDB; TLS is
    /// supported for remote wss:// endpoints, while certificate validation remains
    /// delegated to the platform.
    /// </summary>
    internal sealed class UnityTcpWebSocket
    {
        private const int HandshakeTimeoutMilliseconds = 10000;
        private const int MaxHandshakeBytes = 65536;
        private const int MaxMessageSize = 0x4000000;
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly string protocol;
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private readonly object stateLock = new();

        private TcpClient? client;
        private Stream? stream;
        private CancellationTokenSource? lifetime;
        private bool closeNotified;

        public UnityTcpWebSocket(string protocol)
        {
            this.protocol = protocol;
            State = WebSocketState.None;
        }

        public event Action? OnConnect;
        public event Action<byte[], DateTime>? OnMessage;
        public event Action<Exception?>? OnClose;
        public event Action<Exception>? OnConnectError;
        public event Action<Exception>? OnSendError;

        public WebSocketState State { get; private set; }
        public bool IsConnected => State == WebSocketState.Open;
        public bool IsConnecting => State == WebSocketState.Connecting;
        public bool IsNoneState => State == WebSocketState.None;

        public async Task Connect(
            string? auth,
            string host,
            string nameOrAddress,
            ConnectionId connectionId,
            Compression compression,
            bool light,
            bool? confirmedReads)
        {
            if (IsConnected || IsConnecting)
            {
                return;
            }

            SetState(WebSocketState.Connecting);
            lifetime = new CancellationTokenSource();

            try
            {
                var uri = BuildUri(host, nameOrAddress, connectionId, compression, light, confirmedReads);
                client = new TcpClient
                {
                    NoDelay = true
                };

                using (var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    handshakeTimeout.CancelAfter(HandshakeTimeoutMilliseconds);
                    await ConnectTcp(uri, handshakeTimeout.Token);
                    stream = await CreateStream(uri, handshakeTimeout.Token);
                    await PerformHandshake(uri, auth, handshakeTimeout.Token);
                }

                SetState(WebSocketState.Open);
                OnConnect?.Invoke();
                _ = ReceiveLoop(lifetime.Token);
            }
            catch (Exception exception)
            {
                SetState(WebSocketState.Closed);
                Abort();
                OnConnectError?.Invoke(exception);
            }
        }

        public void SendBinary(byte[] payload)
        {
            if (!IsConnected)
            {
                OnSendError?.Invoke(new InvalidOperationException("Cannot send on a closed WebSocket."));
                return;
            }

            _ = SendBinaryAsync(payload);
        }

        public Task Close()
        {
            return CloseAsync();
        }

        public void Abort()
        {
            try
            {
                lifetime?.Cancel();
            }
            catch
            {
                // Abort is best effort.
            }

            try
            {
                stream?.Close();
            }
            catch
            {
                // Abort is best effort.
            }

            try
            {
                client?.Close();
            }
            catch
            {
                // Abort is best effort.
            }

            SetState(WebSocketState.Closed);
        }

        private static Uri BuildUri(
            string host,
            string nameOrAddress,
            ConnectionId connectionId,
            Compression compression,
            bool light,
            bool? confirmedReads)
        {
            var uri = $"{host}/v1/database/{nameOrAddress}/subscribe?connection_id={connectionId}&compression={compression}";
            if (light)
            {
                uri += "&light=true";
            }

            if (confirmedReads.HasValue)
            {
                uri += $"&confirmed={(confirmedReads.Value ? "true" : "false")}";
            }

            return new Uri(uri);
        }

        private async Task ConnectTcp(Uri uri, CancellationToken cancellationToken)
        {
            var connectTask = client!.ConnectAsync(uri.Host, uri.Port);
            var timeoutTask = Task.Delay(HandshakeTimeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Timed out connecting to {uri.Host}:{uri.Port}.");
            }

            await connectTask;
        }

        private async Task<Stream> CreateStream(Uri uri, CancellationToken cancellationToken)
        {
            var networkStream = client!.GetStream();
            if (uri.Scheme != "wss" && uri.Scheme != "https")
            {
                return networkStream;
            }

            var sslStream = new SslStream(networkStream, false);
            var authenticateTask = sslStream.AuthenticateAsClientAsync(uri.Host);
            var timeoutTask = Task.Delay(HandshakeTimeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(authenticateTask, timeoutTask) != authenticateTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Timed out negotiating TLS with {uri.Host}.");
            }

            await authenticateTask;
            return sslStream;
        }

        private async Task PerformHandshake(Uri uri, string? auth, CancellationToken cancellationToken)
        {
            var keyBytes = new byte[16];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(keyBytes);
            }

            var key = Convert.ToBase64String(keyBytes);
            var hostHeader = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            var request = new StringBuilder()
                .Append("GET ").Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n")
                .Append("Host: ").Append(hostHeader).Append("\r\n")
                .Append("Connection: Upgrade\r\n")
                .Append("Upgrade: websocket\r\n")
                .Append("Sec-WebSocket-Version: 13\r\n")
                .Append("Sec-WebSocket-Key: ").Append(key).Append("\r\n")
                .Append("Sec-WebSocket-Protocol: ").Append(protocol).Append("\r\n");

            if (!string.IsNullOrWhiteSpace(auth))
            {
                request.Append("Authorization: Bearer ").Append(auth).Append("\r\n");
            }

            request.Append("\r\n");
            var requestBytes = Encoding.ASCII.GetBytes(request.ToString());
            await stream!.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            var responseBytes = await ReadHeaders(cancellationToken);
            var response = Encoding.ASCII.GetString(responseBytes);
            var lines = response.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                throw new InvalidOperationException("WebSocket upgrade returned an empty response.");
            }

            var statusParts = lines[0].Split(new[] { ' ' }, 3);
            if (statusParts.Length < 2 || !int.TryParse(statusParts[1], out var statusCode))
            {
                throw new InvalidOperationException($"WebSocket upgrade returned an invalid status line: {lines[0]}");
            }

            if (statusCode != 101)
            {
                var reason = statusParts.Length == 3 ? statusParts[2] : "Upgrade rejected";
                throw new WebSocketUpgradeException(statusCode, reason);
            }

            var accept = FindHeader(lines, "Sec-WebSocket-Accept");
            string expectedAccept;
            using (var sha1 = SHA1.Create())
            {
                expectedAccept = Convert.ToBase64String(sha1.ComputeHash(
                    Encoding.ASCII.GetBytes(key + WebSocketGuid)));
            }
            if (!string.Equals(accept, expectedAccept, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("WebSocket upgrade returned an invalid Sec-WebSocket-Accept value.");
            }

            var negotiatedProtocol = FindHeader(lines, "Sec-WebSocket-Protocol");
            if (!string.Equals(negotiatedProtocol, protocol, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"WebSocket server negotiated '{negotiatedProtocol ?? "no protocol"}', expected '{protocol}'.");
            }
        }

        private async Task<byte[]> ReadHeaders(CancellationToken cancellationToken)
        {
            var bytes = new MemoryStream();
            var oneByte = new byte[1];
            while (bytes.Length < MaxHandshakeBytes)
            {
                var count = await stream!.ReadAsync(oneByte, 0, 1, cancellationToken);
                if (count == 0)
                {
                    throw new EndOfStreamException("WebSocket closed during the upgrade handshake.");
                }

                bytes.WriteByte(oneByte[0]);
                if (bytes.Length >= 4)
                {
                    var value = bytes.GetBuffer();
                    var length = (int)bytes.Length;
                    if (value[length - 4] == '\r' && value[length - 3] == '\n'
                        && value[length - 2] == '\r' && value[length - 1] == '\n')
                    {
                        return bytes.ToArray();
                    }
                }
            }

            throw new InvalidOperationException("WebSocket upgrade headers exceeded the maximum size.");
        }

        private static string? FindHeader(string[] lines, string name)
        {
            foreach (var line in lines)
            {
                var separator = line.IndexOf(':');
                if (separator > 0 && string.Equals(line.Substring(0, separator), name, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(separator + 1).Trim();
                }
            }

            return null;
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            Exception? closeError = null;
            try
            {
                using (var message = new MemoryStream())
                {
                    var fragmented = false;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var header = new byte[2];
                        if (!await ReadExact(header, cancellationToken))
                        {
                            break;
                        }

                        var fin = (header[0] & 0x80) != 0;
                        var opcode = header[0] & 0x0f;
                        var masked = (header[1] & 0x80) != 0;
                        var length = header[1] & 0x7f;
                        if ((header[0] & 0x70) != 0)
                        {
                            throw new InvalidOperationException("WebSocket frame uses unsupported reserved bits.");
                        }

                        if (masked)
                        {
                            throw new InvalidOperationException("Server WebSocket frames must not be masked.");
                        }

                        if (opcode >= 0x8 && (!fin || length > 125))
                        {
                            throw new InvalidOperationException("WebSocket control frame is invalid.");
                        }

                        var payloadLength = await ReadPayloadLength(length, cancellationToken);
                        if (payloadLength > MaxMessageSize)
                        {
                            throw new InvalidOperationException("WebSocket message exceeded the maximum size.");
                        }

                        var payload = new byte[payloadLength];
                        if (!await ReadExact(payload, cancellationToken))
                        {
                            break;
                        }

                        if (opcode == 0x8)
                        {
                            await SendFrame(0x8, payload, cancellationToken);
                            closeError = DecodeCloseError(payload);
                            break;
                        }

                        if (opcode == 0x9)
                        {
                            await SendFrame(0xA, payload, cancellationToken);
                            continue;
                        }

                        if (opcode == 0xA)
                        {
                            continue;
                        }

                        if (opcode == 0x2)
                        {
                            message.SetLength(0);
                            fragmented = !fin;
                        }
                        else if (opcode == 0x0 && fragmented)
                        {
                            // Continuation frame.
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unsupported WebSocket opcode 0x{opcode:X2}.");
                        }

                        message.Write(payload, 0, payload.Length);
                        if (message.Length > MaxMessageSize)
                        {
                            throw new InvalidOperationException("WebSocket message exceeded the maximum size.");
                        }

                        if (fin)
                        {
                            OnMessage?.Invoke(message.ToArray(), DateTime.UtcNow);
                            fragmented = false;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                closeError = exception;
            }
            finally
            {
                Abort();
                NotifyClose(closeError);
            }
        }

        private async Task<int> ReadPayloadLength(int length, CancellationToken cancellationToken)
        {
            if (length <= 125)
            {
                return length;
            }

            if (length == 126)
            {
                var extended = new byte[2];
                if (!await ReadExact(extended, cancellationToken))
                {
                    throw new EndOfStreamException("WebSocket closed while reading the frame length.");
                }

                return (extended[0] << 8) | extended[1];
            }

            var extendedLong = new byte[8];
            if (!await ReadExact(extendedLong, cancellationToken) || (extendedLong[0] & 0x80) != 0)
            {
                throw new InvalidOperationException("WebSocket frame length is invalid.");
            }

            long result = 0;
            for (var index = 0; index < extendedLong.Length; index++)
            {
                result = (result << 8) | extendedLong[index];
                if (result > MaxMessageSize)
                {
                    throw new InvalidOperationException("WebSocket message exceeded the maximum size.");
                }
            }

            return (int)result;
        }

        private async Task<bool> ReadExact(byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var count = await stream!.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                if (count == 0)
                {
                    return false;
                }

                offset += count;
            }

            return true;
        }

        private async Task SendBinaryAsync(byte[] payload)
        {
            try
            {
                await SendFrame(0x2, payload, lifetime?.Token ?? CancellationToken.None);
            }
            catch (Exception exception)
            {
                OnSendError?.Invoke(exception);
            }
        }

        private async Task CloseAsync()
        {
            if (IsConnected)
            {
                try
                {
                    await SendFrame(0x8, new byte[0], CancellationToken.None);
                }
                catch
                {
                    // Closing is best effort.
                }
            }

            Abort();
        }

        private async Task SendFrame(byte opcode, byte[] payload, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                return;
            }

            await sendLock.WaitAsync(cancellationToken);
            try
            {
                var headerLength = payload.Length <= 125 ? 6 : payload.Length <= ushort.MaxValue ? 8 : 14;
                var frame = new byte[headerLength + payload.Length];
                frame[0] = (byte)(0x80 | opcode);
                var offset = 2;
                if (payload.Length <= 125)
                {
                    frame[1] = (byte)(0x80 | payload.Length);
                }
                else if (payload.Length <= ushort.MaxValue)
                {
                    frame[1] = 0xFE;
                    frame[2] = (byte)(payload.Length >> 8);
                    frame[3] = (byte)payload.Length;
                    offset = 4;
                }
                else
                {
                    frame[1] = 0xFF;
                    var longLength = (ulong)payload.Length;
                    for (var index = 0; index < 8; index++)
                    {
                        frame[2 + index] = (byte)(longLength >> (56 - (index * 8)));
                    }

                    offset = 10;
                }

                var mask = new byte[4];
                using (var random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(mask);
                }

                Buffer.BlockCopy(mask, 0, frame, offset, mask.Length);
                offset += mask.Length;
                for (var index = 0; index < payload.Length; index++)
                {
                    frame[offset + index] = (byte)(payload[index] ^ mask[index % 4]);
                }

                await stream.WriteAsync(frame, 0, frame.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private static Exception? DecodeCloseError(byte[] payload)
        {
            if (payload.Length < 2)
            {
                return null;
            }

            var code = (payload[0] << 8) | payload[1];
            var reason = payload.Length > 2 ? Encoding.UTF8.GetString(payload, 2, payload.Length - 2) : string.Empty;
            return code == 1000 ? null : new Exception($"WebSocket closed with code {code}: {reason}");
        }

        private void NotifyClose(Exception? exception)
        {
            lock (stateLock)
            {
                if (closeNotified)
                {
                    return;
                }

                closeNotified = true;
            }

            OnClose?.Invoke(exception);
        }

        private void SetState(WebSocketState state)
        {
            State = state;
            if (state == WebSocketState.Connecting)
            {
                closeNotified = false;
            }
        }
    }
}
#endif

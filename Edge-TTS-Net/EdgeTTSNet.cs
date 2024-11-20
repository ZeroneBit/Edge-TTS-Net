using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace edge_tts_net
{
    public class EdgeTTSNet : IDisposable
    {
        public Action<TTSMetadata> OnMetadata;
        public Action<TTSStream> OnStream;

        private ClientWebSocket _clientWebSocket;
        private string _webProxy;

        public EdgeTTSNet(string webProxy = null)
        {
            _webProxy = webProxy;
        }

        public void Dispose()
        {
            _clientWebSocket?.Dispose();
        }

        public async Task<TTSResult> TTS(string text, CancellationToken cancellationToken = default, TTSOption option = default)
        {
            if (option == default) 
                option = TTSOption.Default;

            var finalUtterance = new Dictionary<int, int>();
            var prevIdx = -1;
            var shiftTime = -1;
            var idx = 0;

            var metaResult = new List<TTSMetadata>();
            var streamResult = new MemoryStream();

            await EnsureConnection(cancellationToken);
            var texts = SplitTextByByteLength(Escape(RemoveIncompatibleCharacters(text)), CalcMaxMsgSize(option));

            foreach (var partial in texts)
            {
                await SendCommondRequest(cancellationToken);
                await SendSsmlRequest(text, option, cancellationToken);

                var state = SessionState.NotStarted;
                while (_clientWebSocket.State == WebSocketState.Open)
                {
                    var (dataBytes, msg) = await ReceiveMessage(cancellationToken);

                    if (msg.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(dataBytes);
                        var (parameters, data) = GetHeadersAndData(message);
                        parameters.TryGetValue("Path", out var path);
                        switch (state)
                        {
                            case SessionState.NotStarted:
                                {
                                    if (path == "turn.start")
                                    {
                                        state = SessionState.TurnStarted;
                                    }
                                }
                                break;
                            case SessionState.TurnStarted:
                                {
                                    if (path == "turn.end")
                                    {
                                        throw new IOException("Unexpected turn.end");
                                    }
                                    else if (path == "turn.start")
                                    {
                                        throw new IOException("Turn already started");
                                    }
                                }
                                break;
                            case SessionState.Streaming:
                                {
                                    if (path == "audio.metadata")
                                    {
                                        var audioMetadata = JsonSerializer.Deserialize<MetadataModel>(data);
                                        if (audioMetadata == null)
                                            continue;

                                        foreach (var metadata in audioMetadata.Metadata)
                                        {
                                            var metaType = metadata.Type;
                                            if (idx != prevIdx)
                                            {
                                                var sum = 0;
                                                for (var i = 0; i < idx; i++)
                                                    sum += finalUtterance[i];
                                                shiftTime = sum;
                                                prevIdx = idx;
                                            }

                                            if (metaType == "WordBoundary")
                                            {
                                                finalUtterance[idx] = metadata.Data.Offset + metadata.Data.Duration + 8_750_000;

                                                var metaObj = new TTSMetadata
                                                {
                                                    MetaType = metaType,
                                                    Offset = metadata.Data.Offset + shiftTime,
                                                    Duration = metadata.Data.Duration,
                                                    Text = metadata.Data.text.Text,
                                                };

                                                if (OnMetadata != null)
                                                    OnMetadata(metaObj);

                                                metaResult.Add(metaObj);
                                            }
                                            else if (metaType == "SentenceBoundary")//need calculate finalUtterance for sentence?
                                            {
                                                var metaObj = new TTSMetadata
                                                {
                                                    MetaType = metaType,
                                                    Offset = metadata.Data.Offset + shiftTime,
                                                    Duration = metadata.Data.Duration,
                                                    Text = metadata.Data.text.Text,
                                                };

                                                if (OnMetadata != null)
                                                    OnMetadata(metaObj);

                                                metaResult.Add(metaObj);
                                            }
                                            else if (metaType == "SessionEnd")
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                throw new Exception($"Unknown metadata type: {metaType}");
                                            }
                                        }
                                    }
                                    else if (path == "turn.end")
                                    {
                                        return new TTSResult
                                        {
                                            Metadata = metaResult,
                                            Stream = streamResult.ToArray()
                                        };
                                    }
                                    else if (path == "response")
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new IOException($"Unexpected message during streaming: {message}");
                                    }
                                }
                                break;
                            default: break;
                        }
                    }
                    else if (msg.MessageType == WebSocketMessageType.Binary)
                    {
                        if (state == SessionState.NotStarted)
                        {
                            throw new IOException($"Unexpected Binary");
                        }
                        if (dataBytes.Length < 2)
                        {
                            throw new IOException("Message too short");
                        }

                        var headerLength = (dataBytes[0] << 8) | (dataBytes[1]);
                        if (headerLength + 2 > dataBytes.Length)
                        {
                            throw new IOException("Message too short");
                        }

                        state = SessionState.Streaming;
                        using (var audioStream = new MemoryStream(dataBytes, headerLength + 2, dataBytes.Length - headerLength - 2))
                        {
                            if (OnStream != null)
                                OnStream(new TTSStream { PartialStream = audioStream.ToArray() });

                            await audioStream.CopyToAsync(streamResult);
                        }
                    }
                    else if (msg.MessageType == WebSocketMessageType.Close)
                    {
                        throw new IOException("Unexpected closing of connection");
                    }
                }
            }

            return null;
        }

        private async Task<(byte[], WebSocketReceiveResult)> ReceiveMessage(CancellationToken cancellationToken)
        {
            using (var data = new MemoryStream())
            {
                WebSocketReceiveResult received = null;

                while (true)
                {
                    var buffer = new byte[5 * 1024];
                    received = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    data.Write(buffer, 0, received.Count);
                    if (received.Count == 0 || received.EndOfMessage)
                        break;
                }

                return (data.ToArray(), received);
            }
        }

        private async Task SendCommondRequest(CancellationToken cancellationToken)
        {
            var request = $"X-Timestamp:{DateToString()}\r\n" +
                        "Content-Type:application/json; charset=utf-8\r\n" +
                        "Path:speech.config\r\n\r\n" +
                        @"{""context"":{""synthesis"":{""audio"":{""metadataoptions"":{" +
                        @"""sentenceBoundaryEnabled"":true,""wordBoundaryEnabled"":true}," +
                        @"""outputFormat"":""audio-24khz-48kbitrate-mono-mp3""" +
                        "}}}}\r\n";

            await _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task SendSsmlRequest(string text, TTSOption option, CancellationToken cancellationToken)
        {
            var request = SsmlHeadersPlusData(ConnectId(), DateToString(), Mkssml(text, option));

            await _clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task EnsureConnection(CancellationToken cancellationToken)
        {
            while (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
            {
                _clientWebSocket?.Dispose();
                _clientWebSocket = new ClientWebSocket();
                var options = _clientWebSocket.Options;

                foreach (var wssheader in Constants.WSS_HEADERS)
                {
                    options.SetRequestHeader(wssheader.Key, wssheader.Value);
                }

                if (!string.IsNullOrEmpty(_webProxy))
                    options.Proxy = new WebProxy(_webProxy);

                var wssUrl = Constants.WSS_URL + "&Sec-MS-GEC=" + DRM.Generate_Sec_ms_gec() + "&Sec-MS-GEC-Version=" + Constants.SEC_MS_GEC_VERSION + "&ConnectionId=" + ConnectId();
                await _clientWebSocket.ConnectAsync(new Uri(wssUrl), cancellationToken);
                break;

                //var _httpClient = new HttpClient();
                //var request = new HttpRequestMessage(HttpMethod.Get, wssUrl);
                //request.Headers.Add("Connection", "Upgrade");
                //request.Headers.Add("Upgrade", "websocket");
                //request.Headers.Add("Sec-WebSocket-Version", "13");
                //request.Headers.Add("Sec-WebSocket-Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
                //var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                //if (response.StatusCode == HttpStatusCode.SwitchingProtocols)
                //{
                //    try
                //    {
                        
                //    }
                //    catch (WebSocketException ex)
                //    {
                //        Console.WriteLine(ex);
                //    }
                //}
                //else
                //{
                //    response.Headers.TryGetValues("Date", out var date);
                //    DRM.HandleClientResponse401Error(date.First());
                //    Console.WriteLine(  );
                //}
            }
        }

        private (Dictionary<string, string>, string) GetHeadersAndData(string data)
        {
            var headers = new Dictionary<string, string>();

            var index = data.IndexOf("\r\n\r\n");

            var lines = data.Substring(0, index).Split(
                new string[] { "\r\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var split = line.Split(
                    new string[] { ":" }, 2, StringSplitOptions.None);
                var key = split[0];
                var value = split[1];
                headers[key] = value;
            }
            return (headers, data.Substring(index + 4));
        }

        private IEnumerable<string> SplitTextByByteLength(string text, int byteLength)
        {
            if (byteLength < 0)
                throw new ArgumentOutOfRangeException("byteLength", "must be greater than 0");

            while (text.Length > byteLength)
            {
                var splitAt = text.LastIndexOf(' ', 0, byteLength);
                splitAt = splitAt != -1 ? splitAt : byteLength;

                while (true)
                {
                    var ampersandIndex = text.IndexOf('&', 0, splitAt);
                    if (ampersandIndex == -1)
                        break;

                    if (text.IndexOf(';', ampersandIndex, splitAt) != -1)
                        break;

                    splitAt = ampersandIndex - 1;
                    if (splitAt < 0)
                        throw new Exception("Maximum byte length is too small or invalid text.");
                    if (splitAt == 0)
                        break;
                }

                var _newText = text.Substring(0, splitAt).Trim();
                if (!string.IsNullOrEmpty(_newText))
                    yield return _newText;

                if (splitAt == 0)
                    splitAt = 1;

                text = text.Substring(splitAt);
            }

            var newText = text.Trim();
            if (!string.IsNullOrEmpty(newText))
                yield return newText;
        }

        private string RemoveIncompatibleCharacters(string str)
        {
            var chars = str.ToCharArray();

            for (var idx = 0; idx < chars.Length; idx++)
            {
                var c = chars[idx];
                var code = (int)c;
                if ((code >= 0 && code <= 8) ||
                    (code >= 11 && code <= 12) ||
                    (code >= 14 && code <= 31))
                {
                    chars[idx] = ' ';
                }
            }
            return new string(chars);
        }

        private int CalcMaxMsgSize(TTSOption option)
        {
            var websocketMaxSize = (int)Math.Pow(2, 16);
            var overheadPerMessage =
                SsmlHeadersPlusData(
                    ConnectId(),
                    DateToString(),
                    Mkssml("", option)
                ).Length + 50;   // margin of error
            return websocketMaxSize - overheadPerMessage;
        }

        private string SsmlHeadersPlusData(string requestId, string timestamp, string ssml)
        {
            return
                $"X-RequestId:{requestId}\r\n" +
                "Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{timestamp}Z\r\n" +  // This is not a mistake, Microsoft Edge bug.
                "Path:ssml\r\n\r\n" +
                $"{ssml}";
        }

        private string ConnectId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string Escape(string data)
        {
            return data
                .Replace("&", "&amp;")
                .Replace(">", "&gt;")
                .Replace("<", "&lt;");
        }

        private string Mkssml(string text, TTSOption option)
        {
            return
                "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                $"<voice name='{option.Voice}'><prosody pitch='{option.Pitch}' rate='{option.Rate}' volume='{option.Volume}'>" +
                $"{text}</prosody></voice></speak>";
        }

        /// <summary>
        /// Return Javascript-style date string.
        /// </summary>
        /// <returns></returns>
        private string DateToString()
        {
            return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss", CultureInfo.InvariantCulture) +
                " GMT+0000 (Coordinated Universal Time)";
        }
    }
}

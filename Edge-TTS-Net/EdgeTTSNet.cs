using edge_tts_net.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace edge_tts_net
{
    public class EdgeTTSNet
    {
        private TTSOption _option;
        private string _webProxy;

        public EdgeTTSNet(TTSOption option = default, string webProxy = null)
        {
            _webProxy = webProxy;
            _option = option;
            if (_option == default)
                _option = TTSOption.Default;
        }

        public async Task<List<TTSVoice>> GetVoices()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var jsonName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Contains("Voices.json"));
            using (var stream = assembly.GetManifestResourceStream(jsonName))
            {
                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();
                    return JsonSerializer.Deserialize<List<TTSVoice>>(json);
                }
            }
        }

        public async Task Save(string content, string path, CancellationToken cancellationToken = default)
        {
            using (var ms = new MemoryStream())
            {
                await TTS(content, (metaObj) =>
                {
                    if (metaObj.Type == TTSMetadataType.Audio)
                    {
                        ms.Write(metaObj.Data, 0, metaObj.Data.Length);
                    }
                }, cancellationToken);

                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        public async Task TTS(string content, Action<TTSMetadata> onMetadata, CancellationToken cancellationToken = default)
        {
            using (var webSocket = await EnsureConnection(cancellationToken))
            {
                var texts = SplitTextByByteLength(Escape(RemoveIncompatibleCharacters(content)), CalcMaxMsgSize(_option));
                var offset_compensation = 0.0;
                foreach (var text in texts)
                {
                    await SendCommondRequest(webSocket, cancellationToken);
                    await SendSsmlRequest(webSocket, text, _option, cancellationToken);

                    var last_duration_offset = 0.0;
                    while (webSocket.State == WebSocketState.Open)
                    {
                        var (dataBytes, received) = await ReceiveMessage(webSocket, cancellationToken);
                        if (received.MessageType == WebSocketMessageType.Text)
                        {
                            var msg = Encoding.UTF8.GetString(dataBytes);
                            var (parameters, data) = GetHeadersAndData(msg);
                            if (!parameters.TryGetValue("Path", out var path))
                                continue;

                            if (path == "audio.metadata")
                            {
                                foreach (var metadata in ParseMetadata(data, offset_compensation))
                                {
                                    onMetadata?.Invoke(metadata);

                                    if (metadata.Type == TTSMetadataType.WordBoundary)
                                        last_duration_offset = metadata.Offset + metadata.Duration;
                                }
                            }
                            else if (path == "turn.end")
                            {
                                offset_compensation = last_duration_offset;
                                offset_compensation += 8_750_000;
                                break;
                            }
                            else if (path == "response" || path == "turn.start")
                            {
                                continue;
                            }
                            else
                            {
                                throw new IOException($"Unexpected message during  {msg}");
                            }
                        }
                        else if (received.MessageType == WebSocketMessageType.Binary)
                        {
                            foreach (var audio in ParseAudioBytes(dataBytes))
                            {
                                onMetadata?.Invoke(audio);
                            }
                        }
                        else if (received.MessageType == WebSocketMessageType.Close)
                        {
                            throw new IOException("Unexpected closing of connection");
                        }
                    }
                }
            }
        }

        private async Task<(byte[], WebSocketReceiveResult)> ReceiveMessage(ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            using (var data = new MemoryStream())
            {
                WebSocketReceiveResult received = null;

                while (true)
                {
                    var buffer = new byte[5 * 1024];
                    received = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    data.Write(buffer, 0, received.Count);
                    if (received.Count == 0 || received.EndOfMessage)
                        break;
                }

                return (data.ToArray(), received);
            }
        }

        private IEnumerable<TTSMetadata> ParseMetadata(string msg, double offset_compensation)
        {
            var audioMetadata = JsonSerializer.Deserialize<MetadataModel>(msg);
            if (audioMetadata != null)
            {
                foreach (var metadata in audioMetadata.Metadata)
                {
                    var metaType = metadata.Type;
                    if (metaType == "WordBoundary")
                    {
                        yield return new TTSMetadata
                        {
                            Type = TTSMetadataType.WordBoundary,
                            Offset = metadata.Data.Offset + offset_compensation,
                            Duration = metadata.Data.Duration,
                            Text = metadata.Data.text.Text,
                        };
                    }
                    else if(metaType == "SentenceBoundary")
                    {
                        yield return new TTSMetadata
                        {
                            Type = TTSMetadataType.SentenceBoundary,
                            Offset = metadata.Data.Offset + offset_compensation,
                            Duration = metadata.Data.Duration,
                            Text = metadata.Data.text.Text,
                        };
                    }
                    else if(metaType == "SessionEnd")
                    {
                        continue;
                    }
                    else
                    {
                        throw new Exception($"Unknown metadata type: {metaType}");
                    }
                }
            }
        }

        private IEnumerable<TTSMetadata> ParseAudioBytes(byte[] dataBytes)
        {
            if (dataBytes.Length < 2)
            {
                throw new IOException("Message too short");
            }

            var headerLength = (dataBytes[0] << 8) | (dataBytes[1]);
            if (headerLength + 2 > dataBytes.Length)
            {
                throw new IOException("Message too short");
            }

            using (var audioStream = new MemoryStream(dataBytes, headerLength + 2, dataBytes.Length - headerLength - 2))
            {
                var metaObj = new TTSMetadata
                {
                    Type = TTSMetadataType.Audio,
                    Data = audioStream.ToArray(),
                };

                yield return metaObj;
            }
        }

        private async Task SendCommondRequest(ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            var request = $"X-Timestamp:{DateToString()}\r\n" +
                        "Content-Type:application/json; charset=utf-8\r\n" +
                        "Path:speech.config\r\n\r\n" +
                        @"{""context"":{""synthesis"":{""audio"":{""metadataoptions"":{" +
                        @"""sentenceBoundaryEnabled"":true,""wordBoundaryEnabled"":true}," +
                        @"""outputFormat"":""audio-24khz-48kbitrate-mono-mp3""" +
                        "}}}}\r\n";

            await clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task SendSsmlRequest(ClientWebSocket clientWebSocket, string text, TTSOption option, CancellationToken cancellationToken)
        {
            var request = SsmlHeadersPlusData(ConnectId(), DateToString(), Mkssml(text, option));

            await clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task<ClientWebSocket> EnsureConnection(CancellationToken cancellationToken)
        {
            var clientWebSocket = new ClientWebSocket();

            var options = clientWebSocket.Options;

            foreach (var wssheader in Constants.WSS_HEADERS)
            {
                options.SetRequestHeader(wssheader.Key, wssheader.Value);
            }

            if (!string.IsNullOrEmpty(_webProxy))
                options.Proxy = new WebProxy(_webProxy);

            var wssUrl = Constants.WSS_URL + "&Sec-MS-GEC=" + DRM.Generate_Sec_ms_gec() + "&Sec-MS-GEC-Version=" + Constants.SEC_MS_GEC_VERSION + "&ConnectionId=" + ConnectId();
            await clientWebSocket.ConnectAsync(new Uri(wssUrl), cancellationToken);
            return clientWebSocket;

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

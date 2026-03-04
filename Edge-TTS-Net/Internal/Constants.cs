using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace edge_tts_net.Internal
{
    /// <summary>
    /// Constants for the Edge TTS project.
    /// </summary>
    internal class Constants
    {
        public const string BASE_URL = "api.msedgeservices.com/tts/cognitiveservices";
        public const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

        public const string WSS_URL = "wss://" + BASE_URL + "/websocket/v1?Ocp-Apim-Subscription-Key=" + TRUSTED_CLIENT_TOKEN;
        public const string VOICE_LIST = "https://" + BASE_URL + "/voices/list?Ocp-Apim-Subscription-Key=" + TRUSTED_CLIENT_TOKEN;

        public const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
        public const string SEC_MS_GEC_VERSION = "1-" + CHROMIUM_FULL_VERSION;

        public static readonly string CHROMIUM_MAJOR_VERSION = CHROMIUM_FULL_VERSION.Split('.')[0];

        public static readonly Dictionary<string, string> BASE_HEADERS = new Dictionary<string, string>()
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/"+CHROMIUM_MAJOR_VERSION+".0.0.0 Safari/537.36 Edg/"+CHROMIUM_MAJOR_VERSION+".0.0.0" },
            { "Accept-Encoding", "gzip, deflate, br, zstd" },
            { "Accept-Language", "en-US,en;q=0.9" }
        };

        public static readonly Dictionary<string, string> WSS_HEADERS = new Dictionary<string, string>()
        {
           { "Pragma", "no-cache"},
           { "Cache-Control", "no-cache"},
           { "Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold"},
            {"Sec-WebSocket-Protocol", "synthesize" },
            {"Sec-WebSocket-Version", "13" },
        };

        public static readonly Dictionary<string, string> VOICE_HEADERS = new Dictionary<string, string>()
        {
            { "Authority", "speech.platform.bing.com"},
            { "Sec-CH-UA", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\""+CHROMIUM_MAJOR_VERSION+"\", \"Chromium\";v=\""+CHROMIUM_MAJOR_VERSION+"\"" },
            { "Sec-CH-UA-Mobile", "?0"},
            { "Accept", "*/*"},
            { "Sec-Fetch-Site", "none"},
            { "Sec-Fetch-Mode", "cors"},
            { "Sec-Fetch-Dest", "empty"},
        };

        static Constants()
        {
            foreach (var baseheader in BASE_HEADERS)
            {
                WSS_HEADERS.Add(baseheader.Key, baseheader.Value);
                VOICE_HEADERS.Add(baseheader.Key, baseheader.Value);
            }
        }

    }
}

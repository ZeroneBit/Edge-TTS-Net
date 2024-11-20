using System;
using System.Collections.Generic;
using System.IO;

namespace edge_tts_net
{
    public class TTSOption
    {
        public string Voice { get; set; }
        public string Pitch { get; set; }
        public string Rate { get; set; }
        public string Volume { get; set; }

        public TTSOption(string voice, string pitch, string rate, string volume)
        {
            Voice = voice;
            Pitch = pitch;
            Rate = rate;
            Volume = volume;
        }


        private static TTSOption _default = new TTSOption("Microsoft Server Speech Text to Speech Voice (en-US, AriaNeural)", "+0Hz", "+0%", "+100%");
        public static TTSOption Default
        {
            get { return _default; }
        }
    }

    public class TTSResult
    {
        public List<TTSMetadata> Metadata { get; set; }
        public byte[] Stream { get; set; }
    }


    public class TTSMetadata
    {
        public string MetaType { get; internal set; }
        public double Offset { get; internal set; }
        public double Duration { get; internal set; }
        public string Text { get; internal set; }
    }

    public class TTSStream
    {
        public byte[] PartialStream { get; internal set; }
    }

    internal enum SessionState
    {
        NotStarted,
        TurnStarted, // turn.start received
        Streaming, // audio binary started
    }

    internal class MetadataModel
    {
        public List<Metadata> Metadata { get; set; } = new List<Metadata>();
    }

    internal class Metadata
    {
        public string Type { get; set; } = "";
        public MetadataData Data { get; set; } = new MetadataData();
    }

    internal class MetadataData
    {
        public int Offset { get; set; }
        public int Duration { get; set; }
        public MetadataDataText text { get; set; } = new MetadataDataText();
    }

    internal class MetadataDataText
    {
        public string Text { get; set; } = "";
        public int Length { get; set; }
        public string BoundaryType { get; set; } = "";
    }

}

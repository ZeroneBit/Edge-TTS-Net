using System;
using System.Collections.Generic;
using System.IO;

namespace edge_tts_net.Internal
{
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

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

    public class TTSVoice
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Gender { get; set; }
        public string Locale { get; set; }
        public string SuggestedCodec { get; set; }
        public string FriendlyName { get; set; }
        public string Status { get; set; }
        public TTSVoicetag VoiceTag { get; set; }
    }

    public class TTSVoicetag
    {
        public string[] ContentCategories { get; set; }
        public string[] VoicePersonalities { get; set; }
    }

    public class TTSMetadata
    {
        public TTSMetadataType Type { get; internal set; }
        public double Offset { get; internal set; }
        public double Duration { get; internal set; }
        public string Text { get; internal set; }
        public byte[] Data { get; internal set; }
    }

    public enum TTSMetadataType
    {
        Audio = 0,
        WordBoundary = 1,
        SentenceBoundary = 2,
    }
}

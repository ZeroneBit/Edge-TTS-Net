using edge_tts_net;

static async Task GetVoices()
{
    var edgetts = new EdgeTTSNet();
    var voices = await edgetts.GetVoices();
    Console.WriteLine(voices.Count);
}

static async Task BasicSave()
{
    var edgeTts = new EdgeTTSNet();
    await edgeTts.Save(@"Hello, World!", "basic.mp3");
}

static async Task BasicStream()
{
    var fs = new FileStream("basic_stream.mp3", FileMode.Create);

    var edgetts = new EdgeTTSNet();
    await edgetts.TTS(@"Hello, World!", (metaObj) =>
    {
        if (metaObj.Type == TTSMetadataType.Audio)
        {
            fs.Write(metaObj.Data);
        }
    });

    fs.Flush();
    fs.Dispose();
}

static async Task SetOptions()
{
    var edgetts = new EdgeTTSNet();

    var voices = await edgetts.GetVoices();
    var cnVoice = voices.FirstOrDefault(v => v.Locale == "zh-CN");
    var options = new TTSOption
    (
        voice: cnVoice.Name,
        pitch: "+0Hz",
        rate: "+25%",
        volume: "+0%"
    );

    await edgetts.Save("Hello, World", "basic_option.mp3");
}


await GetVoices();

await BasicSave();

await BasicStream();

await SetOptions();

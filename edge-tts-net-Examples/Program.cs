using edge_tts_net;

const string proxy = "";

static async Task GetVoices()
{
    var edgetts = new EdgeTTSNet();
    var voices = await edgetts.GetVoices();
    Console.WriteLine(voices.Count);
}

static async Task BasicSave()
{
    var edgeTts = new EdgeTTSNet(webProxy: proxy);
    await edgeTts.Save(@"Hello, World!", "basic.mp3");
}

static async Task BasicStream()
{
    var fs = new FileStream("basic_stream.mp3", FileMode.Create);

    var edgetts = new EdgeTTSNet(webProxy: proxy);
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


await GetVoices();

await BasicSave();

await BasicStream();

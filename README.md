# Edge-TTS-Net
`edge-tts-net` is an .Net module that allows you to use Microsoft Edge's online text-to-speech service without needing Microsoft Edge or Windows or an API key. It is inspired by the Python module [edge-tts](https://github.com/rany2/edge-tts), and it is actually an .Net version of that.

## Installation
Use NuGet Package Manager to search and install it in visual studio by typing `edge-tts-net`.

## Usage
### List Voices
```C#
static async Task GetVoices()
{
    var edgetts = new EdgeTTSNet();
    var voices = await edgetts.GetVoices();
    Console.WriteLine(voices.Count);
}
```

### Save to audio file
```C#
static async Task BasicSave()
{
    var edgeTts = new EdgeTTSNet();
    await edgeTts.Save(@"Hello, World!", "basic.mp3");
}
```

### Get the stream
```C#
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
```

### Set options
```C#
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
```

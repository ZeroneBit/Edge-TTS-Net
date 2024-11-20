using edge_tts_net;

namespace edge_tts_net_Examples
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BasicTest();
        }

        static void BasicTest()
        {
            EdgeTTSNet edgeTTSNet = new EdgeTTSNet("http://54.255.235.102:808");
            var r = edgeTTSNet.TTS(@"Hello,
in my .NET WindowsForm application written with C #, I automate the insertion of some forms.

Through javascript I can do everything except choosing the file for upload.

Is there a solution to choose the file of an input tag: file or since I can open the file selection dialog, how can I control that window?

please i need help

thanks I am grateful").Result;

            Console.WriteLine(  r.Metadata);
            File.WriteAllBytes("hw.mp3", r.Stream);
        }
    }
}

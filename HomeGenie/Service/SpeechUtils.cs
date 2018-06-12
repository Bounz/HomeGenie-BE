using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HomeGenie.Service
{
    public class SpeechUtils
    {
        private static string picoPath = "/usr/bin/pico2wave";

        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(string pszSound, UIntPtr hmod, uint fdwSound);
        // buffer size for AddFileToZip
        private const long BUFFER_SIZE = 4096;

        public static void Say(string sentence, string locale, bool async = false)
        {
            // if Pico TTS is not installed, then use Google Voice API
            // Note: Pico is only supported in Linux
            if (File.Exists(picoPath) && "#en-us#en-gb#de-de#es-es#fr-fr#it-it#".IndexOf("#"+locale.ToLower()+"#") >= 0)
            {
                if (async)
                {
                    var t = new Thread(() => {
                        PicoSay(sentence, locale);
                    });
                    t.Start();
                }
                else
                {
                    PicoSay(sentence, locale);
                }
            }
            else
            {
                if (async)
                {
                    var t = new Thread(() => {
                        GoogleVoiceSay(sentence, locale);
                    });
                    t.Start();
                }
                else
                {
                    GoogleVoiceSay(sentence, locale);
                }
            }
        }

        public static void Play(string wavFile)
        {

            var os = Environment.OSVersion;
            var platform = os.Platform;
            //
            switch (platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                PlaySound(wavFile, UIntPtr.Zero, (uint)(0x00020000 | 0x00000000));
                break;
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                default:
                //var player = new System.Media.SoundPlayer();
                //player.SoundLocation = wavFile;
                //player.Play();
                Process.Start(new ProcessStartInfo("aplay", "\"" + wavFile + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();
                break;
            }
        }



        private static void PicoSay(string sentence, string locale)
        {
            try
            {
                var wavFile = Path.Combine(Utility.GetTmpFolder(), "_synthesis_tmp.wav");
                if (File.Exists(wavFile))
                    File.Delete(wavFile);

                Process.Start(new ProcessStartInfo(picoPath, " -w " + wavFile + " -l " + locale + " \"" + sentence + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();

                if (File.Exists(wavFile))
                    Play(wavFile);
            }
            catch (Exception e)
            {
                HomeGenieService.LogError(e);
            }
        }

        private static void GoogleVoiceSay(string sentence, string locale)
        {
            try
            {
                var mp3File = Path.Combine(Utility.GetTmpFolder(), "_synthesis_tmp.mp3");
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add("Referer", "http://translate.google.com");
                    var audioData = client.DownloadData("http://translate.google.com/translate_tts?ie=UTF-8&tl=" + Uri.EscapeDataString(locale) + "&q=" + Uri.EscapeDataString(sentence) + "&client=homegenie&ts=" + DateTime.UtcNow.Ticks);

                    if (File.Exists(mp3File))
                        File.Delete(mp3File);

                    var stream = File.OpenWrite(mp3File);
                    stream.Write(audioData, 0, audioData.Length);
                    stream.Close();

                    client.Dispose();
                }

                var wavFile = mp3File.Replace(".mp3", ".wav");
                Process.Start(new ProcessStartInfo("lame", "--decode \"" + mp3File + "\" \"" + wavFile + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();

                if (File.Exists(mp3File))
                    Play(wavFile);
            }
            catch (Exception e)
            {
                HomeGenieService.LogError(e);
            }
        }
    }
}

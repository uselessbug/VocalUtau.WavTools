﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocalUtau.Wavtools.Render
{
    class Program
    {
        static CommandPipe_Client client = null;
        static CachePlayerCommander cmder;
        static CommandPipe_Server cmdReciever;
        static Dictionary<CachePlayer, bool> FinishFlag = new Dictionary<CachePlayer, bool>();
        static void Main(string[] args)
        {
            Console.WriteLine("CreateNamedPipe:" + System.Diagnostics.Process.GetCurrentProcess().Id);
            FinishFlag.Clear();
            int Instance = int.Parse(args[0]);
            if (Instance > 0)
            {
                cmdReciever = new CommandPipe_Server(System.Diagnostics.Process.GetCurrentProcess().Id);
                cmdReciever.OnRecieve += cmdReciever_OnRecieve;
                client = new CommandPipe_Client(Instance);

                string temp = System.Environment.GetEnvironmentVariable("TEMP");
                DirectoryInfo info = new DirectoryInfo(temp);
                DirectoryInfo baseDir = info.CreateSubdirectory("Chorista\\Instance." + Instance);

                Dictionary<int, CachePlayer> CplayerList = new Dictionary<int, CachePlayer>();// = new CachePlayer();
                Dictionary<int, List<Calculators.NoteListCalculator.NotePreRender>> RST = new Dictionary<int, List<Calculators.NoteListCalculator.NotePreRender>>();
                using (System.IO.FileStream ms = new System.IO.FileStream(baseDir.FullName + @"\\RendCmd.binary", System.IO.FileMode.Open))
                {
                    //序列化操作，把内存中的东西写到硬盘中
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter fomatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(null, new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.File));
                    object obj = fomatter.Deserialize(ms);
                    ms.Flush();
                    RST = (Dictionary<int, List<Calculators.NoteListCalculator.NotePreRender>>)obj;
                }
                for (int i = 0; i < RST.Count; i++)
                {
                    CplayerList.Add(i, new CachePlayer());
                    FinishFlag.Add(CplayerList[i], false);
                    CplayerList[i].BufferEmpty_Pause += Program_BufferEmpty_Pause;
                    CplayerList[i].BufferEmpty_Resume+=Program_BufferEmpty_Resume;
                    CplayerList[i].PlayFinished += Program_PlayFinished;
                    Task.Factory.StartNew((object prm) => { 
                        object[] prms = (object[])prm;
                        CachePlayer cplayer = (CachePlayer)prms[0];
                        DirectoryInfo TempDir=(DirectoryInfo)prms[1];
                        List<Calculators.NoteListCalculator.NotePreRender> NList = (List<Calculators.NoteListCalculator.NotePreRender>)prms[2];
                        cplayer.StartRending(TempDir, NList);
                    }, new object[] { CplayerList[i],baseDir, RST[i] });
                }
                cmder = new CachePlayerCommander(CplayerList);
                cmder.PlayAll();
                Console.ReadLine();
            }
        }

        static void cmdReciever_OnRecieve(string data)
        {
            try
            {
                switch (data)
                {
                    case "Cmd:Play": cmder.PlayAll(); break;
                    case "Cmd:Pause": cmder.PauseAll(); break;
                    case "Cmd:Stop": cmder.StopAll(); break;
                }
            }
            catch { ;}
        }

        static void Program_PlayFinished(object sender)
        {
            CachePlayer obj = (CachePlayer)sender;
            FinishFlag[obj] = false;
            if (!FinishFlag.ContainsValue(true))
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
        }

        static void Program_BufferEmpty_Resume(object sender)
        {
            client.SendData("Buffer:Play");
            Console.WriteLine("Play");
            try
            {
                cmder.PlayAll();
            }catch{;}
        }

        static void Program_BufferEmpty_Pause(object sender)
        {
            Console.WriteLine("Empty");
            client.SendData("Buffer:Empty");
            try
            {
                cmder.PauseAll();
            }catch{;}
        }
    }
}
/*//100
           using (FileStream fs = new FileStream(@"D:\\test-t"+tracker.getIndex().ToString()+".bat", FileMode.Create))
           {
               using (StreamWriter sw=new StreamWriter(fs))
               {
                   sw.WriteLine("mkdir \"%temp%\\utaubk\"");
                   for (int i = 0; i < nlc.NotePreRenderList.Count; i++)
                   {
                       //"{RESAMPLEROUTPUT}", "{WAVOUTPUT}");
                       if (nlc.NotePreRenderList[i].ResamplerArg != null)
                       {
                           string resStr = String.Join(" ", nlc.NotePreRenderList[i].ResamplerArgList);
                           resStr = resStr.Replace("{RESAMPLEROUTPUT}", @"temp$$$.wav");
                           sw.WriteLine(@"D:\VocalUtau\VocalUtau.DebugExampleFiles\UTAUKernel\resampler.exe " + resStr);
                       }
                       string wavStr = String.Join(" ", nlc.NotePreRenderList[i].WavtoolArgList);
                       wavStr = wavStr.Replace("{RESAMPLEROUTPUT}", @"temp$$$.wav");
                       wavStr = wavStr.Replace("{WAVOUTPUT}", @"temp.wav");
                       sw.WriteLine(@"D:\VocalUtau\VocalUtau.DebugExampleFiles\UTAUKernel\wavtool.exe " + wavStr);
                   }
               }
           }


           //101
           using (FileStream fs = new FileStream(@"D:\\test-b" + tracker.getIndex().ToString() + ".txt", FileMode.Create))
           {
               using (StreamWriter sw = new StreamWriter(fs))
               {
                   for (int i = 0; i < nlc.NotePreRenderList.Count; i++)
                   {
                       //"{RESAMPLEROUTPUT}", "{WAVOUTPUT}");
                       if (nlc.NotePreRenderList[i].ResamplerArg != null)
                       {
                           string resStr = String.Join(" ", nlc.NotePreRenderList[i].ResamplerArgList);
                           resStr = resStr.Replace("{RESAMPLEROUTPUT}", @"temp$$$.wav");
                           sw.WriteLine(@"resampler.exe " + resStr.Replace(@"D:\VocalUtau\VocalUtau\bin\Debug\voicedb\YongQi_CVVChinese_Version2\",""));
                       }
                       string wavStr = String.Join(" ", nlc.NotePreRenderList[i].WavtoolArgList);
                       wavStr = wavStr.Replace("{RESAMPLEROUTPUT}", @"temp$$$.wav");
                       wavStr = wavStr.Replace("{WAVOUTPUT}", @"temp.wav");
                       sw.WriteLine(@"wavtool.exe " + wavStr.Replace(@"D:\VocalUtau\VocalUtau\bin\Debug\voicedb\YongQi_CVVChinese_Version2\", ""));
                   }
               }
           }*/
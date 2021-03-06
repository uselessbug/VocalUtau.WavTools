﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using VocalUtau.WavTools;
using VocalUtau.WavTools.Model.Pipe;
using VocalUtau.WavTools.Model.Player;
using VocalUtau.WavTools.Model.Wave;
using VocalUtau.WavTools.Model.Wave.NAudio.Extra;

namespace VocalUtau.Wavtools.Render
{
    internal class CacheRender:IRender
    {
        long headSize = 0;
     //   Pipe_Server pserver;
    //    FileStream Fs;

        string _RendingFile = "";

        public string RendingFile
        {
            get { return _RendingFile; }
        }
        public string getRendingFile()
        {
            return _RendingFile;
        }

        bool _IsRending = false;

        public bool IsRending
        {
            get { return _IsRending; }
            set { _IsRending = value; }
        }
        public bool getIsRending()
        {
            return _IsRending;
        }

        public event VocalUtau.WavTools.Model.Player.BufferedPlayer.BufferEventHandler RendingStateChange;

        string CacheSignal = "";
        public CacheRender(string CacheSignal="")
        {
            if (CacheSignal == "")
            {
                this.CacheSignal = CacheSignal = Guid.NewGuid().ToString();
            }
            else
            {
                this.CacheSignal = CacheSignal;
            }
        }


        bool _ExitRending = false;

        public void StopRending()
        {
            _ExitRending = true;
        }

        private long InitFile(out FileStream Fs,string TrackFileName)
        {
            Fs = new FileStream(TrackFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            _RendingFile = TrackFileName;
            byte[] head = IOHelper.GenerateHead();
            Fs.Write(head, 0, head.Length);
            Fs.Seek(head.Length, SeekOrigin.Begin);
            return head.Length;
        }

        Dictionary<int, string> ResamplerCacheDic = new Dictionary<int, string>();
        int PanStep = 2;
        private void DoResampler(System.IO.DirectoryInfo cacheTempDir, List<VocalUtau.Calculators.NoteListCalculator.NotePreRender> NList)
        {
            int i = 0;
            while (i < NList.Count)
            {
                if (_ExitRending) break;
                int ThreadCount = 0;
                Task[] TaskArray = new Task[PanStep];
                while (ThreadCount < PanStep && i < NList.Count)
                {
                    if (_ExitRending) break;
                    string MidFileName = "R.wav";
                    ProcessStartInfo psi = null;
                    if (NList[i].ResamplerArg != null)
                    {
                        string resStr = String.Join(" ", NList[i].ResamplerArgList);
                        string MD5Str = MD5Helper.GetMD5HashString(NList[i].Resampler + resStr);
                        MidFileName = cacheTempDir.FullName + "\\" + NList[i].Note + "_" + MD5Str + ".wav";
                        if (!System.IO.File.Exists(MidFileName))
                        {
                            resStr = resStr.Replace("{RESAMPLEROUTPUT}", MidFileName);
                            psi = new ProcessStartInfo();
                            psi.FileName = NList[i].Resampler;
                            psi.Arguments = resStr;
                            psi.CreateNoWindow = true;
                            psi.WindowStyle = ProcessWindowStyle.Hidden;
                            TaskArray[ThreadCount] = Task.Factory.StartNew((object idx) =>
                            {
                                object[] ox = (object[])idx;
                                ProcessStartInfo psi2 = (ProcessStartInfo)ox[0];
                                int i2 = (int)ox[1];
                                if (psi2 != null)
                                {
                                    try
                                    {
                                        Process p = new Process();
                                        p.StartInfo = psi2;
                                        p.Start();
                                        Console.WriteLine("Resampling[" + i2.ToString() + "/" + (NList.Count - 1).ToString() + "]:" + NList[i2].Note + "  -  " + NList[i2].OtoAtom.PhonemeSymbol);
                                        p.WaitForExit();
                                    }
                                    catch { Console.WriteLine("Resampling[" + i2.ToString() + "/" + (NList.Count - 1).ToString() + "]:Error"); }
                                }
                                lock (ResamplerCacheDic)
                                {
                                    ResamplerCacheDic.Add(i2, MidFileName);
                                }
                            }, new object[] { psi, i });
                            ThreadCount++;
                        }
                        else
                        {
                            //Have
                            lock (ResamplerCacheDic)
                            {
                                ResamplerCacheDic.Add(i, MidFileName);
                            }
                        }
                    }
                    else
                    {
                        //R
                        lock (ResamplerCacheDic)
                        {
                            ResamplerCacheDic.Add(i, MidFileName);
                        }
                    }
                    i++;
                }
                try
                {
                    Task.WaitAll(TaskArray);
                }
                catch { ;}
            }
        }

        public void StartRending(System.IO.DirectoryInfo baseTempDir, List<VocalUtau.Calculators.BarkerCalculator.BgmPreRender> BList, string RendToWav = "") { ;}
        public void StartRending(System.IO.DirectoryInfo baseTempDir,List<VocalUtau.Calculators.NoteListCalculator.NotePreRender> NList,string RendToWav="")
        {
            _IsRending = true;
            _ExitRending = false;
            if (RendingStateChange != null) RendingStateChange(this);

            string ProcessIDStr = Process.GetCurrentProcess().Id.ToString();
            DirectoryInfo tempDir = baseTempDir.CreateSubdirectory("temp");
            DirectoryInfo cacheDir = baseTempDir.CreateSubdirectory("cache");

            string TrackFileName = tempDir.FullName + "\\Track_" + CacheSignal + ".wav";

            FileStream Fs;
            headSize = InitFile(out Fs,TrackFileName);
            Semaphore semaphore=new Semaphore(1,1,"VocalUtau.WavTool." + ProcessIDStr + ".Track_" + CacheSignal);

            ResamplerCacheDic.Clear();
            Task t = Task.Factory.StartNew(() =>
            {
                DoResampler(cacheDir, NList);
            });
            for (int i = 0; i < NList.Count; i++)
            {
                while (!ResamplerCacheDic.ContainsKey(i))
                {
                    if (_ExitRending) break;
                    System.Threading.Thread.Sleep(100);
                }
                if (_ExitRending) break;
                string MidFileName = ResamplerCacheDic[i];
                string wavStr = String.Join(" ", NList[i].WavtoolArgList);
               
                double delay = 0;
                if (NList[i].passTime > 0) delay = NList[i].passTime;
                VocalUtau.WavTools.Model.Args.ArgsStruct parg = VocalUtau.WavTools.Model.Args.ArgsParser.parseArgs(NList[i].WavtoolArgList, false);
                Console.WriteLine("WaveAppending[" + i.ToString() + "/"+(NList.Count-1).ToString()+"]:" + NList[i].Note +(NList[i].Note=="{R}"?"":"  -  " + NList[i].OtoAtom.PhonemeSymbol));


                semaphore.WaitOne();
                try
                {
                    WavAppender.AppendWork(Fs, MidFileName, parg.Offset, parg.Length, parg.Ovr, parg.PV, delay);
                }
                catch { ;}
                Fs.Flush();
                semaphore.Release();
            }
            _IsRending = false;
            long total = Fs.Length;
            byte[] head = IOHelper.GenerateHead((int)(total - headSize));
            Fs.Seek(0, SeekOrigin.Begin);
            Fs.Write(head, 0, head.Length);
            Fs.Flush();
            Fs.Close();
            t.Wait();
            _ExitRending = false;
            if (RendingStateChange != null) RendingStateChange(this);
            if (RendToWav != "")
            {
                File.Copy(TrackFileName, RendToWav, true);
                try
                {
                    File.Delete(TrackFileName);
                }
                catch { ;}
            }
        }
    }
}

﻿using System;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HintMachine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int PROCESS_WM_READ = 0x0010;
        ArchipelagoSession archipelagoSession;
        private long scoreAdress;
        private long survivalKillsAdress;
        private Timer _timer;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeAccessTokenHandle OpenThread(
           ThreadAccess dwDesiredAccess,
           bool bInheritHandle,
           uint dwThreadId
           );

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
          Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationThread(IntPtr processHandle, int threadInformationClass, IntPtr threadInformation, uint threadInformationLength, IntPtr returnLength);

        /*Xenotilt*/
        float score = 0;
        String scoreStr = "0";
        float scoreObjectif = 200000000;

        /*One finger death punch*/
        float survivalKills = 0;
        String survivalKillsStr = "0";
        float survivalKillsObjectif = 450;

        IntPtr processHandle;
        int nbHintsSent = 0;
        private List<long> locationsToRemove = new List<long>();
        private string nomJeu;

        public MainWindow(ArchipelagoSession archipelagoSession, String nomJeu)
        {
            InitializeComponent();
            this.archipelagoSession = archipelagoSession;
            TokenManipulator.AddPrivilege("SeDebugPrivilege");
            TokenManipulator.AddPrivilege("SeSystemEnvironmentPrivilege");
            Process.EnterDebugMode();
            Process process = Process.GetProcessesByName(nomJeu)[0];
            
            this.nomJeu = nomJeu;
            gameName.Content = nomJeu;
            switch (nomJeu)
            {
               
                case "Xenotilt":
                    firstLabel.Content = "Score : ";
                    ProcessModule xenoModule = null;
                    foreach (ProcessModule m in process.Modules)
                    {
                        if (m.FileName.Contains("mono-2.0-bdwgc.dll"))
                        {
                            xenoModule = m;
                        }
                    }
                    processHandle = OpenProcess(PROCESS_WM_READ, false, process.Id);

                    int bytesReadXeno = 0;
                    byte[] bufferXeno = new byte[8];
                    if (xenoModule != null)
                    {

                        long adressToRead = xenoModule.BaseAddress.ToInt64() + 0x007270B8;
                        ReadProcessMemory((int)processHandle, adressToRead, bufferXeno, bufferXeno.Length, ref bytesReadXeno);
                        Console.WriteLine(BitConverter.ToInt64(bufferXeno, 0) +
                            " (" + bytesReadXeno.ToString() + "bytes)");

                        adressToRead = BitConverter.ToInt64(bufferXeno, 0) + 0x30;
                        ReadProcessMemory((int)processHandle, adressToRead, bufferXeno, bufferXeno.Length, ref bytesReadXeno);
                        Console.WriteLine(BitConverter.ToInt64(bufferXeno, 0) +
                            " (" + bytesReadXeno.ToString() + "bytes)");

                        adressToRead = BitConverter.ToInt64(bufferXeno, 0) + 0x7e0;
                        ReadProcessMemory((int)processHandle, adressToRead, bufferXeno, bufferXeno.Length, ref bytesReadXeno);
                        Console.WriteLine(BitConverter.ToInt64(bufferXeno, 0) +
                            " (" + bytesReadXeno.ToString() + "bytes)");

                        adressToRead = BitConverter.ToInt64(bufferXeno, 0) + 0x7C0;
                        scoreAdress = adressToRead;
                        _timer = new System.Timers.Timer { AutoReset = false, Interval = 1000 };
                        _timer.Elapsed += TimerElapsed;
                        _timer.AutoReset = true;
                        _timer.Enabled = true;
                    }
                    break;
                case "One Finger Death Punch":
                    firstLabel.Content = "Kills : ";
                    ProcessModule ofdpModule = process.MainModule;
                    
                    uint adr = ProcessUtils32.CheatengineSpecific.GetThreadStack0(process);
                    processHandle = OpenProcess(PROCESS_WM_READ, false, process.Id);
                    Console.WriteLine("Adress 32 : " + adr);
                    foreach (ProcessThread pt in process.Threads) {
                        Console.WriteLine(OpenThread(ThreadAccess.QUERY_INFORMATION, false, (uint)pt.Id).DangerousGetHandle().ToInt32());
                        
                    }

                    if (ofdpModule != null)
                    {
                        long adressToRead = adr;

                        Console.WriteLine(ProcessUtils32.CheatengineSpecific.ReadPointerChain_64<int>(processHandle, (uint)adressToRead, new int[] {-0x8c8, 0x644, 0x90 }));

                        survivalKillsAdress = adressToRead;
                        _timer = new System.Timers.Timer { AutoReset = false, Interval = 1000 };
                        _timer.Elapsed += TimerElapsed;
                        _timer.AutoReset = true;
                        _timer.Enabled = true;
                        
                    }
                    break;
                
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            switch (nomJeu)
            {

                case "Xenotilt":
                    getAndDisplayScore(processHandle, new byte[8], 0);
                    break;
                case "One Finger Death Punch" :
                    getAndDisplaySurvivalKills(processHandle, new byte[8], 0);
                    break;

            }
        }

        private void getAndDisplayScore(IntPtr processHandle, byte[] buffer, int bytesRead)
        {
            ReadProcessMemory((int)processHandle, scoreAdress, buffer, buffer.Length, ref bytesRead);

            score = BitConverter.ToInt64(buffer, 0);
            if (score > scoreObjectif * (nbHintsSent + 1))
            {
                nbHintsSent++;
                loginAndGetItem();
            }
            String scoreStr = score.ToString("#,#");
            this.Dispatcher.Invoke(() =>
            {
                scoreStr = score.ToString("#,#");
                firstLabelValue.Content = scoreStr;
                progressBarFirst.Value = Math.Floor(((score % scoreObjectif) / scoreObjectif) * 100);
                pourcentageFirstLabel.Content = progressBarFirst.Value + " %";
            });

        }

        private void getAndDisplaySurvivalKills(IntPtr processHandle, byte[] buffer, int bytesRead)
        {
            survivalKills = ProcessUtils32.CheatengineSpecific.ReadPointerChain_64<int>(processHandle, (uint)survivalKillsAdress, new int[] { -0x8c8, 0x644, 0x90 });
            if (survivalKills > survivalKillsObjectif * (nbHintsSent + 1))
            {
                nbHintsSent++;
                loginAndGetItem();
            }
            String survivalKillsStr = survivalKills.ToString("#,#");
            this.Dispatcher.Invoke(() =>
            {
                survivalKillsStr = survivalKills.ToString("#,#");
                firstLabelValue.Content = survivalKillsStr;
                progressBarFirst.Value = Math.Floor(((survivalKills % survivalKillsObjectif) / survivalKillsObjectif) * 100);
                pourcentageFirstLabel.Content = progressBarFirst.Value + " %";
            });

        }
        
        public void Main2()
        {
            loginAndGetItem();
        }
        
        private void loginAndGetItem()
        {

            List<long> copyMissingLocations = archipelagoSession.Locations.AllMissingLocations.ToList();

            Random rnd = new Random();
            int index = rnd.Next(copyMissingLocations.Count);
            locationsToRemove.Add(archipelagoSession.Locations.AllMissingLocations[index]);
            foreach (long l in locationsToRemove)
            {
                copyMissingLocations.Remove(l);
            }

            long locId = archipelagoSession.Locations.AllMissingLocations[index];
            this.Dispatcher.Invoke(() =>
            {
                logTextBox.Text += getItem(archipelagoSession, locId) + "\n";
            });
        }

        private static String getItem(ArchipelagoSession archipelagoSession, long locId)
        {
            Task<LocationInfoPacket> t = archipelagoSession.Locations.ScoutLocationsAsync(false, locId);
            LocationInfoPacket x = t.Result;
            Console.WriteLine(archipelagoSession.Items.GetItemName(x.Locations[0].Item) + " is at " + archipelagoSession.Locations.GetLocationNameFromId(locId));

            return archipelagoSession.Items.GetItemName(x.Locations[0].Item) + " is at " + archipelagoSession.Locations.GetLocationNameFromId(locId);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Main2();
        }
    }
}
﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;

namespace HintMachine
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            Settings.LoadFromFile();
            inputHost.Text = Settings.Host;
            inputSlot.Text = Settings.Slot;
        }

        private void OnConnectButtonClick(object sender, RoutedEventArgs e)
        {
            // Try connecting to Archipelago
            string host = inputHost.Text;
            string slot = inputSlot.Text;
            string password = inputPassword.Text;

            ArchipelagoHintSession archipelagoSession = new ArchipelagoHintSession(host, slot, password);
            if (!archipelagoSession.IsConnected)
            {
                MessageBox.Show("Could not connect to Archipelago: " + archipelagoSession.ErrorMessage, "Connection error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // If connectionn succeeded, store the fields contents for next execution and move on to MainWindow
            Settings.Host = host;
            Settings.Slot = slot;
            Settings.SaveToFile();

            new MainWindow(archipelagoSession).Show();
            Close();
        }
    }
}

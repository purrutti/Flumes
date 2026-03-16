using System;
using System.Configuration;
using System.Windows;
using System.Windows.Media;

namespace SuperviFlume_v2
{
    public partial class MainWindow : Window
    {
        private readonly WebSocketServer _server    = new WebSocketServer();
        private readonly LogWindow       _logWindow = new LogWindow();

        public MainWindow()
        {
            InitializeComponent();

            _server.AquariumReceived   += OnAquariumReceived;
            _server.MasterDataReceived += OnMasterDataReceived;
            _server.Log                += msg => _logWindow.AppendLog(msg);

            string url = ConfigurationManager.AppSettings["serverUrl"] ?? "http://localhost:81/";
            _server.Start(url);
            MenuStartServer.IsEnabled  = false;
            MenuStopServer.IsEnabled   = true;
            TxtServerStatus.Text       = "● Serveur en marche";
            TxtServerStatus.Foreground = Brushes.LightGreen;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Callbacks serveur → mise à jour IHM
        // ─────────────────────────────────────────────────────────────────

        private void OnAquariumReceived(Aquarium a)
        {
            Dispatcher.Invoke(() =>
            {
                // Si consigne > T ambiante → on chauffe (pidChaud = sortiePID, pidFroid = 0)
                // Sinon                    → on refroidit (pidFroid = sortiePID, pidChaud = 0)
                double ambTemp  = _server.LastMasterData?.Data?.Count >= 3
                                  ? _server.LastMasterData.Data[2].Temperature
                                  : 0;
                double sortiePID = a.regulTemp.sortiePID_pc;
                double pidChaud  = a.regulTemp.consigne > ambTemp ? sortiePID : 0;
                double pidFroid  = a.regulTemp.consigne <= ambTemp ? sortiePID : 0;

                if (a.ID >= 1 && a.ID <= 12)
                    UpdateAquarium(
                        a.ID,
                        a.debit, a.temperature, a.pH, a.oxy,
                        a.regulTemp.consigne, a.regulpH.consigne,
                        pidFroid, pidChaud);
                else if (a.ID >= 13 && a.ID <= 20)
                    UpdateFlume(
                        a.ID - 12,
                        a.debit, a.temperature, a.debitCircul, a.oxy, a.pH,
                        pidFroid, pidChaud,
                        a.regulTemp.consigne, a.regulpH.consigne);
            });
        }

        private void OnMasterDataReceived(MasterData md)
        {
            // MasterData contient 3 circuits : [0] Cold, [1] Warm, [2] Ambiant/sortie
            // Mapping vers UpdateGeneralTab — à adapter selon le câblage réel
            if (md.Data == null || md.Data.Count < 3) return;

            var cold = md.Data[1];
            var warm = md.Data[0];
            var amb  = md.Data[2];

            Dispatcher.Invoke(() =>
            {
                UpdateGeneralTab(
                    ambPH:          amb.PH,
                    ambTemp:        amb.Temperature,
                    pres1:          amb.Pression,              flow1:          amb.Debit,
                    pidPresAmb:     amb.RPression.sortiePID_pc, consPresAmb:   amb.RPression.consigne,
                    tCold:          cold.Temperature,           consTCold:     cold.RTemp.consigne,       pidCold:     cold.RTemp.sortiePID_pc,
                    pres2:          cold.Pression,              flow2:         cold.Debit,
                    pidPresCold:    cold.RPression.sortiePID_pc,consPresCold:  cold.RPression.consigne,
                    tWarm:          warm.Temperature,           consTWarm:     warm.RTemp.consigne,       pidWarm:     warm.RTemp.sortiePID_pc,
                    pres3:          warm.Pression,              flow3:         warm.Debit,
                    pidPresWarm:    warm.RPression.sortiePID_pc,consPresWarm:  warm.RPression.consigne);
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Menu – Serveur
        // ─────────────────────────────────────────────────────────────────

        private void MenuStartServer_Click(object sender, RoutedEventArgs e)
        {
            string url = ConfigurationManager.AppSettings["serverUrl"] ?? "http://localhost:81/";
            _server.Start(url);

            MenuStartServer.IsEnabled  = false;
            MenuStopServer.IsEnabled   = true;
            TxtServerStatus.Text       = "● Serveur en marche";
            TxtServerStatus.Foreground = Brushes.LightGreen;
        }

        private void MenuStopServer_Click(object sender, RoutedEventArgs e)
        {
            _server.Stop();

            MenuStartServer.IsEnabled  = true;
            MenuStopServer.IsEnabled   = false;
            TxtServerStatus.Text       = "● Serveur arrêté";
            TxtServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
        }

        private void MenuQuit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _server.Stop();
            // Forcer la fermeture de toutes les fenêtres y compris LogWindow (cachée)
            Application.Current.Shutdown();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Menu – Outils
        // ─────────────────────────────────────────────────────────────────

        private void MenuShowLog_Click(object sender, RoutedEventArgs e)
        {
            _logWindow.Show();
            _logWindow.Activate();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Menu – ?
        // ─────────────────────────────────────────────────────────────────

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SuperviFlumes v2\nCNRS – Station de biologie marine\n",
                "À propos", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Méthodes de mise à jour de l'affichage
        //  (appelées depuis le thread UI via Dispatcher.Invoke)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Mise à jour de l'onglet "General water inlet".</summary>
        public void UpdateGeneralTab(
            double ambPH,       double ambTemp,
            double pres1,       double flow1,
            double pidPresAmb,  double consPresAmb,
            double tCold,       double consTCold,   double pidCold,
            double pres2,       double flow2,
            double pidPresCold, double consPresCold,
            double tWarm,       double consTWarm,   double pidWarm,
            double pres3,       double flow3,
            double pidPresWarm, double consPresWarm)
        {
            lblAmbPH.Content    = $"pH  : {ambPH:F2}";
            lblAmbTemp.Content  = $"T   : {ambTemp:F2} °C";

            lblPres1.Content          = $"P1 : {pres1:F2} bar";
            lblFlow1.Content          = $"F1 : {flow1:F2} L/mn";
            lblAmbPIDPRessure.Content = $"PID  : {pidPresAmb:F2} %";
            lblAmbPIDSetpoint.Content = $"Setpoint : {consPresAmb:F2} bar";

            lblTCold.Content          = $"T    : {tCold:F2} °C";
            lblConsTCold.Content      = $"Cons : {consTCold:F2} °C";
            lblPIDCold.Content        = $"PID  : {pidCold:F2} %";
            lblPres2.Content          = $"P2 : {pres2:F2} bar";
            lblFlow2.Content          = $"F2 : {flow2:F2} L/mn";
            lblColdPIDPRessure.Content= $"PID  : {pidPresCold:F2} %";
            lblColdPIDSetpoint.Content= $"Setpoint : {consPresCold:F2} bar";

            lblTWarm.Content          = $"T    : {tWarm:F2} °C";
            lblConsTWarm.Content      = $"Cons : {consTWarm:F2} °C";
            lblPIDWarm.Content        = $"PID  : {pidWarm:F2} %";
            lblPres3.Content          = $"P3 : {pres3:F2} bar";
            lblFlow3.Content          = $"F3 : {flow3:F2} L/mn";
            lblHotPIDPRessure.Content = $"PID  : {pidPresWarm:F2} %";
            lblHotPIDSetpoint.Content = $"Setpoint : {consPresWarm:F2} bar";

            TxtLastUpdate.Text  = $"Dernière mise à jour : {DateTime.Now:HH:mm:ss}";
        }

        /// <summary>Mise à jour d'un aquarium (index 1–12).</summary>
        public void UpdateAquarium(int id,
            double debit, double temp, double pH, double o2,
            double consTemp, double consPH,
            double pidCold, double pidWarm)
        {
            string q   = $"Q  : {debit:F2} L/mn";
            string t   = $"T  : {temp:F2} °C";
            string ph  = $"pH : {pH:F2}";
            string o   = $"O2 : {o2:F2} %";
            string tc  = $"T°C : {consTemp:F2} °C";
            string phc = $"pH: {consPH:F2}";
            string pc  = $"PID: {pidCold:F2} %";
            string ph2 = $"PID: {pidWarm:F2} %";

            switch (id)
            {
                case  1: lblAq1Q.Content=q; lblAq1T.Content=t; lblAq1PH.Content=ph; lblAq1O2.Content=o; lblAq1ConsT.Content=tc; lblAq1ConsPH.Content=phc; lblAq1PIDCold.Content=pc; lblAq1PIDWarm.Content=ph2; break;
                case  2: lblAq2Q.Content=q; lblAq2T.Content=t; lblAq2PH.Content=ph; lblAq2O2.Content=o; lblAq2ConsT.Content=tc; lblAq2ConsPH.Content=phc; lblAq2PIDCold.Content=pc; lblAq2PIDWarm.Content=ph2; break;
                case  3: lblAq3Q.Content=q; lblAq3T.Content=t; lblAq3PH.Content=ph; lblAq3O2.Content=o; lblAq3ConsT.Content=tc; lblAq3ConsPH.Content=phc; lblAq3PIDCold.Content=pc; lblAq3PIDWarm.Content=ph2; break;
                case  4: lblAq4Q.Content=q; lblAq4T.Content=t; lblAq4PH.Content=ph; lblAq4O2.Content=o; lblAq4ConsT.Content=tc; lblAq4ConsPH.Content=phc; lblAq4PIDCold.Content=pc; lblAq4PIDWarm.Content=ph2; break;
                case  5: lblAq5Q.Content=q; lblAq5T.Content=t; lblAq5PH.Content=ph; lblAq5O2.Content=o; lblAq5ConsT.Content=tc; lblAq5ConsPH.Content=phc; lblAq5PIDCold.Content=pc; lblAq5PIDWarm.Content=ph2; break;
                case  6: lblAq6Q.Content=q; lblAq6T.Content=t; lblAq6PH.Content=ph; lblAq6O2.Content=o; lblAq6ConsT.Content=tc; lblAq6ConsPH.Content=phc; lblAq6PIDCold.Content=pc; lblAq6PIDWarm.Content=ph2; break;
                case  7: lblAq7Q.Content=q; lblAq7T.Content=t; lblAq7PH.Content=ph; lblAq7O2.Content=o; lblAq7ConsT.Content=tc; lblAq7ConsPH.Content=phc; lblAq7PIDCold.Content=pc; lblAq7PIDWarm.Content=ph2; break;
                case  8: lblAq8Q.Content=q; lblAq8T.Content=t; lblAq8PH.Content=ph; lblAq8O2.Content=o; lblAq8ConsT.Content=tc; lblAq8ConsPH.Content=phc; lblAq8PIDCold.Content=pc; lblAq8PIDWarm.Content=ph2; break;
                case  9: lblAq9Q.Content=q; lblAq9T.Content=t; lblAq9PH.Content=ph; lblAq9O2.Content=o; lblAq9ConsT.Content=tc; lblAq9ConsPH.Content=phc; lblAq9PIDCold.Content=pc; lblAq9PIDWarm.Content=ph2; break;
                case 10: lblAq10Q.Content=q; lblAq10T.Content=t; lblAq10PH.Content=ph; lblAq10O2.Content=o; lblAq10ConsT.Content=tc; lblAq10ConsPH.Content=phc; lblAq10PIDCold.Content=pc; lblAq10PIDWarm.Content=ph2; break;
                case 11: lblAq11Q.Content=q; lblAq11T.Content=t; lblAq11PH.Content=ph; lblAq11O2.Content=o; lblAq11ConsT.Content=tc; lblAq11ConsPH.Content=phc; lblAq11PIDCold.Content=pc; lblAq11PIDWarm.Content=ph2; break;
                case 12: lblAq12Q.Content=q; lblAq12T.Content=t; lblAq12PH.Content=ph; lblAq12O2.Content=o; lblAq12ConsT.Content=tc; lblAq12ConsPH.Content=phc; lblAq12PIDCold.Content=pc; lblAq12PIDWarm.Content=ph2; break;
            }
        }

        /// <summary>Mise à jour d'un flume (index 1–8).</summary>
        public void UpdateFlume(int id, double debit, double temp, double speed, double o2, double pH,
            double pidCold, double pidWarm,
            double consTemp, double consPH)
        {
            string q   = $"Q : {debit:F2} L/mn";
            string t   = $"T : {temp:F2} °C";
            string v   = $"V : {speed:F2} m/s";
            string o   = $"O2: {o2:F2} %";
            string ph  = $"pH : {pH:F2}";
            string pc  = $"PID: {pidCold:F2} %";
            string pw  = $"PID: {pidWarm:F2} %";
            string tc  = $"T°C : {consTemp:F2} °C";
            string phc = $"pH: {consPH:F2}";

            switch (id)
            {
                case 1: lblFl1Q.Content=q; lblFl1T.Content=t; lblSpeed1.Content=v; lblFl1O2.Content=o; lblFl1pH.Content=ph; lblFl1PIDCold.Content=pc; lblFl1PIDWarm.Content=pw; lblFl1ConsT.Content=tc; lblFl1ConsPH.Content=phc; break;
                case 2: lblFl2Q.Content=q; lblFl2T.Content=t; lblSpeed2.Content=v; lblFl2O2.Content=o; lblFl2pH.Content=ph; lblFl2PIDCold.Content=pc; lblFl2PIDWarm.Content=pw; lblFl2ConsT.Content=tc; lblFl2ConsPH.Content=phc; break;
                case 3: lblFl3Q.Content=q; lblFl3T.Content=t; lblSpeed3.Content=v; lblFl3O2.Content=o; lblFl3pH.Content=ph; lblFl3PIDCold.Content=pc; lblFl3PIDWarm.Content=pw; lblFl3ConsT.Content=tc; lblFl3ConsPH.Content=phc; break;
                case 4: lblFl4Q.Content=q; lblFl4T.Content=t; lblSpeed4.Content=v; lblFl4O2.Content=o; lblFl4pH.Content=ph; lblFl4PIDCold.Content=pc; lblFl4PIDWarm.Content=pw; lblFl4ConsT.Content=tc; lblFl4ConsPH.Content=phc; break;
                case 5: lblFl5Q.Content=q; lblFl5T.Content=t; lblSpeed5.Content=v; lblFl5O2.Content=o; lblFl5pH.Content=ph; lblFl5PIDCold.Content=pc; lblFl5PIDWarm.Content=pw; lblFl5ConsT.Content=tc; lblFl5ConsPH.Content=phc; break;
                case 6: lblFl6Q.Content=q; lblFl6T.Content=t; lblSpeed6.Content=v; lblFl6O2.Content=o; lblFl6pH.Content=ph; lblFl6PIDCold.Content=pc; lblFl6PIDWarm.Content=pw; lblFl6ConsT.Content=tc; lblFl6ConsPH.Content=phc; break;
                case 7: lblFl7Q.Content=q; lblFl7T.Content=t; lblSpeed7.Content=v; lblFl7O2.Content=o; lblFl7pH.Content=ph; lblFl7PIDCold.Content=pc; lblFl7PIDWarm.Content=pw; lblFl7ConsT.Content=tc; lblFl7ConsPH.Content=phc; break;
                case 8: lblFl8Q.Content=q; lblFl8T.Content=t; lblSpeed8.Content=v; lblFl8O2.Content=o; lblFl8pH.Content=ph; lblFl8PIDCold.Content=pc; lblFl8PIDWarm.Content=pw; lblFl8ConsT.Content=tc; lblFl8ConsPH.Content=phc; break;
            }
        }
    }
}

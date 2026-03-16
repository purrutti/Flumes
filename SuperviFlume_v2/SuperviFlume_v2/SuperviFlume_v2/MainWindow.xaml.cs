using System;
using System.Configuration;
using System.Windows;
using System.Windows.Media;

namespace SuperviFlume_v2
{
    public partial class MainWindow : Window
    {
        private readonly WebSocketServer    _server             = new WebSocketServer();
        private readonly LogWindow          _logWindow          = new LogWindow();
        private          SensorCalibration        _sensorCalibration;
        private          SetpointsWindow          _setpointsWindow;
        private          GeneralInletParamsWindow _generalInletParamsWindow;
        private          AlarmManager             _alarmManager;
        private          AlarmsWindow             _alarmsWindow;

        public MainWindow()
        {
            InitializeComponent();

            _server.AquariumReceived   += OnAquariumReceived;
            _server.MasterDataReceived += OnMasterDataReceived;
            _server.Log                += msg => _logWindow.AppendLog(msg);

            _sensorCalibration        = new SensorCalibration(_server);
            _setpointsWindow          = new SetpointsWindow(_server);
            _generalInletParamsWindow = new GeneralInletParamsWindow(_server);
            _alarmManager             = new AlarmManager();
            _alarmsWindow             = new AlarmsWindow(_alarmManager);

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
                double ambTemp  = _server.LastMasterData?.Data?.Count >= 3
                                  ? _server.LastMasterData.Data[2].Temperature
                                  : 0;
                bool   isWarm    = a.regulTemp.consigne > ambTemp;
                double sortiePID = a.regulTemp.sortiePID_pc;

                if (a.ID >= 1 && a.ID <= 12)
                    UpdateAquarium(
                        a.ID,
                        a.debit, a.temperature, a.pH, a.oxy,
                        a.regulTemp.consigne, a.regulpH.consigne,
                        sortiePID, isWarm, a.regulpH.sortiePID_pc,
                        a.state);
                else if (a.ID >= 13 && a.ID <= 20)
                    UpdateFlume(
                        a.ID - 12,
                        a.debit, a.temperature, a.debitCircul, a.oxy, a.pH,
                        sortiePID, isWarm, a.regulpH.sortiePID_pc,
                        a.regulTemp.consigne, a.regulpH.consigne, a.state);

                _alarmManager.Evaluate(_server.Aquariums);
                ApplyAlarmIndicators();
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

        private void MenuCalibrateSensors_Click(object sender, RoutedEventArgs e)
        {
            _sensorCalibration.Show();
            _sensorCalibration.Activate();
        }

        private void MenuSetpoints_Click(object sender, RoutedEventArgs e)
        {
            _setpointsWindow.Show();
            _setpointsWindow.Activate();
        }

        private void MenuGeneralInletParams_Click(object sender, RoutedEventArgs e)
        {
            _generalInletParamsWindow.Show();
            _generalInletParamsWindow.Activate();
        }

        private void MenuAlarms_Click(object sender, RoutedEventArgs e)
        {
            _alarmsWindow.Show();
            _alarmsWindow.Activate();
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
            double pidTemp, bool isWarm, double pidPH,
            int state)
        {
            string q   = $"Q  : {debit:F2} L/mn";
            string t   = $"T  : {temp:F2} °C";
            string ph  = $"pH : {pH:F2}";
            string o   = $"O2 : {o2:F2} %";
            string tc  = $"T°C : {consTemp:F2} °C";
            string phc = $"pH: {consPH:F2}";
            string pph      = $"PID pH: {pidPH:F0} %";
            string stateStr = state == 0 ? "DISABLED" : (state == 1 ? "CONTROL" : "TREATMENT");
            var    pidFg    = state == 0 ? Brushes.Gray : (isWarm ? Brushes.Red : Brushes.Blue);

            System.Windows.Controls.Label lQ, lT, lPH, lO2, lConsT, lConsPH, lPIDTemp, lPIDpH, lHdr, lSp, lState;
            switch (id)
            {
                case  1: lQ=lblAq1Q;  lT=lblAq1T;  lPH=lblAq1PH;  lO2=lblAq1O2;  lConsT=lblAq1ConsT;  lConsPH=lblAq1ConsPH;  lPIDTemp=lblAq1PIDTemp;  lPIDpH=lblAq1PIDpH;  lHdr=lblAq1Hdr;  lSp=lblAq1;  lState=lblAq1State;  break;
                case  2: lQ=lblAq2Q;  lT=lblAq2T;  lPH=lblAq2PH;  lO2=lblAq2O2;  lConsT=lblAq2ConsT;  lConsPH=lblAq2ConsPH;  lPIDTemp=lblAq2PIDTemp;  lPIDpH=lblAq2PIDpH;  lHdr=lblAq2Hdr;  lSp=lblAq2;  lState=lblAq2State;  break;
                case  3: lQ=lblAq3Q;  lT=lblAq3T;  lPH=lblAq3PH;  lO2=lblAq3O2;  lConsT=lblAq3ConsT;  lConsPH=lblAq3ConsPH;  lPIDTemp=lblAq3PIDTemp;  lPIDpH=lblAq3PIDpH;  lHdr=lblAq3Hdr;  lSp=lblAq3;  lState=lblAq3State;  break;
                case  4: lQ=lblAq4Q;  lT=lblAq4T;  lPH=lblAq4PH;  lO2=lblAq4O2;  lConsT=lblAq4ConsT;  lConsPH=lblAq4ConsPH;  lPIDTemp=lblAq4PIDTemp;  lPIDpH=lblAq4PIDpH;  lHdr=lblAq4Hdr;  lSp=lblAq4;  lState=lblAq4State;  break;
                case  5: lQ=lblAq5Q;  lT=lblAq5T;  lPH=lblAq5PH;  lO2=lblAq5O2;  lConsT=lblAq5ConsT;  lConsPH=lblAq5ConsPH;  lPIDTemp=lblAq5PIDTemp;  lPIDpH=lblAq5PIDpH;  lHdr=lblAq5Hdr;  lSp=lblAq5;  lState=lblAq5State;  break;
                case  6: lQ=lblAq6Q;  lT=lblAq6T;  lPH=lblAq6PH;  lO2=lblAq6O2;  lConsT=lblAq6ConsT;  lConsPH=lblAq6ConsPH;  lPIDTemp=lblAq6PIDTemp;  lPIDpH=lblAq6PIDpH;  lHdr=lblAq6Hdr;  lSp=lblAq6;  lState=lblAq6State;  break;
                case  7: lQ=lblAq7Q;  lT=lblAq7T;  lPH=lblAq7PH;  lO2=lblAq7O2;  lConsT=lblAq7ConsT;  lConsPH=lblAq7ConsPH;  lPIDTemp=lblAq7PIDTemp;  lPIDpH=lblAq7PIDpH;  lHdr=lblAq7Hdr;  lSp=lblAq7;  lState=lblAq7State;  break;
                case  8: lQ=lblAq8Q;  lT=lblAq8T;  lPH=lblAq8PH;  lO2=lblAq8O2;  lConsT=lblAq8ConsT;  lConsPH=lblAq8ConsPH;  lPIDTemp=lblAq8PIDTemp;  lPIDpH=lblAq8PIDpH;  lHdr=lblAq8Hdr;  lSp=lblAq8;  lState=lblAq8State;  break;
                case  9: lQ=lblAq9Q;  lT=lblAq9T;  lPH=lblAq9PH;  lO2=lblAq9O2;  lConsT=lblAq9ConsT;  lConsPH=lblAq9ConsPH;  lPIDTemp=lblAq9PIDTemp;  lPIDpH=lblAq9PIDpH;  lHdr=lblAq9Hdr;  lSp=lblAq9;  lState=lblAq9State;  break;
                case 10: lQ=lblAq10Q; lT=lblAq10T; lPH=lblAq10PH; lO2=lblAq10O2; lConsT=lblAq10ConsT; lConsPH=lblAq10ConsPH; lPIDTemp=lblAq10PIDTemp; lPIDpH=lblAq10PIDpH; lHdr=lblAq10Hdr; lSp=lblAq10; lState=lblAq10State; break;
                case 11: lQ=lblAq11Q; lT=lblAq11T; lPH=lblAq11PH; lO2=lblAq11O2; lConsT=lblAq11ConsT; lConsPH=lblAq11ConsPH; lPIDTemp=lblAq11PIDTemp; lPIDpH=lblAq11PIDpH; lHdr=lblAq11Hdr; lSp=lblAq11; lState=lblAq11State; break;
                case 12: lQ=lblAq12Q; lT=lblAq12T; lPH=lblAq12PH; lO2=lblAq12O2; lConsT=lblAq12ConsT; lConsPH=lblAq12ConsPH; lPIDTemp=lblAq12PIDTemp; lPIDpH=lblAq12PIDpH; lHdr=lblAq12Hdr; lSp=lblAq12; lState=lblAq12State; break;
                default: return;
            }

            lQ.Content=q; lT.Content=t; lPH.Content=ph; lO2.Content=o;
            lConsT.Content=tc; lConsPH.Content=phc; lPIDpH.Content=pph;
            lState.Content=stateStr;
            SetPIDTemp(lPIDTemp, pidTemp, pidFg);

            var valLabels = new[] { lQ, lT, lPH, lO2, lConsT, lConsPH, lPIDpH, lSp, lState };
            if (state == 0)
            {
                foreach (var l in valLabels) l.Foreground = Brushes.Gray;
                lHdr.Background = Brushes.Gray;
            }
            else
            {
                foreach (var l in valLabels) l.ClearValue(System.Windows.Controls.Label.ForegroundProperty);
                lHdr.ClearValue(System.Windows.Controls.Label.BackgroundProperty);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Indicateurs d'alarme sur les labels de mesure
        // ─────────────────────────────────────────────────────────────────

        private void ApplyAlarmIndicators()
        {
            foreach (var alarm in _alarmManager.Alarms)
            {
                var lbl = GetAlarmLabel(alarm);
                if (lbl == null) continue;

                if (alarm.Raised)
                {
                    string content = lbl.Content?.ToString() ?? "";
                    if (!content.StartsWith("⚠ "))
                        lbl.Content = "⚠ " + content;
                    lbl.Foreground = alarm.Variant == "alarm" ? Brushes.Red : Brushes.Orange;
                }
            }
        }

        private System.Windows.Controls.Label GetAlarmLabel(Alarme alarm)
        {
            int id = alarm.AquaID;

            if (id >= 1 && id <= 12)
            {
                System.Windows.Controls.Label lT, lPH, lO2, lQ;
                switch (id)
                {
                    case  1: lT=lblAq1T;  lPH=lblAq1PH;  lO2=lblAq1O2;  lQ=lblAq1Q;  break;
                    case  2: lT=lblAq2T;  lPH=lblAq2PH;  lO2=lblAq2O2;  lQ=lblAq2Q;  break;
                    case  3: lT=lblAq3T;  lPH=lblAq3PH;  lO2=lblAq3O2;  lQ=lblAq3Q;  break;
                    case  4: lT=lblAq4T;  lPH=lblAq4PH;  lO2=lblAq4O2;  lQ=lblAq4Q;  break;
                    case  5: lT=lblAq5T;  lPH=lblAq5PH;  lO2=lblAq5O2;  lQ=lblAq5Q;  break;
                    case  6: lT=lblAq6T;  lPH=lblAq6PH;  lO2=lblAq6O2;  lQ=lblAq6Q;  break;
                    case  7: lT=lblAq7T;  lPH=lblAq7PH;  lO2=lblAq7O2;  lQ=lblAq7Q;  break;
                    case  8: lT=lblAq8T;  lPH=lblAq8PH;  lO2=lblAq8O2;  lQ=lblAq8Q;  break;
                    case  9: lT=lblAq9T;  lPH=lblAq9PH;  lO2=lblAq9O2;  lQ=lblAq9Q;  break;
                    case 10: lT=lblAq10T; lPH=lblAq10PH; lO2=lblAq10O2; lQ=lblAq10Q; break;
                    case 11: lT=lblAq11T; lPH=lblAq11PH; lO2=lblAq11O2; lQ=lblAq11Q; break;
                    case 12: lT=lblAq12T; lPH=lblAq12PH; lO2=lblAq12O2; lQ=lblAq12Q; break;
                    default: return null;
                }
                if (alarm.Libelle.StartsWith("Temperature")) return lT;
                if (alarm.Libelle.StartsWith("pH"))          return lPH;
                if (alarm.Libelle.StartsWith("O2"))          return lO2;
                if (alarm.Libelle.StartsWith("Flowrate"))    return lQ;
            }
            else if (id >= 13 && id <= 20)
            {
                int fi = id - 12;
                System.Windows.Controls.Label lT, lpH, lO2, lQ, lV;
                switch (fi)
                {
                    case 1: lT=lblFl1T; lpH=lblFl1pH; lO2=lblFl1O2; lQ=lblFl1Q; lV=lblSpeed1; break;
                    case 2: lT=lblFl2T; lpH=lblFl2pH; lO2=lblFl2O2; lQ=lblFl2Q; lV=lblSpeed2; break;
                    case 3: lT=lblFl3T; lpH=lblFl3pH; lO2=lblFl3O2; lQ=lblFl3Q; lV=lblSpeed3; break;
                    case 4: lT=lblFl4T; lpH=lblFl4pH; lO2=lblFl4O2; lQ=lblFl4Q; lV=lblSpeed4; break;
                    case 5: lT=lblFl5T; lpH=lblFl5pH; lO2=lblFl5O2; lQ=lblFl5Q; lV=lblSpeed5; break;
                    case 6: lT=lblFl6T; lpH=lblFl6pH; lO2=lblFl6O2; lQ=lblFl6Q; lV=lblSpeed6; break;
                    case 7: lT=lblFl7T; lpH=lblFl7pH; lO2=lblFl7O2; lQ=lblFl7Q; lV=lblSpeed7; break;
                    case 8: lT=lblFl8T; lpH=lblFl8pH; lO2=lblFl8O2; lQ=lblFl8Q; lV=lblSpeed8; break;
                    default: return null;
                }
                if (alarm.Libelle.StartsWith("Temperature")) return lT;
                if (alarm.Libelle.StartsWith("pH"))          return lpH;
                if (alarm.Libelle.StartsWith("O2"))          return lO2;
                if (alarm.Libelle.StartsWith("Flowrate"))    return lQ;
                if (alarm.Libelle.StartsWith("Speed"))       return lV;
            }

            return null;
        }

        private static void SetPIDTemp(System.Windows.Controls.Label lbl, double val, Brush color)
        {
            lbl.Content    = $"PID Temp: {val:F0} %";
            lbl.Foreground = color;
        }

        /// <summary>Mise à jour d'un flume (index 1–8).</summary>
        public void UpdateFlume(int id, double debit, double temp, double speed, double o2, double pH,
            double pidTemp, bool isWarm, double pidPH,
            double consTemp, double consPH, int state)
        {
            string q        = $"Q : {debit:F2} L/mn";
            string t        = $"T : {temp:F2} °C";
            string v        = $"V : {speed:F2} m/s";
            string o        = $"O2: {o2:F2} %";
            string ph       = $"pH : {pH:F2}";
            string pph      = $"PID pH: {pidPH:F0} %";
            string tc       = $"T°C : {consTemp:F2} °C";
            string phc      = $"pH: {consPH:F2}";
            string stateStr = state == 0 ? "DISABLED" : (state == 1 ? "CONTROL" : "TREATMENT");
            var    fg       = state == 0 ? Brushes.Gray : (isWarm ? Brushes.Red : Brushes.Blue);

            System.Windows.Controls.Label lQ, lT, lV, lO2, lpH, lPIDTemp, lPIDpH, lConsT, lConsPH, lHdr, lSp, lState;
            switch (id)
            {
                case 1: lQ=lblFl1Q; lT=lblFl1T; lV=lblSpeed1; lO2=lblFl1O2; lpH=lblFl1pH; lPIDTemp=lblFl1PIDTemp; lPIDpH=lblFl1PIDpH; lConsT=lblFl1ConsT; lConsPH=lblFl1ConsPH; lHdr=lblFl1Hdr; lSp=lblFl1; lState=lblFl1State; break;
                case 2: lQ=lblFl2Q; lT=lblFl2T; lV=lblSpeed2; lO2=lblFl2O2; lpH=lblFl2pH; lPIDTemp=lblFl2PIDTemp; lPIDpH=lblFl2PIDpH; lConsT=lblFl2ConsT; lConsPH=lblFl2ConsPH; lHdr=lblFl2Hdr; lSp=lblFl2; lState=lblFl2State; break;
                case 3: lQ=lblFl3Q; lT=lblFl3T; lV=lblSpeed3; lO2=lblFl3O2; lpH=lblFl3pH; lPIDTemp=lblFl3PIDTemp; lPIDpH=lblFl3PIDpH; lConsT=lblFl3ConsT; lConsPH=lblFl3ConsPH; lHdr=lblFl3Hdr; lSp=lblFl3; lState=lblFl3State; break;
                case 4: lQ=lblFl4Q; lT=lblFl4T; lV=lblSpeed4; lO2=lblFl4O2; lpH=lblFl4pH; lPIDTemp=lblFl4PIDTemp; lPIDpH=lblFl4PIDpH; lConsT=lblFl4ConsT; lConsPH=lblFl4ConsPH; lHdr=lblFl4Hdr; lSp=lblFl4; lState=lblFl4State; break;
                case 5: lQ=lblFl5Q; lT=lblFl5T; lV=lblSpeed5; lO2=lblFl5O2; lpH=lblFl5pH; lPIDTemp=lblFl5PIDTemp; lPIDpH=lblFl5PIDpH; lConsT=lblFl5ConsT; lConsPH=lblFl5ConsPH; lHdr=lblFl5Hdr; lSp=lblFl5; lState=lblFl5State; break;
                case 6: lQ=lblFl6Q; lT=lblFl6T; lV=lblSpeed6; lO2=lblFl6O2; lpH=lblFl6pH; lPIDTemp=lblFl6PIDTemp; lPIDpH=lblFl6PIDpH; lConsT=lblFl6ConsT; lConsPH=lblFl6ConsPH; lHdr=lblFl6Hdr; lSp=lblFl6; lState=lblFl6State; break;
                case 7: lQ=lblFl7Q; lT=lblFl7T; lV=lblSpeed7; lO2=lblFl7O2; lpH=lblFl7pH; lPIDTemp=lblFl7PIDTemp; lPIDpH=lblFl7PIDpH; lConsT=lblFl7ConsT; lConsPH=lblFl7ConsPH; lHdr=lblFl7Hdr; lSp=lblFl7; lState=lblFl7State; break;
                case 8: lQ=lblFl8Q; lT=lblFl8T; lV=lblSpeed8; lO2=lblFl8O2; lpH=lblFl8pH; lPIDTemp=lblFl8PIDTemp; lPIDpH=lblFl8PIDpH; lConsT=lblFl8ConsT; lConsPH=lblFl8ConsPH; lHdr=lblFl8Hdr; lSp=lblFl8; lState=lblFl8State; break;
                default: return;
            }

            lQ.Content=q; lT.Content=t; lV.Content=v; lO2.Content=o; lpH.Content=ph;
            lConsT.Content=tc; lConsPH.Content=phc; lPIDpH.Content=pph;
            lState.Content=stateStr;
            SetPIDTemp(lPIDTemp, pidTemp, fg);

            var valLabels = new[] { lQ, lT, lV, lO2, lpH, lConsT, lConsPH, lPIDpH, lSp, lState };
            if (state == 0)
            {
                foreach (var l in valLabels) l.Foreground = Brushes.Gray;
                lHdr.Background = Brushes.Gray;
            }
            else
            {
                foreach (var l in valLabels) l.ClearValue(System.Windows.Controls.Label.ForegroundProperty);
                lHdr.ClearValue(System.Windows.Controls.Label.BackgroundProperty);
            }
        }
    }
}

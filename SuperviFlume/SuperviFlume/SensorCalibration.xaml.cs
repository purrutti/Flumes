using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WebSocketServerExample;

namespace SuperviFlume
{
    /// <summary>
    /// Logique d'interaction pour SensorCalibration.xaml
    /// </summary>
    public partial class SensorCalibration : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);

        CancellationTokenSource cts = new CancellationTokenSource();

        private static async Task RunPeriodicAsync(Action onTick,
                                          TimeSpan dueTime,
                                          TimeSpan interval,
                                          CancellationToken token)
        {
            // Initial wait time before we begin the periodic loop.
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);

            // Repeat this loop until cancelled.
            while (!token.IsCancellationRequested)
            {
                // Call our onTick function.
                onTick?.Invoke();

                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
        }
        private async Task InitializeAsync()
        {
            var dueTime = TimeSpan.FromMinutes(0);
            var interval = TimeSpan.FromSeconds(1);

            // TODO: Add a CancellationTokenSource and supply the token here instead of None.
            await RunPeriodicAsync(refreshMeasure, dueTime, interval, cts.Token);
        }


        public SensorCalibration()
        {
            InitializeComponent();

            cts = new CancellationTokenSource();
            cbAquaNumber.SelectedIndex = 0;
            InitializeAsync();
        }

        private void btnSetOffset_Click(object sender, RoutedEventArgs e)
        {
            int PLCID = 0;
            int sensorID = 0;
            int aquaID = cbAquaNumber.SelectedIndex + 1;
            if (aquaID < 4)
            {
                PLCID = 1;
                sensorID = aquaID + 9;
            }
            else if (aquaID < 7)
            {
                PLCID = 2;
                sensorID = aquaID + 9 - 3;
            }
            else if (aquaID < 10)
            {
                PLCID = 3;
                sensorID = aquaID + 9 - 6;
            }
            else if (aquaID < 13)
            {
                PLCID = 4;
                sensorID = aquaID + 9 - 9;
            }
            else if (aquaID < 17)
            {
                PLCID = 6;
                sensorID = aquaID - 3;
            }
            else if (aquaID < 21)
            {
                PLCID = 7;
                sensorID = aquaID - 7;
            }

            double value = 0.0;
            sendReq(PLCID, aquaID, sensorID, 0, value);
        }

        private void btnSetSlope_Click(object sender, RoutedEventArgs e)
        {
            int PLCID = 0;
            int sensorID = 0;
            int aquaID = cbAquaNumber.SelectedIndex+1 ;
            if (aquaID < 4)
            {
                PLCID = 1;
                sensorID = aquaID + 9;
            }
            else if (aquaID < 7)
            {
                PLCID = 2;
                sensorID = aquaID + 9-3;
            }
            else if (aquaID < 10)
            {
                PLCID = 3;
                sensorID = aquaID + 9 - 6;
            }
            else if (aquaID < 13)
            {
                PLCID = 4;
                sensorID = aquaID + 9 - 9;
            }
            else if (aquaID < 17)
            {
                PLCID = 6;
                sensorID = aquaID - 3;
            }
            else if (aquaID < 21)
            {
                PLCID = 7;
                sensorID = aquaID - 7;
            }


            double value=100.0;
            sendReq(PLCID, aquaID,sensorID, 1, value);
        }

        private void btnCalibratepH_Click(object sender, RoutedEventArgs e)
        {
            int PLCID = 0;
            int sensorID = 0;
            int aquaID = cbAquaNumber.SelectedIndex + 1;
            if (aquaID < 4)
            {
                PLCID = 1;
                sensorID = aquaID - (3*(PLCID-1));
            }
            else if (aquaID < 7)
            {
                PLCID = 2;
                sensorID = aquaID - (3 * (PLCID - 1));
            }
            else if (aquaID < 10)
            {
                PLCID = 3;
                sensorID = aquaID - (3 * (PLCID - 1));
            }
            else if (aquaID < 13)
            {
                PLCID = 4;
                sensorID = aquaID - (3 * (PLCID - 1));
            }
            else if (aquaID < 17)
            {
                PLCID = 6;
                sensorID = aquaID - 12;
            }
            else if (aquaID < 21)
            {
                PLCID = 7;
                sensorID = aquaID - 16;
            }

            double value = 100.0;
            var str = tbpHCalibValue.Text.Replace('.', ',');
            Double.TryParse(str, out value);
            sendReq(PLCID, aquaID, sensorID, 1, value);
        }

        private void cbAquaNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            refreshMeasure();
        }

        private void refreshMeasure()
        {
            Dispatcher.Invoke(() =>
            {
                int id = cbAquaNumber.SelectedIndex;
                labelO2SensorValue.Content = "02 sensor value: " + MW.aquariums[id].oxy + "%";
                labelpHSensorValue.Content = "pH sensor value: " + MW.aquariums[id].pH;
            });
        }


        private void sendReq(int PLCID, int aquaID,int sensorID, int calibParam, double value)
        {
            CultureInfo culture = new CultureInfo("en-US");
            string formattedValue = value.ToString("F2", culture);
            //{ command: 4, condID: 1,senderID: 4, MesoID: 1,sensorID: 2, calibParam: 1, value: 123,45}
            string msg = "{\"cmd\":4,\"PLCID\":" + PLCID + ",\"AquaID\":" + aquaID + ",\"sensorID\":" + sensorID + ",\"calibParam\":" + calibParam + ",\"value\":" + value.ToString("F2", culture) + "}";
            MW.BroadcastMessageAsync(msg);


        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();

            e.Cancel = true;
        }
    }
}

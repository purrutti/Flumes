using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using WebSocketServerExample;

namespace SuperviFlume
{
    public partial class MasterParams : Window
    {
        private MainWindow _mainWindow;
        private int _selectedCondID = 0;

        public MasterParams(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
        }

        private void cbCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCondition.SelectedItem is ComboBoxItem selectedItem)
            {
                _selectedCondID = int.Parse(selectedItem.Tag.ToString());
                LoadParams(_selectedCondID);

                // Hide temperature regulation for Ambiant Water (CondID = 3)
                grpTemp.Visibility = _selectedCondID == 3 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void LoadParams(int condID)
        {
            var md = _mainWindow.GetMasterData();
            if (md?.Data == null || md.Data.Count < condID)
                return;

            var dataItem = md.Data.Find(d => d.ConditionID == condID);
            if (dataItem == null)
                return;

            // Load Temperature params (not for Ambiant Water)
            if (condID != 3 && dataItem.RTemp != null)
            {
                tbTempCons.Text = dataItem.RTemp.consigne.ToString();
                tbTempKp.Text = dataItem.RTemp.Kp.ToString();
                tbTempKi.Text = dataItem.RTemp.Ki.ToString();
                tbTempKd.Text = dataItem.RTemp.Kd.ToString();
                chkTempForcage.IsChecked = dataItem.RTemp.autorisationForcage;
                tbTempConsForcage.Text = dataItem.RTemp.consigneForcage.ToString();
            }

            // Load Pressure params
            if (dataItem.RPression != null)
            {
                tbPressionCons.Text = dataItem.RPression.consigne.ToString();
                tbPressionKp.Text = dataItem.RPression.Kp.ToString();
                tbPressionKi.Text = dataItem.RPression.Ki.ToString();
                tbPressionKd.Text = dataItem.RPression.Kd.ToString();
                chkPressionForcage.IsChecked = dataItem.RPression.autorisationForcage;
                tbPressionConsForcage.Text = dataItem.RPression.consigneForcage.ToString();
            }
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCondID == 0)
            {
                MessageBox.Show("Please select a condition first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                object message;

                if (_selectedCondID == 3)
                {
                    // Ambiant Water - only pressure
                    message = new
                    {
                        cmd = 2,
                        PLCID = 5,
                        AquaID = 0,
                        data = new[]
                        {
                            new
                            {
                                CondID = _selectedCondID,
                                rPression = new
                                {
                                    cons = double.Parse(tbPressionCons.Text),
                                    Kp = double.Parse(tbPressionKp.Text),
                                    Ki = double.Parse(tbPressionKi.Text),
                                    Kd = double.Parse(tbPressionKd.Text),
                                    autorisationForcage = chkPressionForcage.IsChecked ?? false,
                                    consigneForcage = double.Parse(tbPressionConsForcage.Text)
                                }
                            }
                        }
                    };
                }
                else
                {
                    // Hot Water or Cold Water - temperature and pressure
                    message = new
                    {
                        cmd = 2,
                        PLCID = 5,
                        AquaID = 0,
                        data = new[]
                        {
                            new
                            {
                                CondID = _selectedCondID,
                                rTemp = new
                                {
                                    cons = double.Parse(tbTempCons.Text),
                                    Kp = double.Parse(tbTempKp.Text),
                                    Ki = double.Parse(tbTempKi.Text),
                                    Kd = double.Parse(tbTempKd.Text),
                                    autorisationForcage = chkTempForcage.IsChecked ?? false,
                                    consigneForcage = double.Parse(tbTempConsForcage.Text)
                                },
                                rPression = new
                                {
                                    cons = double.Parse(tbPressionCons.Text),
                                    Kp = double.Parse(tbPressionKp.Text),
                                    Ki = double.Parse(tbPressionKi.Text),
                                    Kd = double.Parse(tbPressionKd.Text),
                                    autorisationForcage = chkPressionForcage.IsChecked ?? false,
                                    consigneForcage = double.Parse(tbPressionConsForcage.Text)
                                }
                            }
                        }
                    };
                }

                string json = JsonConvert.SerializeObject(message);
                _mainWindow.BroadcastMessage(json);

                MessageBox.Show("Parameters sent successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

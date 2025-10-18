using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;

namespace EVMS
{
    public class StartClickedEventArgs : EventArgs
    {
        public string Model { get; }
        public string LotNo { get; }
        public string UserId { get; }

        public StartClickedEventArgs(string model, string lotNo, string userId)
        {
            Model = model;
            LotNo = lotNo;
            UserId = userId;
        }
    }

    public partial class EntryPage : UserControl
    {
        public event EventHandler<StartClickedEventArgs> StartClicked;
        private readonly string connectionString;

        public EntryPage()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            LoadActiveModels();
            ShowShiftInLotNo();
        }

        private void LoadActiveModels()
        {
            try
            {
                List<string> activeModels = new List<string>();

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT Para_No FROM Part_Entry WHERE ActivePart = 1";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string model = $"{reader["Para_No"]}";
                            activeModels.Add(model);
                        }
                    }
                }

                if (activeModels.Count == 0)
                {
                    MessageBox.Show("No active part found in the database. Please activate a part first.",
                        "No Active Part", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnLogin.IsEnabled = false;
                    return;
                }

                cmbModels.ItemsSource = activeModels;
                cmbModels.SelectedIndex = 0;
                BtnLogin.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading active models: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnLogin.IsEnabled = false;
            }
        }

        private string GetShiftCode()
        {
            DateTime now = DateTime.Now;
            TimeSpan current = now.TimeOfDay;

            TimeSpan shiftAStart = new TimeSpan(6, 0, 0);   // 06:00
            TimeSpan shiftAEnd = new TimeSpan(13, 59, 59);
            TimeSpan shiftBStart = new TimeSpan(14, 0, 0);  // 14:00
            TimeSpan shiftBEnd = new TimeSpan(21, 59, 59);
            TimeSpan shiftCStart = new TimeSpan(22, 0, 0);  // 22:00
            TimeSpan shiftCEnd = new TimeSpan(5, 59, 59); // next day

            string shift;
            if (current >= shiftAStart && current <= shiftAEnd)
                shift = "A";
            else if (current >= shiftBStart && current <= shiftBEnd)
                shift = "B";
            else
                shift = "C"; // covers 22:00–23:59 and 00:00–05:59

            return shift;
        }

        private void ShowShiftInLotNo()
        {
            string dateShift = DateTime.Now.ToString("yyyyMMdd") + GetShiftCode();
            Txt.Text = dateShift;  // Show shift data like "20251017A" in LotNo TextBox
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (cmbModels.SelectedItem == null || string.IsNullOrWhiteSpace(Txt.Text) || string.IsNullOrWhiteSpace(LblUserId.Text))
            {
                MessageBox.Show("Please fill all fields: Model, Lot No, and User ID.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string model = cmbModels.SelectedItem.ToString();
            string lotNo = Txt.Text.Trim(); // This has date-shift prefix + user suffix

            string userId = LblUserId.Text.Trim();

            // Since Txt.Text already has date-shift prefix, directly use it without duplicating
            string finalLot = lotNo; // or if you want to ensure suffix is included, implement logic here.

            StartClicked?.Invoke(this, new StartClickedEventArgs(model, finalLot, userId));
        }

    }
}

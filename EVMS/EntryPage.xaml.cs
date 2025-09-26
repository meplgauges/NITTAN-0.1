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
        }

        private void LoadActiveModels()
        {
            try
            {
                List<string> activeModels = new List<string>();

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT Para_No, Para_Name FROM Part_Entry WHERE ActivePart = 1";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string model = $"{reader["Para_No"]} - {reader["Para_Name"]}";
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

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (cmbModels.SelectedItem == null || string.IsNullOrWhiteSpace(Txt.Text) || string.IsNullOrWhiteSpace(LblUserId.Text))
            {
                MessageBox.Show("Please fill all fields: Model, Lot No, and User ID.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string model = cmbModels.SelectedItem.ToString();
            string lotNo = Txt.Text.Trim();
            string userId = LblUserId.Text.Trim();

            StartClicked?.Invoke(this, new StartClickedEventArgs(model, lotNo, userId));
        }
    }
}

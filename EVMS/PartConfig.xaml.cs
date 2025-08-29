using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class PartConfig : UserControl
    {
        private readonly string connectionString;

        public PartConfig()
        {
            InitializeComponent();

            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;

            btnAdd.Click += BtnAdd_Click;
            btnUpdate.Click += BtnUpdate_Click;
            btnDelete.Click += BtnDelete_Click;

            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;

            LoadData();
            ClearInputs();
        }

        // ✅ Add
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputs()) return;

                string parameter = txtParameter.Text.Trim();
                decimal nominal = ParseDecimal(txtNominal.Text);
                decimal rTolPlus = ParseDecimal(txtRTolPlus.Text);
                decimal rTolMinus = ParseDecimal(txtRTolMinus.Text);
                decimal yTolPlus = ParseDecimal(txtYTolPlus.Text);
                decimal yTolMinus = ParseDecimal(txtYTolMinus.Text);

                string probeStatus = chkProbe.IsChecked == true ? "Probe" : "Para";

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = @"INSERT INTO PartConfig 
                                    (Parameter, Nominal, RTolPlus, RTolMinus, YTolPlus, YTolMinus, ProbeStatus)
                                    VALUES (@Parameter, @Nominal, @RTolPlus, @RTolMinus, @YTolPlus, @YTolMinus, @ProbeStatus)";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Parameter", parameter);
                        cmd.Parameters.AddWithValue("@Nominal", nominal);
                        cmd.Parameters.AddWithValue("@RTolPlus", rTolPlus);
                        cmd.Parameters.AddWithValue("@RTolMinus", rTolMinus);
                        cmd.Parameters.AddWithValue("@YTolPlus", yTolPlus);
                        cmd.Parameters.AddWithValue("@YTolMinus", yTolMinus);
                        cmd.Parameters.AddWithValue("@ProbeStatus", probeStatus);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("✅ Record inserted successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
                ClearInputs();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Database error: {sqlEx.Message}", "SQL Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ Update
        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputs()) return;

                string parameter = txtParameter.Text.Trim();
                decimal nominal = ParseDecimal(txtNominal.Text);
                decimal rTolPlus = ParseDecimal(txtRTolPlus.Text);
                decimal rTolMinus = ParseDecimal(txtRTolMinus.Text);
                decimal yTolPlus = ParseDecimal(txtYTolPlus.Text);
                decimal yTolMinus = ParseDecimal(txtYTolMinus.Text);

                string probeStatus = chkProbe.IsChecked == true ? "Probe" : "Para";

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = @"UPDATE PartConfig SET 
                                        Nominal=@Nominal, 
                                        RTolPlus=@RTolPlus, 
                                        RTolMinus=@RTolMinus, 
                                        YTolPlus=@YTolPlus, 
                                        YTolMinus=@YTolMinus, 
                                        ProbeStatus=@ProbeStatus
                                    WHERE Parameter=@Parameter";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Parameter", parameter);
                        cmd.Parameters.AddWithValue("@Nominal", nominal);
                        cmd.Parameters.AddWithValue("@RTolPlus", rTolPlus);
                        cmd.Parameters.AddWithValue("@RTolMinus", rTolMinus);
                        cmd.Parameters.AddWithValue("@YTolPlus", yTolPlus);
                        cmd.Parameters.AddWithValue("@YTolMinus", yTolMinus);
                        cmd.Parameters.AddWithValue("@ProbeStatus", probeStatus);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                        {
                            MessageBox.Show("⚠️ No record found to update.", "Not Found",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                MessageBox.Show("✅ Record updated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
                ClearInputs();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Database error: {sqlEx.Message}", "SQL Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ Delete
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataGrid.SelectedItem is not DataRowView row)
                {
                    MessageBox.Show("⚠️ Please select a record to delete.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string parameter = row["Parameter"].ToString();

                if (MessageBox.Show($"Are you sure you want to delete '{parameter}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    return;

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "DELETE FROM PartConfig WHERE Parameter=@Parameter";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Parameter", parameter);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("✅ Record deleted successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
                ClearInputs();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Database error: {sqlEx.Message}", "SQL Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ Load Data
        private void LoadData()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    string query = @"
                SELECT 
                    ROW_NUMBER() OVER (ORDER BY SrNo) AS SrNo,
                    Parameter, 
                    Nominal, 
                    RTolPlus, 
                    RTolMinus, 
                    YTolPlus, 
                    YTolMinus, 
                    ProbeStatus 
                FROM PartConfig
                ORDER BY SrNo";

                    SqlDataAdapter da = new SqlDataAdapter(query, con);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    dataGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Database error: {sqlEx.Message}", "SQL Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ✅ Clear TextBoxes
        private void ClearInputs()
        {
            txtParameter.Clear();
            txtNominal.Clear();
            txtRTolPlus.Clear();
            txtRTolMinus.Clear();
            txtYTolPlus.Clear();
            txtYTolMinus.Clear();
            chkProbe.IsChecked = false;

            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        // ✅ Safe decimal parse
        private decimal ParseDecimal(string input)
        {
            return decimal.TryParse(input, out decimal value) ? value : 0;
        }

        // ✅ Validate inputs before DB
        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(txtParameter.Text))
            {
                MessageBox.Show("⚠️ Parameter cannot be empty.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!decimal.TryParse(txtNominal.Text, out _))
            {
                MessageBox.Show("⚠️ Nominal value must be a number.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // ✅ When row selected → fill inputs
        private void dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem is DataRowView row)
            {
                txtParameter.Text = row["Parameter"].ToString();
                txtNominal.Text = row["Nominal"].ToString();
                txtRTolPlus.Text = row["RTolPlus"].ToString();
                txtRTolMinus.Text = row["RTolMinus"].ToString();
                txtYTolPlus.Text = row["YTolPlus"].ToString();
                txtYTolMinus.Text = row["YTolMinus"].ToString();
                chkProbe.IsChecked = row["ProbeStatus"].ToString() == "Probe";

                btnUpdate.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
        }
    }
}

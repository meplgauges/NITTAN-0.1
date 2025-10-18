using DocumentFormat.OpenXml.VariantTypes;
using Microsoft.Data.SqlClient;
using System;
using System.Configuration;
using System.Data;
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

            cmbPartNo.SelectionChanged += CmbPartNo_SelectionChanged;

            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;

            LoadPartNumbers();   // Load PartNo values into ComboBox
            LoadData();          // Initial load: load all or first part number's data
            ClearInputs();
        }

        // Load distinct PartNo for ComboBox
        private void LoadPartNumbers()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT Para_No FROM PART_ENTRY";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader reader = cmd.ExecuteReader();

                    cmbPartNo.Items.Clear();
                    while (reader.Read())
                    {
                        cmbPartNo.Items.Add(reader["Para_No"].ToString());
                    }

                    if (cmbPartNo.Items.Count > 0)
                        cmbPartNo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Part Numbers: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Load data filtered by selected PartNo or all if none selected
        private void LoadData()
        {
            if (cmbPartNo.SelectedItem != null)
                LoadDataByPartNo(cmbPartNo.SelectedItem.ToString());
            else
                LoadAllData();
        }

        // Load all data without filter - preserve old functionality
        private void LoadAllData()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            SrNo,
                            ROW_NUMBER() OVER (ORDER BY SrNo) AS RowNo,
                            Para_No,
                            Parameter, 
                            Nominal, 
                            RTolPlus, 
                            RTolMinus, 
                            YTolPlus, 
                            YTolMinus, 
                            ProbeStatus,
                            ShortName
                        FROM PartConfig
                        ORDER BY SrNo";
                    SqlDataAdapter da = new SqlDataAdapter(query, con);
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    dataGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Load data filtered by PartNo
        private void LoadDataByPartNo(string? Para_No)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            SrNo,
                            ROW_NUMBER() OVER (ORDER BY SrNo) AS RowNo,
                            Para_No,
                            Parameter, 
                            Nominal, 
                            RTolPlus, 
                            RTolMinus, 
                            YTolPlus, 
                            YTolMinus, 
                            ProbeStatus,
                            ShortName
                        FROM PartConfig
                        WHERE Para_No = @Para_No
                        ORDER BY SrNo";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@Para_No", Para_No);
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    dataGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data for Part No '{Para_No}': {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbPartNo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPartNo.SelectedItem == null) return;
            LoadDataByPartNo(cmbPartNo.SelectedItem.ToString());
            ClearInputs();
        }

        private bool IsParameterExists(string parameter)
        {
            try
            {
                using var con = new SqlConnection(connectionString);
                con.Open();

                string checkQuery = "SELECT COUNT(*) FROM PartConfig WHERE Parameter = @Parameter";
                using var cmd = new SqlCommand(checkQuery, con);
                cmd.Parameters.AddWithValue("@Parameter", parameter);

                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking parameter existence: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return true; // Treat error as existing to prevent inserts during DB issues
            }
        }


        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputs()) return;

                string? Para_No = cmbPartNo.SelectedItem?.ToString() ?? "";
                string? parameter = txtParameter.Text.Trim();
                string? ShortName = txtShort.Text.Trim();

                if (IsParameterExists(parameter))
                {
                    MessageBox.Show("⚠️ Parameter already exists. Please use a different name.", "Duplicate Entry",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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
                                    (Para_No, Parameter, Nominal, RTolPlus, RTolMinus, YTolPlus, YTolMinus, ProbeStatus,ShortName)
                                    VALUES (@Para_No, @Parameter, @Nominal, @RTolPlus, @RTolMinus, @YTolPlus, @YTolMinus, @ProbeStatus,@ShortName)";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Para_No", Para_No);
                        cmd.Parameters.AddWithValue("@Parameter", parameter);
                        cmd.Parameters.AddWithValue("@Nominal", nominal);
                        cmd.Parameters.AddWithValue("@RTolPlus", rTolPlus);
                        cmd.Parameters.AddWithValue("@RTolMinus", rTolMinus);
                        cmd.Parameters.AddWithValue("@YTolPlus", yTolPlus);
                        cmd.Parameters.AddWithValue("@YTolMinus", yTolMinus);
                        cmd.Parameters.AddWithValue("@ProbeStatus", probeStatus);
                        cmd.Parameters.AddWithValue("@ShortName", ShortName);

                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("✅ Record inserted successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inserting record: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputs()) return;

                if (dataGrid.SelectedItem is not DataRowView row)
                {
                    MessageBox.Show("⚠️ Please select a record to update.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int srNo = Convert.ToInt32(row["SrNo"]);
                string Para_No = cmbPartNo.SelectedItem?.ToString() ?? "";
                string parameter = txtParameter.Text.Trim();
                string? ShortName = txtShort.Text.Trim();


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
                                        Para_No=@Para_No,
                                        Parameter=@Parameter,
                                        Nominal=@Nominal, 
                                        RTolPlus=@RTolPlus, 
                                        RTolMinus=@RTolMinus, 
                                        YTolPlus=@YTolPlus, 
                                        YTolMinus=@YTolMinus, 
                                        ProbeStatus=@ProbeStatus,
                                        ShortName=@ShortName
                                    WHERE SrNo=@SrNo";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Para_No", Para_No);
                        cmd.Parameters.AddWithValue("@Parameter", parameter);
                        cmd.Parameters.AddWithValue("@Nominal", nominal);
                        cmd.Parameters.AddWithValue("@RTolPlus", rTolPlus);
                        cmd.Parameters.AddWithValue("@RTolMinus", rTolMinus);
                        cmd.Parameters.AddWithValue("@YTolPlus", yTolPlus);
                        cmd.Parameters.AddWithValue("@YTolMinus", yTolMinus);
                        cmd.Parameters.AddWithValue("@ProbeStatus", probeStatus);
                        cmd.Parameters.AddWithValue("@ShortName", ShortName);

                        cmd.Parameters.AddWithValue("@SrNo", srNo);
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating record: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                int srNo = Convert.ToInt32(row["SrNo"]);
                string? parameter = row["Parameter"].ToString();

                if (MessageBox.Show($"Are you sure you want to delete '{parameter}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    return;

                using var con = new SqlConnection(connectionString);
                {
                    con.Open();
                    string query = "DELETE FROM PartConfig WHERE SrNo=@SrNo";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@SrNo", srNo);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("✅ Record deleted successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting record: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearInputs()
        {
            txtParameter.Clear();
            txtShort.Clear();
            txtNominal.Clear();
            txtRTolPlus.Clear();
            txtRTolMinus.Clear();
            txtYTolPlus.Clear();
            txtYTolMinus.Clear();
            chkProbe.IsChecked = false;

            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        private decimal ParseDecimal(string input)
        {
            return decimal.TryParse(input, out var value) ? value : 0;
        }

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
        private void dataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
{
    e.Row.Header = (e.Row.GetIndex() + 1).ToString();
}

        private void dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem is DataRowView row)
            {
                // Set PartNo ComboBox selection if possible
                string? Para_No = row["Para_No"].ToString();
                if (!string.IsNullOrEmpty(Para_No) && cmbPartNo.Items.Contains(Para_No))
                {
                    cmbPartNo.SelectedItem = Para_No;
                }
                txtParameter.Text = row["Parameter"].ToString();
                txtNominal.Text = row["Nominal"].ToString();
                txtRTolPlus.Text = row["RTolPlus"].ToString();
                txtRTolMinus.Text = row["RTolMinus"].ToString();
                txtYTolPlus.Text = row["YTolPlus"].ToString();
                txtYTolMinus.Text = row["YTolMinus"].ToString();
                chkProbe.IsChecked = row["ProbeStatus"].ToString() == "Probe";
                txtShort.Text = row["ShortName"].ToString();


                btnUpdate.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
        }
    }
}

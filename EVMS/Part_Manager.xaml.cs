using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class Part_Manager : UserControl
    {
        private readonly string connectionString;

        public Part_Manager()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;

            btnUpdate.Click += BtnUpdate_Click;
            btnDelete.Click += BtnDelete_Click;

            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;

            LoadData();
            ClearInputs();
        }

        // Add method with validation and uniqueness check
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string paraNo = txtPartNumber.Text.Trim();
                string paraName = txtPartName.Text.Trim();

                if (string.IsNullOrEmpty(paraNo) || string.IsNullOrEmpty(paraName))
                {
                    MessageBox.Show("⚠️ Part Number and Part Name cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    // Check if Part Number already exists
                    string checkQuery = "SELECT COUNT(*) FROM Part_Entry WHERE Para_No = @Para_No";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@Para_No", paraNo);
                        int count = (int)checkCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            MessageBox.Show("⚠️ Part Number already exists. Please use a different Part Number.", "Duplicate Entry",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Insert new record
                    string insertQuery = "INSERT INTO Part_Entry (Para_No, Para_Name) VALUES (@Para_No, @Para_Name)";
                    using (SqlCommand cmd = new SqlCommand(insertQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@Para_No", paraNo);
                        cmd.Parameters.AddWithValue("@Para_Name", paraName);
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

        // Update method with validation and uniqueness check
        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataGrid.SelectedItem is not DataRowView selectedRow)
                {
                    MessageBox.Show("⚠️ Please select a record to update.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string paraNo = txtPartNumber.Text.Trim();
                string paraName = txtPartName.Text.Trim();

                if (string.IsNullOrEmpty(paraNo) || string.IsNullOrEmpty(paraName))
                {
                    MessageBox.Show("⚠️ Part Number and Part Name cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int id = Convert.ToInt32(selectedRow["ID"]);

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    // Check if Part Number exists in another record
                    string checkQuery = "SELECT COUNT(*) FROM Part_Entry WHERE Para_No = @Para_No AND ID != @ID";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@Para_No", paraNo);
                        checkCmd.Parameters.AddWithValue("@ID", id);
                        int count = (int)checkCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            MessageBox.Show("⚠️ Part Number already exists in another record. Please use a different Part Number.", "Duplicate Entry",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Update record
                    string updateQuery = "UPDATE Part_Entry SET Para_No = @Para_No, Para_Name = @Para_Name WHERE ID = @ID";
                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@Para_No", paraNo);
                        cmd.Parameters.AddWithValue("@Para_Name", paraName);
                        cmd.Parameters.AddWithValue("@ID", id);

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

        // Delete method
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

                int id = Convert.ToInt32(row["ID"]);
                string paraNo = row["Para_No"].ToString();

                if (MessageBox.Show($"Are you sure you want to delete '{paraNo}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    return;

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    string query = "DELETE FROM Part_Entry WHERE ID = @ID";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ID", id);
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

        // Load data and hide ID column
        private void LoadData()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT
                            ID,
                            ROW_NUMBER() OVER (ORDER BY ID) AS SrNo,
                            Para_No,
                            Para_Name
                        FROM Part_Entry
                        ORDER BY ID";

                    SqlDataAdapter da = new SqlDataAdapter(query, con);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    dataGrid.ItemsSource = dt.DefaultView;

                    // Hide the ID column (first column)
                    if (dataGrid.Columns.Count > 0)
                        dataGrid.Columns[0].Visibility = Visibility.Collapsed;
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

        // Clear input fields and disable buttons
        private void ClearInputs()
        {
            txtPartNumber.Text = string.Empty;
            txtPartName.Text = string.Empty;
            btnUpdate.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        // Handle DataGrid selection changed
        private void dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem is DataRowView row)
            {
                txtPartNumber.Text = row["Para_No"].ToString();
                txtPartName.Text = row["Para_Name"].ToString();
                btnUpdate.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ClearInputs();
            }
        }
    }
}

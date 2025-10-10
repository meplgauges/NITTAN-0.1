using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EVMS
{
    public partial class SettingsPage : UserControl
    {
        private string? connectionString;
        private Dictionary<int, ToggleButton> outputButtons = new Dictionary<int, ToggleButton>();
        private List<ControlItem> outputDevices = new List<ControlItem>();


        public SettingsPage()
        {
            InitializeComponent();

            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Database connection string 'EVMSDb' not found.", "Config Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AddButton.Click += AddButton_Click;
            UpdateButton.Click += UpdateButton_Click;
            DeleteButton.Click += DeleteButton_Click;

            LoadAndGenerate();
        }

        private class ControlItem
        {
            public string? Description { get; set; }
            public int Bit { get; set; }
        }

        private void LoadAndGenerate()
        {
            outputDevices = LoadItemsFromDatabase();
            GenerateInputButtons();
        }

        private List<ControlItem> LoadItemsFromDatabase()
        {
            var list = new List<ControlItem>();
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "SELECT Description, Bit FROM Controls";
                    var cmd = new SqlCommand(sql, conn);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ControlItem
                            {
                                Description = reader.GetString(0),
                                Bit = reader.GetBoolean(1) ? 1 : 0
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading data: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return list;
        }

        private void GenerateInputButtons()
        {
            try
            {
                ToggleGrid1.Children.Clear();
                outputButtons.Clear();

                foreach (var device in outputDevices)
                {
                    try
                    {
                        StackPanel container = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(5),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        ToggleButton outputButton = new ToggleButton
                        {
                            Tag = device,
                            IsEnabled = false,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 5)
                        };

                        try
                        {
                            outputButton.Style = (Style)FindResource("ModernSwitchToggleStyle");
                        }
                        catch (ResourceReferenceKeyNotFoundException)
                        {
                            // Style not found, set default properties
                            outputButton.Width = 80;
                            outputButton.Height = 36;
                        }

                        TextBlock deviceLabel = new TextBlock
                        {
                            Text = $"{device.Bit}\n{device.Description}",
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.Bold,
                            FontSize = 12,
                            TextAlignment = TextAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 0)
                        };

                        outputButton.Checked += OutputButton_Checked;
                        outputButton.Unchecked += OutputButton_Unchecked;
                        outputButtons[device.Bit] = outputButton;

                        container.Children.Add(outputButton);
                        container.Children.Add(deviceLabel);
                        ToggleGrid1.Children.Add(container);
                    }
                    catch (Exception buttonEx)
                    {
                        MessageBox.Show($"Error creating output button for {device.Bit}: {buttonEx.Message}",
                            "Button Creation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating output buttons: {ex.Message}", "UI Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OutputButton_Checked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle != null)
            {
                // Implement your logic for when the output button is checked
                var device = toggle.Tag as ControlItem;
                // Example: MessageBox.Show($"Output button checked: {device.Description}");
            }
        }

        private void OutputButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle != null)
            {
                // Implement your logic for when the output button is unchecked
                var device = toggle.Tag as ControlItem;
                // Example: MessageBox.Show($"Output button unchecked: {device.Description}");
            }
        }

        private void UpdateBitInDatabase(string description, int bitValue)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "UPDATE Controls SET Bit = @bit WHERE Description = @desc";
                    var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@bit", bitValue);
                    cmd.Parameters.AddWithValue("@desc", description);
                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        MessageBox.Show($"No record found for '{description}' to update.", "Update Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating bit: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string description = DescTextBox.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                MessageBox.Show("Please enter a description.", "Input Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(BitTextBox.Text.Trim(), out int bitInt) || (bitInt != 0 && bitInt != 1))
            {
                MessageBox.Show("Bit must be 0 or 1.", "Input Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "INSERT INTO Controls (Description, Bit) VALUES (@desc, @bit)";
                    var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@bit", bitInt);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                LoadAndGenerate();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding record: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            string description = DescTextBox.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                MessageBox.Show("Please enter a description.", "Input Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(BitTextBox.Text.Trim(), out int bitInt) || (bitInt != 0 && bitInt != 1))
            {
                MessageBox.Show("Bit must be 0 or 1.", "Input Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "UPDATE Controls SET Bit = @bit WHERE Description = @desc";
                    var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@bit", bitInt);
                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        MessageBox.Show("No record found to update.", "Update Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                LoadAndGenerate();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating record: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string description = DescTextBox.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                MessageBox.Show("Please enter a description to delete.", "Input Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "DELETE FROM Controls WHERE Description = @desc";
                    var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@desc", description);
                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        MessageBox.Show("No record found to delete.", "Delete Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                LoadAndGenerate();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting record: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearInputs()
        {
            DescTextBox.Text = "";
            BitTextBox.Text = "";
        }
    }
}

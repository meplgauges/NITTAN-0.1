using System;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    // Custom EventArgs to carry entered values
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
        // Change event to send StartClickedEventArgs
        public event EventHandler<StartClickedEventArgs> StartClicked;

        public EntryPage()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string model = TxtEmail.Text?.Trim() ?? string.Empty;
            string lotNo = Txt.Text?.Trim() ?? string.Empty;
            string userId = LblUserId.Text?.Trim() ?? string.Empty;

            //// Validate that all required fields have values
            //if (string.IsNullOrEmpty(model))
            //{
            //    MessageBox.Show("Please enter a model.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    TxtEmail.Focus();
            //    return;
            //}
            //if (string.IsNullOrEmpty(lotNo))
            //{
            //    MessageBox.Show("Please enter a Lot No.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    Txt.Focus();
            //    return;
            //}
            //if (string.IsNullOrEmpty(userId))
            //{
            //    MessageBox.Show("Please enter User ID.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    LblUserId.Focus();
            //    return;
            //}

            // If validation passes, raise the event to navigate
            StartClicked?.Invoke(this, new StartClickedEventArgs(model, lotNo, userId));
        }

    }
}

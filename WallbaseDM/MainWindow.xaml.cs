using System;
using System.ComponentModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace WallbaseDM
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            PuritySFW.IsChecked = user.Default.puritySFW;
            PurityNSFW.IsChecked = user.Default.purityNSFW;
            PuritySKETCHY.IsChecked = user.Default.puritySKETCHY;
            CategoryHR.IsChecked = user.Default.categoryHR;
            CategoryW.IsChecked = user.Default.categoryW;
            CategoryWG.IsChecked = user.Default.categoryWG;
            SearchQuery.Text = user.Default.searchQuery;
            txtDestination.Text = user.Default.lastDestination;
            Order.SelectedIndex = user.Default.orderBy;
            OrderMode.SelectedIndex = user.Default.orderMode;
	        txtLimit.Text = user.Default.limit;
	        txtLogin.Text = user.Default.login;
	        txtPass.Password = user.Default.pass;

			ServicePointManager.Expect100Continue = false;
	        ServicePointManager.DefaultConnectionLimit = 50;

	        queue.ItemsSource = Wallbase.toDownload;
        }

        private async void ButtonStart_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonStart.IsEnabled = false;
            bool isNSFW = PurityNSFW.IsEnabled && (bool)PurityNSFW.IsChecked;
            bool isSFW = (bool)PuritySFW.IsChecked;
            bool isSKETCHY = (bool)PuritySKETCHY.IsChecked;
            bool isHR = (bool)CategoryHR.IsChecked;
            bool isW = (bool)CategoryW.IsChecked;
            bool isWG = (bool)CategoryWG.IsChecked;
            string query = SearchQuery.Text;
            string order = "relevance";
            if (Order.SelectedValue != null)
            {
                string value = (string) ((ComboBoxItem) Order.SelectedValue).Content;
                switch (value)
                {
                    case "Relevance":
                        order = "relevance";
                        break;
                    case "Date added":
                        order = "date";
                        break;
                    case "Views":
                        order = "date";
                        break;
                    case "Favorites":
                        order = "favs";
                        break;
                    case "Random":
                        order = "random";
                        break;
                }
            }
            bool isDesc = !(OrderMode.SelectedValue != null && ((ComboBoxItem)OrderMode.SelectedValue).Content.Equals("Ascending"));
            user.Default.orderMode = (byte)OrderMode.SelectedIndex;

            int limit = 0;
            Int32.TryParse(txtLimit.Text, out limit);

            string queryString = Wallbase.Instance.BuildQuery(isSFW, isSKETCHY, isNSFW, isW, isWG, isHR, "", query, isDesc, order);

            if (await Wallbase.Instance.DownloadWallpapers(queryString, txtDestination.Text, false,
                                                     limit > 0 ? limit : Int32.MaxValue))
            {
                ButtonStart.IsEnabled = true;
	            Title = "WallbaseDM";
				Log("Completed!");
            }
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            user.Default.purityNSFW = (bool) PurityNSFW.IsChecked;
            user.Default.puritySFW = (bool) PuritySFW.IsChecked;
            user.Default.puritySKETCHY = (bool)PuritySKETCHY.IsChecked;
            user.Default.categoryHR = (bool) CategoryHR.IsChecked;
            user.Default.categoryW = (bool)CategoryW.IsChecked;
            user.Default.categoryWG = (bool)CategoryWG.IsChecked;
            user.Default.searchQuery = SearchQuery.Text;
            user.Default.orderBy = (byte) Order.SelectedIndex;
            user.Default.orderMode = (byte) OrderMode.SelectedIndex;
	        user.Default.limit = txtLimit.Text;
	        user.Default.login = txtLogin.Text;
			if((bool)chkSave.IsChecked)
				user.Default.pass = txtPass.Password;

            user.Default.Save();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (await Wallbase.Instance.Authenticate(txtLogin.Text, txtPass.Password))
            {
                PurityNSFW.IsEnabled = true;
                PurityNSFW.ToolTip = "Adult only";
                BtnLogin.IsEnabled = false;
            }
        }

        public void Log(string text)
        {
            txtLog.AppendText(text+"\r\n");
        }

        private void btnChangeDestination_Click(object sender, RoutedEventArgs e)
        {
            SelectFolder();
        }

        private void TxtDestination_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            SelectFolder();
        }

        private void SelectFolder()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (String.IsNullOrEmpty(user.Default.lastDestination))
                dialog.SelectedPath = Environment.SpecialFolder.MyPictures.ToString();
            else
                dialog.SelectedPath = user.Default.lastDestination;

            dialog.ShowDialog();

            txtDestination.Text = dialog.SelectedPath;
            user.Default.lastDestination = dialog.SelectedPath;
        }

    }
}

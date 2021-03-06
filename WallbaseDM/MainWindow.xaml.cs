﻿using System;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace WallbaseDM
{
    public partial class MainWindow : Window
    {
		private const string GITHUB_URL = "https://github.com/CrawniX/wallbase-dm/releases/tag/public";

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

			CheckNewVersion();
        }

		private void CheckNewVersion()
		{
			string response = string.Empty;
			try
			{
				WebClient client = new WebClient();
				response = client.DownloadString(GITHUB_URL);
			}
			catch (Exception)
			{
			}
			
			Regex regex = new Regex(@"\<b\>v.([0-9.]+)\</b\>");
			Match match = regex.Match(response);

			if (match.Groups.Count > 0)
			{
				Version version = Assembly.GetExecutingAssembly().GetName().Version;

				Version onlineVersion;
				Version.TryParse(match.Groups[1].ToString(), out onlineVersion);

				if (version.CompareTo(onlineVersion) < 0)
					if (System.Windows.MessageBox.Show("New version available! Open the download page?", "New version",
					                                   MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
					{
						try
						{
							System.Diagnostics.Process.Start(GITHUB_URL);
						}
						catch (Exception)
						{
							System.Windows.MessageBox.Show("Opening of webpage seems to be failed :(\n\n" +
							                               "To get a new version. Visit the next url:\n\n"+ 
														   GITHUB_URL + "\n\n\n" +
							                               "Also link can be found in the Wallbase forum thread.", "Failed :(");
						}
					}
			}
		}

        private void ButtonStart_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonStart.IsEnabled = false;

			int limit = 0;
			Int32.TryParse(txtLimit.Text, out limit);
			string destination = string.IsNullOrEmpty(txtDestination.Text) ? "./WDMDownloads/" : txtDestination.Text;

	        if ((bool) radioSearch.IsChecked)
	        {
		        bool isNSFW = PurityNSFW.IsEnabled && (bool) PurityNSFW.IsChecked;
		        bool isSFW = (bool) PuritySFW.IsChecked;
		        bool isSKETCHY = (bool) PuritySKETCHY.IsChecked;
		        bool isHR = (bool) CategoryHR.IsChecked;
		        bool isW = (bool) CategoryW.IsChecked;
		        bool isWG = (bool) CategoryWG.IsChecked;
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
		        bool isDesc =
			        !(OrderMode.SelectedValue != null && ((ComboBoxItem) OrderMode.SelectedValue).Content.Equals("Ascending"));
		        user.Default.orderMode = (byte) OrderMode.SelectedIndex;

		       

		        string queryString = Wallbase.Instance.BuildQuery(isSFW, isSKETCHY, isNSFW, isW, isWG, isHR, "", query, isDesc,
		                                                          order);
			

		        new Thread(() =>
			        {
				        Task<bool> task = Wallbase.Instance.DownloadWallpapers(queryString, destination, false,
				                                                               limit > 0 ? limit : Int32.MaxValue);
				        task.Wait();
				        Dispatcher.Invoke(delegate
					        {
						        ButtonStart.IsEnabled = true;
						        Log("Completed!");
					        });

			        }).Start();
	        }
	        else
	        {
		        string url = txtFavUrl.Text;

				Regex urlRegex = new Regex(@"(http://)?wallbase.cc/(favorites|collection)/?([0-9]+)?", RegexOptions.IgnoreCase);

		        Match match = urlRegex.Match(url);

		        if (match.Groups.Count < 1)
			        System.Windows.MessageBox.Show("Invalid favorites / collection url.", "Invalid URL", MessageBoxButton.OK,
			                                       MessageBoxImage.Error);
		        else
		        {
			        new Thread(() =>
				        {
							if (url.EndsWith("favorites"))
								url += "/0/";
							else if (url.EndsWith("favorites/"))
								url += "0/";
					        Task<bool> task = Wallbase.Instance.DownloadCollection(url, destination, limit > 0 ? limit : Int32.MaxValue);
					        task.Wait();
					        Dispatcher.Invoke(delegate
						        {
							        ButtonStart.IsEnabled = true;
							        Log("Completed!");
						        });

				        }).Start();

		        }
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

		private void RadioSearch_OnChecked(object sender, RoutedEventArgs e)
		{
			SearchQuery.IsEnabled = true;
			txtFavUrl.IsEnabled = false;
			ToggleFilters();
		}

	    private void RadioFavs_OnChecked(object sender, RoutedEventArgs e)
	    {
			txtFavUrl.IsEnabled = true;
		    SearchQuery.IsEnabled = false;
			ToggleFilters(false);
	    }

		private void ToggleFilters(bool enable = true)
		{
			if (!enable)
			{
				PurityNSFW.IsEnabled = false;
				PuritySFW.IsEnabled = false;
				PuritySKETCHY.IsEnabled = false;
				CategoryHR.IsEnabled = false;
				CategoryW.IsEnabled = false;
				CategoryWG.IsEnabled = false;
				OrderMode.IsEnabled = false;
				Order.IsEnabled = false;
			}
			else
			{
				if(Wallbase.Instance.IsAuthenticated)
					PurityNSFW.IsEnabled = true;
				PuritySFW.IsEnabled = true;
				PuritySKETCHY.IsEnabled = true;
				CategoryHR.IsEnabled = true;
				CategoryW.IsEnabled = true;
				CategoryWG.IsEnabled = true;
				OrderMode.IsEnabled = true;
				Order.IsEnabled = true;
			}
		}
    }
}

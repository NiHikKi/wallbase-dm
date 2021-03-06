﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WallbaseDM
{
    class Wallbase
    {
	    private const int DOWNLOAD_DELAY = 1500;
	    private const int CONCURRENCY_LEVEL = 1;

        private const string WALLBASE_LOGIN_URL = "http://wallbase.cc/user/login";
        private const string WALLBASE_AUTH_URL = "http://wallbase.cc/user/do_login";
        private const string WALLBASE_INDEX_URL = "http://wallbase.cc/";
        private const string WALLBASE_SEARCH_PAGE = "http://wallbase.cc/search";
        private const string WALLBASE_WALLPAPER_URL = "http://wallbase.cc/wallpaper/";
        private string USER_AGENT = "WallbaseDM v." + Assembly.GetExecutingAssembly().GetName().Version;

        private static Wallbase _instance;
	    public static ObservableCollection<WallbasePicture> toDownload = new ObservableCollection<WallbasePicture>();

	    private static long lastRequest = 0;
	    private static long lastDownload = 0;
	    private static int downloaded = 0;

        public static Wallbase Instance
        {
            get
            {
                if (_instance == null) _instance = new Wallbase();
                return _instance;
            }
        }

        public bool IsAuthenticated = false;
        private CookieContainer _cookies = new CookieContainer();

	    public string BuildQuery(bool isSFW, bool isSKETCHY, bool isNSFW, bool isW, bool isWG, bool isHR, string color,
                                 string query, bool isDesc, string order)
        {
            string purity = (isSFW ? "1" : "0") + (isSKETCHY ? "1" : "0") + (isNSFW ? "1" : "0");
            string board = (isWG ? "2" : "") + (isW ? "1" : "") + (isHR ? "3" : "");
            string orderMode = isDesc ? "desc" : "asc";


            string queryString = "?q=" + query + "&color=" + color + "&section=" + "&order_mode=" + orderMode +
                                 "&order=" + order + "&thpp=60&purity=" + purity + "&board=" + board;

            return queryString;
        }

		public async Task<bool> DownloadCollection(string url, string destination, int limit = Int32.MaxValue)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			List<WallbasePicture> wallpaperList = await GetCollectionWallpaperList(url, limit);
			Log("Get in " + stopwatch.ElapsedMilliseconds + "ms");
			
			List<string> alreadyDownloaded = GetDownloadedWallpaperList(destination);
			
			toDownload = new ObservableCollection<WallbasePicture>();

			foreach (WallbasePicture picture in wallpaperList)
			{
				if (!alreadyDownloaded.Contains(picture.Name))
					toDownload.Add(picture);
			}

			Log("To download: " + toDownload.Count);
			downloaded = 0;
			MainWindow mw = null;
			Application.Current.Dispatcher.Invoke(delegate
			{
				mw = Application.Current.MainWindow as MainWindow;
			});
			if (mw != null)
				mw.Dispatcher.Invoke(delegate
				{
					mw.queue.ItemsSource = toDownload;
					mw.queue.Items.Refresh();
					mw.txtCount.Content = "Count: " + toDownload.Count;

					mw.Title = "WallbaseDM - Downloading";
					mw.progressBar.Value = 0;
					mw.progressBar.Maximum = toDownload.Count;
				});

			stopwatch.Restart();

			Regex regex = new Regex("<img src=\\\"(http://wallpapers.wallbase.cc/(.*))\\\" class=\\\"wall");

			int concurrencyLevel = CONCURRENCY_LEVEL;
			int current = 0;
			Stopwatch stopwatchDownload = new Stopwatch();
			var downloadTasks = new List<Task<WallbasePicture>>();
			while (current < concurrencyLevel && current < toDownload.Count)
			{
				string response =
					await MakeRequest(WALLBASE_WALLPAPER_URL + toDownload[current].Name, toDownload[current].Referer, null, 250);

				Match match = regex.Match(response);

				toDownload[current].Url = match.Groups[1].ToString();
				toDownload[current].LocalPath = destination + "/" + toDownload[current].Name + ".jpg";

				if (!string.IsNullOrEmpty(toDownload[current].Url))
				{
					downloadTasks.Add(DownloadImageAsync(toDownload[current]));
				}

				current++;
			}

			while (downloadTasks.Count > 0)
			{
				try
				{
					stopwatchDownload.Start();
					Task<WallbasePicture> downloadTask = await Task.WhenAny(downloadTasks);
					downloadTasks.Remove(downloadTask);

					await downloadTask;
					Log("From last download elapsed " + stopwatchDownload.ElapsedMilliseconds + "ms"); 
					if (stopwatchDownload.ElapsedMilliseconds < DOWNLOAD_DELAY)
						Thread.Sleep(DOWNLOAD_DELAY);
					stopwatchDownload.Restart();
					mw.Dispatcher.Invoke(delegate
					{
						mw.queue.Items.Refresh();
						mw.progressBar.Value = downloaded;
						mw.Title = Math.Round(((double)downloaded / toDownload.Count) * 100, 2) + "% - WallbaseDM";
					});

				}
				catch (Exception e)
				{
					Log("Error: " + e.Message);
				}

				if (current < toDownload.Count)
				{
					string response =
						await MakeRequest(WALLBASE_WALLPAPER_URL + toDownload[current].Name, toDownload[current].Referer, null, 250);
					Match match = regex.Match(response);

					toDownload[current].Url = match.Groups[1].ToString();
					toDownload[current].LocalPath = destination + "/" + toDownload[current].Name + ".jpg";

					downloadTasks.Add(DownloadImageAsync(toDownload[current]));
					current++;
				}
			}

			Log("Download of " + downloaded + " completed in " + stopwatch.ElapsedMilliseconds + "ms");
			stopwatch.Stop();
			mw.Dispatcher.Invoke(delegate
			{
				mw.Title = "WallbaseDM";
			});

			return true;
		}

	    public async Task<bool> DownloadWallpapers(string queryString, string destination, bool byPurity = false,
	                                               int limit = Int32.MaxValue)
	    {
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
		    List<WallbasePicture> wallpaperList = await GetWallpaperList(queryString, limit);
			Log("Get in " + stopwatch.ElapsedMilliseconds + "ms");
		    List<string> alreadyDownloaded = GetDownloadedWallpaperList(destination);

		    toDownload = new ObservableCollection<WallbasePicture>();

		    foreach (WallbasePicture picture in wallpaperList)
		    {
			    if (!alreadyDownloaded.Contains(picture.Name))
				    toDownload.Add(picture);
		    }

		    Log("To download: " + toDownload.Count);
		    downloaded = 0;
		    MainWindow mw = null;
		    Application.Current.Dispatcher.Invoke(delegate
			    {
				    mw = Application.Current.MainWindow as MainWindow;
			    });
		    if (mw != null)
			    mw.Dispatcher.Invoke(delegate
				    {
					    mw.queue.ItemsSource = toDownload;
					    mw.queue.Items.Refresh();
						mw.txtCount.Content = "Count: " + toDownload.Count;

					    mw.Title = "WallbaseDM - Downloading";
					    mw.progressBar.Value = 0;
					    mw.progressBar.Maximum = toDownload.Count;
				    });

			stopwatch.Restart();

		    Regex regex = new Regex("<img src=\\\"(http://wallpapers.wallbase.cc/(.*))\\\" class=\\\"wall");

			int concurrencyLevel = CONCURRENCY_LEVEL;
		    int current = 0;

			Stopwatch stopwatchDownload = new Stopwatch();

		    var downloadTasks = new List<Task<WallbasePicture>>();
		    while (current < concurrencyLevel && current < toDownload.Count)
		    {
			    string response =
				    await MakeRequest(WALLBASE_WALLPAPER_URL + toDownload[current].Name, toDownload[current].Referer, null, 250);

			    Match match = regex.Match(response);

			    toDownload[current].Url = match.Groups[1].ToString();
			    toDownload[current].LocalPath = destination + "/" + toDownload[current].Name + ".jpg";

			    if (!string.IsNullOrEmpty(toDownload[current].Url))
			    {
				    downloadTasks.Add(DownloadImageAsync(toDownload[current]));
			    }

			    current++;
		    }

		    while (downloadTasks.Count > 0)
		    {
			    try
			    {
				    Task<WallbasePicture> downloadTask = await Task.WhenAny(downloadTasks);
				    downloadTasks.Remove(downloadTask);

				    await downloadTask;
					Log("From last download elapsed " + stopwatchDownload.ElapsedMilliseconds + "ms");
					if(stopwatchDownload.ElapsedMilliseconds < DOWNLOAD_DELAY)
						Thread.Sleep(DOWNLOAD_DELAY);
					stopwatchDownload.Restart();
					
				    mw.Dispatcher.Invoke(delegate
					    {
						    mw.queue.Items.Refresh();
							mw.Title = Math.Round(((double)downloaded / toDownload.Count) * 100, 2) + "% - WallbaseDM";
						    mw.progressBar.Value = downloaded;
					    });

			    }
			    catch (Exception e)
			    {
				    Log("Error: " + e.Message);
			    }

			    if (current < toDownload.Count)
			    {
				    string response =
					    await MakeRequest(WALLBASE_WALLPAPER_URL + toDownload[current].Name, toDownload[current].Referer, null, 500);
				    Match match = regex.Match(response);

				    toDownload[current].Url = match.Groups[1].ToString();
				    toDownload[current].LocalPath = destination + "/" + toDownload[current].Name + ".jpg";

				    downloadTasks.Add(DownloadImageAsync(toDownload[current]));
				    current++;
			    }
		    }
			Log("Download of " + downloaded + " completed in " + stopwatch.ElapsedMilliseconds + "ms");
			stopwatch.Stop();
		    mw.Dispatcher.Invoke(delegate
			    {
				    mw.Title = "WallbaseDM";
			    });

		    return true;
	    }

	    private List<string> GetDownloadedWallpaperList(string destination)
	    {
		    if (!Directory.Exists(destination))
			    Directory.CreateDirectory(destination);
		    IEnumerable<string> list = Directory.EnumerateFiles(destination, "*.jpg", SearchOption.TopDirectoryOnly);
			List<string> result = new List<string>();
		    foreach (string s in list)
		    {
				FileInfo fi = new FileInfo(s);

				result.Add(fi.Name.Substring(0, fi.Name.Length - 4));
		    }

		    return result;
	    }

	    private async Task<WallbasePicture> DownloadImageAsync(WallbasePicture picture)
	    {
			int retries = 3;
            while (retries > 0)
            {
                try
                {
                    HttpWebRequest request = WebRequest.CreateHttp(picture.Url);
                    request.UserAgent = USER_AGENT;

	                long current = DateTime.Now.Ticks - lastDownload;
	                if (current < DOWNLOAD_DELAY*1000)
	                {
		                Thread.Sleep((int) Math.Abs(DOWNLOAD_DELAY - current/1000));
	                }

	                HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;

					if (response != null &&
						((response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect ||
						  response.StatusCode == HttpStatusCode.Moved) &&
						 response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase)))
					{
						using (Stream stream = response.GetResponseStream())
						{
							using (Stream file = File.OpenWrite(picture.LocalPath))
							{
								if (stream != null)
								{
									byte[] buffer = new byte[4096];
									int bytesRead;
									do
									{
										bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
										await file.WriteAsync(buffer, 0, bytesRead);
									} while (bytesRead != 0);
									picture.Downloaded = true;
									downloaded++;
									lastDownload = DateTime.Now.Ticks;

									return picture;
								}
							}
						}
					}
					else if (response != null && response.StatusCode == HttpStatusCode.NotFound)
					{
						downloaded++;
						return picture;
					}
                }
                catch (Exception e)
                {
                    Log("Error: " + e.Message + "\n" + e.StackTrace);
					Log("Image url: " + picture.Url);
					Thread.Sleep(2000);
	                retries--;
                }
            }
			Log("Aborted!");
		    downloaded++;
		    return picture;
	    }


		private async Task<List<WallbasePicture>> GetCollectionWallpaperList(string url, int limit)
		{
			try
			{
				List<WallbasePicture> result = new List<WallbasePicture>();

				string response = await MakeRequest(url, WALLBASE_SEARCH_PAGE);

				Regex regexLast = new Regex("results_count: ([0-9]+),\n");

				Match matchLast = regexLast.Match(response);

				int until = Int32.Parse(matchLast.Groups[1].ToString());
				Log("Found: " + until);

				int current = 0;
				Regex regex = new Regex("<div id=\\\"thumb([0-9]+)\\\" class=\\\"thumbnail purity-([0-9]+)\\\"");
				string currentUrl;

				MainWindow mw = null;
				Application.Current.Dispatcher.Invoke(delegate
				{
					mw = Application.Current.MainWindow as MainWindow;
				});
				until = until > limit ? limit : until;

				if (mw != null)
				{
					mw.Dispatcher.Invoke(delegate
					{
						mw.progressBar.Maximum = until;
						mw.progressBar.Value = 0;
						mw.Title = "WallbaseDM - Collecting";
					});
				}
				while (current < until)
				{
					if (current >= limit)
						break;

					currentUrl = url + "/" + current;
					response = await MakeRequest(currentUrl);

					if (response != null)
					{
						MatchCollection matches = regex.Matches(response);

						foreach (Match match in matches)
						{
							if (current >= limit)
								break;

							int purity = Int32.Parse(match.Groups[2].ToString());
							result.Add(new WallbasePicture(match.Groups[1].ToString(), currentUrl, (Purity)purity));
							current++;
						}
						mw.Dispatcher.Invoke(delegate
						{
							mw.progressBar.Value = current;
						});
						if (matches.Count == 0)
							break;
					}
				}
				return result;
			}
			catch (Exception)
			{
				return new List<WallbasePicture>();
			}
		}

        private async Task<List<WallbasePicture>> GetWallpaperList(string query, int limit)
        {
            try
            {
                List<WallbasePicture> result = new List<WallbasePicture>();

                string response = await MakeRequest(WALLBASE_SEARCH_PAGE + query, WALLBASE_SEARCH_PAGE);
                
                Regex regexLast = new Regex("results_count: ([0-9]+),\n");

                Match matchLast = regexLast.Match(response);

                int until = Int32.Parse(matchLast.Groups[1].ToString());
                Log("Found: " + until);

                int current = 0;
                Regex regex = new Regex("<div id=\\\"thumb([0-9]+)\\\" class=\\\"thumbnail purity-([0-9]+)\\\"");
                string url;

	            MainWindow mw = null;
				Application.Current.Dispatcher.Invoke(delegate
					{
						mw = Application.Current.MainWindow as MainWindow;
					});
                until = until > limit ? limit : until;

	            if (mw != null)
	            {
					mw.Dispatcher.Invoke(delegate
						{
							mw.progressBar.Maximum = until;
							mw.progressBar.Value = 0;
							mw.Title = "WallbaseDM - Collecting";		
						});
	            }
	            while (current < until)
                {
                    if(current >= limit)
                        break;
                    
                    url = WALLBASE_SEARCH_PAGE + "/" + current + query;
                    response = await MakeRequest(url);

                    if (response != null)
                    {
                        MatchCollection matches = regex.Matches(response);

                        foreach (Match match in matches)
                        {
                            if (current >= limit)
                                break;

                            int purity = Int32.Parse(match.Groups[2].ToString());
                            result.Add(new WallbasePicture(match.Groups[1].ToString(), url, (Purity) purity));
                            current++;
                        }
						mw.Dispatcher.Invoke(delegate
							{
								mw.progressBar.Value = current;		
							});
                        if(matches.Count == 0)
                            break;
                    }
                }
                return result;
            }
            catch (Exception)
            {
                return new List<WallbasePicture>();
            }
        }

        private async Task<string> MakeRequest(string url, string referer = null, byte[] postData = null, int delay = 1000)
        {
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    HttpWebRequest request = WebRequest.CreateHttp(url);
                    request.UserAgent = USER_AGENT;
                    request.Referer = referer ?? WALLBASE_INDEX_URL;
                    request.CookieContainer = _cookies;

                    if (postData != null)
                    {
                        request.Method = "POST";
                        request.ContentLength = postData.Length;
                        request.ContentType = "application/x-www-form-urlencoded";

                        using (Stream reqStream = request.GetRequestStream())
                        {
                            reqStream.Write(postData, 0, postData.Length);
                        }
                    }

                    string result;

	                long current = DateTime.Now.Ticks - lastRequest;

					if (current < delay*1000)
					{
						Thread.Sleep(delay);
					}

                    HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;
	                
                    using (Stream stream = response.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream);
                        result = reader.ReadToEnd();
                        reader.Close();
                    }

					lastRequest = DateTime.Now.Ticks;

                    _BugFix_CookieDomain(_cookies);

                    return result;
                }
                catch (Exception e)
                {
					Thread.Sleep(2000);
					retries--;
                }
            }
            Log("Request Aborted!");
            return string.Empty;
        }

        public async Task<bool> Authenticate(string login, string pass)
        {
            try
            {
				string result = await MakeRequest(WALLBASE_LOGIN_URL);
                
                Regex regex = new Regex("<input type=\"hidden\" name=\"csrf\" value=\"(.+)\"");
                Regex regex2 = new Regex("<input type=\"hidden\" name=\"ref\" value=\"(.+)\"");
                Match matchCsrf = regex.Match(result);
                Match matchRef = regex2.Match(result);
                
                string param = "csrf="+matchCsrf.Groups[1];
                param += "&ref=" + matchRef.Groups[1];
                param += "&username="+login;
                param += "&password="+pass;

                byte[] buffer = Encoding.ASCII.GetBytes(param);

                result = await MakeRequest(WALLBASE_AUTH_URL, WALLBASE_LOGIN_URL, buffer);

                regex = new Regex("<div class=\"homessage.*\">(.*)</div>\n");
                Match resultMatch = regex.Match(result);
                
                Log(resultMatch.Groups[1].ToString());

                return true;
            }
            catch (Exception)
            {
	            MessageBox.Show("Invalid login/password or connection error.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void _BugFix_CookieDomain(CookieContainer cookieContainer)
        {
            var table = (System.Collections.Hashtable)cookieContainer.GetType().InvokeMember("m_domainTable",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.GetField |
                System.Reflection.BindingFlags.Instance,
                null,
                cookieContainer,
                new object[] { }
            );
            var keys = new System.Collections.ArrayList(table.Keys);
            foreach (string keyObj in keys)
            {
                string key = keyObj;
                if (key[0] == '.')
                {
                    string newKey = key.Remove(0, 1);
                    table[newKey] = table[keyObj];
                }
            }
        }

	    private void Log(string msg)
	    {
		    MainWindow mw = null;
		    Application.Current.Dispatcher.Invoke(delegate
			    {
				    mw = Application.Current.MainWindow as MainWindow;
			    });
		    if (mw != null)
			    mw.Dispatcher.Invoke(delegate
				    {
					    mw.Log(msg);
				    });
	    }
    }
}

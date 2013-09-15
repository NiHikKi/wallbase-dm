﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace WallbaseDM
{
    class Wallbase
    {
        private const string WALLBASE_LOGIN_URL = "http://wallbase.cc/user/login";
        private const string WALLBASE_AUTH_URL = "http://wallbase.cc/user/do_login";
        private const string WALLBASE_INDEX_URL = "http://wallbase.cc/";
        private const string WALLBASE_SEARCH_PAGE = "http://wallbase.cc/search";
        private const string WALLBASE_WALLPAPER_URL = "http://wallbase.cc/wallpaper/";
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/29.0.1547.66 Safari/537.36";

        private static Wallbase _instance;

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
        private WebClient _client = new WebClient();

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

        public async Task<bool> DownloadWallpapers(string queryString, string destination, bool byPurity = false, int limit = Int32.MaxValue)
        {
            List<WallbasePicture> wallpaperList = await GetWallpaperList(queryString, limit);

            Log("Count: " + wallpaperList.Count);

            Regex regex = new Regex("<img src=\\\"(http://wallpapers.wallbase.cc/(.*))\\\" class=\\\"wall");

            foreach (var wallbasePicture in wallpaperList)
            {
                string response = await MakeRequest(WALLBASE_WALLPAPER_URL + wallbasePicture.Name, wallbasePicture.Referer);

                Match match = regex.Match(response);

                DownloadImage(match.Groups[1].ToString(), destination + "/" + wallbasePicture.Name + ".jpg");

                Log(match.Groups[1].ToString());
            }

            return true;
        }

        private async void DownloadImage(string url, string destination)
        {
            try
            {
                HttpWebRequest request = HttpWebRequest.CreateHttp(url);
                request.UserAgent = USER_AGENT;
                
                HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;

                if ((response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect ||
                     response.StatusCode == HttpStatusCode.Moved) &&
                    response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (Stream file = File.OpenWrite(destination))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;
                            do
                            {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                await file.WriteAsync(buffer, 0, bytesRead);
                            } while (bytesRead != 0);
                        }
                    }
                }
            }
            catch (Exception)
            {
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

                while (current <= until && current < limit)
                {
                    url = WALLBASE_SEARCH_PAGE + "/" + current + query;
                    response = await MakeRequest(url);

                    MatchCollection matches = regex.Matches(response);

                    foreach (Match match in matches)
                    {
                        if (current >= limit)
                            break;

                            int purity = Int32.Parse(match.Groups[2].ToString());
                            result.Add(new WallbasePicture(match.Groups[1].ToString(), url, (Purity) purity));
                            current++;
                    }
                    Log(current + " from " + until);
                }

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        } 

        private async Task<string> MakeRequest(string url, string referer = null, byte[] postData = null)
        {
            try
            {
                HttpWebRequest request = HttpWebRequest.CreateHttp(url);
                request.UserAgent = USER_AGENT;
                request.Referer = referer != null ? referer : WALLBASE_INDEX_URL;
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

                HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;
                using (Stream stream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    result = reader.ReadToEnd();
                    reader.Close();
                }

                _BugFix_CookieDomain(_cookies);

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        public async Task<bool> Authenticate(string login, string pass)
        {
            try
            {
                ServicePointManager.Expect100Continue = false;
                
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
            ((MainWindow) Application.Current.MainWindow).Log(msg);
        }
    }
}
//-----------------------------------------------------------------------
// <created file="Account.cs">
//     Mail.ru cloud client created in 2016.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

namespace MailRuCloudApi
{
    using System;
    using System.Net;
    using System.Net.Configuration;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>
    /// MAIL.RU account info.
    /// </summary>
    public class Account
    {
        /// <summary>
        /// Default cookies.
        /// </summary>
        private CookieContainer cookies = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Account" /> class.
        /// </summary>
        /// <param name="login">Login name as email.</param>
        /// <param name="password">Password related with this login</param>
        public Account(string login, string password)
        {
            this.LoginName = login;
            this.Password = password;
        }

        /// <summary>
        /// Gets or sets connection proxy.
        /// </summary>
        /// <value>Proxy settings.</value>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets authorization token.
        /// </summary>
        /// <value>Access token.</value>
        public string AuthToken { get; private set; }

        /// <summary>
        /// Gets account cookies.
        /// </summary>
        /// <value>Account cookies.</value>
        public CookieContainer Cookies
        {
            get
            {
                return this.cookies ?? (this.cookies = new CookieContainer());
            }

            private set
            {
                this.cookies = value ?? new CookieContainer();
            }
        }

        /// <summary>
        /// Gets or sets login name.
        /// </summary>
        /// <value>Account email.</value>
        public string LoginName { get; set; }

        /// <summary>
        /// Gets or sets email password.
        /// </summary>
        /// <value>Password related with login.</value>
        public string Password { get; set; }

        /// <summary>
        /// Authorize on MAIL.RU server.
        /// </summary>
        /// <returns>True or false result operation.</returns>
        public bool Login()
        {
            return this.LoginAsync().Result;
        }

        /// <summary>
        /// Async call to authorize on MAIL.RU server.
        /// </summary>
        /// <returns>True or false result operation.</returns>
        public async Task<bool> LoginAsync()
        {
            if (string.IsNullOrEmpty(this.LoginName))
            {
                throw new ArgumentException("LoginName is null or empty.");
            }

            if (string.IsNullOrEmpty(this.Password))
            {
                throw new ArgumentException("Password is null or empty.");
            }

            if (new DefaultProxySection().Enabled)
            {
                this.Proxy = WebProxy.GetDefaultProxy();
            }

            string reqString = string.Format("Login={0}&Domain={1}&Password={2}", this.LoginName, ConstSettings.Domain, HttpUtility.UrlEncode(this.Password));
            byte[] requestData = Encoding.UTF8.GetBytes(reqString);
            var request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/cgi-bin/auth", ConstSettings.AuthDomen));
            request.Proxy = this.Proxy;
            request.CookieContainer = this.Cookies;
            request.Method = "POST";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = ConstSettings.DefaultAcceptType;
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await await task.ContinueWith(async (t) =>
             {
                 using (var s = t.Result)
                 {
                     s.Write(requestData, 0, requestData.Length);
                     using (var response = (HttpWebResponse)request.GetResponse())
                     {
                         if (response.StatusCode != HttpStatusCode.OK)
                         {
                             throw new Exception();
                         }

                         if (this.Cookies != null && this.Cookies.Count > 0)
                         {
                             await this.EnsureSdcCookie();
                             return await this.GetAuthToken();
                         }
                         else
                         {
                             return false;
                         }
                     }
                 }
             });
        }

        /// <summary>
        /// Get disk usage for account.
        /// </summary>
        /// <returns>Returns Total/Free/Used size.</returns>
        public async Task<DiskUsage> GetDiskUsage()
        {
            this.CheckAuth();
            var uri = new Uri(string.Format("{0}/api/v2/user/space?api=2&email={1}&token={2}", ConstSettings.CloudDomain, this.LoginName, this.AuthToken));
            var request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
            request.Proxy = this.Proxy;
            request.CookieContainer = this.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var response = t.Result as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonParser.Parse(new MailRuCloud().ReadResponseAsText(response), PObject.DiskUsage) as DiskUsage;
                    }
                    else
                    {
                        throw new Exception("The disk usage statistic can't be retrieved.");
                    }
                }
            });
        }

        /// <summary>
        /// Need to add this function for all calls.
        /// </summary>
        internal void CheckAuth()
        {
            if (this.LoginName == null || this.Password == null)
            {
                throw new Exception("Login or password is empty.");
            }

            if (string.IsNullOrEmpty(this.AuthToken))
            {
                if (!this.Login())
                {
                    throw new Exception("Auth token has't been retrieved.");
                }
            }
        }

        /// <summary>
        /// Retrieve SDC cookies.
        /// </summary>
        /// <returns>Returns nothing. Just tusk.</returns>
        private async Task EnsureSdcCookie()
        {
            var request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/sdc?from={1}/home", ConstSettings.AuthDomen, ConstSettings.CloudDomain));
            request.Proxy = this.Proxy;
            request.CookieContainer = this.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = ConstSettings.DefaultAcceptType;
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
            await task.ContinueWith((t) =>
            {
                using (var response = t.Result as HttpWebResponse)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception();
                    }
                }
            });
        }

        /// <summary>
        /// Get authorization token.
        /// </summary>
        /// <returns>True or false result operation.</returns>
        private async Task<bool> GetAuthToken()
        {
            var uri = new Uri(string.Format("{0}/api/v2/tokens/csrf", ConstSettings.CloudDomain));
            var request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
            request.Proxy = this.Proxy;
            request.CookieContainer = this.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var response = t.Result as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return !string.IsNullOrEmpty(this.AuthToken = JsonParser.Parse(new MailRuCloud().ReadResponseAsText(response), PObject.Token) as string);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            });
        }
    }
}

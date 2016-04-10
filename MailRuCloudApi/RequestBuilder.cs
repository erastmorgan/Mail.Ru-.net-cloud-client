using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MailRuCloudApi
{
    internal enum RequestType
    {
        UploadFile = 0,
        DownloadFile = 1,
        CreateFile = 2,
        CreateFolder = 3,
        Rename = 4,
        Publish = 5,
        Unpublish = 6,
        Move = 7,
        Copy = 8,
        Remove = 9,
        PublishDirectLink = 10,
        Login = 11,
        SdcCookie = 12,
        Token = 13,
        ShardInfoWithToken = 14,
        ShardInfoWithAnonymously = 15
    }

    internal class RequestBuilder
    {
        public static HttpWebRequest Build(Account account, long contentLength, RequestType reqType, Dictionary<string, string> additionalHeaders)
        {
            var reqMethod = HttpMethod.Get;
            ShardInfo shard = null;
            Uri url = null;
            switch (reqType)
            {
                case RequestType.UploadFile:
                    reqMethod = HttpMethod.Post;
                    shard = ShardInfo.GetShardInfo(ShardType.Upload, account);
                    url = new Uri(string.Format("{0}?cloud_domain=2&{1}", shard.Url, account.LoginName));
                    break;
                case RequestType.DownloadFile:
                    reqMethod = HttpMethod.Get;
                    break;
                case RequestType.CreateFile:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.CreateFolder:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Rename:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Publish:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Unpublish:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Move:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Copy:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Remove:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.PublishDirectLink:
                    url = new Uri(ConstSettings.CloudApiLink + "tokens/download");
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.Login:
                    reqMethod = HttpMethod.Post;
                    break;
                case RequestType.SdcCookie:
                    reqMethod = HttpMethod.Get;
                    break;
                case RequestType.Token:
                    reqMethod = HttpMethod.Get;
                    break;
                case RequestType.ShardInfoWithToken:
                    url = new Uri(string.Format("{0}/dispatcher?token={1}", ConstSettings.CloudApiLink, account.AuthToken));
                    reqMethod = HttpMethod.Get;
                    break;
                case RequestType.ShardInfoWithAnonymously:
                    reqMethod = HttpMethod.Get;
                    url = new Uri(ConstSettings.CloudApiLink + "dispatcher?api=2");
                    break;
            }

            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = account.Proxy;
            request.CookieContainer = account.Cookies;
            request.Method = reqMethod.Method;
            if (contentLength != -1)
            {
                request.ContentLength = contentLength;
            }

            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                {
                    if (header.Key.ToLower() == "referer")
                    {
                        request.Referer = header.Value;
                        continue;
                    }

                    request.Headers.Add(header.Key, header.Value);
                }
            }

            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            return request;
        }

        public static byte[] GetRequestBody(Dictionary<string, string> attributes)
        {
            var result = string.Empty;
            foreach (var item in attributes)
            {
                result += string.Format("{0}={1}&", item.Key, item.Value);
            }

            return Encoding.UTF8.GetBytes(result.TrimEnd(new []{ '&' }));
        }
    }
}

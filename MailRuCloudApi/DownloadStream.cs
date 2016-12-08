using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MailRuCloudApi
{
    public class DownloadStream : Stream
    {
        private readonly File _file;
        private readonly ShardInfo _shard;
        private readonly Account _account;
        private readonly CancellationTokenSource _cancelToken;
        private readonly long? _start;
        private readonly long? _end;

        private RingBufferedStream _innerStream;// = new RingBufferedStream(3000000);
        //private readonly PipeStream _innerStream = new PipeStream();
        //private readonly MemoryStream _innerStream = new MemoryStream();


        public DownloadStream(File file, ShardInfo shard, Account account, CancellationTokenSource cancelToken, long? start, long? end)
        {
            _file = file;
            _shard = shard;
            _account = account;
            _cancelToken = cancelToken;
            _start = start;
            _end = end;
            Initialize();
        }

        private void Initialize()
        {
            string[] filePaths;
            if (_file.Type == FileType.MultiFile)
            {
                var fileBytes = (byte[]) GetFile(new[] {_file.FullPath}).Result;
                var multiFile = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));

                var folder = _file.FullPath.Substring(0,
                    _file.FullPath.LastIndexOf(_file.PrimaryName, StringComparison.Ordinal));
                filePaths = multiFile.Parts.OrderBy(v => v.Order).Select(x => folder + x.OriginalFileName).ToArray();
            }
            else
                filePaths = new[] {_file.FullPath};

            _innerStream = new RingBufferedStream(65536);

            var t = GetFileStream(filePaths);
        }



        private async Task<object> GetFileStream(string[] sourceFullFilePaths)
        {

            foreach (var sourceFile in sourceFullFilePaths)
            {
                var request = (HttpWebRequest) WebRequest.Create($"{_shard.Url}{Uri.EscapeDataString(sourceFile)}");
                request.Proxy = _account.Proxy;
                request.CookieContainer = _account.Cookies;
                request.Method = "GET";
                request.ContentType = ConstSettings.DefaultRequestType;
                request.Accept = ConstSettings.DefaultAcceptType;
                request.UserAgent = ConstSettings.UserAgent;
                request.AllowReadStreamBuffering = false;

                var length = _file.Size.DefaultValue;
                if (_start != null)
                {
                    var start = _start ?? 0;
                    var end = Math.Min(_end ?? long.MaxValue, length - 1);
                    length = end - start + 1;

                    request.Headers.Add("Content-Range", $"bytes {start}-{end} / {length}");
                }

                var task = Task.Factory.FromAsync(request.BeginGetResponse,
                    asyncResult => request.EndGetResponse(asyncResult), null);
                await task.ContinueWith(
                    (t, m) =>
                    {
                        var token = (CancellationToken) m;
                        {
                            try
                            {
                                ReadResponseAsByte(t.Result, token, _innerStream);
                                return _innerStream;
                            }
                            catch (Exception ex)
                            {
                                return null;
                            }
                        }
                    },
                    _cancelToken.Token, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            //_innerStream.Seek(0, SeekOrigin.Begin);
            //_innerStream.FinishedWrite = true;
            _innerStream.Flush();
            return _innerStream;
        }


        private MultiFile DeserializeMultiFileConfig(string xml)
        {
            MultiFile data;
            using (var reader = new StringReader(xml))
            {
                var serializer = new XmlSerializer(typeof (MultiFile));
                data = (MultiFile) serializer.Deserialize(reader);
            }

            return data;
        }

        private async Task<object> GetFile(string[] sourceFullFilePaths)
        {
            var memoryStream = new MemoryStream();

            foreach (var sourceFile in sourceFullFilePaths)
            {
                var request = (HttpWebRequest) WebRequest.Create($"{_shard.Url}{Uri.EscapeDataString(sourceFile.TrimStart('/'))}");
                request.Proxy = _account.Proxy;
                request.CookieContainer = _account.Cookies;
                request.Method = "GET";
                request.ContentType = ConstSettings.DefaultRequestType;
                request.Accept = ConstSettings.DefaultAcceptType;
                request.UserAgent = ConstSettings.UserAgent;
                request.AllowReadStreamBuffering = false;
                var task = Task.Factory.FromAsync(request.BeginGetResponse,
                    asyncResult => request.EndGetResponse(asyncResult), null);
                await task.ContinueWith(
                    (t, m) =>
                    {
                        var token = (CancellationToken) m;
                        {
                            try
                            {
                                ReadResponseAsByte(t.Result, token, memoryStream);
                                return memoryStream.ToArray() as object;
                            }
                            catch
                            {
                                return null;
                            }
                        }
                    },
                    _cancelToken.Token);
            }

            var result = memoryStream.ToArray() as object;

            //memoryStream.Dispose();
            //memoryStream.Close();

            return result;
        }

        public override void Close()
        {
            _innerStream.Close();
            base.Close();
        }

        private void ReadResponseAsByte(WebResponse resp, CancellationToken token, Stream outputStream = null)
        {
            using (Stream responseStream = resp.GetResponseStream())
            {
                var buffer = new byte[65536];
                int bytesRead;
                int totalRead = 0;

                while (responseStream != null && (bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();

                    if (totalRead + bytesRead > Length)
                    {
                        bytesRead = (int)(Length - totalRead);
                    }
                    totalRead += bytesRead;

                    outputStream?.Write(buffer, 0, bytesRead);
                }
            }
        }


        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            //throw new NotImplementedException();
            int readed = _innerStream.Read(buffer, offset, count);
            return readed;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead { get; } = true;
        public override bool CanSeek { get; } = true;
        public override bool CanWrite { get; } = false;

        public override long Length {
            get
            {
                if (_start != null && _end != null)
                {
                    var l = _end.Value - _start.Value;
                    return l;
                }

                return _file.Size?.DefaultValue ?? 0; 
                
            }
        }

        public override long Position { get; set; }
    }
}

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MailRuCloudApi
{
    internal class UploadStream : Stream
    {
        private readonly ShardInfo _shard;
        private readonly Account _account;
        
        private readonly CancellationTokenSource _cancelToken;


        private readonly File _file;

        public UploadStream(string destinationPath, ShardInfo shard, Account account, CancellationTokenSource cancelToken, long size)
        {
            _file = new File(destinationPath, size, FileType.SingleFile, null);

            _shard = shard;
            _account = account;
            _cancelToken = cancelToken;
            _maxFileSize = _account.Info.FileSizeLimit;
            Initialize();
        }

        private HttpWebRequest _request;
        private byte[] _endBoundaryRequest;
        private readonly long _maxFileSize;



        private void Initialize()
        {
            long allowedSize = _maxFileSize - _file.Name.BytesCount();
            if (_file.Size.DefaultValue > allowedSize)
            {
                throw new OverflowException("Not supported file size.", new Exception($"The maximum file size is {allowedSize} byte. Currently file size is {_file.Size.DefaultValue} bytes + {_file.Name.BytesCount()} bytes for filename."));
            }

            var boundary = Guid.NewGuid();

            //// Boundary request building.
            var boundaryBuilder = new StringBuilder();
            boundaryBuilder.AppendFormat("------{0}\r\n", boundary);
            boundaryBuilder.AppendFormat("Content-Disposition: form-data; name=\"file\"; filename=\"{0}\"\r\n", Uri.EscapeDataString(_file.Name));
            boundaryBuilder.AppendFormat("Content-Type: {0}\r\n\r\n", ConstSettings.GetContentType(_file.Extension));

            var endBoundaryBuilder = new StringBuilder();
            endBoundaryBuilder.AppendFormat("\r\n------{0}--\r\n", boundary);

            _endBoundaryRequest = Encoding.UTF8.GetBytes(endBoundaryBuilder.ToString());
            var boundaryRequest = Encoding.UTF8.GetBytes(boundaryBuilder.ToString());

            var url = new Uri($"{_shard.Url}?cloud_domain=2&{_account.LoginName}");
            _request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            _request.Proxy = _account.Proxy;
            _request.CookieContainer = _account.Cookies;
            _request.Method = "POST";

            _request.ContentLength = _file.Size.DefaultValue + boundaryRequest.LongLength + _endBoundaryRequest.LongLength;
            //_request.SendChunked = true;

            _request.Referer = $"{ConstSettings.CloudDomain}/home/{Uri.EscapeDataString(_file.Path)}";
            _request.Headers.Add("Origin", ConstSettings.CloudDomain);
            _request.Host = url.Host;
            _request.ContentType = $"multipart/form-data; boundary=----{boundary}";
            _request.Accept = "*/*";
            _request.UserAgent = ConstSettings.UserAgent;
            _request.AllowWriteStreamBuffering = false;


            //_request.GetRequestStream();

            _task = Task.Factory.FromAsync(_request.BeginGetRequestStream, asyncResult => _request.EndGetRequestStream(asyncResult), 
                null);

            _task =  _task.ContinueWith
                (
                            (t, m) =>
                            {
                                try
                                {
                                    var token = (CancellationToken)m;
                                    var s = t.Result;
                                    WriteBytesInStream(boundaryRequest, s, token, boundaryRequest.Length);
                                }
                                catch (Exception)
                                {
                                    return (Stream)null;
                                }
                                return t.Result;
                            },
                        _cancelToken.Token, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private Task<Stream> _task;

        private const long MaxBufferSize = 3000000;

        private readonly AutoResetEvent _canWrite = new AutoResetEvent(true);

        private long BufferSize
        {
            set
            {
                lock (_bufferSizeLocker)
                {
                    _bufferSize = value;
                    if (_bufferSize > MaxBufferSize)
                        _canWrite.Reset();
                    else _canWrite.Set();
                }
            }
            get
            {
                lock (_bufferSizeLocker)
                {
                    return _bufferSize;
                }
            }
        }

        private long _bufferSize;

        private readonly object _bufferSizeLocker = new object();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _canWrite.WaitOne();
            BufferSize += buffer.Length;

            var zbuffer = new byte[buffer.Length];
            buffer.CopyTo(zbuffer, 0);
            var zcount = count;
            _task = _task.ContinueWith(
                            (t, m) =>
                            {
                                try
                                {
                                    var token = (CancellationToken)m;
                                    var s = t.Result;
                                    WriteBytesInStream(zbuffer, s, token, zcount);
                                }
                                catch (Exception ex)
                                {
                                    return null;
                                }

                                return t.Result;
                            },
                        _cancelToken.Token, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        public override void Close()
        {


           var z =   _task.ContinueWith(
                (t, m) =>
                {
                    try
                    {
                        var token = (CancellationToken) m;
                        var s = t.Result;
                        WriteBytesInStream(_endBoundaryRequest, s, token, _endBoundaryRequest.Length);
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                    finally
                    {
                        var st = t.Result;
                        st?.Close();
                        st?.Dispose();
                    }


                    using (var response = (HttpWebResponse)_request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var resp = ReadResponseAsText(response, _cancelToken).Split(';');
                            var hashResult = resp[0];
                            var sizeResult = long.Parse(resp[1].Trim('\r', '\n', ' '));

                            _file.Hash = hashResult;
                            _file.Size.DefaultValue = sizeResult;

                            return AddFileInCloud(_file).Result;
                        }
                    }

                    return true;
                },
            _cancelToken.Token, TaskContinuationOptions.OnlyOnRanToCompletion);

            z.Wait();

            base.Close();
        }

        private enum ResolveFileConflictMethod 
        {
            Rename,
            Rewrite
        }

        private string GetConflictSolverParameter(ResolveFileConflictMethod conflict = ResolveFileConflictMethod.Rewrite)
        {
            switch (conflict)
            {
                case ResolveFileConflictMethod.Rewrite:
                    return "rewrite";
                case ResolveFileConflictMethod.Rename:
                    return "rename";
                default: throw new NotImplementedException("File conflict method not implemented");
            }
        }


        private async Task<bool> AddFileInCloud(File fileInfo, ResolveFileConflictMethod conflict = ResolveFileConflictMethod.Rewrite)
        {
            //var hasFile = fileInfo.Hash != null && fileInfo.Size.DefaultValue != 0;
            //var filePart = hasFile ? $"&hash={fileInfo.Hash}&size={fileInfo.Size.DefaultValue}" : string.Empty;
            var filePart = $"&hash={fileInfo.Hash}&size={fileInfo.Size.DefaultValue}";

            string addFileString = $"home={Uri.EscapeDataString(fileInfo.FullPath)}&conflict={GetConflictSolverParameter(conflict)}&api=2&token={_account.AuthToken}" + filePart;
            var addFileRequest = Encoding.UTF8.GetBytes(addFileString);

            //var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/{(hasFile ? "file" : "folder")}/add");
            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/file/add");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = _account.Proxy;
            request.CookieContainer = _account.Cookies;
            request.Method = "POST";
            request.ContentLength = addFileRequest.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home{Uri.EscapeDataString(fileInfo.Path)}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith(t =>
            {
                using (var s = t.Result)
                {
                    s.Write(addFileRequest, 0, addFileRequest.Length);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new Exception();
                        }

                        return true;
                    }
                }
            });
        }


        private string ReadResponseAsText(WebResponse resp, CancellationTokenSource cancelToken)
        {
            using (var stream = new MemoryStream())
            {
                try
                {
                    ReadResponseAsByte(resp, cancelToken.Token, stream);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
                catch
                {
                    //// Cancellation token.
                    return "7035ba55-7d63-4349-9f73-c454529d4b2e";
                }
            }
        }

        private void ReadResponseAsByte(WebResponse resp, CancellationToken token, Stream outputStream = null)
        {
            using (Stream responseStream = resp.GetResponseStream())
            {
                var buffer = new byte[65536];
                int bytesRead;

                while (responseStream != null && (bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    outputStream?.Write(buffer, 0, bytesRead);
                }
            }
        }


        private long WriteBytesInStream(byte[] bytes, Stream outputStream, CancellationToken token, long length, bool includeProgressEvent = false, OperationType operation = OperationType.None)
        {
            BufferSize -= bytes.Length;

            using (var stream = new MemoryStream(bytes))
            {
                using (var source = new BinaryReader(stream))
                {
                    return WriteBytesInStream(source, outputStream, token, length);
                }
            }
        }

        private long WriteBytesInStream(BinaryReader sourceStream, Stream outputStream, CancellationToken token, long length)
        {
            int bufferLength = 65536;
            var totalWritten = 0L;
            if (length < bufferLength)
            {
                var z = sourceStream.ReadBytes((int) length);
                outputStream.Write(z, 0, (int)length);
            }
            else
            {
                while (length > totalWritten)
                {
                    token.ThrowIfCancellationRequested();

                    var bytes = sourceStream.ReadBytes(bufferLength);
                    outputStream.Write(bytes, 0, bufferLength);

                    totalWritten += bufferLength;
                    if (length - totalWritten < bufferLength)
                    {
                        bufferLength = (int)(length - totalWritten);
                    }
                }

                
            }
            return totalWritten;
        }


        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            _file.Size.DefaultValue = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _file.Size.DefaultValue;
        public override long Position { get; set; }
    }
}

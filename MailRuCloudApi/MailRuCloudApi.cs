﻿//-----------------------------------------------------------------------
// <created file="MailRuCloudApi.cs">
//     Mail.ru cloud client created in 2016.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

namespace MailRuCloudApi
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml.Serialization;


    /// <summary>
    /// Cloud client.
    /// </summary>
    public class MailRuCloud
    {
        /// <summary>
        /// Async tasks cancelation token.
        /// </summary>
        private readonly CancellationTokenSource _cancelToken = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="MailRuCloud" /> class. Do not forget to set Account property before using any API functions.
        /// </summary>
        public MailRuCloud()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailRuCloud" /> class.
        /// </summary>
        /// <param name="login">Login name as the email.</param>
        /// <param name="password">Password, associated with this email.</param>
        public MailRuCloud(string login, string password)
        {
            Account = new Account(login, password);
            if (!Account.Login())
            {
                throw new Exception("Auth token has't been retrieved.");
            }
        }

        /// <summary>
        /// Changing progress event, works only for GetFileAsync and UploadFileAsync functions.
        /// </summary>
        public event ProgressChangedEventHandler ChangingProgressEvent;

        /// <summary>
        /// Gets or sets account to connect with cloud.
        /// </summary>
        /// <value>Account info.</value>
        public Account Account { get; set; }

        /// <summary>
        /// Abort all prolonged async operations.
        /// </summary>
        public void AbortAllAsyncThreads()
        {
            _cancelToken.Cancel(true);
        }

        /// <summary>
        /// Copying folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to copying.</param>
        /// <param name="destinationEntry">Destination entry on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(Folder folder, Entry destinationEntry)
        {
            return await Copy(folder, destinationEntry.FullPath);
        }

        /// <summary>
        /// Copying folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to copying.</param>
        /// <param name="destinationFolder">Destination folder on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(Folder folder, Folder destinationFolder)
        {
            return await Copy(folder, destinationFolder.FullPath);
        }

        /// <summary>
        /// Copying folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to copying.</param>
        /// <param name="destinationPath">Destination path on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(Folder folder, string destinationPath)
        {
            return !string.IsNullOrEmpty(await MoveOrCopy(folder.Name, folder.FullPath, destinationPath, false));
        }

        /// <summary>
        /// Copying file in another space on the server.
        /// </summary>
        /// <param name="file">File info to copying.</param>
        /// <param name="destinationEntry">Destination entry on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(File file, Entry destinationEntry)
        {
            return await Copy(file, destinationEntry.FullPath);
        }

        /// <summary>
        /// Copying file in another space on the server.
        /// </summary>
        /// <param name="file">File info to copying.</param>
        /// <param name="destinationFolder">Destination folder on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(File file, Folder destinationFolder)
        {
            return await Copy(file, destinationFolder.FullPath);
        }

        /// <summary>
        /// Copying file in another space on the server.
        /// </summary>
        /// <param name="file">File info to copying.</param>
        /// <param name="destinationPath">Destination path on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Copy(File file, string destinationPath)
        {
            bool result;
            if (file.Type == FileType.MultiFile)
            {
                result = await MoveOrCopyMultiFile(file, destinationPath, false);
            }
            else
            {
                result = !string.IsNullOrEmpty(await MoveOrCopy(file.Name, file.FullPath, destinationPath, false));
            }

            return result;
        }

        /// <summary>
        /// Rename folder on the server.
        /// </summary>
        /// <param name="folder">Source folder info.</param>
        /// <param name="newFileName">New folder name.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Rename(Folder folder, string newFileName)
        {
            return await Rename(folder.Name, folder.FullPath, newFileName);
        }

        /// <summary>
        /// Rename file on the server.
        /// </summary>
        /// <param name="file">Source file info.</param>
        /// <param name="newFileName">New file name.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Rename(File file, string newFileName)
        {
            bool result;
            if (file.Type == FileType.MultiFile)
            {
                file.Type = FileType.SingleFile;
                var fileBytes = await GetFile(file, false);
                var conf = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
                var sourcePath = file.FullPath.Substring(0, file.FullPath.LastIndexOf("/", StringComparison.Ordinal) + 1);

                foreach (var item in conf.Parts)
                {
                    var newPartName = item.OriginalFileName.Replace(file.Name, newFileName);
                    result = await Rename(item.OriginalFileName, sourcePath + item.OriginalFileName, newPartName);
                }

                conf.Parts.ToList().ForEach(x => x.OriginalFileName = x.OriginalFileName.Replace(file.Name, newFileName));
                var remove = Remove(file);
                if (result = await remove)
                {
                    var newConfName = file.PrimaryName.Replace(file.Name, newFileName);
                    conf.OriginalFileName = newFileName;
                    var tempFile = Path.GetTempFileName();
                    System.IO.File.WriteAllText(tempFile, GenerateMultiFileConfig(conf));
                    result = await UploadFile(newConfName, tempFile, string.Empty, 0, new FileInfo(tempFile).Length, sourcePath, false);
                    if (System.IO.File.Exists(tempFile))
                    {
                        try
                        {
                            System.IO.File.Delete(tempFile);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            else
            {
                result = await Rename(file.Name, file.FullPath, newFileName);
            }

            return result;
        }

        /// <summary>
        /// Move folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to moving.</param>
        /// <param name="destinationFolder">Destination folder on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(Folder folder, Folder destinationFolder)
        {
            return await Move(folder, destinationFolder.FullPath);
        }

        /// <summary>
        /// Move folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to moving.</param>
        /// <param name="destinationEntry">Destination entry on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(Folder folder, Entry destinationEntry)
        {
            return await Move(folder, destinationEntry.FullPath);
        }

        /// <summary>
        /// Move folder in another space on the server.
        /// </summary>
        /// <param name="folder">Folder info to move.</param>
        /// <param name="destinationPath">Destination path on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(Folder folder, string destinationPath)
        {
            return !string.IsNullOrEmpty(await MoveOrCopy(folder.Name, folder.FullPath, destinationPath, true));
        }

        /// <summary>
        /// Move file in another space on the server.
        /// </summary>
        /// <param name="file">File info to move.</param>
        /// <param name="destinationEntry">Destination entry on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(File file, Entry destinationEntry)
        {
            return await Move(file, destinationEntry.FullPath);
        }

        /// <summary>
        /// Move file in another space on the server.
        /// </summary>
        /// <param name="file">File info to move.</param>
        /// <param name="destinationFolder">Destination folder on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(File file, Folder destinationFolder)
        {
            return await Move(file, destinationFolder.FullPath);
        }

        /// <summary>
        /// Move file in another space on the server.
        /// </summary>
        /// <param name="file">File info to move.</param>
        /// <param name="destinationPath">Destination path on the server.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Move(File file, string destinationPath)
        {
            bool result;
            if (file.Type == FileType.MultiFile)
            {
                result = await MoveOrCopyMultiFile(file, destinationPath, true);
            }
            else
            {
                result = !string.IsNullOrEmpty(await MoveOrCopy(file.Name, file.FullPath, destinationPath, true));
            }

            return result;
        }

        /// <summary>
        /// Create folder on the server.
        /// </summary>
        /// <param name="name">New path name.</param>
        /// <param name="createIn">Destination path.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> CreateFolder(string name, string createIn)
        {
            return
                await
                    AddFileInCloud(new File(createIn.EndsWith("/") ? createIn + name : createIn + "/" + name, 0,
                        FileType.SingleFile, null));
            //{
            //    //Name = name,
            //    FullPath = createIn.EndsWith("/") ? createIn + name : createIn + "/" + name,
            //    Hash = null,
            //    Size = new FileSize
            //    {
            //        DefaultValue = 0
            //    }
            //});
        }

        /// <summary>
        /// Remove the file on server.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Remove(File file)
        {
            bool result;
            if (file.Type == FileType.MultiFile)
            {
                file.Type = FileType.SingleFile;
                var fileBytes = await GetFile(file, false);
                var conf = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
                var sourcePath = file.FullPath.Substring(0, file.FullPath.LastIndexOf("/", StringComparison.Ordinal) + 1);
                foreach (var item in conf.Parts)
                {
                    result = await Remove(sourcePath + item.OriginalFileName);
                }

                result = await Remove(file.FullPath);
            }
            else
            {
                result = await Remove(file.FullPath);
            }

            return result;
        }

        /// <summary>
        /// Remove the folder on server.
        /// </summary>
        /// <param name="folder">Folder info.</param>
        /// <returns>True or false operation result.</returns>
        public async Task<bool> Remove(Folder folder)
        {
            return await Remove(folder.FullPath);
        }

        /// <summary>
        /// Get direct link by public file URL. Direct link works only for one session.
        /// </summary>
        /// <param name="publishLink">Public file link.</param>
        /// <param name="fileType">File type.</param>
        /// <returns>Direct link.</returns>
        public async Task<string> GetPublishDirectLink(string publishLink, FileType fileType)
        {
            if (fileType == FileType.MultiFile)
            {
                return string.Empty;
            }

            CookieContainer cookie = null;
            var shard = await GetShardInfo(ShardType.WeblinkGet, true, cookie);
            var addFileRequest = Encoding.UTF8.GetBytes($"api={2}");

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/tokens/download");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = cookie;
            request.Method = "POST";
            request.ContentLength = addFileRequest.LongLength;
            request.Referer = publishLink;
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
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

                        var token = (string)JsonParser.Parse(ReadResponseAsText(response), PObject.Token);
                        return $"{shard.Url}/{publishLink.Replace(ConstSettings.PublishFileLink, string.Empty)}?key={token}";
                    }
                }
            });
        }

        /// <summary>
        /// Remove the file from public access.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <returns>True or false result of the operation.</returns>
        public async Task<bool> UnpublishLink(File file)
        {
            if (file.Type == FileType.MultiFile)
            {
                return false;
            }

            return (await PublishUnpulishLink(file.Name, file.FullPath, false, file.PublicLink)).ToUpper() == file.FullPath.ToUpper();
        }

        /// <summary>
        /// Remove the folder from public access.
        /// </summary>
        /// <param name="folder">Folder info.</param>
        /// <returns>True or false result of the operation.</returns>
        public async Task<bool> UnpublishLink(Folder folder)
        {
            return (await PublishUnpulishLink(folder.Name, folder.FullPath, false, folder.PublicLink)).ToUpper() == folder.FullPath.ToUpper();
        }

        /// <summary>
        /// Get public access to the file.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <returns>Public file link.</returns>
        public async Task<string> GetPublishLink(File file)
        {
            if (file.Type == FileType.MultiFile)
            {
                return string.Empty;
            }

            return await PublishUnpulishLink(file.Name, file.FullPath, true, null);
        }

        /// <summary>
        /// Get public access to the folder.
        /// </summary>
        /// <param name="folder">Folder info.</param>
        /// <returns>Public folder link.</returns>
        public async Task<string> GetPublishLink(Folder folder)
        {
            return await PublishUnpulishLink(folder.Name, folder.FullPath, true, null);
        }

        /// <summary>
        /// Get list of files and folders from account.
        /// </summary>
        /// <param name="folder">Folder info.</param>
        /// <returns>List of the items.</returns>
        public async Task<Entry> GetItems(Folder folder)
        {
            return await GetItems(folder.FullPath);
        }

        /// <summary>
        /// Get list of files and folders from account.
        /// </summary>
        /// <param name="path">Path in the cloud to return the list of the items.</param>
        /// <returns>List of the items.</returns>
        public async Task<Entry> GetItems(string path)
        {
            CheckAuth();
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            var uri = new Uri(
                $"{ConstSettings.CloudDomain}/api/v2/folder?token={Account.AuthToken}&home={HttpUtility.UrlEncode(path)}");
            var request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
            Entry entry = null;
            var result = await task.ContinueWith((t) =>
            {
                using (var response = t.Result as HttpWebResponse)
                {
                    if (response != null && response.StatusCode == HttpStatusCode.OK)
                    {
                        entry = (Entry)JsonParser.Parse(ReadResponseAsText(response), PObject.Entry);
                        return true;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            });

            if (result)
            {
                var pattern = @"^.+-[{(]?[0-9a-f]{8}[-]?([0-9a-f]{4}[-]?){3}[0-9a-f]{12}[)}]?-\.Multifile-Parts-Config";
                var multiFileConfigs = entry.Files.Where(x => !string.IsNullOrEmpty(Regex.Match(x.Name, pattern).Value)).ToList();
                var tempFiles = new List<File>();
                multiFileConfigs.ForEach(x => x.Size = new FileSize()
                {
                    DefaultValue = 0
                });

                var multiFileParts = new List<MultiFilePart>();
                foreach (var file in multiFileConfigs)
                {
                    var fileBytes = await GetFile(file);
                    var conf = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));

                    tempFiles.Add(new File(file.FullPath, conf.Size, FileType.MultiFile, null));
                    //{
                    //    //Name = conf.OriginalFileName,
                    //    Size = new FileSize()
                    //    {
                    //        DefaultValue = conf.Size
                    //    },
                    //    FullPath = file.FullPath,
                    //    Type = FileType.MultiFile,
                    //    PrimaryName = file.PrimaryName
                    //});

                    multiFileParts.AddRange(conf.Parts);
                }

                if (multiFileConfigs.Count > 0)
                {
                    tempFiles.AddRange(entry.Files.Where(v => multiFileParts.FirstOrDefault(x => x.OriginalFileName == v.Name) == null && multiFileConfigs.FirstOrDefault(m => m.Name == v.Name) == null));
                    entry.NumberOfFiles = tempFiles.Count;
                    entry.Files = tempFiles;
                }
            }

            return entry;
        }

        /// <summary>
        /// Download file asynchronously, if not use async await will be use synchronously operation.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <param name="destinationPath">Destination path on the file system.</param>
        /// <param name="includeProgressEvent">Include changing progress event.</param>
        /// <returns>True or false result of the operation.</returns>
        public async Task<bool> GetFile(File file, string destinationPath, bool includeProgressEvent = true)
        {
            MultiFile multiFile = null;
            if (file.Type == FileType.MultiFile)
            {
                var fileBytes = (byte[])(await GetFile(new[] { file.FullPath }, null, null));
                multiFile = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
            }

            var taskAction = new object[] { file, destinationPath, multiFile };
            return await Task.Factory.StartNew(
                (action) =>
            {
                var param = action as object[];
                var fileInfo = param[0] as File;
                var filePaths = new[] { fileInfo.FullPath };

                if (fileInfo.Type == FileType.MultiFile)
                {
                    var folder = fileInfo.FullPath.Substring(0, fileInfo.FullPath.LastIndexOf(fileInfo.PrimaryName, StringComparison.Ordinal));
                    filePaths = (param[2] as MultiFile).Parts.OrderBy(v => v.Order).Select(x => folder + x.OriginalFileName).ToArray();
                }

                return (bool)GetFile(filePaths, fileInfo.Name, param[1] as string, fileInfo.Size.DefaultValue).Result;
            },
            taskAction);
        }

        /// <summary>
        /// Download file asynchronously, if not use async await will be use synchronously operation.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <param name="includeProgressEvent">Include changing progress event.</param>
        /// <returns>File as byte array.</returns>
        public async Task<byte[]> GetFile(File file, bool includeProgressEvent = true)
        {
            MultiFile multiFile = null;
            if (file.Type == FileType.MultiFile)
            {
                var fileBytes = (byte[])await GetFile(new[] { file.FullPath }, null, null);
                multiFile = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
            }

            var taskAction = new object[] { file, multiFile };
            return await Task.Factory.StartNew(
                (action) =>
            {
                var param = action as object[];
                var fileInfo = param[0] as File;
                var filePaths = new[] { fileInfo.FullPath };

                if (fileInfo.Type == FileType.MultiFile)
                {
                    var folder = fileInfo.FullPath.Substring(0, fileInfo.FullPath.LastIndexOf(fileInfo.PrimaryName, StringComparison.Ordinal));
                    filePaths = (param[1] as MultiFile).Parts.OrderBy(v => v.Order).Select(x => folder + x.OriginalFileName).ToArray();
                }

                return (byte[])GetFile(filePaths, null, null, includeProgressEvent ? fileInfo.Size.DefaultValue : 0).Result;
            },
            taskAction);
        }

        

        public async Task<Stream> GetFileStream(File file, bool includeProgressEvent = true)
        {
            CheckAuth();
            CookieContainer cookie = Account.Cookies;
            var shard = await GetShardInfo(ShardType.Get, true, cookie);
            Stream stream = new DownloadStream(file, shard, Account, _cancelToken);
            return stream;


            //MultiFile multiFile = null;
            //if (file.Type == FileType.MultiFile)
            //{
            //    var fileBytes = (byte[])(await this.GetFile(new[] { file.FullPath }, null, null, 0));
            //    multiFile = this.DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
            //}

            //var taskAction = new object[] { file, multiFile };
            //return await Task.Factory.StartNew(
            //    (action) =>
            //    {
            //        var param = action as object[];
            //        var fileInfo = param[0] as File;
            //        var filePaths = new string[] { fileInfo.FullPath };

            //        if (fileInfo.Type == FileType.MultiFile)
            //        {
            //            var folder = fileInfo.FullPath.Substring(0, fileInfo.FullPath.LastIndexOf(fileInfo.PrimaryName));
            //            filePaths = (param[1] as MultiFile).Parts.OrderBy(v => v.Order).Select(x => folder + x.OriginalFileName).ToArray();
            //        }

            //        return this.GetFileStream(filePaths, includeProgressEvent ? fileInfo.Size.DefaultValue : 0).Result as Stream;
            //    },
            //taskAction);
        }


        public Stream GetUploadStream(string fileName, string destinationPath, string extension, long size)
        {
            CheckAuth();
            var shard = GetShardInfo(ShardType.Upload).Result;

            var res = new UploadStream(fileName, destinationPath, shard, Account, _cancelToken, size);

            return res;
        }

        /// <summary>
        /// Upload file on the server asynchronously, if not use async await will be use synchronously operation.
        /// </summary>
        /// <param name="file">File info.</param>
        /// <param name="destinationPath">Destination path on the server.</param>
        /// <returns>True or false result of the operation.</returns>
        public async Task<bool> UploadFile(FileInfo file, string destinationPath)
        {
            var maxFileSize = 2L * 1000L * 1000L * 1000L;
            if (maxFileSize >= file.Length)
            {
                return await UploadFile(file.Name, file.FullName, file.Extension, 0, file.Length, destinationPath, true);
            }

            var diffLength = maxFileSize;
            bool result;
            var curPosition = 0L;
            var guid = Guid.NewGuid().ToString();
            var partCount = 1;
            var multiFileParts = new List<MultiFilePart>();
            while (diffLength == maxFileSize)
            {
                if (file.Length - curPosition < maxFileSize)
                {
                    diffLength = file.Length - curPosition;
                }

                var partName = $"{file.Name}-{guid}-.Multifile-Part{partCount}";
                if (!(result = await UploadFile(partName, file.FullName, string.Empty, curPosition, diffLength, destinationPath, true)))
                {
                    return result;
                }

                multiFileParts.Add(new MultiFilePart()
                {
                    OriginalFileName = partName,
                    Size = diffLength,
                    Order = partCount
                });

                curPosition += diffLength;
                partCount++;
            }

            var multiFileConf = GenerateMultiFileConfig(new MultiFile()
            {
                OriginalFileName = file.Name,
                Size = file.Length,
                Parts = multiFileParts.ToArray()
            });

            var tempFile = Path.GetTempFileName();
            System.IO.File.WriteAllText(tempFile, multiFileConf);
            result = await UploadFile($"{file.Name}-{guid}-.Multifile-Parts-Config", tempFile, string.Empty, 0, new FileInfo(tempFile).Length, destinationPath, false);
            if (System.IO.File.Exists(tempFile))
            {
                try
                {
                    System.IO.File.Delete(tempFile);
                }
                catch
                {
                    // ignored
                }
            }

            return result;
        }

        /// <summary>
        /// Read web response as text.
        /// </summary>
        /// <param name="resp">Web response.</param>
        /// <returns>Converted text.</returns>
        internal string ReadResponseAsText(WebResponse resp)
        {
            using (var stream = new MemoryStream())
            {
                try
                {
                    ReadResponseAsByte(resp, _cancelToken.Token, stream);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
                catch
                {
                    //// Cancellation token.
                    return "7035ba55-7d63-4349-9f73-c454529d4b2e";
                }
            }
        }

        /// <summary>
        /// Read web response as byte array.
        /// </summary>
        /// <param name="resp">Web response.</param>
        /// <param name="token">Async task cancellation token.</param>
        /// <param name="outputStream">Output stream to writing the response.</param>
        /// <param name="contentLength">Length of the stream.</param>
        /// <param name="operation">Currently operation type.</param>
        internal void ReadResponseAsByte(WebResponse resp, CancellationToken token, Stream outputStream = null, long contentLength = 0, OperationType operation = OperationType.None)
        {
            int bufSizeChunk = 30000;
            int totalBufSize = bufSizeChunk;
            byte[] fileBytes = new byte[totalBufSize];
            double percentComplete = 0;

            int totalBytesRead = 0;

            using (var reader = new BinaryReader(resp.GetResponseStream()))
            {
                int bytesRead;
                while ((bytesRead = reader.Read(fileBytes, totalBytesRead, totalBufSize - totalBytesRead)) > 0)
                {
                    token.ThrowIfCancellationRequested();

                    outputStream?.Write(fileBytes, totalBytesRead, bytesRead);

                    totalBytesRead += bytesRead;

                    if ((totalBufSize - totalBytesRead) == 0)
                    {
                        totalBufSize += bufSizeChunk;
                        Array.Resize(ref fileBytes, totalBufSize);
                    }

                    if (contentLength != 0 && contentLength >= outputStream.Position)
                    {
                        var tempPercentComplete = 100.0 * (double)outputStream.Position / (double)contentLength;
                        if (tempPercentComplete - percentComplete >= 1)
                        {
                            percentComplete = tempPercentComplete;
                            OnChangedProgressPercent(new ProgressChangedEventArgs(
                                (int)percentComplete,
                                new ProgressChangeTaskState()
                                {
                                    Type = operation,
                                    TotalBytes = new FileSize()
                                    {
                                        DefaultValue = contentLength
                                    },
                                    BytesInProgress = new FileSize()
                                    {
                                        DefaultValue = outputStream.Position
                                    }
                                }));
                        }
                    }
                }

                if (contentLength != 0 && outputStream.Position == contentLength)
                {
                    OnChangedProgressPercent(new ProgressChangedEventArgs(
                                100,
                                new ProgressChangeTaskState()
                                {
                                    Type = operation,
                                    TotalBytes = new FileSize()
                                    {
                                        DefaultValue = contentLength
                                    },
                                    BytesInProgress = new FileSize()
                                    {
                                        DefaultValue = outputStream.Position
                                    }
                                }));
                }
            }
        }

        /// <summary>
        /// Function to set data for ChangingProgressEvent.
        /// </summary>
        /// <param name="e">Progress changed argument. User state of this argument may return <see cref="ProgressChangeTaskState"/> object.</param>
        protected virtual void OnChangedProgressPercent(ProgressChangedEventArgs e)
        {
            ChangingProgressEvent?.Invoke(this, e);
        }

        /// <summary>
        /// Upload file on the server asynchronously, if not use async await will be use synchronously operation.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <param name="fullFilePath">Full file path on the file system.</param>
        /// <param name="extension">File extension.</param>
        /// <param name="startPosition">Start stream position to writing. Stream will create from file info.</param>
        /// <param name="size">Bytes count to write from source stream in another.</param>
        /// <param name="destinationPath">Destination file path on the server.</param>
        /// <param name="includeProgressEvent">On or off progress event for operation.</param>
        /// <returns>True or false result operation.</returns>
        private async Task<bool> UploadFile(string fileName, string fullFilePath, string extension, long startPosition, long size, string destinationPath, bool includeProgressEvent)
        {
            destinationPath = destinationPath.EndsWith("/") ? destinationPath : destinationPath + "/";
            var maxFileSize = 2L * 1024L * 1024L * 1024L;
            if (size > maxFileSize)
            {
                throw new OverflowException("Not supported file size.", new Exception(
                    $"The maximum file size is {maxFileSize} byte. Currently file size is {size} byte."));
            }

            CheckAuth();
            var shard = await GetShardInfo(ShardType.Upload);
            var boundary = Guid.NewGuid();

            //// Boundary request building.
            var boundaryBuilder = new StringBuilder();
            boundaryBuilder.AppendFormat("------{0}\r\n", boundary);
            boundaryBuilder.AppendFormat("Content-Disposition: form-data; name=\"file\"; filename=\"{0}\"\r\n", fileName);
            boundaryBuilder.AppendFormat("Content-Type: {0}\r\n\r\n", ConstSettings.GetContentType(extension));

            var endBoundaryBuilder = new StringBuilder();
            endBoundaryBuilder.AppendFormat("\r\n------{0}--\r\n", boundary);

            var endBoundaryRequest = Encoding.UTF8.GetBytes(endBoundaryBuilder.ToString());
            var boundaryRequest = Encoding.UTF8.GetBytes(boundaryBuilder.ToString());

            var url = new Uri($"{shard.Url}?cloud_domain=2&{Account.LoginName}");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = size + boundaryRequest.LongLength + endBoundaryRequest.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home{HttpUtility.UrlEncode(destinationPath)}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = $"multipart/form-data; boundary=----{boundary}";
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            request.AllowWriteStreamBuffering = false;

            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith(
                (t, m) =>
                {
                    try
                    {
                        var token = (CancellationToken)m;
                        using (var s = t.Result)
                        {
                            WriteBytesInStream(boundaryRequest, s, token);
                            WriteBytesInStream(fullFilePath, startPosition, size, s, token, includeProgressEvent, OperationType.Upload);
                            WriteBytesInStream(endBoundaryRequest, s, token);

                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    var resp = ReadResponseAsText(response).Split(';');
                                    var hashResult = resp[0];
                                    var sizeResult = long.Parse(resp[1].Replace("\r\n", string.Empty));


                                    var f = new File(HttpUtility.UrlDecode(destinationPath) + fileName, sizeResult, FileType.SingleFile, hashResult);
                                    return AddFileInCloud(f).Result;
                                    //return this.AddFileInCloud(new File()
                                    //{
                                    //    //Name = fileName,
                                    //    FullPath = HttpUtility.UrlDecode(destinationPath) + fileName,
                                    //    Hash = hashResult,
                                    //    Size = new FileSize()
                                    //    {
                                    //        DefaultValue = sizeResult
                                    //    }
                                    //}).Result;
                                }
                            }
                        }
                    }
                    catch
                    {
                        return false;
                    }
                    finally
                    {
                        t.Result?.Dispose();
                    }

                    return true;
                },
            _cancelToken.Token);
        }

        /// <summary>
        /// Download file asynchronously, if not use async await will be use synchronously operation.
        /// </summary>
        /// <param name="sourceFullFilePaths">Full file paths on the server.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="destinationPath">Destination full file path on the file system.</param>
        /// <param name="contentLength">File length.</param>
        /// <returns>File as byte array.</returns>
        private async Task<object> GetFile(string[] sourceFullFilePaths, string fileName, string destinationPath, long contentLength = 0)
        {
            CheckAuth();
            var shard = await GetShardInfo(ShardType.Get);
            destinationPath = destinationPath == null || destinationPath.EndsWith(@"\") ? destinationPath : destinationPath + @"\";
            FileStream fileStream = null;
            MemoryStream memoryStream = null;
            if (destinationPath != null && fileName != null)
            {
                fileStream = System.IO.File.Create(destinationPath + fileName);
            }
            else
            {
                memoryStream = new MemoryStream();
            }

            foreach (var sourceFile in sourceFullFilePaths)
            {
                var request = (HttpWebRequest)WebRequest.Create($"{shard.Url}{sourceFile.TrimStart('/')}");
                request.Proxy = Account.Proxy;
                request.CookieContainer = Account.Cookies;
                request.Method = "GET";
                request.ContentType = ConstSettings.DefaultRequestType;
                request.Accept = ConstSettings.DefaultAcceptType;
                request.UserAgent = ConstSettings.UserAgent;
                request.AllowReadStreamBuffering = false;
                var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
                await task.ContinueWith(
                    (t, m) =>
                {
                    var token = (CancellationToken)m;
                    if (destinationPath != null && fileName != null)
                    {
                        try
                        {
                            ReadResponseAsByte(t.Result, token, fileStream, contentLength, OperationType.Download);
                            return fileStream.Length > 0 as object;
                        }
                        catch
                        {
                            if (System.IO.File.Exists(destinationPath + fileName))
                            {
                                try
                                {
                                    System.IO.File.Delete(destinationPath + fileName);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }

                            return false as object;
                        }
                    }

                    try
                    {
                        ReadResponseAsByte(t.Result, token, memoryStream, contentLength, OperationType.Download);
                        return memoryStream.ToArray() as object;
                    }
                    catch
                    {
                        return null;
                    }
                },
                _cancelToken.Token);
            }

            var result = destinationPath != null && fileName != null ? fileStream.Length > 0 : memoryStream.ToArray() as object;
            if (fileStream != null)
            {
                fileStream.Dispose();
                fileStream.Close();
            }

            if (memoryStream != null)
            {
                memoryStream.Dispose();
                memoryStream.Close();
            }

            return result;
        }


        /// <summary>
        /// Publish or not to publish call for item in cloud.
        /// </summary>
        /// <param name="name">Folder or file name.</param>
        /// <param name="fullPath">Full file or folder name.</param>
        /// <param name="publish">Publish or not to publish operation.</param>
        /// <param name="publishLink">Public item link if publish operation is used.</param>
        /// <returns>Public link or public item id.</returns>
        private async Task<string> PublishUnpulishLink(string name, string fullPath, bool publish, string publishLink)
        {
            var addFileRequest = Encoding.UTF8.GetBytes(
                string.Format(
                "{5}={0}&api={1}&token={2}&email={3}&x-email={4}",
                publish ? fullPath : publishLink.Replace(ConstSettings.PublishFileLink, string.Empty),
                2,
                Account.AuthToken,
                Account.LoginName,
                Account.LoginName,
                publish ? "home" : "weblink"));

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/file/{(publish ? "publish" : "unpublish")}");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = addFileRequest.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home{fullPath.Substring(0, fullPath.LastIndexOf(name, StringComparison.Ordinal))}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var s = t.Result)
                {
                    s.Write(addFileRequest, 0, addFileRequest.Length);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var publicLink = (string)JsonParser.Parse(ReadResponseAsText(response), PObject.BodyAsString);
                            if (publish)
                            {
                                return ConstSettings.PublishFileLink + publicLink;
                            }
                            else
                            {
                                return publicLink;
                            }
                        }

                        throw new HttpListenerException((int)response.StatusCode);
                    }
                }
            });
        }

        /// <summary>
        /// Rename item on server.
        /// </summary>
        /// <param name="name">File or folder name.</param>
        /// <param name="fullPath">Full path of the file or folder.</param>
        /// <param name="newName">New file or path name.</param>
        /// <returns>True or false result operation.</returns>
        private async Task<bool> Rename(string name, string fullPath, string newName)
        {
            var moveRequest = Encoding.UTF8.GetBytes(string.Format("home={0}&api={1}&token={2}&email={3}&x-email={3}&conflict=rename&name={4}", fullPath, 2, Account.AuthToken, Account.LoginName, newName));

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/file/rename");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = moveRequest.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home{fullPath.Substring(0, fullPath.LastIndexOf(name, StringComparison.Ordinal))}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var s = t.Result)
                {
                    s.Write(moveRequest, 0, moveRequest.Length);
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

        /// <summary>
        /// Move or copy item on server.
        /// </summary>
        /// <param name="sourceName">Source file or path name.</param>
        /// <param name="sourceFullPath">Full path source or file name.</param>
        /// <param name="destinationPath">Destination path to cope or move.</param>
        /// <param name="move">Move or copy operation.</param>
        /// <returns>New created file name.</returns>
        private async Task<string> MoveOrCopy(string sourceName, string sourceFullPath, string destinationPath, bool move)
        {
            var moveRequest = Encoding.UTF8.GetBytes(string.Format("home={0}&api={1}&token={2}&email={3}&x-email={3}&conflict=rename&folder={4}", sourceFullPath, 2, Account.AuthToken, Account.LoginName, destinationPath));

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/file/{(move ? "move" : "copy")}");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = moveRequest.LongLength;
            request.Referer =
                $"{ConstSettings.CloudDomain}/home{sourceFullPath.Substring(0, sourceFullPath.LastIndexOf(sourceName, StringComparison.Ordinal))}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var s = t.Result)
                {
                    s.Write(moveRequest, 0, moveRequest.Length);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new Exception();
                        }

                        var result = (string)JsonParser.Parse(ReadResponseAsText(response), PObject.BodyAsString);
                        return result.Substring(result.LastIndexOf("/", StringComparison.Ordinal) + 1);
                    }
                }
            });
        }

        /// <summary>
        /// Move or cope large file.
        /// </summary>
        /// <param name="file">File info, should have multi file type.</param>
        /// <param name="destinationPath">Destination path to move or copy.</param>
        /// <param name="move">Operation type move or copy.</param>
        /// <returns>True or false result operation</returns>
        private async Task<bool> MoveOrCopyMultiFile(File file, string destinationPath, bool move)
        {
            var taskAction = new object[] { file, destinationPath, move };
            return await Task.Factory.StartNew(
                (action) =>
                {
                    var param = action as object[];
                    var fileInfo = param[0] as File;
                    var destPath = param[1] as string;
                    var needMove = (bool)param[2];

                    bool result;
                    fileInfo.Type = FileType.SingleFile;
                    var fileBytes = GetFile(fileInfo, false).Result;
                    var conf = DeserializeMultiFileConfig(Encoding.UTF8.GetString(fileBytes));
                    var sourcePath = fileInfo.FullPath.Substring(0, fileInfo.FullPath.LastIndexOf(fileInfo.PrimaryName, StringComparison.Ordinal));
                    var newParts = new Dictionary<string, string>();
                    foreach (var item in conf.Parts)
                    {
                        var newPart = MoveOrCopy(item.OriginalFileName, sourcePath + item.OriginalFileName, destPath, needMove).Result;
                        newParts.Add(item.OriginalFileName, newPart);
                    }

                    conf.Parts.ToList().ForEach(x => x.OriginalFileName = newParts[x.OriginalFileName]);
                    var newConfName = MoveOrCopy(fileInfo.PrimaryName, fileInfo.FullPath, destPath, needMove).Result;
                    if (result = newConfName != fileInfo.PrimaryName)
                    {

                        var f = new File(destPath.EndsWith("/") ? destPath + newConfName : destPath + "/" + newConfName, 0, FileType.SingleFile, null);
                        result = Remove(f).Result;

                        if (result)
                        {
                            var oldCopySuffixIndex = fileInfo.PrimaryName.LastIndexOf(" (", StringComparison.Ordinal);
                            var oldCopySuffix = oldCopySuffixIndex != -1 && fileInfo.PrimaryName.EndsWith(")") ? fileInfo.PrimaryName.Substring(oldCopySuffixIndex) : string.Empty;
                            if (oldCopySuffix != string.Empty)
                            {
                                conf.OriginalFileName = conf.OriginalFileName.Replace(oldCopySuffix, string.Empty);
                            }

                            var copySuffix = newConfName.Substring(newConfName.LastIndexOf(" (", StringComparison.Ordinal));
                            var extIndex = conf.OriginalFileName.LastIndexOf(".", StringComparison.Ordinal);
                            var ext = extIndex != -1 ? conf.OriginalFileName.Substring(extIndex) : string.Empty;
                            conf.OriginalFileName = extIndex != -1 ? conf.OriginalFileName.Substring(0, extIndex) + copySuffix + ext : conf.OriginalFileName + copySuffix;
                            var tempFile = Path.GetTempFileName();
                            System.IO.File.WriteAllText(tempFile, GenerateMultiFileConfig(conf));
                            result = UploadFile(newConfName, tempFile, string.Empty, 0, new FileInfo(tempFile).Length, destPath, false).Result;
                            if (System.IO.File.Exists(tempFile))
                            {
                                try
                                {
                                    System.IO.File.Delete(tempFile);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                    }

                    return result;
                },
            taskAction);
        }

        /// <summary>
        /// Remove file or folder.
        /// </summary>
        /// <param name="fullPath">Full file or folder name.</param>
        /// <returns>True or false result operation.</returns>
        private async Task<bool> Remove(string fullPath)
        {
            var removeRequest = Encoding.UTF8.GetBytes(string.Format("home={0}&api={1}&token={2}&email={3}&x-email={3}", HttpUtility.UrlEncode(fullPath), 2, Account.AuthToken, Account.LoginName));

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/file/remove");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = removeRequest.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home{fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal) + 1)}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var s = t.Result)
                {
                    s.Write(removeRequest, 0, removeRequest.Length);
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

        /// <summary>
        /// Create file record in the cloud.
        /// </summary>
        /// <param name="fileInfo">File info.</param>
        /// <returns>True or false result operation.</returns>
        private  async Task<bool> AddFileInCloud(File fileInfo)
        {
            var hasFile = fileInfo.Hash != null && fileInfo.Size.DefaultValue != 0;
            var filePart = hasFile ? $"&hash={fileInfo.Hash}&size={fileInfo.Size.DefaultValue}" : string.Empty;
            var addFileRequest = Encoding.UTF8.GetBytes(
                $"home={HttpUtility.UrlEncode(fileInfo.FullPath)}&conflict=rename&api={2}&token={Account.AuthToken}" + filePart);

            var url = new Uri($"{ConstSettings.CloudDomain}/api/v2/{(hasFile ? "file" : "folder")}/add");
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = Account.Cookies;
            request.Method = "POST";
            request.ContentLength = addFileRequest.LongLength;
            request.Referer =
                $"{ConstSettings.CloudDomain}/home{HttpUtility.UrlEncode(fileInfo.FullPath.Substring(0, fileInfo.FullPath.LastIndexOf(fileInfo.Name, StringComparison.Ordinal)))}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "*/*";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetRequestStream, asyncResult => request.EndGetRequestStream(asyncResult), null);
            return await task.ContinueWith((t) =>
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

        /// <summary>
        /// Write file in the stream.
        /// </summary>
        /// <param name="fullFilePath">Full file path, included file name.</param>
        /// <param name="startPosition">Started read position in input stream. Input stream will create from file info.</param>
        /// <param name="size">File size.</param>
        /// <param name="outputStream">Stream to writing.</param>
        /// <param name="token">Async task cancellation token.</param>
        /// <param name="includeProgressEvent">On or off progress change event.</param>
        /// <param name="operation">Currently operation type.</param>
        private void WriteBytesInStream(string fullFilePath, long startPosition, long size, Stream outputStream, CancellationToken token, bool includeProgressEvent = false, OperationType operation = OperationType.None)
        {
            using (var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8196))
            {
                using (var source = new BinaryReader(stream))
                {
                    source.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                    WriteBytesInStream(source, outputStream, token, size, includeProgressEvent, operation);
                }
            }
        }

        /// <summary>
        /// Write one stream to another.
        /// </summary>
        /// <param name="sourceStream">Source stream reader.</param>
        /// <param name="outputStream">Stream to writing.</param>
        /// <param name="token">Async task cancellation token.</param>
        /// <param name="length">Stream length.</param>
        /// <param name="includeProgressEvent">On or off progress change event.</param>
        /// <param name="operation">Currently operation type.</param>
        private void WriteBytesInStream(BinaryReader sourceStream, Stream outputStream, CancellationToken token, long length, bool includeProgressEvent = false, OperationType operation = OperationType.None)
        {
            if (includeProgressEvent && (sourceStream.BaseStream.Length == length || sourceStream.BaseStream.Position == 0))
            {
                OnChangedProgressPercent(new ProgressChangedEventArgs(
                                0,
                                new ProgressChangeTaskState()
                                {
                                    Type = operation,
                                    TotalBytes = new FileSize()
                                    {
                                        DefaultValue = sourceStream.BaseStream.Length
                                    },
                                    BytesInProgress = new FileSize()
                                    {
                                        DefaultValue = 0L
                                    }
                                }));
            }

            int bufferLength = 8192;
            var totalWritten = 0L;
            if (length < bufferLength)
            {
                sourceStream.BaseStream.CopyTo(outputStream);
            }
            else
            {
                double percentComplete = 0;
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

                    if (includeProgressEvent && length != 0 && sourceStream.BaseStream.Length >= sourceStream.BaseStream.Position)
                    {
                        double tempPercentComplete = 100.0 * (double)sourceStream.BaseStream.Position / (double)sourceStream.BaseStream.Length;
                        if (tempPercentComplete - percentComplete >= 1)
                        {
                            percentComplete = tempPercentComplete;
                            OnChangedProgressPercent(new ProgressChangedEventArgs(
                                (int)percentComplete,
                                new ProgressChangeTaskState()
                                {
                                    Type = operation,
                                    TotalBytes = new FileSize()
                                    {
                                        DefaultValue = sourceStream.BaseStream.Length
                                    },
                                    BytesInProgress = new FileSize()
                                    {
                                        DefaultValue = sourceStream.BaseStream.Position
                                    }
                                }));
                        }
                    }
                }
            }

            if (includeProgressEvent && (sourceStream.BaseStream.Length == length || sourceStream.BaseStream.Position == sourceStream.BaseStream.Length))
            {
                OnChangedProgressPercent(new ProgressChangedEventArgs(
                                100,
                                new ProgressChangeTaskState()
                                {
                                    Type = operation,
                                    TotalBytes = new FileSize()
                                    {
                                        DefaultValue = sourceStream.BaseStream.Length
                                    },
                                    BytesInProgress = new FileSize()
                                    {
                                        DefaultValue = sourceStream.BaseStream.Position == 0 ? length : sourceStream.BaseStream.Position
                                    }
                                }));
            }
        }

        /// <summary>
        /// Write byte array in the stream.
        /// </summary>
        /// <param name="bytes">Byte array.</param>
        /// <param name="outputStream">Stream to writing.</param>
        /// <param name="token">Async task cancellation token.</param>
        /// <param name="includeProgressEvent">On or off progress change event.</param>
        /// <param name="operation">Currently operation type.</param>
        private void WriteBytesInStream(byte[] bytes, Stream outputStream, CancellationToken token, bool includeProgressEvent = false, OperationType operation = OperationType.None)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var source = new BinaryReader(stream))
                {
                    WriteBytesInStream(source, outputStream, token, bytes.LongLength, includeProgressEvent, operation);
                }
            }
        }

        /// <summary>
        /// Get shard info that to do post get request.
        /// </summary>
        /// <param name="shardType">Shard type as numeric type.</param>
        /// <returns>Shard info.</returns>
        private async Task<ShardInfo> GetShardInfo(ShardType shardType)
        {
            CookieContainer cookie = null;
            return await GetShardInfo(shardType, false, cookie);
        }

        /// <summary>
        /// Get shard info that to do post get request. Can be use for anonymous user.
        /// </summary>
        /// <param name="shardType">Shard type as numeric type.</param>
        /// <param name="useAnonymousUser">To get anonymous user.</param>
        /// <param name="cookie">Generated cookie.</param>
        /// <returns>Shard info.</returns>
        private async Task<ShardInfo> GetShardInfo(ShardType shardType, bool useAnonymousUser, CookieContainer cookie)
        {
            CheckAuth();
            var uri = new Uri(string.Format("{0}/api/v2/dispatcher?{2}={1}", ConstSettings.CloudDomain, !useAnonymousUser ? Account.AuthToken : 2.ToString(), !useAnonymousUser ? "token" : "api"));
            var request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
            request.Proxy = Account.Proxy;
            request.CookieContainer = !useAnonymousUser ? Account.Cookies : new CookieContainer();
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = ConstSettings.UserAgent;
            var task = Task.Factory.FromAsync(request.BeginGetResponse, asyncResult => request.EndGetResponse(asyncResult), null);
            return await task.ContinueWith((t) =>
            {
                using (var response = t.Result as HttpWebResponse)
                {
                    if (response != null && response.StatusCode == HttpStatusCode.OK)
                    {
                        cookie = request.CookieContainer;
                        return (ShardInfo)JsonParser.Parse(ReadResponseAsText(response), PObject.Shard, shardType.GetEnumDescription());
                    }
                    throw new Exception();
                }
            });
        }

        /// <summary>
        /// Need to add this function for all calls.
        /// </summary>
        private void CheckAuth()
        {
            if (Account == null)
            {
                throw new Exception("Account is null or empty");
            }

            if (string.IsNullOrEmpty(Account.AuthToken))
            {
                if (!Account.Login())
                {
                    throw new Exception("Auth token has't been retrieved.");
                }
            }
        }

        /// <summary>
        /// Generate multi file config content.
        /// </summary>
        /// <param name="multiFile">Serialization object.</param>
        /// <returns>XML content.</returns>
        private string GenerateMultiFileConfig(MultiFile multiFile)
        {
            string result;
            var serializer = new XmlSerializer(typeof(MultiFile));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, multiFile);
                result = writer.ToString();
            }

            return result;
        }

        /// <summary>
        /// Deserialize multi file config.
        /// </summary>
        /// <param name="xml">Multi file config content as string.</param>
        /// <returns>Deserialized object.</returns>
        private MultiFile DeserializeMultiFileConfig(string xml)
        {
            MultiFile data;
            using (var reader = new StringReader(xml))
            {
                var serializer = new XmlSerializer(typeof(MultiFile));
                data = (MultiFile)serializer.Deserialize(reader);
            }

            return data;
        }
    }
}

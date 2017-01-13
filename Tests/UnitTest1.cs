using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MailRuCloudApi;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System;
using System.Threading.Tasks;
using System.Text;

namespace Tests
{
    [TestClass]
    public class UnitTest1
    {
        private const string Login = "your";
        private const string Password = "your";
        private Account account = new Account(Login, Password);

        [TestMethod]
        public async Task GetPublishDirectLinkTest()
        {
            MailRuCloud cloud = new MailRuCloud();
            string downloadLink = await cloud.GetPublishDirectLink("https://cloud.mail.ru/public/Euhr/WwtEeZmKH", FileType.SingleFile);
            Assert.IsNotNull(downloadLink);
        }

        [TestMethod]
        public void A1LoginTest()
        {
            account.Login();
            Assert.IsNotNull(account.AuthToken);
        }

        [TestMethod]
        public void TestGettingAccountInfo()
        {
            var diskUsage = this.account.GetDiskUsage().Result;
            Assert.IsTrue(diskUsage.Free.DefaultValue > 0L
                && diskUsage.Total.DefaultValue > 0L
                && diskUsage.Used.DefaultValue > 0L);
        }

        [TestMethod]
        public void TestUploadFileFromStream()
        {
            var fileName = "UploadTest.txt";
            var content = "MyTestContent";
            var destinationPath = "/";
            var source = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var size = source.Length;

            var api = new MailRuCloud() { Account = account };

            var result = api.UploadFileAsync(fileName, source, destinationPath).Result;

            Assert.IsInstanceOfType(result, typeof(MailRuCloudApi.File));
            Assert.AreEqual(size, result.Size.DefaultValue);
        }

        [TestMethod]
        public void TestDownloadFileToStream()
        {
            var fileName = "UploadTest.txt";
            var content = "MyTestContent";
            var sourcePath = "/";

            var api = new MailRuCloud() { Account = account };

            var result = new MemoryStream(api.GetFile(new MailRuCloudApi.File(fileName, sourcePath + fileName), false).Result);

            using (var streamReader = new StreamReader(result))
                Assert.AreEqual(content, streamReader.ReadToEnd());
        }

        [TestMethod]
        public void TestUploadBinaryFileFromStream()
        {
            var fileName = "UploadTestBinary.bin";
            var content = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            var destinationPath = "/";
            var source = new MemoryStream(content);
            var size = source.Length;

            var api = new MailRuCloud() { Account = account };

            var result = api.UploadFileAsync(fileName, source, destinationPath).Result;

            Assert.IsInstanceOfType(result, typeof(MailRuCloudApi.File));
            Assert.AreEqual(size, result.Size.DefaultValue);
        }

        [TestMethod]
        public void TestDownloadBinaryFileToStream()
        {
            var fileName = "UploadTestBinary.bin";
            var content = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            var sourcePath = "/";

            var api = new MailRuCloud() { Account = account };

            var result = new MemoryStream(api.GetFile(new MailRuCloudApi.File(fileName, sourcePath + fileName), false).Result);

            var output = new byte[result.Length];
            result.Read(output, 0, (int)result.Length);
            CollectionAssert.AreEqual(content, output);
        }


        [TestMethod]
        public void TestRemoveFileByFullPath()
        {
            var fileName = "RemoveTest.txt";
            var content = "MyTestContent";
            var destinationPath = "/";
            var source = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var size = source.Length;

            var api = new MailRuCloud() { Account = account };

            var entry = api.GetItems(destinationPath).Result;
            Assert.IsFalse(entry.Files.Any(i => i.Name == fileName));

            var result = api.UploadFileAsync(fileName, source, destinationPath).Result;
            Assert.IsInstanceOfType(result, typeof(MailRuCloudApi.File));

            entry = api.GetItems(destinationPath).Result;
            Assert.IsTrue(entry.Files.Any(i => i.Name == fileName));

            api.Remove(destinationPath + fileName).Wait();

            entry = api.GetItems(destinationPath).Result;
            Assert.IsFalse(entry.Files.Any(i => i.Name == fileName));
        }

        //[TestMethod]
        //public void GetItemsTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;
        //    var items = api.GetItems("/new folder/new folder 2").Result;
        //    Assert.IsNotNull(items);
        //    Assert.IsTrue(items.Files.Count == items.NumberOfFiles || items.Folders.Count == items.NumberOfFolders);

        //    var percent = 0;
        //    api.ChangingProgressEvent += delegate (object sender, ProgressChangedEventArgs e)
        //    {
        //        percent = e.ProgressPercentage;
        //    };

        //    var fileToDownload = items.Files.First(t => t.Size.DefaultValue <= 1 * 1024 * 1024);
        //    var task = api.GetFile(fileToDownload);
        //    Assert.IsNotNull(task.Result);
        //    Assert.IsTrue(percent == 100);

        //    percent = 0;
        //    var task2 = api.GetFile(fileToDownload, @"C:\Development\MailRuCloudApi\");
        //    Assert.IsTrue(task2.Result);
        //    Assert.IsTrue(percent == 100);

        //    var fileInfo = new FileInfo(@"C:\Development\MailRuCloudApi\" + fileToDownload.Name);
        //    Assert.IsTrue(fileInfo.Exists, "File is not created.");
        //    Assert.IsTrue(fileInfo.Length > 0, "File size in not retrieved.");
        //}

        [TestMethod]
        public void UploadFileTest()
        {
            var api = new MailRuCloud();
            api.Account = this.account;

            var percent = 0;
            api.ChangingProgressEvent += delegate (object sender, ProgressChangedEventArgs e)
            {
                percent = e.ProgressPercentage;
            };

            var task = api.UploadFile(new FileInfo(@"..\..\Properties\AssemblyInfo.cs"), "/");
            Assert.IsTrue(task.Result);
            Assert.IsTrue(percent == 100);
            Thread.Sleep(5000);
        }

        //[TestMethod]
        //public void GetPublishDirectLinkTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var items = api.GetItems("/Camera Uploads");
        //    var fileToDownload = items.Files.First(t => t.Size.DefaultValue <= 1 * 1024 * 1024);
        //    var publicFileLink = api.GetPublishLink(fileToDownload);

        //    Assert.IsTrue(!string.IsNullOrEmpty(publicFileLink));

        //    var directLink = api.GetPublishDirectLink(publicFileLink);

        //    Assert.IsTrue(!string.IsNullOrEmpty(directLink));

        //    var unpublishFile = api.UnpublishLink(fileToDownload);
        //    Assert.IsTrue(unpublishFile);
        //}

        //[TestMethod]
        //public void PublicUnpublishLink()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var items = api.GetItems("/Camera Uploads");
        //    var fileToDownload = items.Files.First(t => t.Size.DefaultValue <= 1 * 1024 * 1024);
        //    var publicFileLink = api.GetPublishLink(fileToDownload);

        //    var folder = new Folder(
        //        0,
        //        0,
        //        "Camera Uploads",
        //        new FileSize()
        //        {
        //            DefaultValue = 0
        //        },
        //        "/Camera Uploads");
        //    var publishFolderLink = api.GetPublishLink(folder);

        //    Assert.IsTrue(!string.IsNullOrEmpty(publicFileLink));
        //    Assert.IsTrue(!string.IsNullOrEmpty(publishFolderLink));

        //    var unpublishFile = api.UnpublishLink(fileToDownload);
        //    var unpublishFolder = api.UnpublishLink(folder);

        //    Assert.IsTrue(unpublishFile);
        //    Assert.IsTrue(unpublishFolder);
        //}

        ////[TestMethod]
        ////public void GetShardTest()
        ////{
        ////    var objToTestPrivateMethod = new PrivateObject(typeof(MailRuCloud));
        ////    objToTestPrivateMethod.SetFieldOrProperty("Account", this.account);
        ////    var result = objToTestPrivateMethod.Invoke("GetShardInfo", ShardType.Get);

        ////    Assert.IsNotNull(result);
        ////    Assert.IsTrue(!string.IsNullOrEmpty((result as ShardInfo).Url));
        ////}

        //[TestMethod]
        //public void RemoveFileFolderTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var result = api.UploadFileAsync(new FileInfo(@"C:\Development\MailRuCloudApi\1.txt"), "/");
        //    var file = api.GetItems("/").Files.First(x => x.Name == "1.txt");
        //    api.Remove(file);

        //    api.CreateFolder("new test folder", "/");
        //    var folder = api.GetItems("/").Folders.First(x => x.Name == "new test folder");
        //    api.Remove(folder);
        //}

        //[TestMethod]
        //public void MoveFileFolderTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var result = api.UploadFileAsync(new FileInfo(@"D:\1.stl"), "/");
        //    if (result.Result)
        //    {
        //        var file = api.GetItems("/").Files.First(x => x.Name == "1.stl");
        //        api.Move(file, "/Misuc");

        //        api.CreateFolder("new test folder", "/");
        //        var folder = api.GetItems("/").Folders.First(x => x.Name == "new test folder");
        //        api.Move(folder, "/Misuc");

        //        var entry = api.GetItems("/Misuc");

        //        Assert.IsNotNull(entry.Folders.FirstOrDefault(x => x.Name == "new test folder"));
        //        Assert.IsNotNull(entry.Files.FirstOrDefault(x => x.Name == "1.stl"));
        //    }
        //}

        //[TestMethod]
        //public void CopyFileFolderTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var result = api.UploadFileAsync(new FileInfo(@"D:\1.stl"), "/");
        //    if (result.Result)
        //    {
        //        var file = api.GetItems("/").Files.First(x => x.Name == "1.stl");
        //        api.Copy(file, "/Misuc");

        //        api.CreateFolder("new test folder", "/");
        //        var folder = api.GetItems("/").Folders.First(x => x.Name == "new test folder");
        //        api.Copy(folder, "/Misuc");

        //        var entry = api.GetItems("/Misuc");

        //        Assert.IsNotNull(entry.Folders.FirstOrDefault(x => x.Name == folder.Name));
        //        Assert.IsNotNull(entry.Files.FirstOrDefault(x => x.Name == file.Name));
        //    }
        //}

        //[TestMethod]
        //public void RenameTest()
        //{
        //    var api = new MailRuCloud();
        //    api.Account = this.account;

        //    var result = api.UploadFileAsync(new FileInfo(@"D:\1.stl"), "/");
        //    if (result.Result)
        //    {
        //        var file = api.GetItems("/").Files.First(x => x.Name == "1.stl");
        //        api.Rename(file, "rename stl test.stl");

        //        api.CreateFolder("new test folder", "/");
        //        var folder = api.GetItems("/").Folders.First(x => x.Name == "new test folder");
        //        api.Rename(folder, "rename folder test");

        //        var entry = api.GetItems("/");

        //        Assert.IsNotNull(entry.Folders.FirstOrDefault(x => x.Name == "rename folder test"));
        //        Assert.IsNotNull(entry.Files.FirstOrDefault(x => x.Name == "rename stl test.stl"));
        //    }
        //}
    }
}

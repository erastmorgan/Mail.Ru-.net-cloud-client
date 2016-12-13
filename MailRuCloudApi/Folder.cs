﻿//-----------------------------------------------------------------------
// <created file="Folder.cs">
//     Mail.ru cloud client created in 2016.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

using System;
using System.IO;

namespace MailRuCloudApi
{
    /// <summary>
    /// Server file info.
    /// </summary>
    public class Folder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Folder" /> class.
        /// </summary>
        public Folder(string fullPath)
        {
            FullPath = fullPath;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Folder" /> class.
        /// </summary>
        /// <param name="foldersCount">Number of folders.</param>
        /// <param name="filesCount">Number of files.</param>
        /// <param name="size">Folder size.</param>
        /// <param name="fullPath">Full folder path.</param>
        /// <param name="publicLink">Public folder link.</param>
        public Folder(int foldersCount, int filesCount, FileSize size, string fullPath, string publicLink = null):this(fullPath)
        {
            NumberOfFolders = foldersCount;
            NumberOfFiles = filesCount;
            Size = size;
            PublicLink = publicLink;
        }

        /// <summary>
        /// Gets number of folders in folder.
        /// </summary>
        /// <value>Number of folders.</value>
        public int NumberOfFolders { get; internal set; }

        /// <summary>
        /// Gets number of files in folder.
        /// </summary>
        /// <value>Number of files.</value>
        public int NumberOfFiles { get; internal set; }

        /// <summary>
        /// Gets folder name.
        /// </summary>
        /// <value>Folder name.</value>
        public string Name => FullPath == "/" ? "" : FullPath.TrimEnd('/').Remove(0, FullPath.LastIndexOf('/') + 1);

        /// <summary>
        /// Gets folder size.
        /// </summary>
        /// <value>Folder size.</value>
        public FileSize Size { get; internal set; }

        /// <summary>
        /// Gets full folder path on the server.
        /// </summary>
        /// <value>Full folder path.</value>
        public string FullPath
        {
            get;
        }

        /// <summary>
        /// Gets public folder link.
        /// </summary>
        /// <value>Public link.</value>
        public string PublicLink { get; internal set; }

        public DateTime CreationTimeUtc { get; set; } = DateTime.Now.AddDays(-1);

        public DateTime LastWriteTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


        public DateTime LastAccessTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


        public FileAttributes Attributes { get; set; } = FileAttributes.Directory;
    }
}

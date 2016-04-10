//-----------------------------------------------------------------------
// <created file="File.cs">
//     Mail.ru cloud client created in 2016.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

namespace MailRuCloudApi
{
    /// <summary>
    /// Server file info.
    /// </summary>
    public class File
    {
        /// <summary>
        /// Gets file name.
        /// </summary>
        /// <value>File name.</value>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets file hash value.
        /// </summary>
        /// <value>File hash.</value>
        public string Hash { get; internal set; }

        /// <summary>
        /// Gets file size.
        /// </summary>
        /// <value>File size.</value>
        public long Size { get; internal set; }

        /// <summary>
        /// Gets full file path with name in server.
        /// </summary>
        /// <value>Full file path.</value>
        public string FulPath { get; internal set; }
    }
}

//-----------------------------------------------------------------------
// <created file="DiskUsage.cs">
//     Mail.ru cloud client created in 2017.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

namespace MailRuCloudApi
{
    /// <summary>
    /// Get cloud disk usage for current account.
    /// </summary>
    public class DiskUsage
    {
        /// <summary>
        /// Gets total disk size.
        /// </summary>
        public FileSize Total { get; internal set; }

        /// <summary>
        /// Gets used disk size.
        /// </summary>
        public FileSize Used { get; internal set; }

        /// <summary>
        /// Gets free disk size.
        /// </summary>
        public FileSize Free
        {
            get
            {
                return new FileSize
                {
                    DefaultValue = this.Total.DefaultValue - this.Used.DefaultValue
                };
            }
        }
    }
}

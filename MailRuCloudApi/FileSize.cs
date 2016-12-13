//-----------------------------------------------------------------------
// <created file="FileSize.cs">
//     Mail.ru cloud client created in 2016.
// </created>
// <author>Korolev Erast.</author>
//-----------------------------------------------------------------------

namespace MailRuCloudApi
{
    /// <summary>
    /// File size definition.
    /// </summary>
    public class FileSize
    {
        /// <summary>
        /// Private variable for default value.
        /// </summary>
        private long _defValue;

        /// <summary>
        /// Gets default size in bytes.
        /// </summary>
        /// <value>File size.</value>
        public long DefaultValue
        {
            get
            {
                return _defValue;
            }

            internal set
            {
                _defValue = value;
                SetNormalizedValue();
            }
        }

        /// <summary>
        /// Gets normalized  file size, auto detect storage unit.
        /// </summary>
        /// <value>File size.</value>
        public float NormalizedValue { get; private set; }

        /// <summary>
        /// Gets auto detected storage unit by normalized value.
        /// </summary>
        public StorageUnit NormalizedType { get; private set; }

        /// <summary>
        /// Normalized value detection and auto detection storage unit.
        /// </summary>
        private void SetNormalizedValue()
        {
            if (_defValue < 1024L)
            {
                NormalizedType = StorageUnit.Byte;
                NormalizedValue = (float)_defValue;
            }
            else if (_defValue >= 1024L && _defValue < 1024L * 1024L)
            {
                NormalizedType = StorageUnit.Kb;
                NormalizedValue = (float)_defValue / 1024f;
            }
            else if (_defValue >= 1024L * 1024L && _defValue < 1024L * 1024L * 1024L)
            {
                NormalizedType = StorageUnit.Mb;
                NormalizedValue = (float)_defValue / 1024f / 1024f;
            }
            else
            {
                NormalizedType = StorageUnit.Gb;
                NormalizedValue = (float)_defValue / 1024f / 1024f / 1024f;
            }
        }
    }
}

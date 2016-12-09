namespace MailRuCloudApi
{
    public class AccountInfo
    {
        private long _fileSizeLimit;

        public long FileSizeLimit
        {
            get { return _fileSizeLimit <= 0 ? long.MaxValue : _fileSizeLimit; }
            set { _fileSizeLimit = value; }
        }
    }
}
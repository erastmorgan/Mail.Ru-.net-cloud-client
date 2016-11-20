using System;

namespace MailRuCloudApi
{
    public class Quota
    {
        public bool OverQuota { get; set; }
        public long Used { get; set; }
        public long Total { get; set; }

        public long Free => Total - Used;
    }
}
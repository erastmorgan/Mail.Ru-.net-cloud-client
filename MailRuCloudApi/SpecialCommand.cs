using System;

namespace MailRuCloudApi
{
    public class SpecialCommand
    {
        private readonly string _param;

        public SpecialCommand(string param)
        {
            _param = param;
        }

        public string Value
        {
            get
            {
                if (null != _command) return _command;

                int pos = _param.LastIndexOf("/>>", StringComparison.Ordinal);
                _command = pos > -1 
                    ? _param.Substring(pos + 3) 
                    : string.Empty;

                return _command;
            }
        }
        private string _command;

        public bool IsCommand => !string.IsNullOrEmpty(Value);

        public string Path
        {
            get
            {
                int pos = _param.LastIndexOf("/>>", StringComparison.Ordinal);
                return pos > -1 
                    ? _param.Substring(0, pos) 
                    : string.Empty;
            }
        }


    }
}

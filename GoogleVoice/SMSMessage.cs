using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public class SMSMessage
    {
        public string To { get; set; }
        public string From { get; set; }
        public string Time { get; set; }
        public string Msg { get; set; }
    }
}

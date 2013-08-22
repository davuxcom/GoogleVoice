using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public class Message
    {
        public enum MessageType
        {
            SMS, Voicemail, Placed, Missed, Received, Unknown
        }

        public MessageType Class = MessageType.Unknown;

        public string MessageText { get; set; }
        public string ID { get; set; }
        public string Number { get; set; }
        public DateTime Time { get; set; }
    }
}

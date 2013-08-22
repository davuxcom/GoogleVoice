using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GoogleVoice
{
    public class Feed_Call : Feed
    {
        private Message.MessageType MessageType = Message.MessageType.Unknown;
        private string URL = "";
        public Feed_Call(Account account, string url, Message.MessageType type)
            : base(account)
        {
            MessageType = type;
            URL = url;
            Name = "Call " +  type.ToString();
        }

        internal override void Update(bool verbose, string page)
        {
            // TODO verbose and page are not used!
            try
            {
                HttpResult ret = Account.GVPost("https://www.google.com/voice/m/x?m=list&l=" 
                    + this.MessageType.ToString().ToLower() + "&o=0&lm=10");

                string json = ret.Page.Split('\n')[1];
                JObject o = JObject.Parse(json);
                JToken conversation = o["conversations_response"]["conversationgroup"];

                if (conversation == null) return;

                foreach (var convo in conversation)
                {
                    string cid = ((JValue)convo["conversation"]["id"]).Value.ToString();
                    foreach (var msg in convo["call"])
                    {
                        string id = ((JValue)msg["id"]).Value.ToString();
                        //string name = ((JValue)msg["contact"]["name"]).Value.ToString();
                        string phone_number = ((JValue)msg["phone_number"]).Value.ToString();
                        //string start_time = ((JValue)msg["start_time"]).Value.ToString();

                        Message vm = new Message
                        {
                            ID = id,
                            Number = phone_number,
                            Time = DateTime.Now, // GoogleUtils.UnixTimeToDateTime(start_time),
                            Class = this.MessageType,
                        };

                        if (!SeenMessage(vm))
                        {
                            Account.OnMessage_Internal(vm);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("GoogleVoice/Feed_SMS/Update *** " + ex);
            }
        }
    }
}

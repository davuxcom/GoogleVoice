using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GoogleVoice
{
    public class SMS : Message
    {
        public bool Self { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string CID { get; set; }

        public SMS()
        {
            Class = MessageType.SMS;
        }
    }

    public class Feed_SMS : Feed
    {
        public Feed_SMS(Account account)
            : base(account)
        {
            this.Name = "SMS";
        }

        internal override void Update(bool verbose, string page)
        {
            try
            {
                HttpResult ret = Account.GVPost("https://www.google.com/voice/m/x?m=list&l=sms&o=0&lm=10");
                string json = ret.Page.Split('\n')[1];

                JObject o = JObject.Parse(json);
                JToken conversation = o["conversations_response"]["conversationgroup"];

                if (conversation == null) return;

                foreach (var convo in conversation)
                {
                    //var convo = c_group["conversation"];

                    string cid = ((JValue)convo["conversation"]["id"]).Value.ToString();
                    foreach (var msg in convo["call"])
                    {
                        string id = ((JValue)msg["id"]).Value.ToString();
                        string message_text = ((JValue)msg["message_text"]).Value.ToString();
                        string status = ((JValue)msg["status"]).Value.ToString();
                        //string name = ((JValue)msg["contact"]["name"]).Value.ToString();
                        string phone_number = ((JValue)msg["phone_number"]).Value.ToString();
                        string start_time = ((JValue)msg["start_time"]).Value.ToString();

                        SMS sms = new SMS
                        {
                            CID = cid,
                            ID = id,
                            Name = phone_number,
                            Number = phone_number,
                            Text = message_text,
                            Time = GoogleUtils.UnixTimeToDateTime(start_time),
                            Self = status == "1",
                        };

                        if (!SeenMessage(sms))
                        {
                            Account.OnMessage_Internal(sms);
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

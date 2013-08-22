using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.IO;

namespace GoogleVoice
{
    public class VoiceMailMessage : Message
    {
        public string CID { get; set; }

        public VoiceMailMessage()
        {
            this.Class = MessageType.Voicemail;
        }
    }

    public class Feed_Voicemail : Feed
    {
        public Feed_Voicemail(Account Account) : base(Account)
        {
            this.Name = "Voicemail";
        }

        internal override void Update(bool verbose, string page)
        {
            try
            {
                HttpResult ret = Account.GVPost("https://www.google.com/voice/m/x?m=list&l=voicemail&o=0&lm=10");
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

                        string transcript_status = ((JValue)msg["transcript_status"]).Value.ToString();
                        string transcript_text = "";
                        if (transcript_status == "1")
                        {
                            try
                            {
                                JToken transcript = msg["transcript"];
                                if (transcript != null)
                                {
                                    JToken word_tokens = transcript["word_tokens"];
                                    foreach (var word in word_tokens)
                                    {
                                        // TODO we can pull out the accuracy and make pretty
                                        // looking message transcriptions
                                        string w = ((JValue)word["word"]).Value.ToString();
                                        transcript_text += w + " ";
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.Write("transcript processing: " + ex.Message);
                            }
                        }
                        else
                        {
                            Trace.WriteLine("Transcript " + "noname" + ": " + transcript_status);
                            // TODO wait for transcript
                            // figure out status code!
                            // continue;
                        }

                        var vm = new VoiceMailMessage
                        {
                            ID = id,
                            Time = DateTime.Now, // GoogleUtils.UnixTimeToDateTime(start_time),
                            Number = phone_number,
                            CID = cid,
                            MessageText = transcript_text,
                        };

                        if (!SeenMessage(vm))
                        {
                            DownloadMessage(vm.ID);
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

        void DownloadMessage(string id)
        {
            if (Account.Cache_Dir == null) return;
            try
            {
                if (!File.Exists(Account.Cache_Dir + "\\" + id + ".mp3"))
                {
                    byte[] mp3 = Account.GetFile("https://www.google.com/voice/media/send_voicemail/" + id);
                    File.WriteAllBytes(Account.Cache_Dir + "\\" + id + ".mp3", mp3);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("GoogleVoice/Feed_Voicemail/DownloadMessage: ***: " + ex.Message);
            }
        }
    }
}

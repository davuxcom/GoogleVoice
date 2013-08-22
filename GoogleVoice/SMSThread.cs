using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using mshtml;

namespace GoogleVoice
{
    public class SMSThread
    {
        public List<SMSMessage> Messages = new List<SMSMessage>();

        private HTMLDocument GetDoc(string html)
        {
            HTMLDocument dx = new HTMLDocumentClass();
            IHTMLDocument2 doc2 = (IHTMLDocument2)dx;
            doc2.write(new object[] { html });
            return dx;
        }

        private Voice voice;


        public SMSThread(string c, Voice v)
        {
            try
            {
                voice = v;
                //Trace.WriteLine("SMS Thread with length: " + c.Length);

                HTMLDocument dx = GetDoc(c);
                IHTMLElementCollection ihec = dx.getElementsByTagName("span");

                SMSMessage myMsg = null;
                //Trace.WriteLine("Got spans");
                foreach (IHTMLElement e in ihec)
                {
                    if (e == null)
                    {
                        continue;
                    }
                    try
                    {
                        //Trace.WriteLine(e.className);

                        // gc-message-time
                        // gc-message-relative
                        // gc-message-sms-from
                        // gc-message-sms-text
                        // gc-message-sms-time

                        if (e.className == "gc-message-name")
                        {

                            // if there is an A, get its innerText
                            // if not, get the innerText of e
                            //Trace.WriteLine("Found Name Node");
                            HTMLDocument node = GetDoc(e.innerHTML);

                            IHTMLElementCollection tags = node.getElementsByTagName("a");

                            if (tags.length > 0)
                            {
                                foreach (IHTMLElement A in tags)
                                {

                                    // hack to remove "Me to " from messages
                                    Contact.Name = A.innerText;
                                    Trace.WriteLine("Set Name " + Contact.Name + " From " + e.className);
                                    break;
                                }
                            }
                            else
                            {
                                // this is a number
                                //Trace.WriteLine("From: " + e.innerText);
                                if (e.innerText != null)
                                {

                                    string from = e.innerText.Trim();

                                    if (from.StartsWith("Me to "))
                                    {
                                        from = from.Substring(6);
                                    }

                                    Contact.Phone.Number = Contact.Name = from;
                                    Trace.WriteLine("Set Name2 " + Contact.Name + " From " + e.className);
                                }
                            }

                        }
                        if (e.className == "gc-message-type")
                        {
                            // Trace.WriteLine("Add Type");
                            if (e.innerText != null)
                            {
                                Contact.ParseNumberWithType(e.innerText);
                            }
                            //Trace.WriteLine("Contact: " + Contact);
                        }
                       // Trace.WriteLine("Add Voice contact");
                        voice.AddContact(Contact);

                        if (e.className == "gc-message-sms-from" && e.innerText != null)
                        {
                            //Trace.WriteLine("SMSFrom: " + e.innerText);

                            string f = e.innerText.Trim();

                            if (f.EndsWith(":") && f.Length > 1)
                            {
                                f = f.Substring(0, f.Length - 1);
                            }

                            myMsg = new SMSMessage();
                            myMsg.From = f;
                        }
                        if (e.className == "gc-message-sms-time")
                        {
                          //  Trace.WriteLine("SMSTime: " + e.innerText);
                        }
                        if (e.className == "gc-message-sms-text")
                        {
                           // Trace.WriteLine("SMSText: " + e.innerText);
                            if (myMsg != null)
                            {
                                myMsg.Msg = e.innerText;
                                Messages.Add(myMsg);
                            }
                            else
                            {
                                Trace.WriteLine("myMsg is null, rcv text before sender");
                            }
                        }
                       // Trace.WriteLine("End Loop");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("Error in loop: " + ex);
                        Trace.WriteLine("Error in loop: " + ex.InnerException);
                    }
                }

            }
            catch (Exception ex)
            {
                Trace.WriteLine("ThreadSMS: " + ex);
            }
            //Trace.WriteLine("-----");
        }

        public SMSContact Contact = new SMSContact();

        public string ID { get; set; }
    }
}

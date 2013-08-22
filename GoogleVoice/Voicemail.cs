using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mshtml;
using System.Diagnostics;

namespace GoogleVoice
{
    public class Voicemail
    {
        public string Transcript { get; set; }

        private HTMLDocument GetDoc(string html)
        {
            HTMLDocument dx = new HTMLDocumentClass();
            IHTMLDocument2 doc2 = (IHTMLDocument2)dx;
            doc2.write(new object[] { html });
            return dx;
        }

        private Voice voice;

        public Voicemail()
        {
        }

        public Voicemail(string c, Voice v)
        {
            voice = v;
            //Trace.WriteLine("VM Thread with length: " + c.Length);
            HTMLDocument dx = GetDoc(c);

            IHTMLElementCollection idiv = dx.getElementsByTagName("div");

            foreach (IHTMLElement e in idiv)
            {
                if (e.className == "gc-message-message-display")
                {
                    //Trace.WriteLine("Got Text: " + e.innerText);
                    Transcript = e.innerText;
                }
            }


            IHTMLElementCollection ihec = dx.getElementsByTagName("span");

            SMSMessage myMsg = null;
            //Trace.WriteLine("Got spans");
            foreach (IHTMLElement e in ihec)
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
                            // this is a name
                            //Trace.WriteLine("From: " + A.innerText);

                            // hack to remove "Me to " from messages


                            Contact.Name = A.innerText;
                            break;
                        }
                    }
                    else
                    {
                        // this is a number
                       // Trace.WriteLine("From: " + e.innerText);


                        string from = e.innerText.Trim();

                        if (from.StartsWith("Me to "))
                        {
                            from = from.Substring(6);
                        }

                        Contact.Phone.Number = from;
                    }
                    
                }
                if (e.className == "gc-message-type")
                {
                   // Trace.WriteLine("Add Type");
                    Contact.ParseNumberWithType(e.innerText);
                   // Trace.WriteLine("Contact: " + Contact);
                }

                voice.AddContact(Contact);
            }
            //Trace.WriteLine("-----");
        }

        public SMSContact Contact = new SMSContact();

        public string ID { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Windows.Forms;
using mshtml;

namespace GoogleVoice
{
    public class Voice
    {
        public Voice(string UserName, string Password)
        {
            this.UserName = UserName;
            this.Password = Password;
            this.RNR_SE = null;

            ShortDelay = 10;
            LongDelay = 60;
            ShortDelayN = 6;

            VMDelay = 1;
        }

        public string UserName { get; set; }
        public string Password { get; set; }
        private string RNR_SE { get; set; }
        private string GSessionID { get; set; }

        public static bool UseMobile = false;

        private bool CheckImmediately = false;

        public bool Authenticated { get; set; }

        public int ShortDelay { get; set; }
        public int LongDelay { get; set; }
        public int ShortDelayN { get; set; }
        public int VMDelay { get; set; }

        CookieContainer jar = new CookieContainer();

        public delegate void ContactAdded(SMSContact contact);
        public event ContactAdded OnContactAdded;

        private List<SMSContact> Contacts = new List<SMSContact>();

        public void AddContact(SMSContact contact)
        {

            if (string.IsNullOrEmpty(contact.Phone.Number))
            {
                return;
            }

            bool Add = true;
            foreach (SMSContact c in Contacts)
            {
                if (c == null)
                {
                    continue;
                }
                if (contact.Name == "")
                {
                    if (contact.Phone.Number == c.Phone.Number)
                    {
                        Add = false;
                        break;
                    }
                }
                else
                {
                    if (contact.Name == c.Name)
                    {
                        Add = false;
                        break;
                    }
                }
            }

            if (Add)
            {
                Contacts.Add(contact);
                if (OnContactAdded != null)
                {
                    OnContactAdded.Invoke(contact);
                }
            }
        }

        public bool Authenticate()
        {
            //Trace.WriteLine("Attemping to authenticate");
            try
            {
                //Trace.WriteLine("R1");
                HttpWebRequest request = HttpWebRequest.Create("https://www.google.com/voice") as HttpWebRequest;
                request.CookieContainer = jar;
                request.AllowAutoRedirect = true;
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                StreamReader reader = new StreamReader(response.GetResponseStream());
                string ret = reader.ReadToEnd();

                //Trace.WriteLine("-> " + response.ResponseUri.ToString());

                string gc = response.ResponseUri.ToString();
                int x = gc.IndexOf("gsessionid");
                if (x > -1)
                {
                    gc = gc.Substring(x + 11);
                }
                else
                {
                    Trace.WriteLine("No GSessionID");
                    gc = "";
                }
                GSessionID = gc;
               // Trace.WriteLine("Session: " + gc);
                //Trace.WriteLine("R2");
                request = HttpWebRequest.Create("https://www.google.com/accounts/ServiceLogin?passive=true&service=grandcentral&ltmpl=bluebar&continue=https%3A%2F%2Fwww.google.com%2Fvoice%2Faccount%2Fsignin%2F%3Fprev%3D%252F&gsessionid=" + gc) as HttpWebRequest;
                request.AllowAutoRedirect = true;
                request.CookieContainer = jar;

                response = request.GetResponse() as HttpWebResponse;
                reader = new StreamReader(response.GetResponseStream());
                ret = reader.ReadToEnd();

                //Trace.WriteLine("-> " + response.ResponseUri.ToString());
                //Trace.WriteLine("Cookies: " + jar.Count);

                //string r = wc.DownloadString("https://www.google.com/voice");


                //Trace.WriteLine("Sending Authentication...");

                //https://www.google.com/accounts/ServiceLoginAuth?service=grandcentral


                CookieCollection cc = jar.GetCookies(new Uri("https://www.google.com/accounts"));

                string galx = "";
                foreach (Cookie c in cc)
                {
                    Trace.WriteLine(c.Name + ": " + c.Value);
                    if (c.Name.ToUpper() == "GALX")
                    {
                        galx = c.Value;
                    }
                }

                if (galx == "")
                {
                    Trace.WriteLine("GALX was not found!");
                }

                string p = "ltmpl=bluebar&continue=https%3A%2F%2Fwww.google.com%2Fvoice%2Faccount%2Fsignin%2F%3Fprev%3D%252F&service=grandcentral&ltmpl=bluebar&ltmpl=bluebar&GALX=" + galx + "&Email=" + System.Web.HttpUtility.UrlEncode( UserName) + "&Passwd=" + System.Web.HttpUtility.UrlEncode(Password) + "&rmShown=1&signIn=Sign+in&asts=";

                byte[] data = Encoding.ASCII.GetBytes(p);
                //Trace.WriteLine("R3");
                request = HttpWebRequest.Create("https://www.google.com/accounts/ServiceLoginAuth?service=grandcentral") as HttpWebRequest;
                request.Method = "POST";
                request.AllowAutoRedirect = true;
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = p.Length;
                request.Referer = response.ResponseUri.ToString();



                request.ContentLength = p.Length;
                request.CookieContainer = jar;


                Stream newStream = request.GetRequestStream();
                // Send the data.
                newStream.Write(data, 0, data.Length);
                newStream.Close();

                response = request.GetResponse() as HttpWebResponse;
                reader = new StreamReader(response.GetResponseStream());
                ret = reader.ReadToEnd();

                //File.WriteAllText("f:\\response.html", ret);

                int s = ret.IndexOf("_rnr_se");
                if (s > -1)
                {
                    s = ret.IndexOf("value=", s);
                    if (s > -1)
                    {
                        s += 7;
                        RNR_SE = ret.Substring(s, ret.IndexOf("\"", s) - s);
                    }
                }
                else
                {
                    Trace.WriteLine("RNR_SE is missing!");
                    return false;
                }
                //Trace.WriteLine("rnr_se: " + RNR_SE);
                //Trace.WriteLine("-> " + response.ResponseUri.ToString());
                //Trace.WriteLine("Cookies: " + jar.Count);
                //GetPhones();
                StartMonitor();
                Authenticated = true;
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Couldn't authenticate: " + ex);
                return false;
            }

        }

        public void GetPhones()
        {

            HttpWebRequest request = HttpWebRequest.Create("https://www.google.com/voice/settings/tab/phones?v=236") as HttpWebRequest;
            request.CookieContainer = jar;
            request.AllowAutoRedirect = true;
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string ret = reader.ReadToEnd();

            UpdatePhone(ret);
        }

        public void SendSMS(string number, string msg)
        {
            System.Net.ServicePointManager.Expect100Continue = false;
            if (RNR_SE != null || Authenticate())
            {
                number = number.Replace("(", "");
                number = number.Replace(")", "");
                number = number.Replace("-", "");
                number = number.Replace(" ", "");
                number = number.Trim();



                try
                {
                    string p = "";
                    if (UseMobile)
                    {
                        p = "_rnr_se=" + System.Web.HttpUtility.UrlEncode(RNR_SE) + "&number=" + number + "&smstext=" + System.Web.HttpUtility.UrlEncode(msg) + "&submit=Send";
                        Trace.WriteLine("Sending mobile SMS to " + number + " with msg len " + msg.Length);
                    }
                    else
                    {
                        if (number.Length == 10)
                        {
                            number = "%2B1" + number;
                        }
                        Trace.WriteLine("Sending SMS to " + number + " with msg len " + msg.Length); 
                        p = "id=&phoneNumber=" + number + "&text=" + System.Web.HttpUtility.UrlEncode(msg) + "&_rnr_se=" + System.Web.HttpUtility.UrlEncode(RNR_SE);
                    }
                        Trace.WriteLine("Posting: " + p);

                    byte[] data = Encoding.ASCII.GetBytes(p);


                    HttpWebRequest request = HttpWebRequest.Create( UseMobile ? "https://www.google.com/voice/m/sendsms": "https://www.google.com/voice/sms/send/") as HttpWebRequest;
                    request.CookieContainer = jar;
                    request.Method = "POST";
                    request.AllowAutoRedirect = true;
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = data.Length;
                    request.AllowWriteStreamBuffering = false;
                    request.SendChunked = false;
                    request.Expect = "";
                    request.ServicePoint.Expect100Continue = false;
                    request.Referer = "https://www.google.com/voice/?gsessionid=" + GSessionID;
                    request.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.0.15) Gecko/2009101601 Firefox/3.0.15 (.NET CLR 3.5.30729)";
                    request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    Stream newStream = request.GetRequestStream();
                    // Send the data.
                    newStream.Write(data, 0, data.Length);
                    newStream.Close();

                    PrintHeaders(request.Headers);

                    


                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                    StreamReader reader = new StreamReader(response.GetResponseStream());

                    string ret = reader.ReadToEnd();

                    //Trace.WriteLine("-> " + response.ResponseUri.ToString());

                    QuickCount = 4;
                }
                catch (WebException ex)
                {
                    Trace.WriteLine("WebException Start ------------");
                    Trace.WriteLine(ex);
                    Trace.WriteLine(ex.InnerException);
                    Trace.WriteLine(ex.TargetSite);
                    try
                    {
                        PrintHeaders(ex.Response.Headers);
                        try
                        {
                            StreamReader reader = new StreamReader(ex.Response.GetResponseStream());
                            string ret = reader.ReadToEnd();
                            System.IO.File.WriteAllText( Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+ System.IO.Path.DirectorySeparatorChar + "gv_notifier_response.txt", ret);
                        }
                        catch (Exception ex3)
                        {
                            Trace.WriteLine("Print Header2 Error: " + ex3);
                        }
                    }
                    catch (Exception ex2)
                    {
                        Trace.WriteLine("Print Header Error: " + ex2);
                    }
                    throw;
                }
            }
        }

        private void PrintHeaders(WebHeaderCollection webHeaderCollection)
        {
            if (webHeaderCollection != null)
            {
                Trace.WriteLine("Headers:");
                foreach (string key in webHeaderCollection.AllKeys)
                {
                    string[] values = webHeaderCollection.GetValues(key);
                    Trace.WriteLine(key + ": " + string.Join("; ", values.ToArray()));
                }
            }
        }

        public void Call(string to, string from)
        {
            if (RNR_SE != null || Authenticate())
            {
                to = to.Replace("(", "");
                to = to.Replace(")", "");
                to = to.Replace("-", "");
                to = to.Replace(" ", "");
                Trace.WriteLine("Connecting call " + from + " (local) to " + to);
                string p = "";

                if (UseMobile)
                {
                    p = "_rnr_se=" + System.Web.HttpUtility.UrlEncode(RNR_SE) + "&phone=" + from + "&number=" + to + "&call=Call";
                }
                else
                {
                    p = "outgoingNumber=%2B1" + to + "&forwardingNumber=%2B1" + from + "&subscriberNumber=undefined&remember=0&phoneType=1&_rnr_se=" + System.Web.HttpUtility.UrlEncode(RNR_SE);
                }

                byte[] data = Encoding.ASCII.GetBytes(p);


                HttpWebRequest request = HttpWebRequest.Create(UseMobile ? "https://www.google.com/voice/m/sendcall" : "https://www.google.com/voice/call/connect/") as HttpWebRequest;
                request.CookieContainer = jar;
                request.Method = "POST";
                request.AllowAutoRedirect = true;
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = p.Length;
                request.Referer = "https://www.google.com/voice/?gsessionid=" + GSessionID;
                
                Stream newStream = request.GetRequestStream();
                // Send the data.
                newStream.Write(data, 0, data.Length);
                newStream.Close();


                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                StreamReader reader = new StreamReader(response.GetResponseStream());

                string ret = reader.ReadToEnd();

                //Trace.WriteLine("-> " + response.ResponseUri.ToString());
            }
        }

        public void CancelCall()
        {
            if (RNR_SE != null || Authenticate())
            {

                Trace.WriteLine("Cancel Connect");
                string p = "outgoingNumber=undefined&forwardingNumber=undefined&cancelType=C2C&_rnr_se=" + System.Web.HttpUtility.UrlEncode(RNR_SE);

                byte[] data = Encoding.ASCII.GetBytes(p);


                HttpWebRequest request = HttpWebRequest.Create("https://www.google.com/voice/call/cancel/") as HttpWebRequest;
                request.CookieContainer = jar;
                request.Method = "POST";
                request.AllowAutoRedirect = true;
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = p.Length;


                Stream newStream = request.GetRequestStream();
                // Send the data.
                newStream.Write(data, 0, data.Length);
                newStream.Close();


                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                StreamReader reader = new StreamReader(response.GetResponseStream());

                string ret = reader.ReadToEnd();

                //Trace.WriteLine("-> " + response.ResponseUri.ToString());
            }
        }



        public void Stop()
        {
            UserName = "";
            Password = "";
            // quit sms mon
        }






        public delegate void SMSMessageReceived(SMSThread thread, SMSMessage msg);

        public event SMSMessageReceived OnSms;
        //
        //string txt = System.IO.File.ReadAllText("f:\\sms.txt");

        public List<SMSThread> Threads = new List<SMSThread>();

        public List<SMSThread> OldThreads = null;

        public List<Phone> Phones = new List<Phone>();

        private Thread worker = null;

        private Thread vWorker = null;

        public List<Voicemail> vThreads = new List<Voicemail>();
        public List<Voicemail> vOldThreads = null;

        public delegate void VoicemailReceived(Voicemail vm);
        public event VoicemailReceived OnVoicemail;

        public static int QuickCount = 0;

        internal void StartMonitor()
        {
            if (worker != null)
            {
                Trace.WriteLine("SMS Monitor already running");
            }
            else
            {
                worker = new Thread(WorkerStart);
                worker.Start();
            }

            if (vWorker != null)
            {
                Trace.Write("Voicemail monitor already running");
            }
            else
            {
                vWorker = new Thread(VWorkerStart);
                vWorker.Start();
            }
        }

        public void CheckNow()
        {
            CheckImmediately = true;
        }

        private void WorkerStart()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        Trace.WriteLine("Checking SMS ...");
                        HttpWebRequest request = HttpWebRequest.Create("https://www.google.com/voice/inbox/recent/sms/") as HttpWebRequest;
                        request.CookieContainer = jar;
                        request.AllowAutoRedirect = true;
                        HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                        StreamReader reader = new StreamReader(response.GetResponseStream());

                        string ret = reader.ReadToEnd();

                        //Trace.WriteLine("-> " + response.ResponseUri.ToString());

                        //File.WriteAllText("sms.xml", ret);

                        Update(ret);

                        //Trace.WriteLine("Comparing SMS...");
                        CompareSMS();
                        Trace.WriteLine("SMS Update Complete");
                    }
                    catch (IOException ex)
                    {
                        Trace.WriteLine("Error in sms request: " + ex);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("2 Error in sms request: " + ex);
                    }

                    if (fireCounter < 3)
                    {
                        QuickCount = 1;
                    }

                    int quantums = LongDelay;

                    if (QuickCount > 0)
                    {
                        //Trace.WriteLine("Sleeping for " + ShortDelay + " seconds");
                        quantums = ShortDelayN;
                    }
                    else
                    {
                        //Trace.WriteLine("Sleeping for " + LongDelay + " seconds");
                    }


                    while (quantums > 0)
                    {
                        Thread.Sleep(1000);
                        quantums--;
                        if (QuickCount > 0)
                        {
                            quantums = Math.Min(quantums, ShortDelay);
                            QuickCount--;
                        }
                        if (CheckImmediately)
                        {
                            CheckImmediately = false;
                            quantums = 0;
                            break;
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("SMS Worker Exiting: " + ex);
                worker = null;
                StartMonitor();
            }
            finally
            {
                worker = null;
                
            }
        }

        private void VWorkerStart()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        Trace.WriteLine("Checking Voicemail ...");
                        HttpWebRequest request = HttpWebRequest.Create("https://www.google.com/voice/inbox/recent/voicemail/") as HttpWebRequest;
                        request.CookieContainer = jar;
                        request.AllowAutoRedirect = true;
                        HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                        StreamReader reader = new StreamReader(response.GetResponseStream());

                        string ret = reader.ReadToEnd();

                        //Trace.WriteLine("-> " + response.ResponseUri.ToString());

                        //File.WriteAllText("sms.xml", ret);

                        UpdateVM(ret);

                        //Trace.WriteLine("Comparing VM...");
                        CompareVM();
                        Trace.WriteLine("Voicemail Update Complete");
                    }
                    catch (IOException ex)
                    {
                        Trace.WriteLine("Error in sms request: " + ex);
                    }


                    Thread.Sleep(VMDelay * 1000 * 60);

                    /*

                    int quantums = LongDelay;

                    if (QuickCount > 0)
                    {
                        Trace.WriteLine("Sleeping for " + ShortDelay + " seconds");
                        quantums = ShortDelayN;
                    }
                    else
                    {
                        Trace.WriteLine("Sleeping for " + LongDelay + " seconds");
                    }


                    while (quantums > 0)
                    {
                        Thread.Sleep(1000);
                        quantums--;
                        if (QuickCount > 0)
                        {
                            quantums = Math.Min(quantums, ShortDelay);
                            QuickCount--;
                        }
                    }

                    */
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("VM Worker Exiting: " + ex);
                vWorker = null;
                StartMonitor();
            }
            finally
            {
                vWorker = null;

            }
        }

        private void CompareSMS()
        {
            fireCounter++;
            if (OldThreads == null)
            {
                OldThreads = Threads;
            }
            else
            {
                foreach (SMSThread newThread in Threads)
                {
                    // check if all newThreads exist in OldThreads
                    bool found = false;

                    foreach (SMSThread oldThread in OldThreads)
                    {
                        if (oldThread.ID == newThread.ID)
                        {
                            found = true;

                            // check that messages match

                            if (oldThread.Messages.Count != newThread.Messages.Count)
                            {
                                // count mismatch

                                for (int i = oldThread.Messages.Count; i < newThread.Messages.Count; i++)
                                {
                                    oldThread.Messages.Add(newThread.Messages[i]);
                                    Trace.WriteLine("New SMS Message.");
                                    if (newThread.Messages[i].From == "Me")
                                    {
                                        Trace.WriteLine("Got new message - but it is from me.");
                                    }
                                    else
                                    {
                                        if (OnSms != null && fireCounter > 2)
                                        {
                                            OnSms.Invoke(newThread, newThread.Messages[i]);
                                        }
                                    }
                                }
                            }


                            break;
                        }
                    }

                    if (!found)
                    {
                        OldThreads.Add(newThread);
                        // entire thread is new.
                        Trace.WriteLine("New SMS Thread.");
                        foreach (SMSMessage m in newThread.Messages)
                        {
                            Trace.WriteLine("New SMS Message.");
                            if (m.From == "Me")
                            {
                                Trace.WriteLine("New SMS message is from me");
                            }
                            else
                            {
                                if (OnSms != null && fireCounter > 2)
                                {
                                    OnSms.Invoke(newThread, m);
                                }
                            }
                        }
                    }
                }


                //OldThreads = Threads;
            }
        }
        int fireCounter = 0;
        private void CompareVM()
        {
            
            if (vOldThreads == null)
            {
                vOldThreads = vThreads;
            }
            else
            {
                foreach (Voicemail newThread in vThreads)
                {
                    // check if all newThreads exist in OldThreads
                    bool found = false;

                    foreach (Voicemail oldThread in vOldThreads)
                    {
                        if (oldThread.ID == newThread.ID)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // entire thread is new.
                        if (OnVoicemail != null && fireCounter > 1)
                        {
                            OnVoicemail.Invoke(newThread);
                        }
                    }
                }


                vOldThreads = vThreads;
            }
        }


        public void Update(string xml)
        {
           // Trace.WriteLine("Updating SMS from XML");

            try
            {
                Threads = new List<SMSThread>();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                XmlNodeList xnl =  doc.GetElementsByTagName("html");

                if (xnl.Count > 0)
                {
                    XmlNode me = xnl[0];

                    xml = me.InnerXml.Trim();

                    if (xml.StartsWith("<![CDATA["))
                    {
                        // Trace.WriteLine("Removing CDATA");
                        xml = xml.Substring("<![CDATA[".Length);
                        xml = xml.Substring(0, xml.Length - 3);
                        xml = xml.Trim();
                    }
                    else
                    {
                        Trace.WriteLine("No CDATA present!");
                    }

                    object[] oPageText = { xml };
                    HTMLDocument dx = new HTMLDocumentClass();
                    IHTMLDocument2 doc2 = (IHTMLDocument2)dx;
                    doc2.write(oPageText);

                    IHTMLElementCollection ihec =  dx.getElementsByTagName("div");

                    foreach (IHTMLElement e in ihec)
                    {
                        if (e.id != null && e.id.Length > 25)
                        {
                            Trace.WriteLine(e.id);
                            if (e.innerHTML != null && e.innerHTML != "")
                            {
                                try
                                {
                                    SMSThread th = new SMSThread(e.innerHTML, this);
                                    th.ID = e.id;
                                    Threads.Add(th);
                                }
                                catch (Exception ex)
                                {
                                    Trace.WriteLine("Error ele: " + ex);
                                }
                            }
                            else
                            {
                                Trace.WriteLine("NULL DIV");
                            }
                        }
                    }

                    foreach (SMSThread s in Threads)
                    {
                        Trace.WriteLine("SMS: " + s.Contact.Name + " " + s.Messages.Count);
                    }
                }
                else
                {
                    Trace.WriteLine("Couldn't find HTML node in SMS XML");
                }
            }
            catch (Exception ex)
            {
                Trace.Write("XError loading SMS: " + ex);
            }
        }



        public void UpdateVM(string xml)
        {
            //Trace.WriteLine("Updating VM from XML");
            vThreads = new List<Voicemail>();

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(xml);

                XmlNodeList xnl = doc.GetElementsByTagName("html");

                if (xnl.Count > 0)
                {
                    XmlNode me = xnl[0];

                    xml = me.InnerXml.Trim();

                    if (xml.StartsWith("<![CDATA["))
                    {
                        //Trace.WriteLine("Removing CDATA");
                        xml = xml.Substring("<![CDATA[".Length);
                        xml = xml.Substring(0, xml.Length - 3);
                        xml = xml.Trim();
                    }
                    else
                    {
                        Trace.WriteLine("No CDATA!");
                    }

                    object[] oPageText = { xml };
                    HTMLDocument dx = new HTMLDocumentClass();
                    IHTMLDocument2 doc2 = (IHTMLDocument2)dx;
                    doc2.write(oPageText);

                    IHTMLElementCollection ihec = dx.getElementsByTagName("div");

                    foreach (IHTMLElement e in ihec)
                    {
                        if (e.id != null && e.id.Length > 25)
                        {
                            //Trace.WriteLine("VMID: " + e.id);
                            if (e.innerHTML != null && e.innerHTML != "")
                            {
                                Voicemail th = new Voicemail(e.innerHTML, this);
                                th.ID = e.id;

                                if (th.Transcript.ToLower() == "transcription in progress")
                                {
                                    Trace.WriteLine("Got a new voicemail, waiting for transcript");
                                }
                                else
                                {
                                    vThreads.Add(th);
                                }
                                
                                
                            }
                        }
                    }

                    foreach (Voicemail s in vThreads)
                    {
                        Trace.WriteLine("VM: " + s.Contact.Name);
                    }
                }
                else
                {
                    Trace.WriteLine("Couldn't find HTML node in SMS XML");
                }
            }
            catch (Exception ex)
            {
                Trace.Write("XError loading SMS: " + ex);
            }
        }





        public void UpdatePhone(string xml)
        {
            
            try
            {
                Phones.Clear();
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                XmlNodeList xnl = doc.GetElementsByTagName("html");

                if (xnl.Count > 0)
                {
                    XmlNode me = xnl[0];

                    xml = me.InnerXml.Trim();

                    if (xml.StartsWith("<![CDATA["))
                    {
                        //Trace.WriteLine("Removing CDATA");
                        xml = xml.Substring("<![CDATA[".Length);
                        xml = xml.Substring(0, xml.Length - 3);
                        xml = xml.Trim();
                    }
                    else
                    {
                        Trace.WriteLine("No CDATA!");
                    }

                    object[] oPageText = { xml };
                    HTMLDocument dx = new HTMLDocumentClass();
                    IHTMLDocument2 doc2 = (IHTMLDocument2)dx;
                    doc2.write(oPageText);

                    IHTMLElementCollection ihec = dx.getElementsByTagName("div");

                    foreach (IHTMLElement e in ihec)
                    {
                        //Trace.WriteLine("Phone: " + e.className + " " + e.id);
                        if (e.className == "gc-forwarding-number-ani goog-inline-block")
                        {
                            Phone p = new Phone();
                            p.Number = e.innerText;
                            Phones.Add(p);
                            Trace.WriteLine("Found Phone: " + p.Number);
                        }
                    }
                }
                else
                {
                    Trace.WriteLine("Couldn't find HTML node in Phone XML");
                }
            }
            catch (Exception ex)
            {
                Trace.Write("XError loading Phone: " + ex);
            }
        }
    }
}

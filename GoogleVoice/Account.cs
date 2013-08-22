using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace GoogleVoice
{
    public class GVLoginException : ApplicationException
    {
        public GVLoginException(string message) : base(message) { }
    }

    public class Account
    {
        public event Func<string> GetSMSPinFromUser;
        public event Action<GVCookie> SMSVCookieUpdated;
        public event Action<Message> OnMessage;
        public event Action<string> OnLoginMessage;

        public event Action Ready;
        public bool IsReady { get; private set; }

        // this indicates more than just caching
        // TODO clean this up, it shouldn't have side effects like not loading contacts
        public string Cache_Dir { get; private set; }

        public Feed_Voicemail VoiceMailFeed  { get; private set; }
        public Feed_Call PlacedCalls { get; private set; }
        public Feed_Call MissedCalls { get; private set; }
        public Feed_Call ReceivedCalls { get; private set; }
        public Feed_SMS SMS { get; private set; }

        public List<ForwardingPhone> ForwardingPhones { get; private set; }

        // TODO let's just expose Contacts
        public ContactsManager ContactsManager { get; private set; }

        public string UserName { get; private set; }
        public string Number { get; private set; }

        private int AppVersion { get; set; }
        Dictionary<string, long> Counts = new Dictionary<string, long>();

        private string Password = "";
        private string Token_GVX = "";
        private string Token_RNR_SE_MustEncode = "";
        private Cookie SMSV = null;

        private HttpSession http = new HttpSession();
        
        /// <summary>
        /// Create a Google Voice account
        /// </summary>
        /// <param name="UserName">Full Google Voice username</param>
        /// <param name="Password"></param>
        /// <param name="Cache_Dir">NULL to disable contacts and extra feed sync</param>
        public Account(string UserName, string Password, GVCookie SMSV, string Cache_Dir)
        {
            this.UserName = UserName;
            this.Password = Password;
            ForwardingPhones = new List<ForwardingPhone>();

            if (Cache_Dir != null)
            {
                this.Cache_Dir = Cache_Dir;
                ContactsManager = new ContactsManager(Cache_Dir, UserName, http);
            }
            
            // load our 2-step verification cookie.  We won't persist login cookies, just
            // the long-term (30 days) cookie.
            if (SMSV != null)
            {
                this.SMSV = SMSV.ToCookie();
                http.Initialize(this.SMSV);
            }
        }

        internal void OnMessage_Internal(Message m)
        {
            if (IsReady)
            {
                if (OnMessage != null)
                    OnMessage(m);
            }
            // else we just drop the messages, they can be picked up from Feed.Messages
        }

        void OnLoginMessage_Internal(string msg)
        {
            if (OnLoginMessage != null)
                OnLoginMessage(msg);
        }

        void SMSVCookieUpdated_Internal(GVCookie c)
        {
            if (SMSVCookieUpdated != null)
                SMSVCookieUpdated(c);
        }

        public void Login()
        {
            // fiddler2
            // GlobalProxySelection.Select = new WebProxy("127.0.0.1", 8888);

            Trace.WriteLine("GoogleVoice/Account/Login UserName: " + UserName);
            try
            {
                OnLoginMessage_Internal("Connecting...");
                HttpResult ret = http.Get("https://www.google.com/voice/m");

                if (ret.Uri.LocalPath == "/ServiceLogin")
                {
                    string Token_GALX = http.GetCookieString("GALX", "https://accounts.google.com/");
                    if (string.IsNullOrEmpty(Token_GALX))
                    {
                        // I'm pretty confident that we can't login without a GALX.
                        throw new GVLoginException("GALX missing");
                    }
                   
                    OnLoginMessage_Internal("Logging in...");
                    ret = http.Post("https://accounts.google.com/ServiceLoginAuth?service=grandcentral",
                        new Dictionary<string, string> {
                            {"ltmpl" , "mobile"},   // Probably don't need one of these
                            {"btmpl" , "mobile"},
                            {"followup", "https://www.google.com/voice/m?initialauth"},
                            {"continue", "https://www.google.com/voice/m?initialauth"},
                            {"service", "grandcentral"},
                            {"bgresponse","js_disabled"},
                            {"PersistentCookie", "yes"},
                            {"GALX", Token_GALX},
                            {"Email", UserName},
                            {"Passwd", Password},
                        });
                    if (ret.Uri.ToString() == "https://www.google.com/voice/m")
                    {
                        // we're logged in!
                        Token_GVX = http.GetCookieString("gvx", "https://www.google.com/voice/m");
                        if (string.IsNullOrEmpty(Token_GVX))
                        {
                            throw new GVLoginException("GVX is missing after primary redirect");
                        }
                    }
                    else if (ret.Uri.ToString().StartsWith("https://accounts.google.com/SmsAuth"))
                    {
                        // 2-step verification - SMS verification required.
                        Match sms = Regex.Match(ret.Page, "name=\"smsToken\".*?value=\"(.*?)\"", RegexOptions.Singleline);
                        if (sms.Success)
                        {
                            string smsToken = sms.Groups[1].Value;
                            string Token_UserSMSPin = GetSMSPinFromUser();

                            Trace.WriteLine("smsToken: " + smsToken);
                            Trace.WriteLine("User-Provided PIN: " + Token_UserSMSPin);
                            
                            ret = http.Post("https://accounts.google.com/SmsAuth?persistent=yes&service=grandcentral",
                                new Dictionary<string, string> {
                                    {"smsToken" , smsToken},
                                    {"smsUserPin" , Token_UserSMSPin},
                                    {"Email", UserName},
                                    {"smsVerifyPin", "Verify"},
                                    {"PersistentCookie", "yes"},
                                });

                            Cookie Token_SMSV = http.GetCookie("SMSV", "https://accounts.google.com/");
                            SMSVCookieUpdated_Internal(new GVCookie(Token_SMSV));
                            
                            // we're at a redirect landing page, so we need to build up a form and post it in
                            var form = Regex.Match(ret.Page, "action=\"(.*?)\"", RegexOptions.Singleline);
                            var fields = Regex.Matches(ret.Page, "input.*?name=\"(.*?)\" value=\"(.*?)\"", RegexOptions.Singleline);
                            if (form.Success && fields.Count > 0)
                            {
                                var dict = new Dictionary<string, string>();
                                foreach (Match m in fields)
                                {
                                    dict.Add(m.Groups[1].Value, m.Groups[2].Value);
                                }

                                ret = http.Post(form.Groups[1].Value, dict);
                                if (ret.Uri.ToString() == "https://www.google.com/voice/m")
                                {
                                    // we're logged in!
                                    Token_GVX = http.GetCookieString("gvx", "https://www.google.com/voice/m");
                                    if (string.IsNullOrEmpty(Token_GVX))
                                    {
                                        throw new GVLoginException("GVX is missing after validation response");
                                    }
                                }
                                else
                                {
                                    Trace.WriteLine("Didn't get expected redirect: " + ret.Page);
                                    throw new GVLoginException("Didn't get expected redirect");
                                }
                            }
                            else
                            {
                                Trace.WriteLine("Couldn't find form in page: " + ret.Page);
                                throw new GVLoginException("Couldn't find form in page");
                            }
                        }
                        else
                        {
                            Trace.WriteLine("Can't find smsToken in page: " + ret.Page);
                            throw new GVLoginException("Can't find smsToken in page");
                        }
                    }
                    else
                    {
                        // CheckCookie ... this is a redirect.
                        // TEST: Canadian Proxy: 199.185.95.42 8080
                        Match m = Regex.Match(ret.Page, "href=\"(.*?)\"");
                        if (m.Success)
                        {
                            // NOTE:  this page has URLs that must be HtmlDecoded!
                            OnLoginMessage_Internal("Redirecting...");
                            ret = http.Get(WebUtil.HtmlDecode(m.Groups[1].Value));
                            Token_GVX = http.GetCookieString("gvx", "https://www.google.com/voice/m");
                            if (string.IsNullOrEmpty(Token_GVX))
                            {
                                throw new GVLoginException("GVX is missing after redirect");
                            }
                        }
                        else
                        {
                            Trace.WriteLine("Couldn't find continue: " + ret.Page);
                            throw new GVLoginException("Couldn't find continue");
                        }
                    }
                }
                else
                {
                    Trace.WriteLine("Couldn't find ServiceLogin: " + ret.Page);
                    throw new GVLoginException("ServiceLogin not found");
                }

                try
                {
                    Match m = Regex.Match(ret.Page, @"appVersion: (.\d*)", RegexOptions.Singleline);
                    if (m.Success)
                    {
                        AppVersion = int.Parse(m.Groups[1].Value);
                    }
                }
                catch (FormatException ex)
                {
                    Trace.WriteLine("Can't get AppVersion: " + ex);
                }

                // We're going to fetch the lite mobile page, and pull out an RNR_SE, so we can make calls
                // the HTML5 calling interface is unsuitable for non-phone devices
                ret = http.Get("https://www.google.com/voice/m/");
                Match rnr = Regex.Match(ret.Page, "name=\"_rnr_se\" value=\"(.*?)\"");
                if (rnr.Success)
                {
                    // NOTE: we won't encode here, because Post'ing will encode it.
                    // but this value MUST be encoded before going out!
                    Token_RNR_SE_MustEncode = rnr.Groups[1].Value;
                }
            }
            catch (WebException ex)
            {
                Trace.WriteLine("Login Failed: " + ex);
                // We want to turn HTTP 401 and 403 into a login exception, otherwise the UI will 
                // assume we don't have a network connection.
                if ((ex.Response as HttpWebResponse).StatusCode == HttpStatusCode.Unauthorized ||
                    (ex.Response as HttpWebResponse).StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new GVLoginException("HTTP 401 or 403 - not authorized");
                }
                else throw;
            }
            catch (GVLoginException ex)
            {
                Trace.WriteLine("Cannot login: " + ex.Message);
                throw;
            }

            Trace.WriteLine("RNR_SE: " + Token_RNR_SE_MustEncode);
            Trace.WriteLine("GVX: " + Token_GVX);
            Trace.WriteLine("GV App Version: " + AppVersion);

            new Thread(() => LoadPhones()).Start();

            OnLoginMessage_Internal("Loading Contacts...");

            AddFeeds();
        }

        public void LoadPhones()
        {
            try
            {
                HttpResult ret = GVPost("https://www.google.com/voice/m/x?m=set&v=" + AppVersion);
                string json = ret.Page.Split('\n')[1];
                JObject o = JObject.Parse(json);

                Number = (o["settings_response"]["general_info"]["primary_did"] as JValue).Value.ToString();
                Trace.WriteLine("My Number: " + Number);

                foreach (var p in o["settings_response"]["user_preferences"]["forwarding"])
                {
                    ForwardingPhone fp = new ForwardingPhone(
                        (p["phone_number"] as JValue).Value.ToString(),
                        (p["name"] as JValue).Value.ToString(),
                        (p["type"] as JValue).Value.ToString());
                    ForwardingPhones.Add(fp);
                    Trace.WriteLine("My Phone: " + fp);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("GoogleVoice/Account/LoadPhones *** " + ex);
            }
        }

        public HttpResult GVPost(string url, bool logging = true)
        {
            return http.Post(url, "{\"gvx\":\"" + Token_GVX + "\"}", logging);
        }

        public byte[] GetFile(string url)
        {
            return http.GetFile(url);
        }

        private void AddFeeds()
        {
            // TODO the feed pattern uses a URL so we can create feeds with more pages
            // TODO investigate whether we really still need that for getting multiple pages
            // it might be worth delegating the authority to the feed tiself
            // additionally, there is a perf win if we can load as few new messages as possible
            // perhaps call with o=0&lm=1, and if there is a delta, load say 10
            VoiceMailFeed = new Feed_Voicemail(this);
            MissedCalls = new Feed_Call(this, "https://www.google.com/voice/m/x?m=list&l=missed&o=0&lm=10", Message.MessageType.Missed);
            PlacedCalls = new Feed_Call(this, "https://www.google.com/voice/m/x?m=list&l=placed&o=0&lm=10", Message.MessageType.Placed);
            ReceivedCalls = new Feed_Call(this, "https://www.google.com/voice/m/x?m=list&l=received&o=0&lm=10", Message.MessageType.Received);
            SMS = new Feed_SMS(this);

            new Thread(() => 
                {
                    Thread.CurrentThread.Name = "GV/Account/AddFeeds";
                    if (ContactsManager != null)
                    {
                        Thread contacts = new Thread(() =>
                        {
                            try
                            {
                                // need to login the first time
                                ContactsManager.Update();
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine("GoogleVoice/Account/AddFeeds/Contacts ***: " + ex);
                            }
                        });
                        contacts.Start();

                        // If we don't have any contacts, let's wait for the first contacts sync
                        // TODO fix this so contacts can come int at any time
                        // BUG a user with zero contacts will have to wait for sync every time
                        // since they have zero contacts, it should be quite quick, though.
                        if (ContactsManager.Contacts.Count == 0)
                        {
                            Trace.WriteLine("Waiting for contacts before feed sync");
                            contacts.Join();
                        }
                    }

                    // TODO I can't see why we need to repeat here, this seems very
                    // broken.

                    while (!IsReady)
                    {
                        try
                        {
                            Trace.WriteLine("GoogleVoice/Account/AddFeeds/ 1");

                            SMS.Update(false, "");
                            VoiceMailFeed.Update(false, "");
                            MissedCalls.Update(false, "");
                            ReceivedCalls.Update(false, "");
                            PlacedCalls.Update(false, "");

                            // register for feeds
                            CheckForUpdate();

                            if (Cache_Dir != null) // gvar no extra feeds
                            {
                               
                                    // TODO bring this back - load a few pages of feeds so we have a bunch
                                    // of call history
                               
                            }
                            Trace.WriteLine("Finished updating feeds");
                            IsReady = true;
                            if (Ready != null) Ready();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine("GoogleVoice/Account/AddFeeds/Main ***: " + ex);
                            Thread.Sleep(1000);
                        }
                    }
                }).Start();
        }

        public void UpdateAsync()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.Name = "GV/Account/UpdateAsync";
                Update();
            }).Start();
        }

        public void UpdateContactsAync()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.Name = "GV/Account/UpdateContactsAsync";
                if (ContactsManager != null) ContactsManager.Update();
            }).Start();
        }

        public void CheckForUpdate()
        {
            // TODO: we need a critical section here so we don't get caught in a loop.
            HttpResult ret = GVPost("https://www.google.com/voice/m/x?m=list&l=all&o=0&lm=1", false);
            string json = ret.Page.Split('\n')[1];

            JObject o = JObject.Parse(json);
            foreach (var label in o["labels_response"]["label"])
            {
                string text = (label["label"] as JValue).Value.ToString();
                long total_count = long.Parse((label["last_modified_timestamp"] as JValue).Value.ToString());
                CheckCountChange(text, total_count);
            }
        }
        
        public void CheckCountChange(string label, long count)
        {
            var supported = new Dictionary<string, Feed> { 
                {"sms", SMS}, 
                {"missed", MissedCalls},
                {"placed", PlacedCalls},
                {"received", ReceivedCalls}, 
                {"voicemail", VoiceMailFeed} 
            };

            if (Counts.ContainsKey(label))
            {
                if (Counts[label] != count)
                {
                    if (supported.ContainsKey(label))
                    {
                        Trace.WriteLine(string.Format("Updating {0} - [{1} -> {2}]",
                            label, Counts[label], count));
                        try
                        {
                            supported[label].Update(true, "");
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine("Error upating feed: " + ex);
                        }
                        Counts[label] = count;
                    }
                }
                // else no change
            }
            else
            {
                // dump all of the feeds, not just the ones we'll look at.
                // maybe useful for debugging who has what feeds
                Trace.WriteLine("Registered: " + label + ": " + count);
                Counts.Add(label, count);
            }
        }

        public void Update()
        {
            if (!IsReady)
            {
                Trace.WriteLine("Not Updating, Feeds are not initialized");
            }
            else
            {
                this.Try(() => CheckForUpdate());
            }
        }

        // TODO this whole thing is incompatible with MVVM in some sense
        // we need to be able to hook up to a contact *after* the contact comes down
        public Contact ContactForNumber(string number)
        {
            if (ContactsManager == null) return null;
            // BUGFIX ToArray to prevent InvalidOperationException on collection modified
            var contact = ContactsManager.Contacts.ToArray().FirstOrDefault(
                c => c.Phones.Exists(
                    p => Util.CompareNumber(p.Number, number)
                    ));
            if (contact != null) return contact;

            // BUGFIX when numbers don't have CID, their number is: Unknown.(a long hex string)
            if (number.StartsWith("Unknown."))
                number = "Unknown";

            Contact cx = new Contact{Name =Util.FormatNumber(number)};
            cx.Phones.Add(new Phone { Number = number });
            return cx;
        }

        public void SendSMS(string number, string message)
        {
            number = Util.StripNumber(number);
            this.TryLogThrow("Sending SMS to: " + number, () => {
                HttpResult ret = GVPost(string.Format(
                    "https://www.google.com/voice/m/x?m=sms&n={0}&txt={1}",
                     WebUtil.UrlEncode(number), WebUtil.UrlEncode(message)));
                // TODO validate response, it's just JSON
                // NOTE:  I've never seen a failure in the response, only via
                // an HTTP exception...
                Trace.WriteLine("SMS Response: " + ret.Page);
            });
        }

        public void Call(string number, ForwardingPhone fwd)
        {
            if (string.IsNullOrEmpty(Token_RNR_SE_MustEncode))
            {
                Trace.WriteLine("Can't make calls, no RNR_SE token");
                throw new InvalidOperationException(@"No RNR_SE Token, can't make calls.  (See %localappdata%\GVNotifierWPF\log.txt for details)");
            }

            number = Util.StripNumber(number);
            this.TryLogThrow("GoogleVoice/Account/Call: " + number,
                () => {
                // NOTE:  the HTML5 call API will return a callback number, instead of starting
                // a callback - I can't see a way we can use that, so we must take a dependency
                // on the old site (or the old mobile site, if possible)
                HttpResult ret = http.Post("https://www.google.com/voice/call/connect/",
                    new Dictionary<string, string>
                    {
                        {"outgoingNumber", number},
                        {"forwardingNumber", fwd.Number},
                        {"phoneType", fwd.PhoneType.ToString()},
                        {"_rnr_se", Token_RNR_SE_MustEncode},
                        {"subscriberNumber", "undefined"},
                        {"remember", "0"},
                    });
                });
        }

        public void CancelCall()
        {
            this.TryLogThrow("GoogleVoice/Account/CancelCall",
            () =>
            {
                HttpResult ret = http.Post("https://www.google.com/voice/call/cancel/",
                    new Dictionary<string, string>
                    {
                        {"outgoingNumber","undefined"},
                        {"forwardingNumber","undefined"},
                        {"cancelType","C2C"},
                        {"_rnr_se", Token_RNR_SE_MustEncode},
                    });
            });
        }

        // string po = "timeStmp=&secTok=&smsToken=" + smsToken + "&email=" + WebUtil.UrlDecode(UserName) + "&smsUserPin=" + Token_UserSMSPin + "&smsVerifyPin=Verify&PersistentCookie=yes";
        // string p = "ltmpl=mobile&continue=https%3A%2F%2Fwww.google.com%2Fvoice%2Fm&service=grandcentral&nui=5&dsh=-8482690240302161516&ltmpl=mobile&btmpl=mobile&ltmpl=mobile&timeStmp=&secTok=&GALX=" + WebUtil.UrlEncode(Token_GALX) + "&Email=" + WebUtil.UrlEncode(UserName) + "&Passwd=" + WebUtil.UrlEncode(Password) + "&PersistentCookie=yes&rmShown=1&signIn=Sign+in";
        /* string p = "outgoingNumber=" + WebUtil.UrlEncode(number) +
            "&forwardingNumber=" + fwd.Number +
            "&subscriberNumber=undefined&remember=0" +
            "&phoneType=" + fwd.PhoneType +
            "&_rnr_se=" + Token_RNR_SE;       
         */
        // "outgoingNumber=undefined&forwardingNumber=undefined&cancelType=C2C&_rnr_se=" + Token_RNR_SE);
        /*
         *                 string p = "outgoingNumber=" + WebUtil.UrlEncode(number) +
            "&forwardingNumber=" + fwd.Number +
            "&subscriberNumber=undefined&remember=0" +
            "&phoneType=" + fwd.PhoneType +
            "&_rnr_se=" + Token_RNR_SE_MustEncode;    */
    }
}

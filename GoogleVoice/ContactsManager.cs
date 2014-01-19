using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace GoogleVoice
{
    public class Contact : INotifyPropertyChanged
    {
        private string _Name = "";
        public string Name
        {
            get { return _Name; }
            set
            {
                if (value != _Name)
                {
                    _Name = value;
                    Changed("Name");
                }
            }
        }

        public string ID { get; set; }
        public string ImageETag { get; set; }
        public List<Phone> Phones = new List<Phone>();

        private string _ImageLocation = "";
        public string ImageLocation
        {
            get { return _ImageLocation; }
            set
            {
                if (value != _ImageLocation)
                {
                    _ImageLocation = value;
                    Changed("ImageLocation");
                }
            }
        }

        private string _Group = "";
        public string Group
        {
            get { return _Group; }
            set
            {
                if (value != _Group)
                {
                    _Group = value;
                    Changed("Group");
                }
            }
        }

        private static int _MaxIconSize = 0;
        public static int MaxIconSize
        {
            get
            {
                return _MaxIconSize;
            }

            set
            {
                if (_MaxIconSize != value)
                {
                    _MaxIconSize = value;
                    //Changed("MaxIconSize");
                }
            }
        }

        public Contact()
        {
            Group = "Other Contacts";
        }

        public override string ToString()
        {
            return Name;
        }

        public bool HasNumber(string number)
        {
            return Phones.Exists(p => Util.CompareNumber(p.Number, number));
        }

        private void Changed(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public string Note { get; set; }
    }

    public class Phone
    {
        public string Number { get; set; }
        public string Type { get; set; }

        public override string ToString()
        {
            return Util.FormatNumber(Number) + " (" + Type + ")";
        }
    }

    class ContactComparer : IComparer<Contact>
    {
        public int Compare(Contact c1, Contact c2)
        {
            if (c1 != null && c2 != null)
            {
                return c2.Name.CompareTo(c1.Name);
            }
            return 1;
        }
    }

    public class ContactsManager
    {
        public GVObservableCollectionEx<Contact> Contacts = null;

        public delegate void ProgressUpdate(int progressValue, int maxValue);
        public event ProgressUpdate ContactsLoadingUpdate;

        private string ImageDir = "";
        private string UserName = "";
        
        private HttpSession http { get; set; }

        // TODO this should be fixed along with Cache_Dir indicating things
        public bool LoadImages = true;

        public event Action OnContactsUpdated;

        internal ContactsManager(string ImageDir, string UserName, HttpSession http)
        {
            ContactsLoadingUpdate = delegate { }; // suppress warning

            this.UserName = UserName;
            this.ImageDir = ImageDir;
            this.http = http;
            try
            {
                var memoryStream = new FileStream(ImageDir + "\\" + UserName + "_contacts.xml", FileMode.OpenOrCreate);
                Contacts = new GVObservableCollectionEx<Contact>((List<Contact>)new XmlSerializer(typeof(List<Contact>)).Deserialize(memoryStream));
                Contacts.Sort(new ContactComparer());
            }
            catch (Exception ex)
            {
                Contacts = new GVObservableCollectionEx<Contact>();
                Trace.WriteLine("GoogleVoice/ContactsManager/ " + ex);
            }
        }

        public void Save()
        {
            try
            {
                MemoryStream memoryStream = new MemoryStream();
                System.Xml.XmlTextWriter xmlTextWriter = new System.Xml.XmlTextWriter(memoryStream, Encoding.UTF8);
                new XmlSerializer(typeof(List<Contact>)).Serialize(xmlTextWriter, (object)Contacts.ToList());
                memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                File.WriteAllText(ImageDir + "\\" + UserName + "_contacts.xml", new UTF8Encoding().GetString(memoryStream.ToArray()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("GoogleVoice/ContactsManager/SaveXml " + ex);
            }
        }

        public void Update()
        {
            try
            {
                List<Contact> OldContacts = new List<Contact>(Contacts);

                HttpResult ret = http.Get("https://www.google.com/voice?ui=desktop");
                var m = Regex.Match(ret.Page, @"_gcData.*?\=(.*?)_gvRun", RegexOptions.Singleline);
                if (m.Success)
                {
                    // hack: :(
                    var json = m.Groups[1].Value.Replace('\'', '"');
                    json = Regex.Replace(json, "\"flags\":\\s*{\\s*};", "", RegexOptions.Singleline);

                    var o = JObject.Parse(json);

                    var contacts = o["contacts"];

                    string BasePhotoUrl = "http://www.google.com/s2/b/0"; // (Body["UserData"]["PhotoUrl"] as JValue).Value.ToString();
                    Trace.WriteLine("Base Photo URL: " + BasePhotoUrl);

                    // We'll fail on any bad contact, because we want to FailFast here
                    // that way, the user will not get any contacts, and hopefully report
                    // the bug to us.
                    foreach (var cx in contacts)
                    {
                        var contact = cx.First;

                        Trace.WriteLine(contact);

                        string ID = (contact["contactId"] as JValue).Value.ToString();
                        string Name = (contact["name"] as JValue).Value.ToString();


                        bool shouldAdd = false;
                        Contact c = Contacts.SingleOrDefault(x => x.ID == ID);
                        if (c == null)
                        {
                            c = new Contact();
                            shouldAdd = true;
                        }
                        else
                        {
                            OldContacts.Remove(c);
                        }

                        c.ID = ID;
                        c.Name = WebUtil.HtmlDecode(Name);

                        if (c.Name.Contains("Microsoft"))
                        {
                           // Debugger.Break();
                        }

                        c.Phones.Clear(); // kill old phones.
                        foreach (var ph in (JArray)contact["numbers"])
                        {
                            try
                            {
                                if (ph.ToString().Contains("phoneType"))
                                {
                                    c.Phones.Add(new Phone
                                    {
                                        Number = (ph["phoneNumber"] as JValue).Value.ToString(),
                                        Type = (ph["phoneType"] as JValue).Value.ToString()
                                    });
                                }
                                else
                                {
                                    // NOTE: 5/5/2012
                                    // Contacts with 'Custom' label don't have a phoneType
                                    c.Phones.Add(new Phone
                                    {
                                        Number = (ph["phoneNumber"] as JValue).Value.ToString(),
                                        Type = "Unknown",
                                    });
                                }
                            }
                            catch (NullReferenceException)
                            {
                                Debugger.Break();
                                // no phoneType = GV number we should ignore
                            }
                        }

                        if (LoadImages)
                        {
                            try
                            {
                                string photoUrl = (contact["photoUrl"] as JValue).Value.ToString();
                                var r_ImgID = Regex.Match(photoUrl, ".*/(.*?)$", RegexOptions.Singleline);
                                string ImgID = "";
                                if (!r_ImgID.Success)
                                {
                                    ImgID = Util.SafeFileName(photoUrl);
                                }
                                else
                                {
                                    ImgID = Util.SafeFileName(r_ImgID.Groups[1].Value);
                                }
                                if (!string.IsNullOrEmpty(ImgID))
                                {
                                    string save = ImageDir + ImgID + ".jpg";
                                    try
                                    {
                                        if (!File.Exists(save))
                                        {
                                            var imageBytes = http.GetFile(BasePhotoUrl + photoUrl);
                                            Trace.WriteLine("Saving: " + save);
                                            File.WriteAllBytes(save, imageBytes);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // if we fail to save, we don't want to attempt it every time the contacts sync
                                        // so we'll just save anyway.
                                        // TODO consider failed=true property on contact download
                                        Trace.WriteLine("GoogleVoice/ContactsManager/Update/Photo Save Error: " + ex.Message);
                                    }
                                    c.ImageLocation = save;
                                    c.ImageETag = photoUrl;
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine("Photo: " + ex.Message);
                            }
                        }

                        if (shouldAdd) Contacts.Add(c);
                    }

                    foreach (Contact deletedContact in OldContacts)
                    {
                        Debug.WriteLine("Removing orphaned contact: " + deletedContact);
                        Contacts.Remove(deletedContact);
                    }

                    Contacts.Sort(new ContactComparer());

                    if (OnContactsUpdated != null) OnContactsUpdated();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error updating contacts: " + ex);
            }
        }


        // Export system
        class CsvContact
        {
            string[] PhoneKeys = { 
                "Primary Phone", 
                "Home Phone", 
                "Home Phone 2", 
                "Mobile Phone", 
                "Pager", 
                "Home Fax", 
                "Company Main Phone", 
                "Business Phone", 
                "Business Phone 2", 
                "Business Fax", 
                "Assistant's Phone", 
                "Other Phone", 
                "Other Fax", 
                "Callback", 
                "Car Phone", 
                "ISDN", 
                "Radio Phone", 
                "TTY/TDD Phone", 
                "Telex", 
            };


            public class CsvPhone
            {
                public string Number { get; set; }
                public string Type { get; set; }
            }
            // First Name + Last Name
            // Company
            public string Name { get; set; }
            public string ID
            {
                get { return Name.GetHashCode().ToString(); }
            }
            public List<CsvPhone> Phones { get; set; }

            public CsvContact(string[] cols, string[] row)
            {
                Name = row[Lookup(cols, "First Name")] + " " + row[Lookup(cols, "Last Name")];
                Name = Name.Trim();
                if (string.IsNullOrEmpty(Name.Trim()))
                {
                    Name = row[Lookup(cols, "Company")].Trim();
                    if (string.IsNullOrEmpty(Name.Trim()))
                    {
                        throw new InvalidDataException("Bad contact name");
                    }
                }

                Phones = new List<CsvPhone>();

                foreach (var type in PhoneKeys)
                {
                    string num = row[Lookup(cols, type)];
                    if (!string.IsNullOrEmpty(num.Trim()))
                    {
                        Phones.Add(new CsvPhone { Number = num, Type = type });
                    }
                }
            }

            private int Lookup(string[] cols, string key)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i].Trim().ToLower() == key.ToLower())
                    {
                        return i;
                    }
                }
                throw new KeyNotFoundException(key);
            }

            public void AddPhone(CsvPhone newPhone)
            {
                bool found = false;
                foreach (var existingPhone in Phones)
                {
                    if (Util.StripNumber(newPhone.Number) == Util.StripNumber(existingPhone.Number))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Phones.Add(newPhone);
                }
            }

            public void AddPhones(List<CsvPhone> phones)
            {
                foreach (var phone in phones)
                {
                    AddPhone(phone);
                }
            }
        }


        List<CsvContact> GetContacts(string contactsData)
        {
            var ms = new MemoryStream();
            var stringBytes = System.Text.Encoding.UTF8.GetBytes(contactsData);
            ms.Write(stringBytes, 0, stringBytes.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(ms);
            parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
            parser.SetDelimiters(new string[] { "," });

            string[] cols = parser.ReadFields();

            List<CsvContact> contacts = new List<CsvContact>();

            while (!parser.EndOfData)
            {
                string[] row = parser.ReadFields();
                try
                {
                    CsvContact contact = new CsvContact(cols, row);

                    var existingEntry = contacts.FirstOrDefault(c => c.ID == contact.ID);
                    if (existingEntry != null)
                    {
                        // merge instead of adding
                        existingEntry.AddPhones(contact.Phones);
                    }
                    else
                    {
                        contacts.Add(contact);
                    }
                }
                catch (InvalidDataException)
                {
                    // Contact doesn't have a good enough name
                }
            }
            return contacts;
        }
    }
}

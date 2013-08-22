using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public class SMSContact
    {
        public override string ToString()
        {
            return Name + " [" + Phone.Number + "/" + Phone.Type + "]";
        }

        public SMSContact()
        {
            Name = "";
            Phone = new Phone();
        }

        public string Name { get; set; }
        public Phone Phone { get; set; }

        public void ParseNumberWithType(string n)
        {
            if (n.Trim().StartsWith("-") && n.Length > 2)
            {
                Phone.Type = n.Substring(2);
            }
            else if (n.IndexOf(" - ") > -1)
            {
                string[] p = n.Split(new string[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length > 1)
                {
                    Phone.Number = p[0].Trim();
                    Phone.Type = p[p.Length - 1].Trim();
                }
            }
            else
            {
                Phone.Number = n;
            }
        }
    }
}

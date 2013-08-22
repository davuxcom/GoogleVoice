using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public class ForwardingPhone
    {
        public string Number { get; private set; }
        public string Label { get; private set; }

        public int PhoneType { get; private set; }
        /*
        {
            get
            {
                string type = Label.ToLower().Trim();

                if (type.Contains("home")) return 1;
                if (type.Contains("mobile")) return 2;
                if (type.Contains("work")) return 3;
                if (type.Contains("gizmo")) return 7;
                if (type.Contains("google talk")) return 9;
                return 1;
            }
        }
        */

        public ForwardingPhone(string Number, string Label, string type)
        {
            this.Number = Number;
            this.Label = Label;
            this.PhoneType = int.Parse(type);
        }

        public override string ToString()
        {
            return Util.FormatNumber(Number) + " (" + Label + ")";
        }
    }
}

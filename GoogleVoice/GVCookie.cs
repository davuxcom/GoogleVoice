using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace GoogleVoice
{
    // sadly, you can't XmlSerialize a System.Uri (and thus a System.Net.Cookie, so we wrap it.
    public class GVCookie
    {
        public GVCookie() { }

        public GVCookie(Cookie copy)
        {
            this.Domain = copy.Domain;
            this.Expires = copy.Expires;
            this.Name = copy.Name;
            this.Path = copy.Path;
            this.Secure = copy.Secure;
            this.Value = copy.Value;
        }

        public Cookie ToCookie()
        {
            return new Cookie
            {
                Domain = Domain,
                Expires = Expires,
                Name = Name,
                Path = Path,
                Secure = Secure,
                Value = Value,
            };
        }

        public string Name { get; set; }
        public string Domain { get; set; }
        public DateTime Expires { get; set; }
        public string Path { get; set; }
        public bool Secure { get; set; }
        public string Value { get; set; }
    }
}

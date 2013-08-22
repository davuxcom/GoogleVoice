using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public abstract class Feed
    {
        public string Name { get; protected set; }

        protected Account Account = null;

        public List<Message> Messages = new List<Message>();

        internal Feed(Account Account)
        {
            this.Name = "Unknown Feed";
            this.Account = Account;
        }

        internal abstract void Update(bool verbose, string page);

        internal bool SeenMessage(Message newMessage)
        {
            if (Messages.Exists(m => m.ID == newMessage.ID)) return true;
            Messages.Add(newMessage);
            return false;
        }
    }
}

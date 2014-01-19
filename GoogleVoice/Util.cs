using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace GoogleVoice
{
    public class Util
    {
        public static bool CompareNumber(string n1, string n2)
        {
            // make +18605551234 equal 8605551234
            n1 = StripNumber(n1).Replace("+", "");
            n2 = StripNumber(n2).Replace("+", "");
            
            if (n1.Length == 11 && n1.StartsWith("1"))
            {
                n1 = n1.Substring(1);
            }
            if (n2.Length == 11 && n2.StartsWith("1"))
            {
                n2 = n2.Substring(1);
            }
            return n1 == n2;
        }

        public static string StripNumber(string number)
        {
            if (number.Contains("@"))
            {
                // handle Google Talk forwarding number case.
                return number;
            }
            else
            {
                // other numbers
                return Regex.Replace(number, @"[^\d|\+]", "");
            }
        }

        public static string FormatNumber(string number)
        {
            if (number == null) return "";
            // this took a long time to get "right" (I guess it's right)
            // lots of emails from people outside the USA:
            // international numbers are left unformatted.
            if (number.StartsWith("+") && !number.StartsWith("+1")) return number;

            if (Regex.IsMatch(number, "^[0-9|\\+]+$"))
            {
                if (number.Length == 7)
                {
                    return Regex.Replace(number, @"(\d{3})(\d{4})", "$1-$2");
                }
                else
                {
                    return Regex.Replace(number, @"(\d{3})(\d{3})(\d{4})$", " ($1) $2-$3").Trim();
                }
            }
            return number;
        }

        public static string SafeFileName(string unsafeFileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                unsafeFileName = unsafeFileName.Replace(c.ToString(), "");
            foreach (char c in Path.GetInvalidPathChars())
                unsafeFileName = unsafeFileName.Replace(c.ToString(), "");
            return unsafeFileName;
        }
    }

    // TODO see if we can deprecate this
    // this may have been here because of the Silverlight port
    // there is a version in DavuxLib2 that is probably better?
    // the worst part is that this isn't really even threadsafe!
    public class GVObservableCollectionEx<T> : ObservableCollection<T>   
    {
        public void Sort()
        {
            this.Sort(0, Count, null);   
        }

        public void Sort(IComparer<T> comparer)
        {
            this.Sort(0, Count, comparer);
        }

        public void Sort(int index, int count, IComparer<T> comparer)
        {
            (Items as List<T>).Sort(index, count, comparer);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void AddRange( T[] items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        public GVObservableCollectionEx()
        {
            _currentDispatcher = Dispatcher.CurrentDispatcher;
        }

        public GVObservableCollectionEx(System.Collections.Generic.IEnumerable<T> collection)
            :base(collection)
        {
            _currentDispatcher = Dispatcher.CurrentDispatcher;
        }

        private readonly Dispatcher _currentDispatcher;

        private void DoDispatchedAction(Action action)
        {
            if (_currentDispatcher.CheckAccess())
                action.Invoke();
            else
                _currentDispatcher.Invoke(DispatcherPriority.DataBind, action);
        }

        protected override void ClearItems()
        {
            DoDispatchedAction(BaseClearItems);
        }

        private void BaseClearItems()
        {
            base.ClearItems();
        }

        protected override void InsertItem(int index, T item)
        {
            DoDispatchedAction(() => BaseInsertItem(index, item));
        }

        private void BaseInsertItem(int index, T item)
        {
            base.InsertItem(index, item);
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            DoDispatchedAction(() => BaseMoveItem(oldIndex, newIndex));
        }

        private void BaseMoveItem(int oldIndex, int newIndex)
        {
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void RemoveItem(int index)
        {
            DoDispatchedAction(() => BaseRemoveItem(index));
        }

        private void BaseRemoveItem(int index)
        {
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            DoDispatchedAction(() => BaseSetItem(index, item));
        }

        private void BaseSetItem(int index, T item)
        {
            base.SetItem(index, item);
        }

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DoDispatchedAction(() => BaseOnCollectionChanged(e));
        }

        private void BaseOnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            DoDispatchedAction(() => BaseOnPropertyChanged(e));
        }

        private void BaseOnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
        }
    }
}

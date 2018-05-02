using System;
using System.Collections.Generic;

namespace HomeGenie.Service
{
    [Serializable()]
    public class TsList<T> : List<T>
    {
        private object syncLock = new object();

        public object LockObject
        {
            get { return syncLock; }
        }

        public new void Clear()
        {
            lock (syncLock)
                base.Clear();
        }

        public new void Add(T value)
        {
            lock (syncLock)
                base.Add(value);
        }

        public new void RemoveAll(Predicate<T> predicate)
        {
            lock (syncLock)
                base.RemoveAll(predicate);
        }

        public new void Remove(T item)
        {
            lock (syncLock)
                base.Remove(item);
        }

        public new void Sort(Comparison<T> comparison)
        {
            lock (syncLock)
                base.Sort(comparison);
        }
    }
}
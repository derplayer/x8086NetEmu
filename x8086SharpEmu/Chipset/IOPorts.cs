using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;
using System.Windows.Forms;

using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class IOPorts : IList<IOPortHandler>
    {

        private X8086 emulator;
        private List<IOPortHandler> list;

        //public IOPortHandler this[int index] {
        //    get { return this.list[index]; }
        //    set { list[index] = value; }
        //}

        public IOPortHandler this[int index]
        {
            get
            {
                return list[index];
            }
            set
            {
                list[index] = value;
            }
        }

        public IOPorts(X8086 emulator)
        {
            this.emulator = emulator;
            list = new List<IOPortHandler>();
        }

        public void Add(IOPortHandler item)
        {
            list.Add(item);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(IOPortHandler item)
        {
            return list.Contains(item);
        }

        public void CopyTo(IOPortHandler[] array, int arrayIndex)
        {

        }

        public int Count
        {
            get
            {
                return list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool Remove(IOPortHandler item)
        {
            return list.Remove(item);
        }

        public IEnumerator<IOPortHandler> GetEnumerator()
        {
            return this.GetEnumerator2();
        }

        public IEnumerator<IOPortHandler> GetEnumerator2()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(IOPortHandler item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, IOPortHandler item)
        {
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator1();
        }

        public IEnumerator GetEnumerator1()
        {
            return list.GetEnumerator();
        }
    }

}

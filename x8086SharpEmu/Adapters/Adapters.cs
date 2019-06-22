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
    public class Adapters : IList<Adapter>
    {

        private X8086 emulator;
        private List<Adapter> list;

        public Adapters(X8086 emulator)
        {
            this.emulator = emulator;
            list = new List<Adapter>();
        }

        public void Add(Adapter adapter)
        {
            emulator.SetUpAdapter(adapter);
            list.Add(adapter);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(Adapter adapter)
        {
            return list.Contains(adapter);
        }

        public void CopyTo(Adapter[] array, int arrayIndex)
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

        public bool Remove(Adapter adapter)
        {
            if (adapter != null)
            {
                adapter.CloseAdapter();
            }
            return list.Remove(adapter);
        }

        public IEnumerator<Adapter> GetEnumerator()
        {
            return this.GetEnumerator2();
        }

        public IEnumerator<Adapter> GetEnumerator2()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(Adapter adapter)
        {
            return list.IndexOf(adapter);
        }

        public void Insert(int index, Adapter adapter)
        {
            list.Insert(index, adapter);
        }

        public Adapter this[int index]
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

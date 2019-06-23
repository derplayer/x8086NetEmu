using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections;


using x8086SharpEmu;

namespace x8086SharpEmu
{
    public class PriorityQueue
    {
        private int nHeap = 0;
        private object[] heapObj = new object[16];
        private long[] heapPri = new long[16];

        public PriorityQueue()
        {
        }

        public void Clear()
        {
            nHeap = 0;
            heapObj = new object[16];
            heapPri = new long[16];
        }

        public void Add(object obj, long priority)
        {
            nHeap++;
            if (nHeap >= heapObj.Length)
            {
                object[] oldHeapObj = heapObj;
                long[] oldHeapPri = heapPri;
                heapObj = new object[2 * nHeap];
                heapPri = new long[2 * nHeap];
                Array.Copy((System.Array)oldHeapObj, 0, (System.Array)heapObj, 0, nHeap);
                Array.Copy(oldHeapPri, 0, heapPri, 0, nHeap);
            }

            heapPri[0] = long.MinValue; // element 0 is a sentinel
            int k = nHeap;
            while (heapPri[k / 2] > priority)
            {
                heapObj[k] = heapObj[k / 2];
                heapPri[k] = heapPri[k / 2];
                k = k / 2;
            }

            heapObj[k] = obj;
            heapPri[k] = priority;
        }

        public long MinPriority()
        {
            return nHeap > 0 ? (heapPri[1]) : long.MaxValue;
        }

        public object RemoveFirst()
        {
            if (nHeap == 0)
            {
                return null;
            }

            object obj = heapObj[1];

            object vo = heapObj[nHeap];
            long vp = heapPri[nHeap];
            nHeap--;

            int k = 1;
            int j = 0;
            while (k <= nHeap / 2)
            {
                j = 2 * k;
                if (j < nHeap && heapPri[j] > heapPri[j + 1])
                {
                    j++;
                }
                if (vp <= heapPri[j])
                {
                    break;
                }

                heapObj[k] = heapObj[j];
                heapPri[k] = heapPri[j];
                k = j;
            }
            heapObj[k] = vo;
            heapPri[k] = vp;

            return obj;
        }

        public void Remove(object obj)
        {
            int k = 1;
            while (k <= nHeap && (!ReferenceEquals(heapObj[k], obj)))
            {
                k++;
            }

            if (k <= nHeap)
            {
                object vo = heapObj[nHeap];
                long vp = heapPri[nHeap];
                nHeap--;

                int j = 0;
                while (k <= nHeap / 2)
                {
                    j = 2 * k;
                    if (j < nHeap && heapPri[j] > heapPri[j + 1])
                    {
                        j++;
                    }
                    if (vp <= heapPri[j])
                    {
                        break;
                    }

                    heapObj[k] = heapObj[j];
                    heapPri[k] = heapPri[j];
                    k = j;
                }
                heapObj[k] = vo;
                heapPri[k] = vp;
            }
        }

        public int Size
        {
            get
            {
                return nHeap;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return nHeap == 0;
            }
        }
    }

}

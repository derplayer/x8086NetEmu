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
    public abstract class VideoAdapter : Adapter
    {

        public enum FontSources
        {
            TrueType,
            BitmapFile,
            ROM
        }

        public enum MainModes
        {
            Unknown = -1,
            Text = 0,
            Graphics = 2
        }

        public delegate void KeyDownEventHandler(object sender, KeyEventArgs e);
        private KeyDownEventHandler KeyDownEvent;

        public event KeyDownEventHandler KeyDown
        {
            add
            {
                KeyDownEvent = (KeyDownEventHandler)System.Delegate.Combine(KeyDownEvent, value);
            }
            remove
            {
                KeyDownEvent = (KeyDownEventHandler)System.Delegate.Remove(KeyDownEvent, value);
            }
        }

        public delegate void KeyUpEventHandler(object sender, KeyEventArgs e);
        private KeyUpEventHandler KeyUpEvent;

        public event KeyUpEventHandler KeyUp
        {
            add
            {
                KeyUpEvent = (KeyUpEventHandler)System.Delegate.Combine(KeyUpEvent, value);
            }
            remove
            {
                KeyUpEvent = (KeyUpEventHandler)System.Delegate.Remove(KeyUpEvent, value);
            }
        }

        public delegate void PreRenderEventHandler(object sender, PaintEventArgs e);
        private PreRenderEventHandler PreRenderEvent;

        public event PreRenderEventHandler PreRender
        {
            add
            {
                PreRenderEvent = (PreRenderEventHandler)System.Delegate.Combine(PreRenderEvent, value);
            }
            remove
            {
                PreRenderEvent = (PreRenderEventHandler)System.Delegate.Remove(PreRenderEvent, value);
            }
        }

        public delegate void PostRenderEventHandler(object sender, PaintEventArgs e);
        private PostRenderEventHandler PostRenderEvent;

        public event PostRenderEventHandler PostRender
        {
            add
            {
                PostRenderEvent = (PostRenderEventHandler)System.Delegate.Combine(PostRenderEvent, value);
            }
            remove
            {
                PostRenderEvent = (PostRenderEventHandler)System.Delegate.Remove(PostRenderEvent, value);
            }
        }


        public override AdapterType Type
        {
            get
            {
                return AdapterType.Video;
            }
        }

        public abstract override string Description { get; }
        public abstract override string Name { get; }
        public abstract override string Vendor { get; }
        public abstract override int VersionMajor { get; }
        public abstract override int VersionMinor { get; }
        public abstract override int VersionRevision { get; }

        public abstract uint VideoMode { get; set; }
        public abstract double Zoom { get; set; }

        public abstract override void CloseAdapter();
        public abstract override void InitiAdapter();
        public abstract override void Out(uint port, ushort value);
        public abstract override ushort In(uint port);
        public abstract override void Run();

        public abstract void Reset();
        protected abstract void AutoSize();
        protected abstract void ResizeRenderControl();

        protected int mStartTextVideoAddress = 0xB0000;
        protected int mEndTextVideoAddress = 0xA0000;

        protected int mStartGraphicsVideoAddress;
        protected int mEndGraphicsVideoAddress;
        protected MainModes mMainMode;

        protected int mTextResolutionX = 40;
        protected int mTextResolutionY = 25;
        protected Size mVideoResolution = new Size(0, 0);
        protected Size mCellSize;

        protected KeyMap keyMap = new KeyMap(); // Used to filter unsupported keystrokes

        public VideoAdapter(X8086 cpu) : base(cpu)
        {
        }

        protected virtual void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (KeyDownEvent != null)
                KeyDownEvent(sender, e);
        }

        protected virtual void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (KeyUpEvent != null)
                KeyUpEvent(sender, e);
        }

        protected virtual void OnPreRender(object sender, PaintEventArgs e)
        {
            if (PreRenderEvent != null)
                PreRenderEvent(sender, e);
        }

        protected virtual void OnPostRender(object sender, PaintEventArgs e)
        {
            if (PostRenderEvent != null)
                PostRenderEvent(sender, e);
        }

        public int StartGraphicsVideoAddress
        {
            get
            {
                return mStartGraphicsVideoAddress;
            }
        }

        public int EndGraphicsVideoAddress
        {
            get
            {
                return mEndGraphicsVideoAddress;
            }
        }

        public int StartTextVideoAddress
        {
            get
            {
                return mStartTextVideoAddress;
            }
        }

        public int EndTextVideoAddress
        {
            get
            {
                return mEndTextVideoAddress;
            }
        }

        public Size GraphicsResolution
        {
            get
            {
                return mVideoResolution;
            }
        }

        public Size CellSize
        {
            get
            {
                return mCellSize;
            }
        }

        //Public Property IsDirty(address As UInt32) As Boolean
        //    Get
        //        Dim r As Boolean = Memory(address)
        //        Memory(address) = False
        //        Return r
        //    End Get
        //    Set(value As Boolean)
        //        Memory(address) = value
        //    End Set
        //End Property

        public MainModes MainMode
        {
            get
            {
                return mMainMode;
            }
        }

        public Rectangle ColRowToRectangle(int col, int row)
        {
            return new Rectangle(new Point(col * mCellSize.Width, row * mCellSize.Height), mCellSize);
        }

        public int ColRowToAddress(int col, int row)
        {
            return StartTextVideoAddress + row * mTextResolutionX * 2 + (col * 2);
        }
    }

}

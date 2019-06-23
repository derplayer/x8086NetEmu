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
    public partial class X8086
    {
        public static bool LogToConsole { get; set; }

        private double mMPIs;
        private bool mEmulateINT13 = true;
        private bool mVic20;

        private VideoAdapter mVideoAdapter;
        private KeyboardAdapter mKeyboard;
        private MouseAdapter mMouse;
        private FloppyControllerAdapter mFloppyController;
        private Adapters mAdapters;// = new Adapters(this);
        private IOPorts mPorts;// = new IOPorts(this);
        private bool mEnableExceptions;

        private bool mDebugMode;
        private bool mIsPaused;

        public bool V20
        {
            get
            {
                return mVic20;
            }
        }

        public double MIPs
        {
            get
            {
                return mMPIs;
            }
        }

        public double SimulationMultiplier
        {
            get
            {
                return mSimulationMultiplier;
            }
            set
            {
                mSimulationMultiplier = value;
                SetSynchronization();
            }
        }

        public double Clock
        {
            get
            {
                return mCyclesPerSecond;
            }
            set
            {
                mCyclesPerSecond = (long)value;
                SetSynchronization();
            }
        }

        public REPLoopModes REPELoopMode
        {
            get
            {
                return mRepeLoopMode;
            }
        }

        public Adapters Adapters
        {
            get
            {
                return mAdapters;
            }
        }

        public KeyboardAdapter Keyboard
        {
            get
            {
                return mKeyboard;
            }
        }

        public VideoAdapter VideoAdapter
        {
            get
            {
                return mVideoAdapter;
            }
        }

        public FloppyControllerAdapter FloppyContoller
        {
            get
            {
                return mFloppyController;
            }
        }

        public IOPorts Ports
        {
            get
            {
                return mPorts;
            }
        }

        public bool EnableExceptions
        {
            get
            {
                return mEnableExceptions;
            }
            set
            {
                mEnableExceptions = value;
            }
        }

        public bool DebugMode
        {
            get
            {
                return mDebugMode;
            }
            set
            {
                if (mDebugMode && !value)
                {
                    mDebugMode = false;
                    debugWaiter.Set();
                }
                else
                {
                    mDebugMode = value;
                    mDoReSchedule = true;
                    if (DebugModeChangedEvent != null)
                        DebugModeChangedEvent(this, new EventArgs());
                }
            }
        }

        public bool IsExecuting
        {
            get
            {
                return mIsExecuting;
            }
        }

        private bool mIsHalted;
        public bool IsHalted
        {
            get
            {
                return mIsHalted;
            }
        }

        public bool IsPaused
        {
            get
            {
                return mIsPaused;
            }
        }

        public X8086.Models Model
        {
            get
            {
                return mModel;
            }
        }

        public MouseAdapter Mouse
        {
            get
            {
                return mMouse;
            }
        }

        public bool EmulateINT13
        {
            get
            {
                return mEmulateINT13;
            }
        }

        private uint GetCpuSpeed()
        {
            //TODO: REMOVE HARDCODED CPU VALUE OF MY PC... and search some alt. for WMI
            return 3900;

            // FIXME: change WMI managedobj search in favor of registry hack (mono?)
            //using (System.Management.ManagementObject managementObject = new System.Management.ManagementObject("Win32_Processor.DeviceID='CPU0'"))
            //{
            //    return (uint)(managementObject["CurrentClockSpeed"]);
            //}
        }
    }

}

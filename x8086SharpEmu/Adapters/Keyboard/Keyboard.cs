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
    public class KeyboardAdapter : Adapter, IExternalInputHandler
    {

        public KeyboardAdapter(X8086 cpu) : base(cpu)
        {
        }

        public override string Description
        {
            get
            {
                return "Simple Keyboard Driver Emulator";
            }
        }

        public override string Vendor
        {
            get
            {
                return "xFX JumpStart";
            }
        }

        public override int VersionMajor
        {
            get
            {
                return 0;
            }
        }

        public override int VersionMinor
        {
            get
            {
                return 0;
            }
        }

        public override int VersionRevision
        {
            get
            {
                return 19;
            }
        }

        public override Adapter.AdapterType Type
        {
            get
            {
                return AdapterType.Keyboard;
            }
        }

        public override void InitiAdapter()
        {
        }

        public override void CloseAdapter()
        {
        }

        public override ushort In(uint port)
        {
            return (ushort)(0xFF);
        }

        //Private Sub HandleInput(e As KeyEventArgs, released As Boolean)
        //    Dim scan() As Byte

        //    If e.KeyCode = Keys.Pause And Not released Then
        //        '  Pause has special behaviour:
        //        '  Acts as extended ScrollLock if Ctrl is down (Break),
        //        '  other wise acts as extended Ctrl-NumLock.
        //        '  e.KeyCode down and e.KeyCode release scan codes are always sent together.
        //        If (stateKeyMask And (MASK_LCTRL Or MASK_RCTRL)) <> 0 Then
        //            scan = New Byte() {&HE0, &H46,
        //                                &HE0, &HC6}
        //        Else
        //            scan = New Byte() {&HE1, &H1D, &H45,
        //                                &HE1, &H9D, &HC5}
        //        End If
        //        If controller IsNot Nothing Then
        //            controller.PutKeyData(scan)
        //            Exit Sub
        //        End If
        //    End If

        //    ' Lookup e.KeyCode in table
        //    Dim keyinfo As Integer = 0
        //    If e.KeyCode < keytbl.Length Then keyinfo = keytbl(e.KeyCode)
        //    If keyinfo = 0 Then Exit Sub ' ignore unknown e.KeyCode

        //    ' Detect state keys (shift, ctrl, alt)
        //    Dim statebit As Integer = 0
        //    If keyinfo = SCAN_LSHIFT Then statebit = MASK_LSHIFT
        //    If keyinfo = SCAN_CTRL Then statebit = MASK_LCTRL
        //    If keyinfo = SCAN_ALT Then statebit = MASK_LALT

        //    ' Distinguish left/right and numpad/editpad
        //    Dim extend As Boolean = (keyinfo And KEY_EXTEND) <> 0
        //    If statebit <> 0 AndAlso kevt.keyLocation = KeyEvent.KEY_LOCATION_RIGHT Then
        //        If keyinfo = SCAN_LSHIFT Then
        //            keyinfo = SCAN_RSHIFT ' right shift
        //        Else
        //            extend = True ' right ctrl or alt
        //            statebit <<= 1
        //        End If
        //    if keyinfo And KEY_EDIT) <> 0 andalso            kevt.keyLocation = KeyEvent.KEY_LOCATION_STANDARD then
        //            extend = True ' edit pad
        //            keyinfo = keyinfo Or KEY_NONUM
        //        End If
        //        If e.KeyCode = KeyEvent.VK_ENTER AndAlso kevt.keyLocation = KeyEvent.KEY_LOCATION_NUMPAD Then
        //            extend = True ' numpad Enter

        //    if (released) {

        //                ' Undo shift state virtualization that we (may) have
        //                ' started when this e.KeyCode was pressed.
        //        boolean undoVirtual = useVirtualShift AndAlso virtualShiftState &&
        //                              ((keyinfo And (KEY_NOSHIFT Or KEY_NONUM)) <> 0)

        //        if (e.KeyCode = KeyEvent.VK_PRINTSCREEN) {
        //                    ' PrintScreen has special behaviour
        //            if (!isSysRq) {
        //                        keyinfo = 55
        //                        extend = True
        //                        undoVirtual = useVirtualShift AndAlso virtualShiftState
        //            }
        //                        isSysRq = False
        //        }

        //                        ' Construct e.KeyCode release scan code sequence
        //        scan = new byte(((extend) ? 2 : 1) + ((undoVirtual) ? 2 : 0))
        //                        Int(i = 0)
        //                        If (extend) Then
        //            scan(i++) = (byte)&he0
        //        scan(i++) = (byte)(keyinfo Or &h80)
        //        if (undoVirtual) {
        //            scan(i++) = (byte)&he0
        //            scan(i++) = ((stateKeyMask And MASK_LSHIFT) <> 0) ? SCAN_LSHIFT :
        //                        ((stateKeyMask And MASK_RSHIFT) <> 0) ? SCAN_RSHIFT :
        //                        (byte)(&h80 Or SCAN_LSHIFT)
        //                                virtualShiftState = False
        //        }

        //                                ' Update the state e.KeyCode mask
        //        stateKeyMask &= ~statebit
        //                                If (e.KeyCode = KeyEvent.VK_NUM_LOCK) Then
        //                                    stateKeyMask ^= MASK_NUMLOCK ' flip numlock state

        //    } else {

        //                                    ' Figure out how to manipulate the virtual shift state
        //        boolean flipVirtual = false
        //        if ((keyinfo And (KEY_NOSHIFT Or KEY_NONUM)) <> 0) {
        //                                        ' e.KeyCode requires a particular shift state
        //            boolean realShiftState =
        //              ((stateKeyMask And (MASK_LSHIFT|MASK_RSHIFT)) <> 0)
        //            boolean needShiftState =
        //              ((keyinfo And KEY_NOSHIFT) = 0) &&
        //              ((stateKeyMask And MASK_NUMLOCK) <> 0)
        //            flipVirtual = useVirtualShift &&
        //              (virtualShiftState = (realShiftState = needShiftState))
        //        } else {
        //                                        ' Modifier or "regular" e.KeyCode release shift virtualization.
        //                                        flipVirtual = useVirtualShift AndAlso virtualShiftState
        //        }

        //        if (e.KeyCode = KeyEvent.VK_PRINTSCREEN) {
        //                                            ' PrintScreen has special behaviour:
        //                                            ' Acts as SysRq if Alt e.KeyCode is down, otherwise acts as
        //                                            ' extended Asterisk with forced Shift unless Ctrl is down.
        //            isSysRq = ((stateKeyMask And (MASK_LALT|MASK_RALT)) <> 0)
        //            if (!isSysRq) {
        //                                                keyinfo = 55
        //                                                extend = True
        //                boolean needVirtualShift = ((stateKeyMask &
        //                  (MASK_LSHIFT|MASK_RSHIFT|MASK_LCTRL|MASK_RCTRL)) = 0)
        //                flipVirtual = useVirtualShift &&
        //                  (virtualShiftState <> needVirtualShift)
        //            }
        //        }

        //                                                ' Construct e.KeyCode down scan code sequence
        //        scan = new byte(((extend) ? 2 : 1) + ((flipVirtual) ? 2 : 0))
        //                                                Int(i = 0)
        //        if (flipVirtual) {
        //            scan(i++) = (byte)&he0
        //            scan(i++) = (byte)(
        //              ( (virtualShiftState) ? &h00 : &h80 ) ^
        //              ( ((stateKeyMask And MASK_LSHIFT) <> 0) ? SCAN_LSHIFT :
        //                ((stateKeyMask And MASK_RSHIFT) <> 0) ? SCAN_RSHIFT :
        //                (&h80 Or SCAN_LSHIFT) ) )
        //                                                    virtualShiftState = !virtualShiftState
        //        }
        //                                                    If (extend) Then
        //            scan(i++) = (byte)&he0
        //        scan(i++) = (byte)keyinfo

        //                                                        ' Update the state e.KeyCode mask
        //        stateKeyMask |= statebit

        //    }

        //                                                        If (keyboardController <> null) Then
        //                                                            keyboardController.putKeyData(scan)
        //End Sub

        public override string Name
        {
            get
            {
                return "Keyboard";
            }
        }

        public override void Out(uint port, ushort value)
        {
        }

        public override void Run()
        {

        }

        public void HandleInput(ExternalInputEvent e)
        {
            KeyEventArgs keyEvent = (KeyEventArgs)e.TheEvent;
            bool isUp = System.Convert.ToBoolean(e.Extra);

            if (base.CPU.PPI != null)
            {
                base.CPU.PPI.PutKeyData(keyEvent.KeyValue & 0xFF, isUp);
            }
        }
    }

}

//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//using System.Windows.Forms;

//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//	// MODE 0x13: http://www.brackeen.com/vga/basics.html

//	public partial class RenderCtrlGDI
//	{
//		public RenderCtrlGDI()
//		{
//			// This call is required by the designer.
//			InitializeComponent();

//			// Add any initialization after the InitializeComponent() call.
//			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
//			this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
//			this.SetStyle(ControlStyles.ResizeRedraw, true);
//			this.SetStyle(ControlStyles.Selectable, true);
//			this.SetStyle(ControlStyles.UserPaint, true);

//			// Capturing is automatically disabled from the Dispose event

//			// This is used to force the arrow keys to generate a KeyDown event
//			// It also allows us to capture the Alt key
//			PreviewKeyDown += (sender, PreviewKeyDownEventArgs e) => e.IsInputKey = true;
//		}

//		// This method also works
//		//Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
//		//    Select Case keyData
//		//        Case Keys.Up, Keys.Down, Keys.Left, Keys.Right
//		//            OnKeyDown(New KeyEventArgs(keyData))
//		//    End Select
//		//    Return MyBase.ProcessCmdKey(msg, keyData)
//		//End Function
//	}
//}

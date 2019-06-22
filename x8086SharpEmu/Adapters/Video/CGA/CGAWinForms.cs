//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//using System.Windows.Forms;
//using System.Threading;

//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//	// MODE 0x13: http://www.brackeen.com/vga/basics.html
//	// Color Graphics Adapter (CGA) http://webpages.charter.net/danrollins/techhelp/0066.HTM

//	// http://www.powernet.co.za/info/BIOS/Mem/
//	// http://www-ivs.cs.uni-magdeburg.de/~zbrog/asm/memory.html

//	public class CGAWinForms : CGAAdapter
//	{

//		private int blinkCounter;
//		private int frameRate = 30;
//		private List<int> cursorAddress = new List<int>();

//		private readonly Color[] brushCache = new Color[CGAPalette.Length];
//		private readonly Color cursorBrush = Color.FromArgb(128, Color.White);

//		private readonly string preferredFont = "Perfect DOS VGA 437";
//		private Font mFont; 
//		private StringFormat textFormat; 

//		private readonly FontSources fontSourceMode;
//		private Graphics g;

//		private SizeF scale = new SizeF(1, 1);

//		private Control mRenderControl;
//		private bool mHideHostCursor = true;

//		private class TaskSC : Scheduler.Task
//		{

//			public TaskSC(IOPortHandler owner) : base(owner)
//			{
//			}

//			public override void Run()
//			{
//				Owner.Run();
//			}

//			public override string Name
//			{
//				get
//				{
//					return Owner.Name;
//				}
//			}
//		}
//		private Scheduler.Task task = new TaskSC(this);

//		public CGAWinForms(X8086 cpu, Control renderControl, FontSources fontSource = VideoAdapter.FontSources.BitmapFile, string bitmapFontFile = "", bool enableWebUI = false) : base(cpu: cpu, enableWebUI: enableWebUI)
//		{
//			
//			mFont = new Font("Perfect DOS VGA 437", 16, FontStyle.Regular, GraphicsUnit.Pixel);
//			textFormat = new StringFormat(StringFormat.GenericTypographic);

//			fontSourceMode = fontSource;

//			this.RenderControl = renderControl;

//			mRenderControl.KeyDown += (sender, KeyEventArgs e) => HandleKeyDown(this, e);
//			mRenderControl.KeyUp += (sender, KeyEventArgs e) => HandleKeyUp(this, e);

//			mRenderControl.MouseDown += (sender, MouseEventArgs e) => OnMouseDown(this, e);
//			mRenderControl.MouseMove += (sender, MouseEventArgs e)=>
//			{
//				if (base.CPU.Mouse?.IsCaptured)
//				{
//					OnMouseMove(this, e);
//					Cursor.Position = mRenderControl.PointToScreen(base.CPU.Mouse.MidPoint);
//				}
//			};
//			mRenderControl.MouseUp += (sender, MouseEventArgs e) => OnMouseUp(this, e);

//			string fontCGAPath = X8086.FixPath("misc\\" + bitmapFontFile);
//			string fontCGAError = "";

//			switch (fontSource)
//			{
//				case FontSources.BitmapFile:
//					if (System.IO.File.Exists(fontCGAPath))
//					{
//						try
//						{
//							VideoChar.FontBitmaps = System.IO.File.ReadAllBytes(fontCGAPath);
//							mCellSize = new Size(8, 16);
//						}
//						catch (Exception ex)
//						{
//							fontCGAError = ex.Message;
//							fontSourceMode = FontSources.TrueType;
//						}
//					}
//					else
//					{
//						fontCGAError = "File not found";
//						fontSourceMode = FontSources.TrueType;
//					}
//					break;
//				case FontSources.ROM:
//					VideoChar.BuildFontBitmapsFromROM(8, 8, 4, 0xFE000 + 0x1A6E, base.CPU.Memory);
//					mCellSize = new Size(8, 8);
//					break;
//			}

//			if (fontSourceMode == FontSources.TrueType)
//			{
//				if (mFont.Name != preferredFont)
//				{
//					Interaction.MsgBox((fontSource == FontSources.BitmapFile ? ("ASCII VGA Font Data not found at '" + fontCGAPath + "'" + (!string.IsNullOrEmpty(fontCGAError) ? ": " + fontCGAError : "") +
//						"\r\n" + "\r\n") : "") +
//						"CGAWinForms requires the '" + preferredFont + "' font. Please install it before using this adapter", (Microsoft.VisualBasic.MsgBoxStyle) (MsgBoxStyle.Information | MsgBoxStyle.OkOnly), null);
//					mFont = new Font("Consolas", 16, FontStyle.Regular, GraphicsUnit.Pixel);
//					if (mFont.Name != "Consolas")
//					{
//						mFont = new Font("Andale Mono", 16, FontStyle.Regular, GraphicsUnit.Pixel);
//						if (mFont.Name != "Andale Mono")
//						{
//							mFont = new Font("Courier New", 16, FontStyle.Regular, GraphicsUnit.Pixel);
//						}
//					}
//				}
//			}

//			textFormat.FormatFlags = (System.Drawing.StringFormatFlags) (StringFormatFlags.NoWrap |
//				StringFormatFlags.MeasureTrailingSpaces |
//				StringFormatFlags.FitBlackBox |
//				StringFormatFlags.NoClip);

//			System.Threading.Thread tmp = new System.Threading.Thread(()=>
//			{
//				do
//				{
//					Threading.Thread.Sleep(1000 / frameRate);
//					mRenderControl.Invalidate();
//				} while (!cancelAllThreads);
//			});
//			tmp.Start();
//		}

//		public Control RenderControl
//		{
//			get
//			{
//				return mRenderControl;
//			}
//			set
//			{
//				DetachRenderControl();
//				mRenderControl = value;

//				//useSDL = TypeOf mRenderControl Is RenderCtrlSDL
//				//If useSDL Then
//				//    sdlCtrl = CType(mRenderControl, RenderCtrlSDL)
//				//    sdlCtrl.Init(Me, mFont.FontFamily.Name, mFont.Size)
//				//End If

//				InitiAdapter();

//				mRenderControl.Paint += Paint;
//			}
//		}

//		protected void DetachRenderControl()
//		{
//			if (mRenderControl != null)
//			{
//				mRenderControl.Paint -= Paint;
//			}
//		}

//		public override void CloseAdapter()
//		{
//			base.CloseAdapter();
//			DetachRenderControl();
//		}

//		protected override void AutoSize()
//		{
//			if (mRenderControl != null)
//			{
//				if (mRenderControl.InvokeRequired)
//				{
//					mRenderControl.Invoke(() => ResizeRenderControl());
//				}
//				else
//				{
//					ResizeRenderControl();
//				}
//			}
//		}

//		protected override void ResizeRenderControl()
//		{
//			Size ctrlSize = new Size();

//			if (MainMode == MainModes.Text)
//			{
//				ctrlSize = new Size(mCellSize.Width * TextResolution.Width, mCellSize.Height * TextResolution.Height);
//			}
//			else
//			{
//				ctrlSize = new Size(GraphicsResolution.Width, GraphicsResolution.Height);
//			}

//			Size frmSize = new Size((int)(640 * Zoom), (int)(400 * Zoom));
//			Form frm = mRenderControl.FindForm();
//			frm.ClientSize = frmSize;
//			mRenderControl.Location = Point.Empty;
//			mRenderControl.Size = frmSize;
//			if (mCellSize.Width == 0 || mCellSize.Height == 0)
//			{
//				return;
//			}

//			scale = new SizeF((float) ((double) frmSize.Width / ctrlSize.Width), (float) ((double) frmSize.Height / ctrlSize.Height));
//		}

//		private void Paint(object sender, PaintEventArgs e)
//		{
//			Graphics g = e.Graphics;

//			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
//			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
//			g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

//			g.ScaleTransform(scale.Width, scale.Height);

//			OnPreRender(sender, e);
//			g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

//			lock(chars)
//			{
//				g.DrawImageUnscaled(videoBMP, 0, 0);
//			}

//			g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
//			OnPostRender(sender, e);

//			//RenderWaveform(g)
//		}

//		protected override void OnPaletteRegisterChanged()
//		{
//			base.OnPaletteRegisterChanged();

//			if (brushCache != null)
//			{
//				for (int i = 0; i <= CGAPalette.Length - 1; i++)
//				{
//					brushCache[i] = CGAPalette[i];
//				}
//			}
//		}

//		protected override void Render()
//		{
//			if (VideoEnabled)
//			{
//				lock(chars)
//				{
//					if (MainMode == MainModes.Text)
//					{
//						RenderText();
//					}
//					else if (MainMode == MainModes.Graphics)
//					{
//						RenderGraphics();
//					}
//				}
//			}
//		}

//		private void RenderText()
//		{
//			byte b0 = 0;
//			byte b1 = 0;

//			int col = 0;
//			int row = 0;

//			Rectangle r = new Rectangle(Point.Empty, mCellSize);

//			if (!CursorVisible)
//			{
//				blinkCounter = 2 * BlinkRate;
//			}

//			for (int address = mStartTextVideoAddress; address <= mEndTextVideoAddress - 2; address += 2)
//			{
//				b0 = CPU.Memory[address];
//				b1 = CPU.Memory[address + 1];

//				if (BlinkCharOn && double.Parse(b1 & + B1000_0000) != 0)
//				{
//					if (blinkCounter < BlinkRate)
//					{
//						b0 = (byte) 0;
//					}
//				}

//				RenderChar(b0, videoBMP, brushCache[b1.LowNib()], brushCache[b1.HighNib()], r.Location, cursorAddress.Contains(address));
//				cursorAddress.Remove(address);

//				if (CursorVisible && row == CursorRow && col == CursorCol)
//				{
//					if (blinkCounter < BlinkRate)
//					{
//						videoBMP.FillRectangle(brushCache[b1.LowNib()],
//							r.X + 0, r.Y - 1 + mCellSize.Height - (base.CursorEnd - base.CursorStart) - 1,
//							mCellSize.Width, base.CursorEnd - base.CursorStart + 1);
//						cursorAddress.Add(address);
//					}

//					if (blinkCounter >= 2 * BlinkRate)
//					{
//						blinkCounter = 0;
//					}
//					else
//					{
//						blinkCounter++;
//					}
//				}

//				r.X += mCellSize.Width;
//				col++;
//				if (col == TextResolution.Width)
//				{
//					col = 0;
//					row++;
//					if (row == TextResolution.Height)
//					{
//						break;
//					}

//					r.X = 0;
//					r.Y += mCellSize.Height;
//				}
//			}
//		}

//		private void RenderGraphics()
//		{
//			byte b = 0;
//			int xDiv = (int)(PixelsPerByte == 4 ? 2 : 3);

//			for (int y = 0; y <= GraphicsResolution.Height - 1; y++)
//			{
//				for (int x = 0; x <= GraphicsResolution.Width - 1; x++)
//				{
//					b = CPU.Memory[mStartGraphicsVideoAddress + ((y >> 1) * 80) + ((y & 1) * 0x2000) + (x >> xDiv)];

//					if (PixelsPerByte == 4)
//					{
//						switch (x & 3)
//						{
//							case 3:
//								b = b & 3;
//								break;
//							case 2:
//								b = (byte)((b >> 2) & 3);
//								break;
//							case 1:
//								b = (byte)((b >> 4) & 3);
//								break;
//							case 0:
//								b = (byte)((b >> 6) & 3);
//								break;
//						}
//					}
//					else
//					{
//						b = (byte)((b >> (7 - (x & 7))) & 1);
//					}

//					videoBMP.set_Pixel(x, y, CGAPalette[b]);
//				}
//			}
//		}

//		private void RenderChar(int c, DirectBitmap dbmp, Color fb, Color bb, Point p, bool force = false)
//		{
//			if (fontSourceMode == FontSources.TrueType)
//			{
//				using (SolidBrush bbb = new SolidBrush(bb))
//				{
//					g.FillRectangle(bbb, new Rectangle(p, mCellSize));
//					using (SolidBrush bfb = new SolidBrush(fb))
//					{
//						g.DrawString(char.ConvertFromUtf32(c), mFont, bfb, (float) (p.X - (double) mCellSize.Width / 2 + 2), p.Y);
//					}

//				}

//			}
//			else
//			{
//				VideoChar ccc = new VideoChar(c, fb, bb);
//				int idx = 0;

//				if (!force)
//				{
//					idx = (p.Y << 8) + p.X;
//					if (memCache[idx] != null && memCache[idx] == ccc)
//					{
//						return;
//					}
//					memCache[idx] = ccc;
//				}

//				idx = charsCache.IndexOf(ccc);
//				if (idx == -1)
//				{
//					ccc.Render(mCellSize.Width, mCellSize.Height);
//					charsCache.Add(ccc);
//					idx = charsCache.Count - 1;
//				}
//				charsCache[idx].Paint(dbmp, p, scale);
//			}
//		}

//		private void RenderWaveform(Graphics g)
//		{
//#if Win32
//			if (base.CPU.PIT?.Speaker != null)
//			{
//				g.ResetTransform();

//				int h = (int)(mRenderControl.Height * 0.6);
//				int h2 = (int)((double) h / 2);
//				Point p1 = new Point(0, (int)(base.CPU.PIT.Speaker.AudioBuffer[0] / byte.MaxValue * h + h * 0.4));
//				Point p2 = new Point();
//				int len = (int)(base.CPU.PIT.Speaker.AudioBuffer.Length);

//				using (Pen p = new Pen(Brushes.Red, 3))
//				{
//					for (int i = 1; i <= len - 1; i++)
//					{
//						try
//						{
//							p2 = new Point((int)((double) i / len * mRenderControl.Width), (int)(base.CPU.PIT.Speaker.AudioBuffer[i] / byte.MaxValue * h + h * 0.4));
//							g.DrawLine(p, p1, p2);
//							p1 = p2;
//						}
//						catch
//						{
//							goto endOfForLoop;
//						}
//					}
//endOfForLoop:
//					1.GetHashCode() ; 
//				}

//			}
//#endif
//		}

//		private Size MeasureChar(Graphics graphics, int code, char text, Font font)
//		{
//			Size size = new Size();

//			switch (fontSourceMode)
//			{
//				case FontSources.BitmapFile:
//					charSizeCache.Add(code, mCellSize);
//					break;
//				case FontSources.TrueType:
//					if (charSizeCache.ContainsKey(code))
//					{
//						return charSizeCache[code];
//					}

//					RectangleF rect = new RectangleF(0, 0, 1000, 1000);
//					CharacterRange[] ranges = new CharacterRange[] {new CharacterRange(0, 1)};
//					Region[] regions = new Region[] {new Region()};

//					textFormat.SetMeasurableCharacterRanges(ranges);

//					regions = graphics.MeasureCharacterRanges(text.ToString(), font, rect, textFormat);
//					rect = regions[0].GetBounds(graphics);

//					size = new Size((int)(rect.Right - 1), (int)(rect.Bottom));
//					charSizeCache.Add(code, size);
//					break;
//				case FontSources.ROM:
//					size = new Size(8, 8);
//					charSizeCache.Add(code, size);
//					break;
//			}

//			return size;
//		}

//		public override string Description
//		{
//			get
//			{
//				return "CGA WinForms Adapter";
//			}
//		}

//		public override string Name
//		{
//			get
//			{
//				return "CGA WinForms";
//			}
//		}

//		public override void Run()
//		{
//			if (mRenderControl != null)
//			{
//				mRenderControl.Invalidate();
//			}
//		}

//		protected override void InitVideoMemory(bool clearScreen)
//		{
//			base.InitVideoMemory(clearScreen);

//			if (mRenderControl != null)
//			{
//				if (clearScreen || charSizeCache.Count == 0)
//				{
//					charSizeCache.Clear();
//					using (Graphics g = mRenderControl.CreateGraphics())
//					{
//						for (int i = 0; i <= 255; i++)
//						{
//							MeasureChar(g, i, chars[i], mFont);
//						}
//					}

//				}

//				// Monospace... duh!
//				mCellSize = charSizeCache[65];
//			}

//			lock(chars)
//			{
//				if (videoBMP != null)
//				{
//					videoBMP.Dispose();
//				}
//				if (MainMode == MainModes.Text)
//				{
//					videoBMP = new DirectBitmap(640, 400);
//				}
//				else if (MainMode == MainModes.Graphics)
//				{
//					videoBMP = new DirectBitmap(GraphicsResolution.Width, GraphicsResolution.Height);
//				}
//				if (wui != null)
//				{
//					wui.Bitmap = videoBMP;
//				}
//			}

//			if (fontSourceMode == FontSources.TrueType)
//			{
//				if (g != null)
//				{
//					g.Dispose();
//				}
//				g = Graphics.FromImage(videoBMP);
//			}
//		}
//	}

//}

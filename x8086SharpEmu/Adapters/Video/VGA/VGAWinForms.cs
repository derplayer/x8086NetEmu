//using System.Collections.Generic;
//using System;
//using System.Linq;
//using System.Drawing;
//using System.Diagnostics;
//using System.Xml.Linq;
//using System.Collections;
//
//using System.Threading;

//using x8086SharpEmu;

//namespace x8086SharpEmu
//{
//	public class VGAWinForms : VGAAdapter
//	{

//		private int blinkCounter;
//		private Size cursorSize;
//		private int frameRate = 30;
//		private List<int> cursorAddress = new List<int>();

//		private readonly string preferredFont = "Perfect DOS VGA 437";
//		private Font mFont; 
//		private StringFormat textFormat; 

//		private readonly Color[] brushCache = new Color[CGAPalette.Length];
//		private Color cursorBrush; 
//		private int cursorYOffset;

//		private readonly FontSources fontSourceMode;
//		private Graphics g;

//		private SizeF scale = new SizeF(1, 1);

//		private X8086 mCPU;
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


//		public VGAWinForms(X8086 cpu, Control renderControl, FontSources fontSource = VideoAdapter.FontSources.BitmapFile, string bitmapFontFile = "", bool enableWebUI = false) : base(cpu: cpu, enableWebUI: enableWebUI)
//		{
//			
//			mFont = new Font("Perfect DOS VGA 437", 16, FontStyle.Regular, GraphicsUnit.Pixel);
//			textFormat = new StringFormat(StringFormat.GenericTypographic);
//			cursorBrush = Color.FromArgb(128, Color.White);

//			fontSourceMode = fontSource;
//			mCPU = cpu;
//			this.RenderControl = renderControl;

//			mRenderControl.KeyDown += (sender, KeyEventArgs e) => HandleKeyDown(this, e);
//			mRenderControl.KeyUp += (sender, KeyEventArgs e) => HandleKeyUp(this, e);

//			mRenderControl.MouseDown += (sender, MouseEventArgs e) => OnMouseDown(this, e);
//			mRenderControl.MouseMove += (sender, MouseEventArgs e)=>
//			{
//				if (mCPU.Mouse?.IsCaptured)
//				{
//					OnMouseMove(this, e);
//					Cursor.Position = mRenderControl.PointToScreen(mCPU.Mouse.MidPoint);
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
//					VideoChar.BuildFontBitmapsFromROM(8, 16, 14, 0xC0000 + 0x3310, mCPU.Memory);
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

//			InitVideoMemory(false);
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

//		protected override void Render()
//		{
//			if (VideoEnabled)
//			{
//				try
//				{
//					lock(chars)
//					{
//						if (MainMode == MainModes.Text)
//						{
//							RenderText();
//						}
//						else if (MainMode == MainModes.Graphics)
//						{
//							RenderGraphics();
//						}
//					}
//				}
//				catch
//				{
//				}
//			}
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

//			g.DrawImageUnscaled(videoBMP, 0, 0);

//			g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
//			OnPostRender(sender, e);

//			//RenderWaveform(g)
//		}

//		private void RenderGraphics()
//		{
//			byte b0 = 0;
//			byte b1 = 0;
//			int xDiv = (int)(PixelsPerByte == 4 ? 2 : 3);
//			int usePal = (int)((portRAM[0x3D9] >> 5) & 1);
//			int intensity = (int)(((portRAM[0x3D9] >> 4) & 1) << 3);

//			// For modes &h12 and &h13
//			bool planeMode = mVideoMode == 0x12 || mVideoMode == 0x13 ? ((VGA_SC[4] & 6) != 0) : false;
//			uint vgaPage = (uint)(mVideoMode <= 7 || mVideoMode == 0x12 || mVideoMode == 0x13 ? ((VGA_CRTC[0xC] << 8) + VGA_CRTC[0xD]) : 0);

//			uint address = 0;
//			uint h1 = 0;
//			uint h2 = 0;
//			uint k = (uint) (mCellSize.Width * mCellSize.Height);
//			Rectangle r = new Rectangle(Point.Empty, CellSize);

//			if (mVideoMode == 2)
//			{
//				for (int y = 0; y <= mTextResolution.Height - 1; y++)
//				{
//					for (int x = 0; x <= mTextResolution.Width - 1; x++)
//					{
//						if (portRAM[0x3D8] == 9 && portRAM[0x3D4] == 9)
//						{
//							address = vgaPage + mStartGraphicsVideoAddress + ((double) y / 4) * mTextResolution.Width * 2 + h1 * 2;
//							Debugger.Break(); // UNTESTED
//						}
//						else
//						{
//							address = (uint) (mStartGraphicsVideoAddress + y * mTextResolution.Width * 2 + x * 2);
//						}
//						b0 = mCPU.Memory[address];

//						if (b0 == 0)
//						{
//							b1 = (byte) (mCPU.Memory[address + 1] / 16);
//						}
//						else
//						{
//							b1 = (byte)(mCPU.Memory[address + 1] & 15);
//						}
//						RenderChar(b0, videoBMP, brushCache[b1.LowNib()], brushCache[b1.HighNib() && (intensity ? 7 : 0xF)], r.Location);

//						r.X += mCellSize.Width;
//					}
//					r.X = 0;
//					r.Y += mCellSize.Height;
//				}
//				return;
//			}

//			for (int y = 0; y <= GraphicsResolution.Height - 1; y++)
//			{
//				for (int x = 0; x <= GraphicsResolution.Width - 1; x++)
//				{
//					if ((mVideoMode == ((uint) 4)) || (mVideoMode == ((uint) 5)))
//					{
//						b0 = mCPU.Memory[mStartGraphicsVideoAddress + (y * mTextResolution.Width) + ((y & 1) * 0x2000) + (x >> 3)];
//						switch (x & 3)
//						{
//							case 3:
//								b0 = b0 & 3;
//								break;
//							case 2:
//								b0 = (byte)((b0 >> 2) & 3);
//								break;
//							case 1:
//								b0 = (byte)((b0 >> 4) & 3);
//								break;
//							case 0:
//								b0 = (byte)((b0 >> 6) & 3);
//								break;
//						}
//						if (mVideoMode == 4)
//						{
//							b0 = (byte) (b0 * 2 + usePal + intensity);
//							if (b0 == (usePal + intensity))
//							{
//								b0 = (byte) 0;
//							}
//						}
//						else
//						{
//							b0 = (byte) (b0 * 0x3F);
//							b0 = b0 % CGAPalette.Length;
//						}
//						videoBMP.set_Pixel(x, y, CGAPalette[b0 | b1]);
//					}
//					else if (mVideoMode == ((uint) 6))
//					{
//						b0 = mCPU.Memory[mStartGraphicsVideoAddress + ((y >> 1) * mTextResolution.Width) + ((y & 1) * 0x2000) + (x >> 3)];
//						b0 = (byte)((b0 >> (7 - (x & 7))) & 1);
//						b0 *= (byte) 15;
//						videoBMP.set_Pixel(x, y, CGAPalette[b0]);
//					}
//					else if ((mVideoMode == ((uint) (0xD))) || (mVideoMode == ((uint) (0xE))))
//					{
//						h1 = (uint) (x >> 1);
//						h2 = (uint) (y >> 1);
//						address = h2 * mTextResolution.Width + (h1 >> 3);
//						h1 = (uint)(7 - (h1 & 7));
//						b0 = (byte)((vRAM[address] >> h1) & 1);
//						b0 = b0 + ((vRAM[address + 0x10000] >> h1) & 1) << 1;
//						b0 = b0 + ((vRAM[address + 0x20000] >> h1) & 1) << 2;
//						b0 = b0 + ((vRAM[address + 0x30000] >> h1) & 1) << 3;
//						videoBMP.set_Pixel(x, y, vgaPalette[b0]);
//					}
//					else if (mVideoMode == ((uint) (0x10)))
//					{
//						address = (uint) ((y * mTextResolution.Width) + (x >> 3));
//						h1 = (uint)(7 - (x & 7));
//						b0 = (byte)((vRAM[address] >> h1) & 1);
//						b0 = b0 | ((vRAM[address + 0x10000] >> h1) & 1) << 1;
//						b0 = b0 | ((vRAM[address + 0x20000] >> h1) & 1) << 2;
//						b0 = b0 | ((vRAM[address + 0x30000] >> h1) & 1) << 3;
//						videoBMP.set_Pixel(x, y, vgaPalette[b0]);
//					}
//					else if (mVideoMode == ((uint) (0x12)))
//					{
//						address = (uint)((y * mTextResolution.Width) + ((double) x / 8));
//						h1 = (uint)((~x) & 7);
//						b0 = (byte)((vRAM[address] >> h1) & 1);
//						b0 = b0 | ((vRAM[address + 0x10000] >> h1) & 1) << 1;
//						b0 = b0 | ((vRAM[address + 0x20000] >> h1) & 1) << 2;
//						b0 = b0 | ((vRAM[address + 0x30000] >> h1) & 1) << 3;
//						videoBMP.set_Pixel(x, y, vgaPalette[b0]);
//					}
//					else if (mVideoMode == ((uint) (0x13)))
//					{
//						if (planeMode)
//						{
//							b0 = vRAM[((y * mVideoResolution.Width + x) >> 2) + (x & 3) * 0x10000 + vgaPage - (VGA_ATTR[0x13] & 0xF)];
//						}
//						else
//						{
//							b0 = mCPU.Memory[mStartGraphicsVideoAddress + vgaPage + y * mVideoResolution.Width + x];
//						}
//						videoBMP.set_Pixel(x, y, vgaPalette[b0]);
//					}
//					else if (mVideoMode == ((uint) 127))
//					{
//						b0 = mCPU.Memory[mStartGraphicsVideoAddress + ((y & 3) << 13) + ((y >> 2) * 90) + (x >> 3)];
//						b0 = (byte)((b0 >> (7 - (x & 7))) & 1);
//						videoBMP.set_Pixel(x, y, CGAPalette[b0]);
//					}
//					else
//					{
//						b0 = mCPU.Memory[mStartGraphicsVideoAddress + ((y >> 1) * mTextResolution.Width) + ((y & 1) * 0x2000) + (x >> xDiv)];
//						if (PixelsPerByte == 4)
//						{
//							switch (x & 3)
//							{
//								case 3:
//									b0 = b0 & 3;
//									break;
//								case 2:
//									b0 = (byte)((b0 >> 2) & 3);
//									break;
//								case 1:
//									b0 = (byte)((b0 >> 4) & 3);
//									break;
//								case 0:
//									b0 = (byte)((b0 >> 6) & 3);
//									break;
//							}
//						}
//						else
//						{
//							b0 = (byte)((b0 >> (7 - (x & 7))) & 1);
//						}
//						videoBMP.set_Pixel(x, y, CGAPalette[b0]);
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

//			Rectangle r = new Rectangle(Point.Empty, CellSize);

//			int vgaPage = (VGA_CRTC[0xC] << 8) + VGA_CRTC[0xD];
//			bool intensity = (portRAM[0x3D8] & 0x80) != 0;
//			bool mode = (portRAM[0x3D8] == 9) && (portRAM[0x3D4] == 9);

//			// FIXME: Dummy workaround to support the cursor; Haven't found a better way yet...
//			mCursorCol = mCPU.Memory[0x450];
//			mCursorRow = mCPU.Memory[0x451];
//			mCursorVisible = true;

//			for (int address = 0; address <= MemSize - 2; address += 2)
//			{
//				b0 = get_VideoRAM((ushort) address);
//				b1 = get_VideoRAM((ushort) (address + 1));

//				if (mVideoMode == 7 || mVideoMode == 127)
//				{
//					if ((b1 & 0x70) != 0)
//					{
//						b1 = b0 == 0 ? 7 : 0;
//					}
//					else
//					{
//						b1 = b0 == 0 ? 0 : 7;
//					}
//				}

//				RenderChar(b0, videoBMP, brushCache[b1.LowNib()], brushCache[b1.HighNib() && (intensity ? 7 : 0xF)], r.Location, cursorAddress.Contains(address));
//				cursorAddress.Remove(address);

//				if (CursorVisible && row == CursorRow && col == CursorCol)
//				{
//					if (blinkCounter < BlinkRate)
//					{
//						videoBMP.FillRectangle(brushCache[b1.LowNib()],
//							r.X + 0, r.Y - 1 + CellSize.Height - (CursorEnd - CursorStart) - 1,
//							CellSize.Width, CursorEnd - CursorStart + 1);
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

//				r.X += CellSize.Width;
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
//					r.Y += CellSize.Height;
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

//		public override string Description
//		{
//			get
//			{
//				return "VGA WinForms Adapter";
//			}
//		}

//		public override string Name
//		{
//			get
//			{
//				return "VGA WinForms";
//			}
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

//		private Size MeasureChar(Graphics graphics, int code, char text, Font font)
//		{
//			Size size = new Size();

//			switch (fontSourceMode)
//			{
//				case FontSources.BitmapFile:
//					charSizeCache.Add(code, CellSize);
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
//					charSizeCache.Add(code, CellSize);
//					break;
//			}

//			return CellSize;
//		}

//		protected override void InitVideoMemory(bool clearScreen)
//		{
//			base.InitVideoMemory(clearScreen);

//			if (mRenderControl != null)
//			{
//				lock(chars)
//				{
//					if (videoBMP != null)
//					{
//						videoBMP.Dispose();
//					}
//					if (GraphicsResolution.Width == 0)
//					{
//						VideoMode = (uint) 3;
//						return;
//					}
//					videoBMP = new DirectBitmap(GraphicsResolution.Width, GraphicsResolution.Height);

//					if (wui != null)
//					{
//						wui.Bitmap = videoBMP;
//					}
//				}

//				if (clearScreen || charSizeCache.Count == 0)
//				{
//					charSizeCache.Clear();
//					using (var g = mRenderControl.CreateGraphics())
//					{
//						for (int i = 0; i <= 255; i++)
//						{
//							MeasureChar(g, i, chars[i], mFont);
//						}
//					}

//				}

//				charsCache.Clear();

//				if (fontSourceMode == FontSources.TrueType)
//				{
//					if (g != null)
//					{
//						g.Dispose();
//					}
//					g = Graphics.FromImage(videoBMP);
//				}
//			}
//		}

//		public override void Run()
//		{
//			if (mRenderControl != null)
//			{
//				mRenderControl.Invalidate();
//			}
//		}
//	}

//}

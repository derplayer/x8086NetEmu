//'using System.Collections.Generic;
//'using System;
//'using System.Linq;
//'using System.Drawing;
//'using System.Diagnostics;
//'
//'using System.Xml.Linq;
//'using System.Collections;
//'using System.Windows.Forms;

//'using System.Net;
//'using System.Threading;
//'using System.Web;
//'using Microsoft.VisualBasic.CompilerServices;
//'using x8086SharpEmu;

//'namespace x8086SharpEmu
//'{

//'	public class WebUI
//'	{
//'		private System.Net.Sockets.TcpListener client;

//'		private DirectBitmap mBitmap;
//'		private X8086 cpu;
//'		private readonly object syncObj;

//'		private Keys lastKeyDown = System.Windows.Forms.Keys.A;
//'		private long lastKeyDownTime;

//'		private Keys lastKeyUp = System.Windows.Forms.Keys.A;
//'		private long lastKeyUpTime;

//'		public WebUI(X8086 cpu, DirectBitmap dBmp, object syncObj)
//'		{
//'			this.cpu = cpu;
//'			this.syncObj = syncObj;
//'			mBitmap = dBmp;

//'			CreateClient();

//'			System.Threading.Tasks.Task.Run(ListenerSub);
//'		}

//'		public DirectBitmap Bitmap
//'		{
//'			get
//'			{
//'				return mBitmap;
//'			}
//'			set
//'			{
//'				lock(syncObj)
//'				{
//'					mBitmap = value;
//'				}
//'			}
//'		}

//'		private void ListenerSub()
//'		{
//'			do
//'			{
//'				try
//'				{
//'					if (client?.Pending)
//'					{
//'						using (System.Net.Sockets.TcpClient tcp = client.AcceptTcpClient())
//'						{
//'							using (System.Net.Sockets.NetworkStream netStream = tcp.GetStream())
//'							{
//'								byte[] buffer = new byte[1024 * 16];
//'								List<byte> data = new List<byte>();

//'								do
//'								{
//'									int len = netStream.Read(buffer, 0, buffer.Length);
//'									if (len > 0)
//'									{
//'										data.AddRange(buffer);
//'									}
//'									if (len < buffer.Length)
//'									{
//'										break;
//'									}
//'								} while (true);

//'								// See '\Projects\SDFWebCuadre\SDFWebCuadre\ModuleMain.vb' for information
//'								// on how to handle binary data, such as images

//'								string rcvData = System.Text.Encoding.UTF8.GetString(data.ToArray());
//'								byte[] sndData = null;
//'								string resource = GetResource(rcvData);
//'								string cntType = "text/html; text/html; charset=UTF-8";
//'								string @params = "";
//'								if (resource.Contains("?"))
//'								{
//'									@params = HttpUtility.UrlDecode(System.Convert.ToString(resource.Split("?".ToCharArray())[1]));
//'									resource = System.Convert.ToString(resource.Split("?".ToCharArray())[0]);
//'								}

//'								switch (resource)
//'								{
//'									case "/":
//'										sndData = System.Text.UTF8Encoding.UTF8.GetBytes(GetUI());
//'										break;
//'									case "/frame":
//'										sndData = GetFrame();
//'										cntType = "image/png";
//'										break;
//'									case "/keyDown":
//'										Keys k_1 = (Keys) (@params.Split("=".ToCharArray())[1]);
//'										if (k_1 == lastKeyDown && (int)(DateTime.Now.Ticks - lastKeyDownTime) < 3000000)
//'										{
//'											break;
//'										}
//'										lastKeyDown = k_1;
//'										lastKeyDownTime = DateTime.Now.Ticks;
//'										cpu.PPI.PutKeyData((System.Int32) lastKeyDown, false);
//'										break;
//'									case "/keyUp":
//'										Keys k = (Keys) (@params.Split("=".ToCharArray())[1]);
//'										if (k == lastKeyUp && (int)(DateTime.Now.Ticks - lastKeyUpTime) < 3000000)
//'										{
//'											break;
//'										}
//'										lastKeyUp = k;
//'										lastKeyUpTime = DateTime.Now.Ticks;
//'										cpu.PPI.PutKeyData((System.Int32) lastKeyUp, true);
//'										break;
//'								}

//'								if (sndData?.Length > 0)
//'								{
//'									System.Text.StringBuilder sb = new System.Text.StringBuilder();
//'									sb.Append("HTTP/1.0 200 OK" + ControlChars.CrLf);
//'									sb.Append("Content-Type: {cntType}{ControlChars.CrLf}");
//'									sb.Append("Content-Length: {sndData.Length}{ControlChars.CrLf}");
//'									sb.Append(ControlChars.CrLf);

//'									byte[] b = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
//'									Array.Resize(ref b, b.Length + sndData.Length);
//'									Array.Copy(sndData, 0, b, sb.ToString().Length, sndData.Length);

//'									netStream.Write(b, 0, b.Length);
//'								}

//'								netStream.Close();
//'							}


//'							tcp.Close();
//'						}

//'					}
//'					else
//'					{
//'						Thread.Sleep(100);
//'					}
//'				}
//'				catch (Exception)
//'				{
//'					X8086.Notify("WebUI Error: {ex.Message}", X8086.NotificationReasons.Err);
//'					//Exit Do
//'				}
//'			} while (client != null);
//'		}

//'		private string GetUI()
//'		{
//'			// FIXME: The zoom compensation is not implemented correctly
//'			return <["DOCTYPE html>"];
//'//			System.Xml.Linq.XElement.Parse("<html lang=\"\"en\"\">" + "<head>" + "<title>x8086SharpEmu WebUI</title>" + "<meta charset=\"\"utf-8\"\">" + "<style>" + "canvas {{" + "padding: 0;" + "margin: auto;" + "display: block;" + "width:  {640 * cpu.VideoAdapter.Zoom};" + "height: {400 * cpu.VideoAdapter.Zoom};" + "position: absolute;" + "top: 0;" + "bottom: 0;" + "left: 0;" + "right: 0;" + "}}" + "</style>" + "<script type=\"\"text/JavaScript\"\">" + "var host = \"\"http://\"\"+window.location.hostname+\"\":8086\"\";" + "var canvas;" + "var context;" + "var xmlHttp = new XMLHttpRequest();" + "var img = new Image();" + "var lastWidth = 0;" + "var lastHeight = 0;" + + "function init() {{" + "canvas = document.getElementById(\"\"x8086\"\");" + "context = canvas.getContext(\"\"2d\"\");" + "setInterval(updateFrame, 60);" + + "document.onkeydown = function(e) {{" + "e = e || window.event;" + "xmlHttp.open(\"\"GET\"\", host + \"\"/keyDown?key=\"\" + e.keyCode, true);" + "xmlHttp.send(null);" + "e.preventDefault();" + "}};" + + "document.onkeyup = function(e) {{" + "e = e || window.event;" + "xmlHttp.open(\"\"GET\"\", host + \"\"/keyUp?key=\"\" + e.keyCode, true);" + "xmlHttp.send(null);" + "e.preventDefault();" + "}};" + + "img.onload = function() {{" + "if((canvas.width != img.width) || (canvas.height = img.height)) {{" + "canvas.width =  {640 * cpu.VideoAdapter.Zoom};" + "canvas.height = {400 * cpu.VideoAdapter.Zoom};" + "lastWidth = img.width;" + "lastHeight = img.height;" + "}}" + "context.imageSmoothingEnabled = false;" + "context.drawImage(img, 0, 0, canvas.width, canvas.height);" + "}};" + "}}" + + "function updateFrame() {{" + "img.src = host + \"\"/frame\"\" + \"\"?d=\"\" + Date.now();" + "}}" + "</script>" + + "<title>x8086 WebUI</title>" + "</head>" + "<body onload=\"\"init()\"\" bgcolor=\"\"#1F1F1F\"\">" + "<canvas tabindex=\"\"1\"\" id=\"\"x8086\"\" width=\"\"640\"\" height=\"\"480\"\"/>" + "</body>" + "</html>");
//'		}

//'		private byte[] GetFrame()
//'		{
//'			try
//'			{
//'				lock(syncObj)
//'				{
//'					// FIXME: When using the VGA adapter and UseVRAM is true we need to send the VGA adapter's RAM instead
//'					return ((byte[]) mBitmap);
//'				}
//'			}
//'			catch
//'			{
//'				return null;
//'			}
//'		}

//'		private void CreateClient()
//'		{
//'			Close();

//'			client = new System.Net.Sockets.TcpListener(IPAddress.Any, 8086);
//'			client.Start();
//'		}

//'		public void Close()
//'		{
//'			if (client != null)
//'			{
//'				try
//'				{
//'					client.Stop();
//'					client = null;
//'				}
//'				catch (Exception)
//'				{
//'				}
//'			}
//'		}

//'		private Dictionary<string, string> GetParams(string @params)
//'		{
//'			Dictionary<string, string> data = new Dictionary<string, string>();

//'			string[] tokens = @params.ToLower().Split('&');
//'			foreach (var token in tokens)
//'			{
//'				if (token.Contains("="))
//'				{
//'					string[] subTokens = token.Split('=');
//'					data.Add(subTokens[0], subTokens[1]);
//'				}
//'				else
//'				{
//'					data.Add(token, "");
//'				}
//'			}
//'			return data;
//'		}

//'		private string GetResource(string data)
//'		{
//'			return ((data.StartsWith("GET /")) ? (data.Split(" ".ToCharArray())[1]) : "404");
//'		}
//'	}

//'}

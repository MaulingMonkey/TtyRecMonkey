using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShinyConsole;
using SlimDX.Windows;
using Font = ShinyConsole.Font;

namespace TtyPlayer {
	[System.ComponentModel.DesignerCategory("")]
	class VT100Form : BasicShinyConsoleForm<VT100Form.VT100Character> {
		public struct VT100Character : IConsoleCharacter {
			public new uint Foreground, Background;
			public bool Bold, LowIntensity, Underline, Blink, Reverse, Invisible, Italic;

			public uint ActualForeground;// { get { return base.Foreground; } set { base.Foreground = value; }}
			public uint ActualBackground;// { get { return base.Background; } set { base.Background = value; }}
			public char Glyph;

			uint IConsoleCharacter.Foreground { get { return ActualForeground; }}
			uint IConsoleCharacter.Background { get { return ActualBackground; }}
			Font IConsoleCharacter.Font       { get { return (Bold==LowIntensity) ? StandardFont : Bold ? AlternateFont : StandardFont; }}
			char IConsoleCharacter.Glyph      { get { return Glyph; }}
		}

		static readonly Font StandardFont  = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",7f,FontStyle.Regular), 8, 12 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",7f,FontStyle.Bold   ), 8, 12 );

		Point CursorPosition      = new Point(0,0);
		Point SavedCursorPosition = new Point(0,0);

		VT100Character Prototype = new VT100Character()
			{ Foreground = 0xFFFFFFFFu
			, Background = 0xFF000000u
			, Glyph      = ' '
			, Blink      = false
			};

		public VT100Form(): base(80,50) {
			Text = "TtyPlayer#";

			GlyphSize = new Size(8,12);
			ClientSize = new Size( 80*GlyphSize.Width, 50*GlyphSize.Height );

			for ( int y=0 ; y<Height ; ++y )
			for ( int x=0 ; x<Width  ; ++x )
			{
				Buffer[x,y] = Prototype;
			}
		}

		public override void Redraw() {
			var now = DateTime.Now;
			int w=Width, h=Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
				//bool flipbg = (Mode.ScreenReverse!=Buffer[x,y].Reverse);
				//bool flipfg = (Buffer[x,y].Invisible || (Buffer[x,y].Blink && now.Millisecond<500) ) ? !flipbg : flipbg;
				bool flipbg=false, flipfg=false;

				Buffer[x,y].ActualForeground = flipfg ? Buffer[x,y].Background : Buffer[x,y].Foreground;
				Buffer[x,y].ActualBackground = flipbg ? Buffer[x,y].Foreground : Buffer[x,y].Background;
			}

			base.Redraw();
		}

		void Resize( int w, int h ) {
			var newbuffer = new VT100Character[w,h];

			var ow = Width;
			var oh = Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
				newbuffer[x,y] = (x<ow&&y<oh) ? Buffer[x,y] : Prototype;
			}

			Buffer = newbuffer;
		}

		struct Packet {
			public int      Sec,USec;
			public byte[]   Payload;

			public TimeSpan SinceStart;
		}

		static TimeSpan Delta( Packet before, Packet after ) {
			return new TimeSpan( 0, 0, 0, after.Sec-before.Sec, (after.USec-before.USec)/1000 );
		}

#pragma warning disable 649 // Handle is used! Just not via C#.
		struct Terminal { public IntPtr Handle; }
#pragma warning restore 649

		[DebuggerDisplay("{Character} {attr}")]
		struct PuttyTermChar {
			public uint chr;
			public uint attr;
			public int  cc_next;

			public char Character { get { return (char)chr; }}

			public bool Blink       { get { return (0x200000u & attr) != 0; }}
			public bool Wide        { get { return (0x400000u & attr) != 0; }}
			public bool Narrow      { get { return (0x800000u & attr) != 0; }}
			public bool Bold        { get { return (0x040000u & attr) != 0; }}
			public bool Underline   { get { return (0x080000u & attr) != 0; }}
			public bool Reverse     { get { return (0x100000u & attr) != 0; }}
			public uint ForegroundI { get { return (0x0001FFu & attr) >> 0; }}
			public uint BackgroundI { get { return (0x03FE00u & attr) >> 9; }}
			public uint Colors      { get { return (0x03FFFFu & attr) >> 9; }}

			public uint Foreground { get { return ColorTable[(int)ForegroundI]; }}
			public uint Background { get { return ColorTable[(int)BackgroundI]; }}

			static readonly List<uint> ColorTable;

			static PuttyTermChar() {
				ColorTable = new List<uint>()
					{ 0xFF000000
					, 0xFFCC0000
					, 0xFF00CC00
					, 0xFFCCCC00
					, 0xFF0000CC
					, 0xFFCC00CC
					, 0xFF00CCCC
					, 0xFFCCCCCC

					, 0xFF808080
					, 0xFFFF0000
					, 0xFF00FF00
					, 0xFFFFFF00
					, 0xFF0000FF
					, 0xFFFF00FF
					, 0xFF00FFFF
					, 0xFFFFFFFF
					};

				Debug.Assert( 6*6*6 == 216 );

				for ( uint r=0 ; r<6 ; ++r )
				for ( uint g=0 ; g<6 ; ++g )
				for ( uint b=0 ; b<6 ; ++b )
				{
					ColorTable.Add( (0xFFu<<24) + ((33u*r)<<16) + ((33u*g)<<8) + ((33u*b)<<0) );
				}

				for ( uint grey=0 ; grey<24 ; ++grey ) {
					var component = 0xFFu*(grey+1)/26;
					ColorTable.Add( (0xFFu<<24) + 0x10101u * component );
				}

				Debug.Assert( ColorTable.Count==256 );

				ColorTable.Add( 0xFFE0E0E0 ); // default foreground
				ColorTable.Add( 0xFFFFFFFF ); // default bold foreground
				ColorTable.Add( 0xFF000000 ); // default background
				ColorTable.Add( 0xFF000000 ); // default bold background
				ColorTable.Add( 0xFF00FF00 ); // cursor foreground
				ColorTable.Add( 0xFF00FF00 ); // cursor background
			}
		}

		[DllImport(@"PuttyDLL.dll")]        static extern void     InitializePutty     ();
		[DllImport(@"PuttyDLL.dll")]        static extern Terminal CreatePuttyTerminal ( int width, int height );
		[DllImport(@"PuttyDLL.dll")]        static extern void     DestroyPuttyTerminal( Terminal terminal );
		[DllImport(@"PuttyDLL.dll")] unsafe static extern void     SendPuttyTerminal   ( Terminal terminal, int stderr, byte* data, int length );
		unsafe static void SendPuttyTerminal( Terminal terminal, bool stderr, byte[] data ) {
			fixed ( byte* pinned_data = data ) {
				SendPuttyTerminal( terminal, stderr?1:0, pinned_data, data.Length );
			}
		}
		[DllImport(@"PuttyDLL.dll")]        static extern void           PaintPuttyTerminal  ( Terminal terminal, IntPtr context, int w, int h );
		[DllImport(@"PuttyDLL.dll")] unsafe static extern PuttyTermChar* GetPuttyTerminalLine( Terminal terminal, int y, int unused );

		unsafe static PuttyTermChar[] GetPuttyTerminalLine( Terminal terminal, int y ) {
			var buffer = new PuttyTermChar[80];
			var src = GetPuttyTerminalLine( terminal, y, 0 );
			for ( int x=0 ; x<80 ; ++x ) buffer[x]=src[x];
			return buffer;
		}

		[STAThread] static void Main() {
			var form = new VT100Form() { Visible = true };

			var open = new OpenFileDialog()
				{ CheckFileExists = true
				, DefaultExt = "ttyrec"
				, Filter = "TtyRec Files|*.ttyrec|All Files|*"
				, InitialDirectory = @"I:\home\media\ttyrecs\"
				, Multiselect = false
				, RestoreDirectory = true
				, Title = "Select a TtyRec to play"
				};
			if ( open.ShowDialog(form) != DialogResult.OK ) using ( form ) return;
			var file = open.OpenFile();

			using ( open ) {} open = null;

			var packets = new List<Packet>();

			using ( var reader = new BinaryReader(file) )
			while ( file.Position < file.Length )
			{
				var p = new Packet();
				p.Sec     = reader.ReadInt32();
				p.USec    = reader.ReadInt32();
				var len   = reader.ReadInt32();
				p.Payload = reader.ReadBytes(len);
				packets.Add(p);
			}

			for ( int i=0 ; i<packets.Count ; ++i ) {
				var p = packets[i];
				p.SinceStart = Delta( packets[0], packets[i] );
				packets[i] = p;
			}

			InitializePutty();
			var putty = CreatePuttyTerminal( 80, 25 );

			var start = DateTime.Now;
			var packeti = 0;

			MainLoop mainloop = () => {
				while ( packeti<packets.Count && start+packets[packeti].SinceStart < DateTime.Now ) {
					SendPuttyTerminal( putty, false, packets[packeti].Payload );
					++packeti;
				}

				for ( int y=0 ; y<25 ; ++y ) {
					var line = GetPuttyTerminalLine( putty, y );
					for ( int x=0 ; x<80 ; ++x ) {
						form.Buffer[x,y].Glyph = (char)(byte)(line[x].chr);
#if true
						form.Buffer[x,y].Foreground = line[x].Foreground;
						form.Buffer[x,y].Background = line[x].Background;
#else
						var fgi = line[x].attr>>0 & 0x1FF;
						var bgi = line[x].attr>>9 & 0x1FF;

						if ( fgi<colors.Length ) {
							form.Buffer[x,y].Foreground = colors[fgi];
						} else {
							//Debugger.Break();
						}
						if ( bgi<colors.Length ) {
							form.Buffer[x,y].Background = colors[bgi];
						} else {
							//Debugger.Break();
						}
#endif
					}
				}

				form.Text = string.Format("TtyPlayer# -- Frame {0} of {1}",packeti,packets.Count);
				form.Redraw();
			};
			form.KeyDown += (s,e) => {
				if ( e.KeyCode == Keys.Right && packeti<packets.Count ) SendPuttyTerminal( putty, false, packets[packeti++].Payload );
			};

			MessagePump.Run( form, mainloop );

			DestroyPuttyTerminal( putty );
		}
	}
}

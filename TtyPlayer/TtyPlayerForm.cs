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

#if false
		static readonly Font StandardFont  = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",8f,FontStyle.Regular), 8, 12 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",8f,FontStyle.Bold   ), 8, 12 );
#elif false
		static readonly Font StandardFont  = ShinyConsole.Font.FromBitmap( Properties.Resources.Font1, 8, 12 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromBitmap( Properties.Resources.Font1, 8, 12 );
#else
		static readonly Font StandardFont  = ShinyConsole.Font.FromBitmap( Properties.Resources.Font2, 6, 8 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromBitmap( Properties.Resources.Font2, 6, 8 );
#endif

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

			GlyphSize = new Size(6,8);
			GlyphOverlap = new Size(1,1);
			FitWindowToMetrics();

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

			var putty = Putty.CreatePuttyTerminal( 80, 50 );

			var start = DateTime.Now;
			var packeti = 0;

			var frames = new Queue<DateTime>();

			MainLoop mainloop = () => {
				var now = DateTime.Now;

				frames.Enqueue(now);
				while ( frames.Peek().AddSeconds(1)<now ) frames.Dequeue();

				while ( packeti<packets.Count && start+packets[packeti].SinceStart < DateTime.Now ) {
					Putty.SendPuttyTerminal( putty, false, packets[packeti].Payload );
					++packeti;
				}

				for ( int y=0 ; y<50 ; ++y ) {
					var line = Putty.GetPuttyTerminalLine( putty, y );
					for ( int x=0 ; x<80 ; ++x ) {
						form.Buffer[x,y].Glyph = (char)(byte)(line[x].chr);
						form.Buffer[x,y].Foreground = PuttyTermPalette.Default[ line[x].ForegroundPaletteIndex ];
						form.Buffer[x,y].Background = PuttyTermPalette.Default[ line[x].BackgroundPaletteIndex ];
					}
				}

				form.Text = string.Format("TtyPlayer# -- {2} FPS -- Packet {0} of {1}",packeti,packets.Count,frames.Count);
				form.Redraw();
			};
			form.KeyDown += (s,e) => {
				if ( e.KeyCode == Keys.Right && packeti<packets.Count ) Putty.SendPuttyTerminal( putty, false, packets[packeti++].Payload );
				if ( e.KeyCode == Keys.Z ) form.Zoom++;
				if ( e.KeyCode == Keys.X && form.Zoom>1 ) form.Zoom--;
			};
#if false
			form.MouseClick += (s,e) => {
				var x = (e.X + form.GlyphOverlap.Width /2) / (form.GlyphSize.Width -form.GlyphOverlap.Width );
				var y = (e.Y + form.GlyphOverlap.Height/2) / (form.GlyphSize.Height-form.GlyphOverlap.Height);
				if ( x<0 || form.Width <=x ) return;
				if ( y<0 || form.Height<=y ) return;
				var line = GetPuttyTerminalLine( putty, y );
				MessageBox.Show( form, String.Format("{0},{1} :=\n\tchr:'{2}'\n\tattr:{3}", x, y, (char)line[x].chr, line[x].attr ), "Character Data" );
			};
#endif

			MessagePump.Run( form, mainloop );

			Putty.DestroyPuttyTerminal( putty );
		}
	}
}

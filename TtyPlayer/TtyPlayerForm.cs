using System;
using System.Collections.Generic;
using System.Drawing;
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

		static string PrettyTimeSpan( TimeSpan ts ) {
			return string.Format( "{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds );
		}

		static string PrettyByteCount( long bytes ) {
			if ( bytes<10000 ) return string.Format( "{0:0,0}B", bytes );
			bytes /= 1000;
			if ( bytes<10000 ) return string.Format( "{0:0,0}KB", bytes );
			bytes /= 1000;
			if ( bytes<10000 ) return string.Format( "{0:0,0}MB", bytes );
			bytes /= 1000;
			if ( bytes<10000 ) return string.Format( "{0:0,0}GB", bytes );
			bytes /= 1000;
			if ( bytes<10000 ) return string.Format( "{0:0,0}TB", bytes );
			bytes /= 1000;
			return string.Format( "{0:0,0}PB", bytes );
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

			var decoder = new TtyRecDecoder();
#if true
			decoder.StartDecoding(file);
#elif true
			decoder.StartDecoding( file, a=> { try { form.BeginInvoke(a); } catch ( InvalidOperationException ) {} } );
#else
			decoder.DecodeAll(file);
#endif

			var speed = +1;
			var seek = TimeSpan.Zero;
			var frames = new List<DateTime>();

			var previous_frame = DateTime.Now;
			MainLoop mainloop = () => {
				var now = DateTime.Now;

				frames.Add(now);
				frames.RemoveAll(f=>f.AddSeconds(1)<now);

				var dt = Math.Max( 0, Math.Min( 0.1, (now-previous_frame).TotalSeconds ) );
				previous_frame = now;

				seek += TimeSpan.FromSeconds(dt*speed);

				decoder.Seek( seek );

				var frame = decoder.CurrentFrame.Data;
				if ( frame != null )
				for ( int y=0 ; y<50 ; ++y )
				for ( int x=0 ; x<80 ; ++x )
				{
					var ch = (x<frame.GetLength(0) && y<frame.GetLength(1)) ? frame[x,y] : default(PuttyTermChar);

					form.Buffer[x,y].Glyph = (char)(byte)(ch.chr);
					form.Buffer[x,y].Foreground = PuttyTermPalette.Default[ ch.ForegroundPaletteIndex ];
					form.Buffer[x,y].Background = PuttyTermPalette.Default[ ch.BackgroundPaletteIndex ];
				}

				//form.Text = string.Format("TtyPlayer# -- {2} FPS -- Packet {0} of {1}","???","???",frames.Count);
				form.Text = string.Format
					( "TtyPlayer# -- {0} FPS -- {1} @ {2} of {3} -- (using {4} pagefile) -- Speed {5}"
					, frames.Count
					, PrettyTimeSpan( seek )
					, PrettyTimeSpan( decoder.CurrentFrame.SinceStart )
					, PrettyTimeSpan( decoder.Length )
					, PrettyByteCount( decoder.SizeInBytes )
					, speed
					);
				form.Redraw();
			};
			form.KeyDown += (s,e) => {
				switch ( e.KeyCode ) {
				case Keys.Z: speed=-10; break;
				case Keys.X: speed= -1; break;
				case Keys.C: speed=  0; break;
				case Keys.V: speed= +1; break;
				case Keys.B: speed=+10; break;
				case Keys.A: ++form.Zoom; break;
				case Keys.S: if ( form.Zoom>1 ) --form.Zoom; break;
				}
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

			using ( decoder ) decoder = null;
		}
	}
}

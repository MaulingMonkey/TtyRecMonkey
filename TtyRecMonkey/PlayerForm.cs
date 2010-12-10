// Copyright (c) 2010 Michael B. Edwin Rickert
//
// See the file LICENSE.txt for copying permission.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Putty;
using ShinyConsole;
using SlimDX.Windows;
using Font = ShinyConsole.Font; // Disambiguate from System.Drawing.Font

namespace TtyRecMonkey {
	[System.ComponentModel.DesignerCategory("")]
	class PlayerForm : BasicShinyConsoleForm<PlayerForm.Character> {
		public struct Character : IConsoleCharacter {
			public new uint Foreground, Background;
			public uint ActualForeground;// { get { return base.Foreground; } set { base.Foreground = value; }}
			public uint ActualBackground;// { get { return base.Background; } set { base.Background = value; }}
			public char Glyph;

			uint IConsoleCharacter.Foreground { get { return ActualForeground; }}
			uint IConsoleCharacter.Background { get { return ActualBackground; }}
			Font IConsoleCharacter.Font       { get { return StandardFont; }}
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

		Character Prototype = new Character()
			{ Foreground = 0xFFFFFFFFu
			, Background = 0xFF000000u
			, Glyph      = ' '
			};

		public PlayerForm(): base(80,50) {
			Text = "TtyRecMonkey";

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

		new void Resize( int w, int h ) {
			var newbuffer = new Character[w,h];

			var ow = Width;
			var oh = Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
				newbuffer[x,y] = (x<ow&&y<oh) ? Buffer[x,y] : Prototype;
			}

			Buffer = newbuffer;
		}

		static string PrettyTimeSpan( TimeSpan ts ) {
			return string.Format( "{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds );
		}

		static string PrettyByteCount( long bytes ) {
			if ( bytes<10000L ) return string.Format( "{0:0,0}B", bytes );
			bytes /= 1000;
			if ( bytes<10000L ) return string.Format( "{0:0,0}KB", bytes );
			bytes /= 1000;
			if ( bytes<10000L ) return string.Format( "{0:0,0}MB", bytes );
			bytes /= 1000;
			if ( bytes<10000L ) return string.Format( "{0:0,0}GB", bytes );
			bytes /= 1000;
			if ( bytes<10000L ) return string.Format( "{0:0,0}TB", bytes );
			bytes /= 1000;
			return string.Format( "{0:0,0}PB", bytes );
		}

		[STAThread] static void Main() {
			var form = new PlayerForm() { Visible = true };

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

#if true
			var decoder = new TtyRecKeyframeDecoder(file);
#else
			var decoder = new TtyRecDecoder();
			decoder.StartDecoding(file);
#endif

			var speed = +1;
			var seek = TimeSpan.Zero;
			var frames = new List<DateTime>();

			long framenum = 0;

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
					var ch = (x<frame.GetLength(0) && y<frame.GetLength(1)) ? frame[x,y] : default(TerminalCharacter);

					form.Buffer[x,y].Glyph      = ch.Character;
					form.Buffer[x,y].Foreground = Palette.Default[ ch.ForegroundPaletteIndex ];
					form.Buffer[x,y].Background = Palette.Default[ ch.BackgroundPaletteIndex ];
				}

				if ( ++framenum%100 == 0 ) form.Text = string.Format
					( "TtyPlayer# -- {0} FPS -- {1} @ {2} of {3} ({6} keyframes) -- (using {4} pagefile) -- Speed {5}"
					, frames.Count
					, PrettyTimeSpan( seek )
					, PrettyTimeSpan( decoder.CurrentFrame.SinceStart )
					, PrettyTimeSpan( decoder.Length )
					, PrettyByteCount( decoder.SizeInBytes )
					, speed
					, decoder.Keyframes
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

			MessagePump.Run( form, mainloop );

			using ( decoder ) {} decoder = null;
		}
	}
}

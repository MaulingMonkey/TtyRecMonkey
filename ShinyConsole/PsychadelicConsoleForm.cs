using System;
using System.Collections.Generic;
using SlimDX.Windows;

namespace ShinyConsole {
	[System.ComponentModel.DesignerCategory("")]
	class PsychedelicConsoleForm : BasicShinyConsoleForm<ConsoleCharacter> {
		readonly List<DateTime> FrameTimes = new List<DateTime>();
		readonly Gradient1D Gradiant = new Gradient1D()
			{ { 0/6f, 0xFFFF0000 }
			, { 1/6f, 0xFFFFFF00 }
			, { 2/6f, 0xFF00FF00 }
			, { 3/6f, 0xFF00FFFF }
			, { 4/6f, 0xFF0000FF }
			, { 5/6f, 0xFFFF00FF }
			, { 6/6f, 0xFFFF0000 }
			};

		public PsychedelicConsoleForm( int w, int h ): base(w,h) {}

		public override void Redraw() {
			var now = DateTime.Now;
			FrameTimes.Add(now);
			FrameTimes.RemoveAll(t=>t<now.AddSeconds(-1) || t>now);
			var s = string.Format("......{0} FPS",FrameTimes.Count);
			for ( int i=0, n=s.Length ; i<n ; ++i ) this[ Width-n+i, 0 ].Glyph = s[i];

			var w=Width;
			var h=Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
#if false
				this[x,y].Foreground = Gradiant.GetColorUInt( ( x*1f/w + now.Second/60f + 0f/2 ) % 1 );
				this[x,y].Background = Gradiant.GetColorUInt( ( x*1f/w + now.Second/60f + 1f/2 ) % 1 );
#else
				this[x,y].Foreground = Gradiant.GetColorUInt( ( x*1f/w + now.Millisecond/1000f + 0f/2 ) % 1 );
				this[x,y].Background = Gradiant.GetColorUInt( ( x*1f/w + now.Millisecond/1000f + 1f/2 ) % 1 );
#endif
			}

			base.Redraw();
		}

		[STAThread] static void Main() {
			var form = new PsychedelicConsoleForm(80,25);
			//form[0,0].Glyph = '!';

			var s = "Hello, World!";
			for ( int i=0 ; i<s.Length ; ++i ) {
				form[i+(80-s.Length)/2,11].Glyph=s[i];
				var ch = form[i+(80-s.Length)/2,12];
				ch.Glyph=s[i];
				ch.Foreground = (i%2==0) ? 0xFFFF0000u : 0xFF0000FFu;
				ch.Background = (i%2!=0) ? 0xFFFF0000u : 0xFF0000FFu;
			}

			MessagePump.Run( form, form.Redraw );
		}
	}
}

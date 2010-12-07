using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShinyConsole {
	public class Font {
		public Bitmap Bitmap;
		public Size   MaxGlyphSize;

		public static Font FromBitmap( System.Drawing.Bitmap bitmap, int glyphw, int glyphh ) {
			return new Font()
				{ Bitmap = bitmap
				, MaxGlyphSize = new Size(glyphw,glyphh)
				};
		}

		public static Font FromGdiFont( System.Drawing.Font gdifont, int glyphw, int glyphh ) {
			for ( int i=0 ; i<256 ; ++i ) {
				var ch = (char)i;
				if ( "\r\n".Contains(ch) ) continue;
				var m = TextRenderer.MeasureText(((char)ch).ToString(),gdifont);
				//if ( m.Width>glyphw || m.Height>glyphh ) throw new ArgumentException( "Glyph size too small" );
			}

			var f = new Font()
				{ Bitmap = new Bitmap( glyphw*16, glyphh*16, System.Drawing.Imaging.PixelFormat.Format32bppArgb )
				, MaxGlyphSize = new Size(glyphw,glyphh)
				};

			using ( var fx = Graphics.FromImage(f.Bitmap) ) {
				fx.Clear( Color.Black );
				for ( int y=0 ; y<16 ; ++y )
				for ( int x=0 ; x<16 ; ++x )
				try
				{
					TextRenderer.DrawText( fx, ((char)(x+y*16)).ToString(), gdifont, new Rectangle( x*glyphw+1, y*glyphh-2, glyphw, glyphh ), Color.White, Color.Black, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter );
				}
				catch ( Exception )
				{
				}
			}

			return f;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ShinyConsole;
using SlimDX.Windows;
using DCH = TtyPlayer.DebugDumpForm.Character;
using Font = ShinyConsole.Font;

namespace TtyPlayer {
	struct VT100AttrChar {
		public uint Char;
		public uint Attr;
	}

	struct VT100Mode {
		public bool Newline; // carriage return after newline?
		public bool VT52; // currently unused
		public bool ScreenReverse;
		public bool AutoWrap;
		public bool ShowCursor;

		public int ScrollRegionStart;
		public int ScrollRegionEnd;
	}

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

		static readonly Font StandardFont  = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",7f,FontStyle.Regular), 12, 12 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",7f,FontStyle.Bold   ), 12, 12 );

		Point CursorPosition      = new Point(0,0);
		Point SavedCursorPosition = new Point(0,0);

		VT100Character Prototype = new VT100Character()
			{ Foreground = 0xFFFFFFFFu
			, Background = 0xFF000000u
			, Glyph      = ' '
			, Blink      = false
			};

		VT100Mode Mode = new VT100Mode()
			{ ScreenReverse = false
			};

		public VT100Form(): base(80,50) {
			Text = "TtyPlayer#";
			Reset();
		}

		public override void Redraw() {
			var now = DateTime.Now;
			int w=Width, h=Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
				bool flipbg = (Mode.ScreenReverse!=Buffer[x,y].Reverse);
				bool flipfg = (Buffer[x,y].Invisible || (Buffer[x,y].Blink && now.Millisecond<500) ) ? !flipbg : flipbg;

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
			Mode.ScrollRegionStart = 0;
			Mode.ScrollRegionEnd   = h;
			Buffer = newbuffer;
			ClampCursor();
		}

		void Clear() {
			var w=Width;
			var h=Height;

			for ( int y=0 ; y<h ; ++y )
			for ( int x=0 ; x<w ; ++x )
			{
				Buffer[x,y] = Prototype;
			}
		}

		void ClampCursor() {
			if ( CursorPosition.X<0 ) CursorPosition.X=0;
			if ( CursorPosition.Y<0 ) CursorPosition.Y=0;
			if ( CursorPosition.X>=Width  ) CursorPosition.X=Width-1;
			if ( CursorPosition.Y>=Height ) CursorPosition.Y=Height-1;

			if ( SavedCursorPosition.X<0 ) CursorPosition.X=0;
			if ( SavedCursorPosition.Y<0 ) CursorPosition.Y=0;
			if ( SavedCursorPosition.X>=Width  ) CursorPosition.X=Width-1;
			if ( SavedCursorPosition.Y>=Height ) CursorPosition.Y=Height-1;
		}

		void PartialLinearClear( int start, int length ) {
			for ( int y=0 ; y<Height ; ++y )
			for ( int x=0 ; x<Width ; ++x )
			{
				var i = x+y*Width;
				if ( start<=i && i<start+length ) Buffer[x,y] = Prototype;
			}
			// clear buffer+start to buffer+start+length
		}

		void PartialLinearClear( Point start, Point end ) {
			var starti = start.X + start.Y*Width;
			var endi   = end  .X + end  .Y*Width;
			PartialLinearClear( starti, endi-starti );
		}

		void Reset() {
			Clear();

			CursorPosition         = new Point(0,0);
			SavedCursorPosition    = new Point(0,0);

			Mode.ScrollRegionStart = 0;
			Mode.ScrollRegionEnd   = Height;
			Mode.AutoWrap          = true;
			Mode.ShowCursor        = true;

			Parse = ParseState.None;
		}

		void Scroll( int newlines ) {
			var w = Width;

			if ( newlines>0 ) for ( int y=Mode.ScrollRegionStart ; y<Mode.ScrollRegionEnd ; ++y ) {
				for ( int x=0 ; x<w ; ++x ) Buffer[x,y] = (y+newlines<Mode.ScrollRegionEnd) ? Buffer[x,y+newlines] : Prototype;
			} else if ( newlines<0 ) for ( int y=Mode.ScrollRegionEnd-1 ; y>=Mode.ScrollRegionStart ; --y ) {
				for ( int x=0 ; x<w ; ++x ) Buffer[x,y] = (y+newlines<Mode.ScrollRegionEnd) ? Buffer[x,y+newlines] : Prototype;
			}
		}

		enum ParseState {
			Esc                     , // <ESC>
			EscLeftBracket          , // <ESC> [
			EscLeftBracketQuestion  , // <ESC> [ ?
			EscLeftBracketParameters, // <ESC> [ ...
			EscLeftParenthesis      , // <ESC> (
			EscRightParenthesis     , // <ESC> )
			EscPercent              , // <ESC> %
			None
		}
		ParseState Parse = ParseState.None;

		void WriteOut( char ch ) {
			if ( CursorPosition.X>=Width ) {
				WriteNewline();
				CursorPosition.X = 0;
			}
			Buffer[CursorPosition.X  ,CursorPosition.Y] = Prototype;
			Buffer[CursorPosition.X++,CursorPosition.Y].Glyph = ch;
			if ( CursorPosition.X>=Width ) {
				WriteNewline();
				CursorPosition.X = 0;
			}
		}
		void WriteNewline() {
			if (++CursorPosition.Y==Height) {
				Scroll(1);
				--CursorPosition.Y;
			}
		}

		static readonly List<int> Parameters = new List<int>();
		void Play( string payload, DCH[] debugout ) {
			Debug.Assert( debugout==null || debugout.Length == payload.Length );

			for ( int i=0 ; i<payload.Length ; ++i ) switch ( Parse ) {
			case ParseState.None:
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFF00FF00u
					};

				switch ( payload[i] ) {
				case '\0': break;
				case '\b': --CursorPosition.X; break;
				case '\t': do { WriteOut(' '); } while ( CursorPosition.X<Width && CursorPosition.X%8!=0 ); break;
				case '\r': CursorPosition.X=0; break;
				case '\n': WriteNewline(); break;
				case (char)12: Clear(); break; // new page / form feed
				//case (char)14: Prototype.Font = AlternateFont; break; // "Shift out"
				//case (char)15: Prototype.Font = AlternateFont; break; // "Shift in"




				case (char)24: // CAN (cancel)
				case (char)26: // SUB (substitute)
					Parse = ParseState.None; // null op?
					break;

				case '\u001B':
					if ( debugout!=null ) debugout[i].Background = 0xFFFF8000u;
					Parse=ParseState.Esc;
					break;
				default:
					if ( payload[i]>(char)26 ) WriteOut(payload[i]);
					else if ( debugout!=null ) debugout[i].Background = 0xFF008000u;
					break;
				}

				//ClampCursor();
				break;
			case ParseState.Esc:
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				switch ( payload[i] ) { // <ESC> payload[i]
				case '[': Parse = ParseState.EscLeftBracket; break;
				case '(': Parse = ParseState.EscLeftParenthesis; break;
				case ')': Parse = ParseState.EscRightParenthesis; break;
				case '%': Parse = ParseState.EscPercent; break;
				case '7': SavedCursorPosition = CursorPosition; Parse = ParseState.None; break;
				case '8': CursorPosition = SavedCursorPosition; Parse = ParseState.None; break;
				case 'D': WriteNewline();                       Parse = ParseState.None; break;
				case 'E': WriteNewline(); CursorPosition.X=0;   Parse = ParseState.None; break;
				case 'M': // reverse newline
					if(--CursorPosition.Y==Height) {
						Scroll(-1);
						++CursorPosition.Y;
					}
					Parse = ParseState.None;
					break;
				case '=': // application keypad mode
				case '>': // numeric keypad mode
					Parse = ParseState.None;
					break;
				default:
					Parse = ParseState.None;
					break;
				}

				if ( Parse == ParseState.None && debugout!=null ) debugout[i].Background = 0xFF808000u;

				break;
			case ParseState.EscLeftBracket: // <ESC> [ payload[i]
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				bool maybe_parameter = false;

				switch ( payload[i] ) {
				case '?': // <ESC> [ ?
					Parse = ParseState.EscLeftBracketQuestion;
					Parameters.Clear();
					Parameters.Add(0);
					break;
				default:
					maybe_parameter = true;
					break;
				}

				if ( maybe_parameter ) {
					Parameters.Clear();
					Parameters.Add(0);
					goto case ParseState.EscLeftBracketParameters;
				}
				break;
			case ParseState.EscLeftBracketParameters:
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				var lasti = Parameters.Count-1;
				if ( '0'<=payload[i] && payload[i]<='9' ) {
					Parameters[lasti] = Parameters[lasti]*10 + (payload[i]-'0');
				} else {
					Parse = ParseState.None; // require explicit continuation

					switch ( payload[i] ) {
					case ';':
						Parameters.Add(0);
						Parse = ParseState.EscLeftBracketParameters; // continue
						break;
					case 'A': // <ESC> [ <n>? A
						Debug.Assert(Parameters.Count==1);
						CursorPosition.Y -= Math.Max(1,Parameters[0]);
						if ( CursorPosition.Y<0 ) CursorPosition.Y=0;
						break;
					case 'B': // <ESC> [ <n>? B
						Debug.Assert(Parameters.Count==1);
						CursorPosition.Y += Math.Max(1,Parameters[0]);
						if ( CursorPosition.Y>=Height ) CursorPosition.Y = Height-1;
						break;
					case 'C': // <ESC> [ <n>? C
						Debug.Assert(Parameters.Count==1);
						CursorPosition.X += Math.Max(1,Parameters[0]);
						if ( CursorPosition.X>=Width ) CursorPosition.X = Width-1;
						break;
					case 'D': // <ESC> [ <n>? D
						Debug.Assert(Parameters.Count==1);
						CursorPosition.X -= Math.Max(1,Parameters[0]);
						if ( CursorPosition.X<0 ) CursorPosition.X=0;
						break;
					case 'G':
						CursorPosition.X = Parameters[0]-1;
						ClampCursor();
						break;
					case 'H':
						CursorPosition.Y = Parameters[0]-1;
						CursorPosition.X = (Parameters.Count>1) ? Parameters[1]-1 : 0;
						ClampCursor();
						break;
					case 'J': // <ESC> [ <n>? J
						Debug.Assert(Parameters.Count==1);
						switch ( Parameters[0] ) {
						case 0: PartialLinearClear( CursorPosition, new Point(Width-1,Height-1) ); break; // Clear screen from cursor
						case 1: PartialLinearClear( new Point(0,0), CursorPosition );              break; // Clear screen to   cursor
						case 2: PartialLinearClear( new Point(0,0), new Point(Width-1,Height-1) ); break; // Clear screen entirely
						default: Debug.Fail( "Invalid escape <ESC> [ "+string.Join( " ; ", Parameters.Select(j=>j.ToString()).ToArray() ) + " J" ); break;
						}
						break;
					case 'K': // <ESC> [ <n>? K
						Debug.Assert(Parameters.Count==1);
						switch ( Parameters[0] ) {
						case 0: PartialLinearClear( CursorPosition, new Point(Width-1,CursorPosition.Y) );                break; // Clear line from cursor
						case 1: PartialLinearClear( new Point(0,CursorPosition.Y), CursorPosition );                      break; // Clear line to   cursor
						case 2: PartialLinearClear( new Point(0,CursorPosition.Y), new Point(Width-1,CursorPosition.Y) ); break; // Clearl ine entirely
						default: Debug.Fail( "Invalid escape <ESC> [ "+string.Join( " ; ", Parameters.Select(j=>j.ToString()).ToArray() ) + " K" ); break;
						}
						break;
					case 'L': // <ESC> [ <n>? L -- inserts n||1 lines into the scrolling region
						if ( CursorPosition.Y < Mode.ScrollRegionStart || Mode.ScrollRegionEnd <= CursorPosition.Y ) break;
						var oldstart = Mode.ScrollRegionStart;
						Mode.ScrollRegionStart = CursorPosition.Y;
						Scroll(-Math.Max(1,Parameters[0]));
						Mode.ScrollRegionStart = oldstart;
						break;
					case 'M': // <ESC> [ <n>? M -- deletes n||1 lines from the scrolling region
						if ( CursorPosition.Y < Mode.ScrollRegionStart || Mode.ScrollRegionEnd <= CursorPosition.Y ) break;
						oldstart = Mode.ScrollRegionStart;
						Mode.ScrollRegionStart = CursorPosition.Y;
						Scroll(-Math.Max(1,Parameters[0]));
						Mode.ScrollRegionStart = oldstart;
						break;
					case 'X':
						PartialLinearClear( CursorPosition, new Point( Math.Min(CursorPosition.X+Math.Max(1,Parameters[0]),Width) ) );
						break;
					case 'c':
						Reset();
						break;
					case 'd':
						CursorPosition.Y = Parameters[0]-1;
						ClampCursor();
						break;
					case 'f':
						goto case 'H';
					case 'm': // <ESC> [ <a> ; ... ; <n> m
						foreach ( var p in Parameters ) {
							switch ( p ) {
							case 0: Prototype.Blink = Prototype.Bold = Prototype.Invisible = Prototype.LowIntensity = Prototype.Reverse = Prototype.Underline = false; break;
							case 1: Prototype.Bold = true; Prototype.LowIntensity = false; break;
							case 2: Prototype.LowIntensity = true; Prototype.Bold = false; break;
							case 3: Prototype.Italic = true; break;
							case 4: Prototype.Underline = true; break;
							case 5: Prototype.Blink = true; break;
							case 7: Prototype.Reverse = true; break;
							case 21: Prototype.Bold = Prototype.LowIntensity = false; break;
							case 22: Prototype.Bold = Prototype.LowIntensity = false; break;
							case 23: Prototype.Italic = false; break;
							case 24: Prototype.Underline = false; break;
							case 25: Prototype.Blink = false; break;
							case 27: Prototype.Reverse = false; break;

							case 30: Prototype.Foreground = 0xFF000000u; break;
							case 31: Prototype.Foreground = 0xFFFF0000u; break;
							case 32: Prototype.Foreground = 0xFF00FF00u; break;
							case 33: Prototype.Foreground = 0xFFFFFF00u; break;
							case 34: Prototype.Foreground = 0xFFF000FFu; break;
							case 35: Prototype.Foreground = 0xFFFF00FFu; break;
							case 36: Prototype.Foreground = 0xFFFFFF00u; break;
							case 37: Prototype.Foreground = 0xFFFFFFFFu; break;

							case 40: Prototype.Background = 0xFF000000u; break;
							case 41: Prototype.Background = 0xFFFF0000u; break;
							case 42: Prototype.Background = 0xFF00FF00u; break;
							case 43: Prototype.Background = 0xFFFFFF00u; break;
							case 44: Prototype.Background = 0xFFF000FFu; break;
							case 45: Prototype.Background = 0xFFFF00FFu; break;
							case 46: Prototype.Background = 0xFFFFFF00u; break;
							case 47: Prototype.Background = 0xFFFFFFFFu; break;
							}
						}
						break;
					case 'r':
						if ( Parameters.SequenceEqual(new[]{0}) ) {
						} else if ( Parameters.Count==2 ) {
							Mode.ScrollRegionStart = Parameters[0]-1;
							Mode.ScrollRegionEnd   = Parameters[1];
						} else {
							Debug.Fail("Invalid number of parameters for <ESC> [ <start> ; <end > r");
						}
						break;
					case 't':
						switch ( Parameters[0] ) {
						case 8:
							Resize( Parameters.Count>2 ? Parameters[2] : Width, Parameters.Count>1 ? Parameters[1] : Height );
							break;
						default:
							Debug.Fail("Unknown extended window manipulation code");
							break;
						}
						break;
					case '`':
						goto case 'G';
					default:
						//Debug.Fail("Unrecognize escape code");
						Parse = ParseState.None;
						break;
					}
					break;
				}
				break;
			case ParseState.EscLeftBracketQuestion: // <ESC> [ ? payload[i]
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				if ( '0' <= payload[i] && payload[i] <= '9' ) {
					lasti = Parameters.Count-1;
					Parameters[lasti] = Parameters[lasti]*10 + (payload[i]-'0');
				} else {
					Parse = ParseState.None;

					switch ( payload[i] ) {
					case ';':
						Parse = ParseState.EscLeftBracketQuestion;
						Parameters.Add(0);
						break;
					case 'h':
						foreach ( var p in Parameters ) switch ( p ) {
						case 7:  Mode.AutoWrap   = true; break;
						case 25: Mode.ShowCursor = true; break;
						default: break; //Debug.Fail("Invalid op"); break;
						}
						break;
					case 'l':
						foreach ( var p in Parameters ) switch ( p ) {
						case 7:  Mode.AutoWrap   = false; break;
						case 25: Mode.ShowCursor = false; break;
						default: break; // Debug.Fail("Invalid op"); break;
						}
						break;
					default:
						//Debug.Fail("Invalid op");
						break;
					}
				}
				break;
			case ParseState.EscLeftParenthesis:
			case ParseState.EscRightParenthesis:
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				// Charset related?
				Parse = ParseState.None;
				break;
			case ParseState.EscPercent:
				if ( debugout!=null ) debugout[i] = new DCH()
					{ Glyph = payload[i]
					, Foreground = 0xFF000000u
					, Background = 0xFFFFFF00u
					};

				switch ( payload[i] ) {
				case '@': // ESC % @
					break;
				case '8':
				case 'G':
					break;
				default:
					//Debug.Fail("Invalid op");
					break;
				}
				Parse = ParseState.None;
				break;
			default:
				throw new InvalidOperationException( "Invalid ParseState" );
			}
		}

		struct Packet {
			public TimeSpan SinceStart;
			public int Sec,USec;
			public string Payload;
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

			var encoding = Encoding.ASCII;
			using ( var reader = new BinaryReader(file) )
			while ( file.Position < file.Length )
			{
				var p = new Packet();
				p.Sec     = reader.ReadInt32();
				p.USec    = reader.ReadInt32();
				var len   = reader.ReadInt32();
				p.Payload = encoding.GetString(reader.ReadBytes(len));
				packets.Add(p);
			}

			for ( int i=0 ; i<packets.Count ; ++i ) {
				var p = packets[i];
				p.SinceStart = Delta( packets[0], packets[i] );
				packets[i] = p;
			}

			//var debug = new DebugDumpForm() { Visible = true };

			var start = DateTime.Now;
			var packeti = 0;
			MainLoop mainloop = () => {
				// TODO:  packet based shenannigans
				while ( packeti<packets.Count && start+packets[packeti].SinceStart < DateTime.Now ) {
					form.Play( packets[packeti].Payload, null );
					++packeti;
				}

				form.Redraw();
			};
			form.KeyDown += (s,e) => {
				var payload = packets[packeti].Payload;
				var l = payload.Length;
				var debugdata = new DebugDumpForm.Character[packets[packeti].Payload.Length];
				for ( int i=0 ; i<debugdata.Length ; ++i ) {
					debugdata[i] = new DebugDumpForm.Character()
						{ Foreground = 0xFF000000
						, Background = 0xFFFF0000
						, Glyph      = payload[i]
						};
				}

				if ( e.KeyCode == Keys.Right ) try {
					form.Play(packets[packeti++].Payload,debugdata);
				} catch ( Exception ) {
				}
				//debug.Payload = debugdata;
			};

			MessagePump.Run( form, mainloop );
		}
	}
}

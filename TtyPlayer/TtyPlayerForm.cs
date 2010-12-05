using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ShinyConsole;
using SlimDX.Windows;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Font = ShinyConsole.Font;

namespace TtyPlayer {
	class VT100Character : ConsoleCharacter {
	}

	[System.ComponentModel.DesignerCategory("")]
	class TtyPlayerForm : BasicShinyConsoleForm<VT100Character> {
		Point CursorPosition = new Point(0,0);
		bool  ShowCursor = true;

		static readonly Font StandardFont  = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",8f,FontStyle.Regular), 12, 12 );
		static readonly Font AlternateFont = ShinyConsole.Font.FromGdiFont( new System.Drawing.Font("Courier New",8f,FontStyle.Bold   ), 12, 12 );

		bool LMN  = false; // when set, \n implies \r
		bool VT52 = false;
		Font CurrentFont       = StandardFont;
		uint CurrentForeground = 0xFFFFFFFFu;
		uint CurrentBackground = 0xFF000000u;
		bool BOLD, LOWINT, UNDERLINE, BLINK, REVERSE, INVISIBLE;

		public TtyPlayerForm(): base(80,25) {
			Text = "TtyPlayer#";
			InitEscapeList();
		}

		struct Packet {
			public int Sec,USec;
			public string Payload;
		}

		static TimeSpan Delta( Packet before, Packet after ) {
			return new TimeSpan( 0, 0, 0, after.Sec-before.Sec, (after.USec-before.USec)/1000 );
		}

		readonly List<Packet> Packets = new List<Packet>();

		void AdvanceLine() {
			if ( ++CursorPosition.Y >= Height ) {
				--CursorPosition.Y;

				for ( int y=Height-1 ; y>0 ; --y ) {
					for ( int x=0 ; x<Width ; ++x ) {
						this[x,y-1].Glyph      = this[x,y].Glyph;
						this[x,y-1].Foreground = this[x,y].Foreground;
						this[x,y-1].Background = this[x,y].Background;
						this[x,y-1].Font       = this[x,y].Font;
					}
				}
				// TODO: Scroll text
			}
		}

		void WriteCharacter( char ch ) {
			switch ( ch ) {
			case '\n':
				AdvanceLine();
				if ( LMN ) CursorPosition.X = 0;
				break;
			case '\r':
				CursorPosition.X=0;
				break;
			case '\t':
				do { WriteCharacter(' '); } while ( CursorPosition.X%8 != 0 );
				break;
			default:
				var cc = this[CursorPosition.X,CursorPosition.Y];
				cc.Glyph      = ch;
				cc.Font       = CurrentFont;
				cc.Foreground = CurrentForeground;
				cc.Background = CurrentBackground;
				if ( ++CursorPosition.X >= Width ) {
					CursorPosition.X=0;
					AdvanceLine();
				}
				break;
			}
		}

		void MoveCursorRelative( int dx, int dy ) {
			CursorPosition.X += dx;
			CursorPosition.Y += dy;
			ClampCursor();
		}

		void ClampCursor() {
			if ( CursorPosition.X < 0 ) CursorPosition.X = 0;
			if ( CursorPosition.Y < 0 ) CursorPosition.Y = 0;
			if ( CursorPosition.X >= Width ) CursorPosition.X = Width-1;
			if ( CursorPosition.Y >= Height ) CursorPosition.Y = Height-1;
		}

		struct SimpleEscapeCode { public string Code; public Action Action; }
		class SimpleEscapeCodeList : List<SimpleEscapeCode> {
			public void Add( string code, Action action ) {
				Add( new SimpleEscapeCode() { Code=code, Action=action } );
			}
		};

		struct RegexEscapeCode { public Regex Regex; public Action<Match> Action; }
		class RegexEscapeCodeList : List<RegexEscapeCode> {
			public void Add( string code, Action<Match> action ) {
				Add( new RegexEscapeCode() { Regex=new Regex(code,RegexOptions.Compiled), Action=action } );
			}
		}

		SimpleEscapeCodeList SimpleEscapeCodes;
		RegexEscapeCodeList  RegexEscapeCodes;

		void InitEscapeList() {
			SimpleEscapeCodes = new SimpleEscapeCodeList()
				{ { "\u001B[20h", ()=>this.LMN = true                 }, { "\u001B[20l", ()=>this.LMN = false                 }
				, { "\u001B[?3h", ()=>this.Resize(132,24)             }, { "\u001B[?3l", ()=>this.Resize(80,24)               }
				, { "\u001B("   , ()=>this.CurrentFont = StandardFont }, { "\u001B)"   , ()=>this.CurrentFont = AlternateFont }

				, { "\u001B[2J", ()=>{
					// Clear screen and home cursor
					Fill(()=>new VT100Character(){Font=CurrentFont, Foreground=CurrentForeground, Background=CurrentBackground, Glyph=' '});
					CursorPosition = new Point(0,0);
				}}

				, { "\u001B[H" , () => CursorPosition = new Point(0,0) }
				, { "\u001B[;H", () => CursorPosition = new Point(0,0) }

				, { "\u001B[m" , () => BOLD = LOWINT = UNDERLINE = BLINK = REVERSE = INVISIBLE = false }
				, { "\u001B[0m", () => BOLD = LOWINT = UNDERLINE = BLINK = REVERSE = INVISIBLE = false }
				, { "\u001B[1m", () => BOLD      = true }
				, { "\u001B[2m", () => LOWINT    = true }
				, { "\u001B[4m", () => UNDERLINE = true }
				, { "\u001B[5m", () => BLINK     = true }
				, { "\u001B[7m", () => REVERSE   = true }
				, { "\u001B[8m", () => INVISIBLE = true }

				// Squelched/ignored escapes:
#if false
				, { "\u001B[?1041l", ()=>{} }
				, { "\u001B[?1051l", ()=>{} }
				, { "\u001B[?1052l", ()=>{} }
				, { "\u001B[?1060l", ()=>{} }
				, { "\u001B[?1061l", ()=>{} }
				, { "\u001B[?1061h", ()=>{} }
#endif
				, { "\u001B[@", ()=>{} } // Set Unicode
				, { "\u001B[G", ()=>{} } // Set non-Unicode
				, { "\u001B[1@", ()=>{} } // Set Unicode???
				
				};

			RegexEscapeCodes = new RegexEscapeCodeList()
				{ { "\u001B\\[(\\d+)(?:;(\\d+))*m", m => {
					for ( int i=1 ; i<m.Groups.Count ; ++i ) {
						switch ( m.Groups[i].Value ) {
						case "0": break; // Reset all attributes
						case "1": break; // Bright
						case "2": break; // Dim
						case "4": break; // Underscore	
						case "5": break; // Blink
						case "7": break; // Reverse
						case "8": break; // Hidden

						//Foreground Colors
						case "30": CurrentForeground = 0xFF000000; break; // Black
						case "31": CurrentForeground = 0xFFFF0000; break; // Red
						case "32": CurrentForeground = 0xFF00FF00; break; // Green
						case "33": CurrentForeground = 0xFFFFFF00; break; // Yellow
						case "34": CurrentForeground = 0xFF0000FF; break; // Blue
						case "35": CurrentForeground = 0xFFFF00FF; break; // Magenta
						case "36": CurrentForeground = 0xFF00FFFF; break; // Cyan
						case "37": CurrentForeground = 0xFFFFFFFF; break; // White

						//Background Colors
						case "40": CurrentBackground = 0xFF000000; break; // Black
						case "41": CurrentBackground = 0xFFFF0000; break; // Red
						case "42": CurrentBackground = 0xFF00FF00; break; // Green
						case "43": CurrentBackground = 0xFFFFFF00; break; // Yellow
						case "44": CurrentBackground = 0xFF0000FF; break; // Blue
						case "45": CurrentBackground = 0xFFFF00FF; break; // Magenta
						case "46": CurrentBackground = 0xFF00FFFF; break; // Cyan
						case "47": CurrentBackground = 0xFFFFFFFF; break; // White
						}
					}
				} }

				, { "\u001B\\[\\?(\\d+)h", m => {
					foreach ( var ch in m.Groups[1].Value )
					switch ( ch ) {
					case '0': LMN=true; break;
					case '1': break; // set cursor key to application
					case '2': break; // not a real code -- would be "Set ANSI"
					case '3': Resize( 132, 24 ); break;
					case '4': break; // set smooth scrolling
					case '5': break; // reverse video on screen -- this means the whole screen?
					case '6': break; // Set origin to relative
					case '7': break; // Set auto wrap mode
					case '8': break; // Set auto repeat mode
					case '9': break; // Set interlacing mode
					default: Debug.Fail( "Invalid mode flag should never happen" ); break;
					}
				} }
				, { "\u001B\\[\\?(\\d+)l", m => {
					foreach ( var ch in m.Groups[1].Value )
					switch ( ch ) {
					case '0': LMN=false; break;
					case '1': break; // set cursor key to cursor
					case '2': VT52=true; break;
					case '3': Resize( 80, 24 ); break;
					case '4': break; // set jump scrolling
					case '5': break; // normal video on screen
					case '6': break; // Set origin to absolute
					case '7': break; // Reset auto wrap mode
					case '8': break; // Reset auto repeat mode
					case '9': break; // Reset interlacing mode
					default: Debug.Fail( "Invalid mode flag should never happen" ); break;
					}
				} }

				, { "\u001B\\[8;(\\d+);(\\d+)t", m => {
					Debugger.Break();
				} } // Set terminal size
				, { "\u001B\\[(\\d+)A", m => MoveCursorRelative( 0, -int.Parse(m.Groups[1].Value)) }
				, { "\u001B\\[(\\d+)B", m => MoveCursorRelative( 0, +int.Parse(m.Groups[1].Value)) }
				, { "\u001B\\[(\\d+)C", m => MoveCursorRelative( +int.Parse(m.Groups[1].Value), 0) }
				, { "\u001B\\[(\\d+)D", m => MoveCursorRelative( -int.Parse(m.Groups[1].Value), 0) }
				, { "\u001B\\[(\\d+);(\\d+)H", m => {
					CursorPosition = new Point( int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value) );
					ClampCursor();
				}}
				};
		}

		string EscapeBuffer = null;
		void Play( string payload ) {
			int i=0;
			if ( EscapeBuffer==null ) for ( ; payload[i]!='\u001B' && i<payload.Length ; ++i ) WriteCharacter(payload[i]);
			if ( i<payload.Length ) EscapeBuffer = (EscapeBuffer??"")+payload.Substring(i);

			if ( !string.IsNullOrEmpty(EscapeBuffer) ) {
				while ( EscapeBuffer.Length>0 && ( TryConsumeNotEscape() || TryConsumeSimpleEscape() || TryConsumeRegexEscape() ) ) {}
				if ( EscapeBuffer.Length>0 ) {
					Debugger.Break();
				} else {
					EscapeBuffer=null;
				}
			}
		}

		bool TryConsumeNotEscape() {
			var esc = EscapeBuffer.IndexOf('\u001B');
			if ( esc==0 ) return false;

			for ( int i=0 ; i<esc ; ++i ) WriteCharacter(EscapeBuffer[i]);
			if ( esc==-1 ) EscapeBuffer="";
			else EscapeBuffer = EscapeBuffer.Substring(esc);
			return true;
		}

		bool TryConsumeSimpleEscape() {
			var esc = SimpleEscapeCodes.Find( sec=>EscapeBuffer.StartsWith(sec.Code) );
			if ( esc.Code==null ) return false;
			EscapeBuffer = EscapeBuffer.Substring(esc.Code.Length);
			esc.Action();
			return true;
		}

		bool TryConsumeRegexEscape() {
			foreach ( var entry in RegexEscapeCodes ) {
				var m = entry.Regex.Match( EscapeBuffer );
				if ( m.Success && m.Index==0 ) {
					entry.Action(m);
					EscapeBuffer = EscapeBuffer.Substring( m.Length );
					return true;
				}
			}
			return false;
		}

		[STAThread] static void Main() {
			var form = new TtyPlayerForm() { Visible = true };

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

			var encoding = Encoding.ASCII;
			using ( var reader = new BinaryReader(file) )
			while ( file.Position < file.Length )
			{
				var p = new Packet();
				p.Sec     = reader.ReadInt32();
				p.USec    = reader.ReadInt32();
				var len   = reader.ReadInt32();
				p.Payload = encoding.GetString(reader.ReadBytes(len));
				form.Packets.Add(p);
			}

			var pthread = new Thread(()=>{
				for ( int i=0 ; i<form.Packets.Count ; ++i ) {
					if (form.IsDisposed || !form.Visible ) return;
					form.BeginInvoke(new Action(()=>{try{form.Play(form.Packets[i].Payload);}catch(Exception){}}));
					if ( i+1 < form.Packets.Count ) {
						var wait = Delta( form.Packets[i+0], form.Packets[i+1] );
						Thread.Sleep(wait);
					}
				}
			});
			pthread.Start();

			MessagePump.Run( form, form.Redraw );
		}
	}
}

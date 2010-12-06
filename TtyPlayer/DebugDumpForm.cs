using ShinyConsole;
using System.Linq;
using Font = ShinyConsole.Font;

namespace TtyPlayer {
	[System.ComponentModel.DesignerCategory("")]
	class DebugDumpForm : BasicShinyConsoleForm<DebugDumpForm.Character> {
		static readonly Font Font = Font.FromGdiFont( new System.Drawing.Font("Courier New", 7f), 12, 12 );

		public struct Character : IConsoleCharacter {
			public uint Foreground;
			public uint Background;
			public char Glyph;

			uint IConsoleCharacter.Foreground { get { return Foreground; }}
			uint IConsoleCharacter.Background { get { return Background; }}
			char IConsoleCharacter.Glyph      { get { return Glyph     ; }}
			Font IConsoleCharacter.Font       { get { return Font      ; }}
		}

		public DebugDumpForm(): base(100,50) {
		}

		public Character[] Payload { set {
			var i = 0;

			for ( int y=0 ; y<Height ; ++y )
			for ( int x=0 ; x<Width  ; ++x )
			{
				bool inrange = i<value.Length;

				Buffer[x,y] = inrange ? value[i] : new Character()
					{ Foreground = 0xFF000000u
					, Background = 0xFF000080u
					, Glyph      = (x%10==9) ? (x/10).ToString().ToCharArray().Last() : '.'
					};

				++i;
			}
		}}
	}
}

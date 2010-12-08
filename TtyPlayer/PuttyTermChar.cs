﻿using System.Diagnostics;

namespace TtyPlayer {
	[DebuggerDisplay("{Character} {attr}")]
	struct PuttyTermChar {
		public uint chr;
		public uint attr;
		public int  cc_next;

		public static bool operator==( PuttyTermChar lhs, PuttyTermChar rhs ) {
			return lhs.chr     == rhs.chr
				&& lhs.attr    == rhs.attr
				&& lhs.cc_next == rhs.cc_next
				;
		}
		public static bool operator!=( PuttyTermChar lhs, PuttyTermChar rhs ) {
			return !(lhs==rhs);
		}
		public override int GetHashCode() {
			return base.GetHashCode();
		}
		public override bool Equals( object obj ) {
			return (obj is PuttyTermChar && this == (PuttyTermChar)obj);
		}

		public char Character { get { return (char)chr; }}

		public bool Blink                  { get { return (0x200000u & attr) != 0; }}
		public bool Wide                   { get { return (0x400000u & attr) != 0; }}
		public bool Narrow                 { get { return (0x800000u & attr) != 0; }}
		public bool Bold                   { get { return (0x040000u & attr) != 0; }}
		public bool Underline              { get { return (0x080000u & attr) != 0; }}
		public bool Reverse                { get { return (0x100000u & attr) != 0; }}
		public int  ForegroundPaletteIndex { get { var fg=(0x0001FFu & attr) >> 0; if ( fg<16 && Bold ) fg|=8; if ( fg>255 && Bold ) fg|=1; return (int)fg; }} // TODO: Reverse modes
		public int  BackgroundPaletteIndex { get { var bg=(0x03FE00u & attr) >> 9; if ( bg<16 && Blink) bg|=8; if ( bg>255 && Blink) bg|=1; return (int)bg; }}
		public uint Colors                 { get { return (0x03FFFFu & attr) >> 9; }}
	}
}

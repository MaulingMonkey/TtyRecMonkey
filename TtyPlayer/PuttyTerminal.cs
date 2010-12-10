using System;

namespace TtyPlayer {
	struct PuttyTerminal {
		IntPtr Handle;

		public static readonly PuttyTerminal Null = new PuttyTerminal() { Handle=IntPtr.Zero };
		public static bool operator==( PuttyTerminal lhs, PuttyTerminal rhs ) { return lhs.Handle == rhs.Handle; }
		public static bool operator!=( PuttyTerminal lhs, PuttyTerminal rhs ) { return lhs.Handle != rhs.Handle; }
		public override bool Equals( object obj ) { return base.Equals(obj); }
		public override int GetHashCode() { return base.GetHashCode(); }
	}
}

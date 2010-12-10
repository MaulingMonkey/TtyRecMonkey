using System.Runtime.InteropServices;

namespace TtyPlayer {
	static class Putty {
		[DllImport(@"PuttyDLL.dll")] public        static extern PuttyTerminal CreatePuttyTerminal ( int width, int height );
		[DllImport(@"PuttyDLL.dll")] public        static extern void          DestroyPuttyTerminal( PuttyTerminal terminal );
		[DllImport(@"PuttyDLL.dll")] public unsafe static extern void          SendPuttyTerminal   ( PuttyTerminal terminal, int stderr, byte* data, int length );
		public unsafe static void SendPuttyTerminal( PuttyTerminal terminal, bool stderr, byte[] data ) {
			fixed ( byte* pinned_data = data ) {
				SendPuttyTerminal( terminal, stderr?1:0, pinned_data, data.Length );
			}
		}
		[DllImport(@"PuttyDLL.dll")] unsafe static extern PuttyTermChar* GetPuttyTerminalLine( PuttyTerminal terminal, int y, int unused );

		public unsafe static PuttyTermChar[] GetPuttyTerminalLine( PuttyTerminal terminal, int y ) {
			var buffer = new PuttyTermChar[80];
			var src = GetPuttyTerminalLine( terminal, y, 0 );
			for ( int x=0 ; x<80 ; ++x ) buffer[x]=src[x];
			return buffer;
		}

		[DllImport(@"PuttyDLL.dll")] public static extern PuttyTerminal ClonePuttyTerminal( PuttyTerminal terminal );
	}
}

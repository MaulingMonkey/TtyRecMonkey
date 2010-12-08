using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Diagnostics;

namespace TtyPlayer {
	struct TtyRecRawFrame {
		public TimeSpan         SinceStart;
		public PuttyTermChar[,] Data;
		public int Width  { get { return Data.GetLength(0); }}
		public int Height { get { return Data.GetLength(1); }}
		public Size Size { get { return new Size(Width,Height); }}
	}

	[DebuggerDisplay("TtyRec Delta Frame (Updates {Right-Left}x{Bottom-Top})")]
	struct TtyRecDeltaFrame {
		public TimeSpan SinceStart;

		public int Top, Left;
		public int Right  { get { return Left+ForwardToThisFrame.GetLength(0); }}
		public int Bottom { get { return Top +ForwardToThisFrame.GetLength(1); }}
		// These should have the same dimensions.  The latter may be skipped if the previous frame's forward buffer was full screen.
		public PuttyTermChar[,] ForwardToThisFrame;
		public PuttyTermChar[,] BackwardsFromThisFrame;

		public int ConsoleWidth, ConsoleHeight;
		public Size ConsoleSize { get { return new Size(ConsoleWidth,ConsoleHeight); }}
	}

	class TtyRecDecoder {
		public static IEnumerable<TtyRecRawFrame> DecodeRawFrames( Stream stream ) {
			var reader = new BinaryReader(stream);
			var term = Putty.CreatePuttyTerminal(80,25);

			var first_sec  = int.MinValue;
			var first_usec = int.MinValue;

			while ( stream.Position < stream.Length ) {
				var sec  = reader.ReadInt32();
				var usec = reader.ReadInt32();
				var len  = reader.ReadInt32();
				var payload = reader.ReadBytes(len);

				if ( first_sec==int.MinValue ) {
					first_sec  = sec;
					first_usec = usec;
				}

				Putty.SendPuttyTerminal( term, false, payload );

				var frame = new TtyRecRawFrame()
					{ SinceStart = new TimeSpan( 100*1000 * (usec-first_usec + 1000*1000*(sec-first_sec)) )
					, Data = new PuttyTermChar[80,25]
					};

				for ( int y=0 ; y<frame.Height ; ++y ) {
					var line = Putty.GetPuttyTerminalLine( term, y );
					for ( int x=0 ; x<frame.Width ; ++x ) frame.Data[x,y] = line[x];
				}

				yield return frame;
			}
		}

		public static IEnumerable<TtyRecDeltaFrame> DecodeDeltaFrames( Stream stream ) {
			return ToDeltaFrames( DecodeRawFrames(stream) );
		}
		public static IEnumerable<TtyRecDeltaFrame> ToDeltaFrames( IEnumerable<TtyRecRawFrame> raw_frames ) {
			TtyRecRawFrame   previous_raw_frame   = default(TtyRecRawFrame);
			TtyRecDeltaFrame previous_delta_frame = default(TtyRecDeltaFrame);

			foreach ( var raw_frame in raw_frames ) {
				if ( previous_raw_frame.Data == null ) { // no previous frame
					previous_raw_frame = raw_frame;
					yield return previous_delta_frame=new TtyRecDeltaFrame()
						{ ForwardToThisFrame = raw_frame.Data
						, BackwardsFromThisFrame = null // impossible to seek backwards
						, Left = 0
						, Top  = 0
						, ConsoleWidth = raw_frame.Width
						, ConsoleHeight = raw_frame.Height
						, SinceStart = raw_frame.SinceStart
						};
				} else { // previous frame is valid
					int top  = 0;
					int left = 0;
					int right  = Math.Max(previous_raw_frame.Width ,raw_frame.Width )-1;
					int bottom = Math.Max(previous_raw_frame.Height,raw_frame.Height)-1;

					while ( right>=left && Enumerable.Range(0,bottom).All( y => {
						var prevchr = right<previous_raw_frame.Width ? previous_raw_frame.Data[right,y] : default(PuttyTermChar);
						var thischr = right<     raw_frame.Width ?      raw_frame.Data[right,y] : default(PuttyTermChar);
						return prevchr == thischr;
					})){
						--right;
					}

					if ( right<left ) continue; // skip this frame, it's content is identical

					while ( right>=left && Enumerable.Range(0,bottom).All( y => {
						var prevchr = right<previous_raw_frame.Width ? previous_raw_frame.Data[left,y] : default(PuttyTermChar);
						var thischr = right<     raw_frame.Width ?      raw_frame.Data[left,y] : default(PuttyTermChar);
						return prevchr == thischr;
					})){
						++left;
					}

					Debug.Assert(right>=left);

					while ( bottom>=top && Enumerable.Range(0,right).All( x => {
						var prevchr = right<previous_raw_frame.Width ? previous_raw_frame.Data[x,bottom] : default(PuttyTermChar);
						var thischr = right<         raw_frame.Width ?          raw_frame.Data[x,bottom] : default(PuttyTermChar);
						return prevchr == thischr;
					})){
						--bottom;
					}

					if ( bottom<top ) continue; // skip this frame, it's content is identical

					while ( bottom>=top && Enumerable.Range(0,right).All( x => {
						var prevchr = right<previous_raw_frame.Width ? previous_raw_frame.Data[x,top] : default(PuttyTermChar);
						var thischr = right<         raw_frame.Width ?          raw_frame.Data[x,top] : default(PuttyTermChar);
						return prevchr == thischr;
					})){
						++top;
					}

					Debug.Assert(bottom>=top);

					var w = right-left+1;
					var h = bottom-top+1;

					var prev_area = new Rectangle
						( previous_delta_frame.Left
						, previous_delta_frame.Top
						, previous_delta_frame.ForwardToThisFrame.GetLength(0)
						, previous_delta_frame.ForwardToThisFrame.GetLength(1)
						);

					var this_area = new Rectangle
						( left
						, top
						, w
						, h
						);

					bool needs_backwards_seek = prev_area.Contains(this_area);

					var frame = new TtyRecDeltaFrame()
						{ SinceStart = raw_frame.SinceStart
						, Left = left
						, Top  = top
						, ForwardToThisFrame     = new PuttyTermChar[w,h]
						, BackwardsFromThisFrame = (needs_backwards_seek ? new PuttyTermChar[w,h] : null)
						};

					for ( int y=top ; y<=bottom ; ++y )
					for ( int x=left ; x<=right ; ++x )
					{
						frame.ForwardToThisFrame[x-left,y-top]
							= (x<raw_frame.Width && y<raw_frame.Height)
							? raw_frame.Data[x,y]
							: default(PuttyTermChar)
							;

						if ( needs_backwards_seek ) frame.BackwardsFromThisFrame[x-left,y-top]
							= (x<previous_raw_frame.Width && y<previous_raw_frame.Height)
							? previous_raw_frame.Data[x,y]
							: default(PuttyTermChar)
							;
					}

					previous_raw_frame = raw_frame;
					yield return previous_delta_frame=frame;
				}
			}
		}
	}
}

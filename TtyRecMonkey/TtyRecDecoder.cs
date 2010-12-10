using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TtyPlayer {
	struct TtyRecPacket {
		public TimeSpan SinceStart;
		public byte[]   Payload;
	}

	struct TtyRecFrame {
		public TimeSpan SinceStart;
		public PuttyTermChar[,] Data;
	}

	class TtyRecDecoder : IDisposable {
		public static IEnumerable<TtyRecPacket> DecodePackets( Stream stream ) {
			var reader = new BinaryReader(stream);

			int first_sec  = int.MinValue;
			int first_usec = int.MinValue;

			while ( stream.Position < stream.Length ) {
				int sec  = reader.ReadInt32();
				int usec = reader.ReadInt32();
				int len  = reader.ReadInt32();

				if ( first_sec==int.MinValue && first_usec==int.MinValue ) {
					first_sec  = sec;
					first_usec = usec;
				}

				var since_start = TimeSpan.FromSeconds(sec-first_sec) + TimeSpan.FromMilliseconds((usec-first_usec)/1000);

				yield return new TtyRecPacket()
					{ SinceStart = since_start
					, Payload    = reader.ReadBytes(len)
					};
			}
		}

		public static IEnumerable<TtyRecFrame> DecodeFrames( Stream stream ) {
			var term = Putty.CreatePuttyTerminal(80,50);

			int frame_number = 0;

			foreach ( var packet in DecodePackets(stream) ) {
				Putty.SendPuttyTerminal( term, false, packet.Payload );

				if ( ++frame_number%10 == 0 ) {
					var clone = Putty.ClonePuttyTerminal(term);
					Putty.DestroyPuttyTerminal(term);
					term = clone;
				}

				var frame = new TtyRecFrame()
					{ SinceStart = packet.SinceStart
					, Data = new PuttyTermChar[80,50]
					};
				for ( int y=0 ; y<50 ; ++y ) {
					var line = Putty.GetPuttyTerminalLine( term, y );
					for ( int x=0 ; x<80 ; ++x ) frame.Data[x,y] = line[x];
				}
				yield return frame;
			}

			Putty.DestroyPuttyTerminal(term);
		}

		struct Header {
			public TimeSpan SinceStart;
			public int Width, Height;
			public long SeekPos;

			public static bool operator==( Header lhs, Header rhs ) {
				return lhs.SinceStart == rhs.SinceStart
					&& lhs.Width      == rhs.Width
					&& lhs.Height     == rhs.Height
					&& lhs.SeekPos    == rhs.SeekPos
					;
			}
			public static bool operator!=( Header lhs, Header rhs ) { return !(lhs==rhs); }
			public override bool Equals( object obj ) { return base.Equals(obj); }
			public override int GetHashCode() { return base.GetHashCode(); }
		}

		readonly List<Header> Frames = new List<Header>();
		public TtyRecFrame CurrentFrame;

		string TempPath;
		Stream Stream;
		Thread Thread;
		bool   StopThread;

		public TtyRecDecoder() {
			TempPath = Path.GetTempFileName();
			Stream = File.Open( TempPath, FileMode.Open, FileAccess.ReadWrite );
		}

		public void DecodeAll( Stream stream ) {
			foreach ( var frame in DecodeFrames(stream) ) {
				Append(frame);
			}
		}

		public void StartDecoding( Stream stream ) {
			Thread = new Thread(()=>{
#if true
				foreach ( var frame in DecodeFrames(stream) ) {
					Thread.MemoryBarrier();
					if (StopThread) break;
					Append(frame);
				}
#elif true
				foreach ( var frame in DecodeFrames(stream) ) {
					Thread.MemoryBarrier();
					var copy = frame;
					sync(()=>{
						Thread.MemoryBarrier();
						this.Append(copy);
						Thread.MemoryBarrier();
					});
					Thread.MemoryBarrier();
				}
#else
				var batch = new List<TtyRecFrame>() { Capacity = 100 };
				foreach ( var frame in DecodeFrames(stream) ) {
					batch.Add(frame);
					if ( batch.Count>=100 ) {
						Thread.MemoryBarrier();
						sync(()=>{
							Thread.MemoryBarrier();
							foreach ( var frame2 in batch ) Append(frame2);
							Thread.MemoryBarrier();
						});
						Thread.MemoryBarrier();
						batch.Clear();
					}
				}
#endif
			});
			Thread.Start();
		}

		public void Dispose() {
			StopThread = true;
			Thread.MemoryBarrier();
			if (!Thread.Join(1000)) Thread.Abort();
			using ( Stream ) Stream = null;
			File.Delete( TempPath );
		}

		public void Append( TtyRecFrame frame ) {
			lock ( Stream ) {
				Stream.Seek(0,SeekOrigin.End);

				var bw = new BinaryWriter(Stream);
				var header = new Header()
					{ SinceStart = frame.SinceStart
					, Width      = frame.Data.GetLength(0)
					, Height     = frame.Data.GetLength(1)
					, SeekPos    = Stream.Length
					};
				for ( int y=0 ; y<frame.Data.GetLength(1) ; ++y )
				for ( int x=0 ; x<frame.Data.GetLength(0) ; ++x )
				{
					bw.Write( frame.Data[x,y].chr  );
					bw.Write( frame.Data[x,y].attr );
				}
				lock ( Frames ) Frames.Add(header);
			}
		}

		long LastSeek=-1;

		void Seek( Header header ) {
			if ( header.SeekPos == LastSeek ) return;
			LastSeek = header.SeekPos;
			Stream.Seek( header.SeekPos, SeekOrigin.Begin );

			CurrentFrame.SinceStart = header.SinceStart;
			if ( CurrentFrame.Data==null || CurrentFrame.Data.GetLength(0) != header.Width || CurrentFrame.Data.GetLength(1) != header.Height ) CurrentFrame.Data = new PuttyTermChar[header.Width,header.Height];

			var br = new BinaryReader(Stream);
			for ( int y=0 ; y<header.Height ; ++y )
			for ( int x=0 ; x<header.Width  ; ++x )
			{
				CurrentFrame.Data[x,y].chr  = br.ReadUInt32();
				CurrentFrame.Data[x,y].attr = br.ReadUInt32();
				CurrentFrame.Data[x,y].cc_next = 0;
			}
		}

		public void Seek( TimeSpan since_start ) {
			lock ( Stream )
			{
				lock ( Frames ) if ( Frames.Count>0 ) {
					var header = Frames.LastOrDefault(f=>f.SinceStart<since_start);
					if ( header != default(Header) ) Seek( header );
				}
			}
		}

		public TimeSpan Length { get {
			lock (Frames) return (Frames.Count>0) ? Frames.Last().SinceStart : TimeSpan.Zero;
		}}

		public long SizeInBytes { get { lock (Stream) return Stream.Length; }}
	}
}

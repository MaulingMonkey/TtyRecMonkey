// Copyright (c) 2010 Michael B. Edwin Rickert
//
// See the file LICENSE.txt for copying permission.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Putty;

namespace TtyRecMonkey {
	struct TtyRecPacket {
		public TimeSpan SinceStart;
		public byte[]   Payload;
	}

	struct TtyRecFrame {
		public TimeSpan SinceStart;
		public TerminalCharacter[,] Data;
	}

	class TtyRecKeyframeDecoder : IDisposable {
		class AnnotatedPacket {
			public TimeSpan             SinceStart;
			public byte[]               Payload;
			public Terminal             RestartPosition;
			public TerminalCharacter[,] DecodedCache;
			public WeakReference        DecodedCacheWeak;
		}

		static IEnumerable<TtyRecPacket> DecodePackets( Stream stream ) {
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

		IEnumerable<AnnotatedPacket> AnnotatePackets( int w, int h, IEnumerable<TtyRecPacket> packets ) {
			using ( var term = new Terminal(w,h) ) {
				var memory_budget3 = Configuration.Main.ChunksTargetMemoryMB * 1000 * 1000;
				var time_budget = TimeSpan.FromMilliseconds( Configuration.Main.ChunksTargetLoadMS );

				var last_restart_position_time = DateTime.MinValue;
				var last_restart_memory_avail  = memory_budget3/3;

				foreach ( var packet in packets ) {
					var now = DateTime.Now;

					bool need_restart
						= (last_restart_position_time+time_budget < now)
						||(last_restart_memory_avail <= 1000)
						;

					yield return new AnnotatedPacket()
						{ SinceStart      = packet.SinceStart
						, Payload         = packet.Payload
						, RestartPosition = need_restart ? new Terminal(term) : null
						};

					if ( need_restart ) {
						last_restart_position_time = now;
						last_restart_memory_avail = memory_budget3/3;
						++Keyframes;
					} else {
						last_restart_memory_avail -= w*h*12;
					}

					term.Send( packet.Payload );
				}
			}
		}

		public void Dispose() {
			foreach ( var ap in Packets ) using ( ap.RestartPosition ) {}
			Packets.Clear();
		}

		TtyRecFrame DumpTerminal( Terminal term, TimeSpan since_start ) {
			var h = Height;
			var w = Width;

			var frame = new TtyRecFrame()
				{ Data = new TerminalCharacter[w,h]
				, SinceStart = since_start
				};

			for ( int y=0 ; y<h ; ++y ) {
				var line = term.GetLine(y);
				for ( int x=0 ; x<w ; ++x ) frame.Data[x,y] = line[x];
			}

			return frame;
		}

		void DumpChunksAround( TimeSpan seektarget ) {
			var before_seek = Packets.FindLastIndex( ap => ap.RestartPosition!=null && ap.SinceStart <= seektarget );
			if ( before_seek == -1 ) before_seek = 0;

			var after_seek = Packets.FindIndex( before_seek+1, ap => ap.RestartPosition!=null && ap.SinceStart > seektarget );
			if ( after_seek == -1 ) after_seek = Packets.Count;

			// we now have goalposts 'before_seek' and 'after_seek' which fence our seek target
			// expand our breadth one more restart pole:

			before_seek = Packets.FindLastIndex( Math.Max(0,before_seek-1), ap=>ap.RestartPosition!=null );
			if ( before_seek == -1 ) before_seek = 0;

			if ( after_seek>=Packets.Count-1 ) {
				after_seek = Packets.Count;
			} else {
				Debug.Assert( after_seek<Packets.Count-1 );
				after_seek = Packets.FindIndex( after_seek+1, ap=>ap.RestartPosition!=null );
				if ( after_seek == -1 ) after_seek = Packets.Count;
			}

			SetActiveRange( before_seek, after_seek );
		}

		void SetActiveRange( int start, int end ) {
			Debug.Assert( start<end );
			Debug.Assert( Packets[start].RestartPosition != null );

			bool need_decode = false;

			// First, we strong reference everything we can, making note if we're missing anything via need_decode:
			for ( int i=start ; i<end ; ++i ) {
				var p = Packets[i];
				if ( p.DecodedCache!=null ) continue;
				var weak = (p.DecodedCache==null) ? null : p.DecodedCacheWeak.Target;

				if ( weak!=null ) {
					p.DecodedCache = (TerminalCharacter[,])weak;
				} else {
					need_decode = true;
				}
			}

			// Next, we stop strong referencing everything otuside this range:
			for ( int i=0   ; i<start         ; ++i ) Packets[i].DecodedCache = null;
			for ( int i=end ; i<Packets.Count ; ++i ) Packets[i].DecodedCache = null;

			if (!need_decode) return;

			// Finally, if necessary, calculate anything we're missing in the range:
			using ( var term = new Terminal( Packets[start].RestartPosition ) )
			for ( int i=start ; i<end ; ++i )
			{
				var p = Packets[i];
				if ( p.DecodedCache==null ) {
					p.DecodedCache = DumpTerminal( term, Packets[i].SinceStart ).Data;
					// p.DecodedCacheWeak = new WeakReference( p.DecodedCache ); // TODO:  Hook up to a configuration flag?  Actually not that useful from the looks of it.
					Packets[i] = p;
				}
				term.Send( p.Payload );
			}
		}

		public void Seek( TimeSpan when ) {
			DumpChunksAround(when);
			var i = Packets.FindLastIndex( ap => ap.SinceStart < when );
			if ( i==-1 ) i=0;
			Debug.Assert(Packets[i].DecodedCache!=null);
			CurrentFrame.SinceStart = Packets[i].SinceStart;
			CurrentFrame.Data       = Packets[i].DecodedCache;
		}

		public TimeSpan Length { get {
			return Packets.Last().SinceStart;
		}}

		readonly List<AnnotatedPacket> Packets;
		public TtyRecFrame CurrentFrame;

		public int Width  { get; private set; }
		public int Height { get; private set; }
		public TtyRecKeyframeDecoder( int w, int h, Stream stream ) {
			Width  = w;
			Height = h;
			Packets = AnnotatePackets( w, h, DecodePackets(stream) ).ToList();
			if ( Packets.Count<=0 ) return;
			CurrentFrame = DumpTerminal( Packets[0].RestartPosition, Packets[0].SinceStart );
		}

		public int  Keyframes { get; private set; }
	}
}

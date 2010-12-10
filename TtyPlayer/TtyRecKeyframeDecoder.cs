using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TtyPlayer {
	class TtyRecKeyframeDecoder : IDisposable {
		struct AnnotatedPacket {
			public TimeSpan         SinceStart;
			public byte[]           Payload;
			public PuttyTerminal    RestartPosition;
			public PuttyTermChar[,] DecodedCache;
		}

		static IEnumerable<AnnotatedPacket> AnnotatePackets( IEnumerable<TtyRecPacket> packets ) {
			var term = Putty.CreatePuttyTerminal(80,50);
			var memory_budget3 = 100 * 1000 * 1000; // ~ 100 MB

			var last_restart_position_time = DateTime.MinValue;
			var last_restart_memory_avail  = memory_budget3/3;

			foreach ( var packet in packets ) {
				var now = DateTime.Now;

				bool need_restart
					= (last_restart_position_time.AddSeconds(0.1) < now)
					||(last_restart_memory_avail <= 1000)
					;

				yield return new AnnotatedPacket()
					{ SinceStart      = packet.SinceStart
					, Payload         = packet.Payload
					, RestartPosition = need_restart ? Putty.ClonePuttyTerminal(term) : PuttyTerminal.Null
					};

				if ( need_restart ) {
					last_restart_position_time = now;
					last_restart_memory_avail = memory_budget3/3;
				} else {
					last_restart_memory_avail -= 80*50*12;
				}

				Putty.SendPuttyTerminal( term, false, packet.Payload );
			}

			Putty.DestroyPuttyTerminal(term);
		}

		public void Dispose() {
			foreach ( var ap in Packets ) if ( ap.RestartPosition != PuttyTerminal.Null ) Putty.DestroyPuttyTerminal(ap.RestartPosition);
			Packets.Clear();
		}

		TtyRecFrame DumpTerminal( PuttyTerminal term, TimeSpan since_start ) {
			var h = 50;
			var w = 80;

			var frame = new TtyRecFrame()
				{ Data = new PuttyTermChar[w,h]
				, SinceStart = since_start
				};

			for ( int y=0 ; y<h ; ++y ) {
				var line = Putty.GetPuttyTerminalLine( term, y );
				for ( int x=0 ; x<w ; ++x ) frame.Data[x,y] = line[x];
			}

			return frame;
		}

		void DumpChunksAround( TimeSpan seektarget ) {
			var preceeding_restart = Packets.FindLastIndex( ap => ap.RestartPosition!=PuttyTerminal.Null && ap.SinceStart <= seektarget );
			if ( preceeding_restart == -1 ) preceeding_restart = 0;

			var postceeding_restart = Packets.FindIndex( preceeding_restart+1, ap => ap.RestartPosition!=PuttyTerminal.Null );
			if ( postceeding_restart == -1 ) postceeding_restart = Packets.Count;

			Decode( preceeding_restart, postceeding_restart );
		}

		void Decode( int start, int end ) {
			Debug.Assert( start<end );
			Debug.Assert( Packets[start].RestartPosition != null );

			if ( Packets[start].DecodedCache != null ) return;

			var term = Putty.ClonePuttyTerminal( Packets[start].RestartPosition );
			for ( int i=start ; i<end ; ++i ) {
				var ap = Packets[i];
				if ( ap.DecodedCache==null ) {
					ap.DecodedCache = DumpTerminal( term, Packets[i].SinceStart ).Data;
					Packets[i] = ap;
				}
				Putty.SendPuttyTerminal( term, false, ap.Payload );
			}
			Putty.DestroyPuttyTerminal(term);
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

		public TtyRecKeyframeDecoder( Stream stream ) {
			Packets = AnnotatePackets( TtyRecDecoder.DecodePackets(stream) ).ToList();
			if ( Packets.Count<=0 ) return;
			CurrentFrame = DumpTerminal( Packets[0].RestartPosition, Packets[0].SinceStart );
		}

		public long SizeInBytes { get {
			return 80*50*12l * (Packets.Count( p => p.RestartPosition != PuttyTerminal.Null ) + Packets.Count( p => p.DecodedCache != null ));
		}}

		public int Keyframes { get { return Packets.Count(ap=>ap.RestartPosition!=PuttyTerminal.Null); }}
	}
}

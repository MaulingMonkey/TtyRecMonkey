using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;

namespace ShinyConsole {
	class Gradient1D : List<Gradient1D.Entry> {
		public struct Entry { public float Key; public uint Value; }
		public void Add( float key, uint value ) {
			if ( Count>0 && this[Count-1].Key >= key ) throw new ArgumentException( "New entry keyed before previous entries" );
			Add( new Entry() { Key=key, Value=value } );
		}

		public Color GetColor( float lerp ) {
			Debug.Assert( this.SequenceEqual( this.OrderBy(e=>e.Key) ) );

			if ( this.Count==0 ) throw new InvalidOperationException( "Requires elements" );
			if ( this.Count==1 ) return Color.FromArgb(unchecked((int)this.First().Value));

			for ( int i=0 ; i<Count-1 ; ++i )
			if ( this[i+0].Key <= lerp && lerp < this[i+1].Key )
			{
				var before = this[i+0];
				var after = this[i+1];
				var real_lerp = (lerp-before.Key)/(after.Key-before.Key);

				var a = Color.FromArgb(unchecked((int)before.Value));
				var b = Color.FromArgb(unchecked((int)after .Value));
				var af = 1-real_lerp;
				var bf = real_lerp;

				return Color.FromArgb
					( Math.Max( 0, Math.Min( 255, (int)(af*a.A + bf*b.A) ) )
					, Math.Max( 0, Math.Min( 255, (int)(af*a.R + bf*b.R) ) )
					, Math.Max( 0, Math.Min( 255, (int)(af*a.G + bf*b.G) ) )
					, Math.Max( 0, Math.Min( 255, (int)(af*a.B + bf*b.B) ) )
					);
			}

			if ( lerp < this[0].Key ) return Color.FromArgb(unchecked((int)this[0      ].Value));
			else                      return Color.FromArgb(unchecked((int)this[Count-1].Value));
		}

		public uint GetColorUInt( float lerp ) {
			return unchecked((uint)GetColor(lerp).ToArgb());
		}
	}
}

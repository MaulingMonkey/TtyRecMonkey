﻿// Copyright (c) 2010 Michael B. Edwin Rickert
//
// See the file LICENSE.txt for copying permission.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using TtyRecMonkey.Properties;

namespace TtyRecMonkey {
	[Serializable] class ConfigurationData1 {
		public bool   ChunksForceGC        = false; // force a garbage collection to unload chunks -- bad idea!
		public int    ChunksTargetMemoryMB =   100; // for all 3 expected 'active' chunks
		public int    ChunksTargetLoadMS   =    10; // per chunk
		public int    FontOverlapX         =     1;
		public int    FontOverlapY         =     1;
		public Bitmap Font                 = Resources.Font2;

		[OptionalField] public int DisplayConsoleSizeW;
		[OptionalField] public int DisplayConsoleSizeH;
		[OptionalField] public int LogicalConsoleSizeW;
		[OptionalField] public int LogicalConsoleSizeH;

		[OptionalField] public Font GdiFont;

		public ConfigurationData1() {
			FillOptionals( default(StreamingContext) );
		}

		[OnDeserialized] void FillOptionals( StreamingContext sc ) {
			if ( DisplayConsoleSizeW == 0 ) DisplayConsoleSizeW = 80;
			if ( DisplayConsoleSizeH == 0 ) DisplayConsoleSizeH = 50;
			if ( LogicalConsoleSizeW == 0 ) LogicalConsoleSizeW = 80;
			if ( LogicalConsoleSizeH == 0 ) LogicalConsoleSizeH = 50;
			if ( GdiFont == null ) GdiFont = new Font("Courier New", 8.25f);
		}
	}

	static class Configuration {
		static readonly string Folder     = Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TtyRecMonkey" );
		static readonly string DataFile   = Path.Combine( Folder, "data.cfg" );

		private static ConfigurationData1 Defaults = new ConfigurationData1();
		public static ConfigurationData1 Main { get; private set; }

		private static ConfigurationData1 Load( Stream data ) {
			var bf = new BinaryFormatter();
			var o = bf.Deserialize(data);
			var cd1 = (ConfigurationData1)o;
			return cd1;
		}

		public static void Load( Form towhineat ) {
			Directory.CreateDirectory(Folder);

			retry:
			Main = new ConfigurationData1();
			try {
				using ( var data = File.OpenRead(DataFile) ) Main = Load(data);
			} catch ( FileNotFoundException ) {
				// Ignore: probably our first run
			} catch ( Exception e ) {
				var result = MessageBox.Show
					( towhineat
					, "There was a problem loading your configuration:\n"
					+ e.Message
					, "Load Error"
					, MessageBoxButtons.AbortRetryIgnore
					, MessageBoxIcon.Error
					);
				switch ( result ) {
				case DialogResult.Retry: goto retry;
				case DialogResult.Abort: Application.Exit(); break;
				case DialogResult.Ignore: break;
				default: throw new ApplicationException("Should never happen!");
				}
			}
		}

		public static void Save( Form towhineat) {
			retry:
			var stream = new MemoryStream();
			var bf = new BinaryFormatter();
			bf.Serialize( stream, Main );
			stream.Position = 0;

			try {
				Load(stream);
			} catch ( Exception e ) {
				var result = MessageBox.Show
					( towhineat
					, "There was a problem saving your configuration:\n"
					+ "Serialization was successful, but deserializing the result threw an exception!\n"
					+ "Save aborted.  The exception was:\n"
					+ e.Message
					, "Save Error"
					, MessageBoxButtons.RetryCancel
					, MessageBoxIcon.Error
					);
				if ( result == DialogResult.Retry ) goto retry;
				return;
			}

			File.WriteAllBytes( DataFile, stream.ToArray() );
		}
	}
}

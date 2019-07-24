using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClapDetector
{
	class WavFile
	{
		public int SamplesPerSecond { get; set; } // frequency
		public int SamplesTotalCount { get; set; }
		public string Filename { get; private set; }
		public byte[] MetaData { get; set; }


		public WavFile(string filename)
		{
			SamplesTotalCount = SamplesPerSecond = -1;
			Filename = filename;
			MetaData = null;
		}

		public bool ReadMono16bit(out short[] samples)
		{
			samples = null;

			using (FileStream fileStream = File.Open(Filename, FileMode.Open))
			using (BinaryReader reader = new BinaryReader(fileStream))
			{
				// header (8 + 4 bytes):

				byte[] riffId = reader.ReadBytes(4);    // "RIFF"
				int fileSize = reader.ReadInt32();      // size of entire file
				byte[] typeId = reader.ReadBytes(4);    // "WAVE"

				if (Encoding.ASCII.GetString(typeId) != "WAVE") return false;

				// chunk 1 (8 + 16 or 18 bytes):

				byte[] fmtId = reader.ReadBytes(4);     // "fmt "
				int fmtSize = reader.ReadInt32();       // size of chunk in bytes
				int fmtCode = reader.ReadInt16();       // 1 - for PCM
				int channels = reader.ReadInt16();      // 1 - mono, 2 - stereo
				int sampleRate = reader.ReadInt32();    // sample rate per second
				int byteRate = reader.ReadInt32();      // bytes per second
				int dataAlign = reader.ReadInt16();     // data align
				int bitDepth = reader.ReadInt16();      // 8, 16, 24, 32, 64 bits

				if (fmtCode != 1) return false;     // not PCM
				if (channels != 1) return false;    // only Mono files in this version
				if (bitDepth != 16) return false;   // only 16-bit in this version

				if (fmtSize == 18) // fmt chunk can be 16 or 18 bytes
				{
					int fmtExtraSize = reader.ReadInt16();  // read extra bytes size
					reader.ReadBytes(fmtExtraSize);         // skip over "INFO" chunk
				}

				// chunk 2 (8 bytes):

				byte[] dataId = reader.ReadBytes(4);    // "data"
				int dataSize = reader.ReadInt32();      // size of audio data

				Debug.Assert(Encoding.ASCII.GetString(dataId) == "data", "Data chunk not found!");

				SamplesPerSecond = sampleRate;                  // sample rate (usually 44100)
				SamplesTotalCount = dataSize;  // total samples count in audio data

				// audio data:

				samples = new short[SamplesTotalCount];

				for (int i = 0; i < SamplesTotalCount; i += 1)
				{
					samples[i] = reader.ReadInt16();
				}

				// metadata:

				long moreBytes = reader.BaseStream.Length - reader.BaseStream.Position;

				if (moreBytes > 0)
				{
					MetaData = reader.ReadBytes((int)moreBytes);
				}
			}
			return true;
		}

		public bool ReadStereo16bit(ref short[] L, ref short[] R)
		{
			using (FileStream fileStream = File.Open(Filename, FileMode.Open))
			using (BinaryReader reader = new BinaryReader(fileStream))
			{
				// header (8 + 4 bytes):

				byte[] riffId = reader.ReadBytes(4);    // "RIFF"
				int fileSize = reader.ReadInt32();      // size of entire file
				byte[] typeId = reader.ReadBytes(4);    // "WAVE"

				if (Encoding.ASCII.GetString(typeId) != "WAVE") return false;

				// chunk 1 (8 + 16 or 18 bytes):

				byte[] fmtId = reader.ReadBytes(4);     // "fmt "
				int fmtSize = reader.ReadInt32();       // size of chunk in bytes
				int fmtCode = reader.ReadInt16();       // 1 - for PCM
				int channels = reader.ReadInt16();      // 1 - mono, 2 - stereo
				int sampleRate = reader.ReadInt32();    // sample rate per second
				int byteRate = reader.ReadInt32();      // bytes per second
				int dataAlign = reader.ReadInt16();     // data align
				int bitDepth = reader.ReadInt16();      // 8, 16, 24, 32, 64 bits

				if (fmtCode != 1) return false;     // not PCM
				if (channels != 2) return false;    // only Stereo files in this version
				if (bitDepth != 16) return false;   // only 16-bit in this version

				if (fmtSize == 18) // fmt chunk can be 16 or 18 bytes
				{
					int fmtExtraSize = reader.ReadInt16();  // read extra bytes size
					reader.ReadBytes(fmtExtraSize);         // skip over "INFO" chunk
				}

				// chunk 2 (8 bytes):

				byte[] dataId = reader.ReadBytes(4);    // "data"
				int dataSize = reader.ReadInt32();      // size of audio data

				Debug.Assert(Encoding.ASCII.GetString(dataId) == "data", "Data chunk not found!");

				SamplesPerSecond = sampleRate;                  // sample rate (usually 44100)
				SamplesTotalCount = dataSize / (bitDepth / 8);  // total samples count in audio data

				// audio data:

				L = R = new short[SamplesTotalCount / 2];

				for (int i = 0, s = 0; i < SamplesTotalCount; i += 2)
				{
					L[s] = reader.ReadInt16();
					R[s] = reader.ReadInt16();
					s++;
				}

				// metadata:

				long moreBytes = reader.BaseStream.Length - reader.BaseStream.Position;

				if (moreBytes > 0)
				{
					MetaData = reader.ReadBytes((int)moreBytes);
				}
			}
			return true;
		}

		public void WriteMono16bit(ReadOnlySpan<short> samples)
		{
			Debug.Assert((SamplesTotalCount != -1) && (SamplesPerSecond != -1), "No sample count or sample rate info!");

			using (FileStream fileStream = File.Create(Filename))
			using (BinaryWriter writer = new BinaryWriter(fileStream))
			{
				int fileSize = 44 + SamplesTotalCount * 2;

				if (MetaData != null)
				{
					fileSize += MetaData.Length;
				}

				// header:

				writer.Write(Encoding.ASCII.GetBytes("RIFF"));  // "RIFF"
				writer.Write((int)fileSize);                  // size of entire file with 16-bit data
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));  // "WAVE"

				// chunk 1:

				writer.Write(Encoding.ASCII.GetBytes("fmt "));  // "fmt "
				writer.Write((int)16);                        // size of chunk in bytes
				writer.Write((short)1);                         // 1 - for PCM
				writer.Write((short)1);                         // only Mono files in this version
				writer.Write((int)SamplesPerSecond);          // sample rate per second (usually 44100)
				writer.Write((int)(2 * SamplesPerSecond));    // bytes per second (usually 88200)
				writer.Write((short)2);                         // data align 4 bytes (2 bytes sample stereo)
				writer.Write((short)16);                        // only 16-bit in this version

				// chunk 2:

				writer.Write(Encoding.ASCII.GetBytes("data"));  // "data"
				writer.Write((int)SamplesTotalCount);   // size of audio data 16-bit

				// audio data:

				for (int i = 0; i < SamplesTotalCount; i += 1)
				{
					writer.Write(samples[i]);
				}

				// metadata:

				if (MetaData != null)
				{
					writer.Write(MetaData);
				}
			}
		}

		public void WriteStereo16bit(short[] L, short[] R)
		{
			Debug.Assert((SamplesTotalCount != -1) && (SamplesPerSecond != -1), "No sample count or sample rate info!");

			using (FileStream fileStream = File.Create(Filename))
			using (BinaryWriter writer = new BinaryWriter(fileStream))
			{
				int fileSize = 44 + SamplesTotalCount * 2;

				if (MetaData != null)
				{
					fileSize += MetaData.Length;
				}

				// header:

				writer.Write(Encoding.ASCII.GetBytes("RIFF"));  // "RIFF"
				writer.Write((int)fileSize);                  // size of entire file with 16-bit data
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));  // "WAVE"

				// chunk 1:

				writer.Write(Encoding.ASCII.GetBytes("fmt "));  // "fmt "
				writer.Write((int)16);                        // size of chunk in bytes
				writer.Write((short)1);                         // 1 - for PCM
				writer.Write((short)2);                         // only Stereo files in this version
				writer.Write((int)SamplesPerSecond);          // sample rate per second (usually 44100)
				writer.Write((int)(4 * SamplesPerSecond));    // bytes per second (usually 176400)
				writer.Write((short)4);                         // data align 4 bytes (2 bytes sample stereo)
				writer.Write((short)16);                        // only 16-bit in this version

				// chunk 2:

				writer.Write(Encoding.ASCII.GetBytes("data"));  // "data"
				writer.Write((int)(SamplesTotalCount * 2));   // size of audio data 16-bit

				// audio data:

				for (int i = 0, s = 0; i < SamplesTotalCount; i += 2)
				{
					writer.Write(L[s]);
					writer.Write(R[s]);
					s++;
				}

				// metadata:

				if (MetaData != null)
				{
					writer.Write(MetaData);
				}
			}
		}
	}
}

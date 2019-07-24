using OpenAL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ClapDetector
{
	class Program
	{
		static void Main(string[] args)
		{
			// inspired by: https://stackoverflow.com/questions/4087727/openal-how-to-create-simple-microphone-echo-programm

			var audioDevice = Alc.OpenDevice(null);

			//var errorCode = Alc.GetError(audioDevice); // TODO: check this after each Alc call
			//var errorCode = Al.GetError(); // TODO: check this after each Al call

			var context = Alc.CreateContext(audioDevice, null);
			Alc.MakeContextCurrent(context);

			const int frequency = 44100;

			var captureDevice = Alc.CaptureOpenDevice(devicename: null, frequency, format: Al.FormatMono16, buffersize: frequency);

			string deviceName = Alc.GetString(captureDevice, Alc.DeviceSpecifier);

			Console.WriteLine($"{deviceName} is open for capture.");

			Alc.CaptureStart(captureDevice);

			Console.WriteLine("listening for keywords..."); // need new line for intensity visualization

			short[] buffer = new short[frequency];
			const int keywordLength = frequency * 1;
			double? previousRMS = null;

			List<short> keywordSamples = new List<short>(capacity: keywordLength);
			bool isRecording = false;
			unsafe
			{
				fixed (int* samplesAvailable = new int[1])
				fixed (short* bufferPointer = buffer)
				{
					IntPtr samplesAvailablePointer = new IntPtr((void*)samplesAvailable);
					int captureCount = 2048; // (captureCount / frequency) seconds
					while (keywordSamples.Count < keywordLength)
					{
						Alc.GetIntegerv(captureDevice, Alc.EnumCaptureSamples, 1, samplesAvailablePointer);
						int samplesAvailableCount = samplesAvailable[0];
						if (samplesAvailableCount > captureCount)
						{
							Alc.CaptureSamples(captureDevice, (void*)bufferPointer, samplesAvailableCount);

							// TODO: ... do something with the buffer
							double rms = calculateRMS(buffer, samplesAvailableCount);

							double maxAmplitude = short.MaxValue;
							double intensity = rms / maxAmplitude;
							printIntensity(intensity);

							// compare current rms with previous values to find beginning of a keyword
							const double factor = 10;
							if (rms > previousRMS * factor) // beginning of window
							{
								isRecording = true;
								Console.Beep(10000, 10);
							}

							if (isRecording)
							{
								for (int i = 0; i < samplesAvailableCount; i++)
								{
									keywordSamples.Add(buffer[i]);
								}
							}

							previousRMS = rms;
						}
					}
				}
			}
			Alc.CaptureStop(captureDevice);
			resetConsoleSettings();

			// write output
			{
				var wavFile = new WavFile("keyword.wav") { SamplesPerSecond = frequency, SamplesTotalCount = keywordSamples.Count };
				wavFile.WriteMono16bit(keywordSamples.ToArray());
			}

			// clean up
			Alc.CaptureCloseDevice(captureDevice);
			Alc.MakeContextCurrent(IntPtr.Zero);
			Alc.DestroyContext(context);
			Alc.CloseDevice(audioDevice);

		}

		private static void printIntensity(double intensity)
		{
			Console.CursorVisible = false;
			clearCurrentConsoleLine();
			int characterCount = (int)Math.Round(Console.WindowWidth * intensity);
			for (int i = 0; i < characterCount; i++)
			{
				double value = (double)i / Console.WindowWidth;
				if (value > 0.66)
					Console.ForegroundColor = ConsoleColor.Red;
				else if (value > 0.33)
					Console.ForegroundColor = ConsoleColor.Yellow;
				else
					Console.ForegroundColor = ConsoleColor.Green;
				Console.Write('■');
			}
		}

		private static void resetConsoleSettings()
		{
			Console.ResetColor();
			Console.CursorVisible = true;
		}

		private static double calculateRMS(short[] values, int count)
		{
			if (count <= 0 || count > values.Length) throw new ArgumentOutOfRangeException(nameof(count), "Argument must be positive integer lower that the values array length.");
			double sqrSum = 0;
			for (int i = 0; i < count; i++)
			{
				sqrSum += (double)values[i] * values[i];
			}
			double sqrSumAvg = sqrSum / count;
			return Math.Sqrt(sqrSumAvg);
		}

		private static void clearCurrentConsoleLine()
		{
			int currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}
	}
}

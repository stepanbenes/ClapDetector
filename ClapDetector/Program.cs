using NAudio.Dsp;
using OpenAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ClapDetector
{
	class Program
	{
		static async Task Main(string[] args)
		{

			// =============================================================================================================
			// inspired by: https://stackoverflow.com/questions/4087727/openal-how-to-create-simple-microphone-echo-programm

			var audioDevice = Alc.OpenDevice(null);

			//var errorCode = Alc.GetError(audioDevice); // TODO: check this after each Alc call
			//var errorCode = Al.GetError(); // TODO: check this after each Al call

			var context = Alc.CreateContext(audioDevice, null);
			Alc.MakeContextCurrent(context);

			const int frequency = 44100;
			const int keywordLength = (int)(frequency * 1.1);

			var captureDevice = Alc.CaptureOpenDevice(devicename: null, frequency, format: Al.FormatMono16, buffersize: frequency);

			string deviceName = Alc.GetString(captureDevice, Alc.DeviceSpecifier);

			Console.WriteLine($"{deviceName} is open for capture.");

			if (args.Length > 0 && args[0] == "record")
			{
				while (true)
				{
					Console.Write("Enter keyword name: ");
					string keywordName = Console.ReadLine();
					if (string.IsNullOrWhiteSpace(keywordName)) // exit if nothing entered
						break;

					Alc.CaptureStart(captureDevice);
					Console.WriteLine("recording keyword..."); // need new line for intensity visualization

					short[] samples = captureSamples(captureDevice, frequency, keywordLength);

					Alc.CaptureStop(captureDevice);

					// write output
					{
						string keywordFilename = $"{keywordName}.wav";
						Debug.Assert(samples.Length >= keywordLength);
						var wavFile = new WavFile(keywordFilename) { SamplesPerSecond = frequency, SamplesTotalCount = keywordLength /* trim samples */ };
						wavFile.WriteMono16bit(samples);
						Console.WriteLine($"'{keywordFilename}' was saved.");
					}
				}
			}
			else
			{
				Alc.CaptureStart(captureDevice);
				Console.WriteLine("listening for keyword..."); // need new line for intensity visualization
				while (true)
				{
					short[] samples = captureSamples(captureDevice, frequency, keywordLength);

					var fftData = calculateFFT(samples);
				}
				Alc.CaptureStop(captureDevice);
			}

			// clean up
			Alc.CaptureCloseDevice(captureDevice);
			Alc.MakeContextCurrent(IntPtr.Zero);
			Alc.DestroyContext(context);
			Alc.CloseDevice(audioDevice);

		}

		private unsafe static short[] captureSamples(IntPtr captureDevice, int frequency, int sampleCount)
		{
			List<short> samples = new List<short>(capacity: sampleCount);

			bool isRecording = false;
			double? previousRMS = null;
			short[] buffer = new short[frequency];
			fixed (int* samplesAvailable = new int[1])
			fixed (short* bufferPointer = buffer)
			{
				while (samples.Count < sampleCount)
				{
					IntPtr samplesAvailablePointer = new IntPtr((void*)samplesAvailable);
					Alc.GetIntegerv(captureDevice, Alc.EnumCaptureSamples, 1, samplesAvailablePointer);
					int samplesAvailableCount = samplesAvailable[0];
					const int captureCount = 2048;
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
							//Console.Beep(10000, 10);
						}

						if (isRecording)
						{
							for (int i = 0; i < samplesAvailableCount; i++)
							{
								samples.Add(buffer[i]);
							}
						}

						previousRMS = rms;
					}
				}
			}

			resetConsoleSettings();

			return samples.ToArray();
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

		private static double[] calculateFFT(short[] dataPcm)
		{
			// the PCM size to be analyzed with FFT must be a power of 2
			int fftPoints = 2;
			while (fftPoints * 2 <= dataPcm.Length)
				fftPoints *= 2;

			// apply a Hamming window function as we load the FFT array then calculate the FFT
			Complex[] fftFull = new Complex[fftPoints];
			for (int i = 0; i < fftPoints; i++)
				fftFull[i].X = (float)(dataPcm[i] * FastFourierTransform.HammingWindow(i, fftPoints));
			FastFourierTransform.FFT(true, (int)Math.Log(fftPoints, 2.0), fftFull);

			// copy the complex values into the double array that will be plotted
			double[] dataFft = new double[fftPoints / 2];
			for (int i = 0; i < fftPoints / 2; i++)
			{
				double fftLeft = Math.Abs(fftFull[i].X + fftFull[i].Y);
				double fftRight = Math.Abs(fftFull[fftPoints - i - 1].X + fftFull[fftPoints - i - 1].Y);
				dataFft[i] = fftLeft + fftRight;
			}
			return dataFft;
		}
	}
}

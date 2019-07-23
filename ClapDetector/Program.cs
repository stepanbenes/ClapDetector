using OpenAL;
using System;
using System.Threading.Tasks;

namespace ClapDetector
{
	class Program
	{
		static async Task Main(string[] args)
		{

			// inspired by: https://stackoverflow.com/questions/4087727/openal-how-to-create-simple-microphone-echo-programm

			var audioDevice = Alc.OpenDevice(null);

			//var errorCode = Alc.GetError(audioDevice); // TODO: check this after each Alc call
			//var errorCode = Al.GetError(); // TODO: check this after each Al call

			var context = Alc.CreateContext(audioDevice, null);
			Alc.MakeContextCurrent(context);

			const int frequency = 22050;

			var captureDevice = Alc.CaptureOpenDevice(devicename: null, frequency, format: Al.FormatMono16, buffersize: frequency);

			Alc.CaptureStart(captureDevice);

			Console.WriteLine("Clap detector is listening..."); // need new line for intensity visualization

			short[] buffer = new short[frequency];
			unsafe
			{
				fixed (int* samplesAvailable = new int[1])
				fixed (short* bufferPointer = buffer)
				{
					IntPtr samplesAvailablePointer = new IntPtr((void*)samplesAvailable);
					int captureCount = 2048;
					while (true)
					{
						Alc.GetIntegerv(captureDevice, Alc.EnumCaptureSamples, 1, samplesAvailablePointer);
						int samplesAvailableCount = samplesAvailable[0];
						if (samplesAvailableCount > captureCount)
						{
							Alc.CaptureSamples(captureDevice, (void*)bufferPointer, samplesAvailableCount);

							// ... do something with the buffer
							double energy = 0;
							for (int i = 0; i < samplesAvailableCount; i++)
							{
								energy += Math.Abs((double)buffer[i]) / (double)short.MaxValue;
							}
							double intensity = energy / samplesAvailableCount;
							printIntensity(intensity);
						}
					}
				}
			}
			Alc.CaptureStop(captureDevice);

			Alc.CaptureCloseDevice(captureDevice);
			Alc.MakeContextCurrent(IntPtr.Zero);
			Alc.DestroyContext(context);
			Alc.CloseDevice(audioDevice);

			void printIntensity(double intensity)
			{
				ClearCurrentConsoleLine();
				int characterCount = (int)Math.Round(Console.WindowWidth * intensity);
				for (int i = 0; i < characterCount; i++)
				{
					double value = (double)i / Console.WindowWidth;
					if (value > 0.8)
						Console.ForegroundColor = ConsoleColor.Red;
					else if (value > 0.5)
						Console.ForegroundColor = ConsoleColor.Yellow;
					else
						Console.ForegroundColor = ConsoleColor.Green;
					Console.Write('\u25A0');
				}
				Console.ResetColor();
			}
		}

		public static void ClearCurrentConsoleLine()
		{
			int currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}
	}
}

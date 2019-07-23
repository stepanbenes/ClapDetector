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

			var captureDevice = Alc.CaptureOpenDevice(devicename: null, frequency, format: Al.FormatMono16, buffersize: frequency * 5); // 1 second window

			Alc.CaptureStart(captureDevice);
			unsafe
			{
				fixed (int* samplesAvailable = new int[1])
				fixed (short* buffer = new short[frequency])
				{
					IntPtr samplesAvailablePointer = new IntPtr((void*)samplesAvailable);
					int captureSize = 2048;
					while (true)
					{
						Alc.GetIntegerv(captureDevice, Alc.EnumCaptureSamples, 1, samplesAvailablePointer);
						if (samplesAvailable[0] > captureSize)
						{
							Alc.CaptureSamples(captureDevice, (void*)buffer, samplesAvailable[0]);

							// ... do something with the buffer
							long energy = 0;
							for (int i = 0; i < samplesAvailable[0]; i++)
							{
								energy += Math.Abs(buffer[i]);
							}
							Console.WriteLine(energy);
						}
					}
				}
			}
			Alc.CaptureStop(captureDevice);

			Alc.CaptureCloseDevice(captureDevice);
			Alc.MakeContextCurrent(IntPtr.Zero);
			Alc.DestroyContext(context);
			Alc.CloseDevice(audioDevice);
		}
	}
}

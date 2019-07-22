using OpenAL;
using System;
using System.Linq;

namespace ClapDetector
{
	class Program
	{
		static void Main(string[] args)
		{
			var device = Alc.OpenDevice(null);
			var context = Alc.CreateContext(device, null);

			Alc.MakeContextCurrent(context);
		}
	}
}

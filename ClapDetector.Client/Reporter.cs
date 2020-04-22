using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace ClapDetector.Client
{
	public class Reporter
	{
		public async Task<string> ReportClapAsync()
		{
			// The port number(5001) must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client =  new Server.Greeter.GreeterClient(channel);
            var reply = await client.SayHelloAsync(
                              new Server.HelloRequest { Name = "GreeterClient" });
            return reply.Message;
		}
	}
}
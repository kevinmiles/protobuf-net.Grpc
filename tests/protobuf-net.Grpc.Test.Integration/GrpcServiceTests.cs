using Grpc.Core;
using ProtoBuf.Grpc.Server;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using Xunit;

namespace protobuf_net.Grpc.Test.Integration
{
    [DataContract]
    public class Apply
    {
        public Apply() { }
        public Apply(int x, int y) => (X, Y) = (x, y);

        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public class ApplyResponse
    {
        public ApplyResponse() { }
        public ApplyResponse(int result) => Result = result;

        [DataMember(Order = 1)]
        public int Result { get; set; }
    }
    
    public class ApplyServices : IGrpcService
    {
        public Task<ApplyResponse> Add(Apply request) => Task.FromResult(new ApplyResponse(request.X + request.Y));
        public Task<ApplyResponse> Mul(Apply request) => Task.FromResult(new ApplyResponse(request.X * request.Y));
        public Task<ApplyResponse> Sub(Apply request) => Task.FromResult(new ApplyResponse(request.X - request.Y));
        public Task<ApplyResponse> Div(Apply request) => Task.FromResult(new ApplyResponse(request.X / request.Y));
    }

    [Serializable]
    public class AdhocRequest
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    [Serializable]
    public class AdhocResponse
    {
        public int Z { get; set; }
    }
    [Service]
    public interface IAdhocService
    {
        AdhocResponse AdhocMethod(AdhocRequest request);
    }

    public class AdhocService : IAdhocService
    {
        public AdhocResponse AdhocMethod(AdhocRequest request)
            => new AdhocResponse { Z = request.X + request.Y };
    }

    static class AdhocConfig
    {
        public static ClientFactory ClientFactory { get; }
            = ClientFactory.Create(BinderConfiguration.Create(new[] {
                    // we'll allow multiple marshallers to take a stab; protobuf-net first,
                    // then try BinaryFormatter for anything that protobuf-net can't handle

                    ProtoBufMarshallerFactory.Default,
#pragma warning disable CS0618 // Type or member is obsolete
                    BinaryFormatterMarshallerFactory.Default, // READ THE NOTES ON NOT DOING THIS
#pragma warning restore CS0618 // Type or member is obsolete
                    }));
    }

    public class GrpcServiceFixture : IAsyncDisposable
    {
        public const int Port = 10042;
        private readonly Server _server;
        
        public GrpcServiceFixture()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            BinaryFormatterMarshallerFactory.I_Have_Read_The_Notes_On_Not_Using_BinaryFormatter = true;
            BinaryFormatterMarshallerFactory.I_Promise_Not_To_Do_This = true; // signed: Marc Gravell
#pragma warning restore CS0618 // Type or member is obsolete

            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            _ = _server.Services.AddCodeFirst(new ApplyServices());
            _ = _server.Services.AddCodeFirst(new AdhocService(), AdhocConfig.ClientFactory);
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    
    public class GrpcServiceTests : IClassFixture<GrpcServiceFixture>
    {
        private readonly GrpcServiceFixture _fixture;
        public GrpcServiceTests(GrpcServiceFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task CanCallAllApplyServicesUnaryAsync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var client = new GrpcClient(http, nameof(ApplyServices));

            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }

        [Fact]
        public async Task CanCallAllApplyServicesTypedUnaryAsync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = http.CreateGrpcService(typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }

        [Fact]
        public void CanCallAllApplyServicesUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = new GrpcClient(http, nameof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }

        [Fact]
        public void CanCallAllApplyServicesTypedUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            
            var client = new GrpcClient(http, typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }

        [Fact]
        public void CanCallAdocService()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new AdhocRequest { X = 12, Y = 7 };
            var client = http.CreateGrpcService<IAdhocService>(AdhocConfig.ClientFactory);
            var response = client.AdhocMethod(request);
            Assert.Equal(19, response.Z);
        }
    }
}
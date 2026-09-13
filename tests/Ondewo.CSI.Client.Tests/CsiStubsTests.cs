using System;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Ondewo.Csi;
using Xunit;

namespace Ondewo.Csi.Client.Tests
{
    /// <summary>
    /// The product-specific half of the suite: concrete assertions against the ONDEWO CSI API,
    /// spelled out with real message, field, enum and RPC names.
    /// <para>
    /// This is the only test file that has to be rewritten when the setup is replicated to another
    /// ONDEWO product - <see cref="GeneratedStubsTests"/> carries over unchanged.
    /// </para>
    /// </summary>
    public class CsiStubsTests
    {
        private const string DummyTarget = "http://localhost:50051";

        /// <summary>
        /// The unary RPCs of <c>ondewo.csi.Conversations</c>. grpc_csharp_plugin gives a unary RPC
        /// both a blocking <c>Rpc</c> and an <c>RpcAsync</c> overload.
        /// </summary>
        private static readonly string[] UnaryRpcs =
        {
            "CreateS2sPipeline", "GetS2sPipeline", "UpdateS2sPipeline", "DeleteS2sPipeline",
            "ListS2sPipelines", "CheckUpstreamHealth", "SetControlStatus",
        };

        /// <summary>
        /// The streaming RPCs, which get exactly one client method returning an
        /// <c>Async*StreamingCall</c> - and deliberately NO <c>...Async</c> twin.
        /// </summary>
        private static readonly string[] StreamingRpcs = { "S2sStream", "GetControlStream" };

        [Fact]
        public void S2sPipelineRoundTripsEveryScalarFieldKind()
        {
            var pipeline = new S2sPipeline
            {
                Id = "pipeline-42",
                S2TPipelineId = "s2t-de-1",
                NluProjectId = "nlu-project-1",
                NluLanguageCode = "de",
                T2SPipelineId = "t2s-de-1",
            };

            byte[] bytes = pipeline.ToByteArray();
            S2sPipeline parsed = S2sPipeline.Parser.ParseFrom(bytes);

            Assert.NotEmpty(bytes);
            Assert.Equal(pipeline, parsed);
            Assert.Equal("pipeline-42", parsed.Id);
            Assert.Equal("s2t-de-1", parsed.S2TPipelineId);
            Assert.Equal("nlu-project-1", parsed.NluProjectId);
            Assert.Equal("de", parsed.NluLanguageCode);
            Assert.Equal("t2s-de-1", parsed.T2SPipelineId);
        }

        [Fact]
        public void S2sStreamRequestRoundTripsBytesAndBooleans()
        {
            var request = new S2sStreamRequest
            {
                PipelineId = "pipeline-42",
                SessionId = "session-1",
                Audio = ByteString.CopyFrom(new byte[] { 0x52, 0x49, 0x46, 0x46 }),
                EndOfStream = true,
                InitialIntentDisplayName = "i_greeting",
            };

            S2sStreamRequest parsed = S2sStreamRequest.Parser.ParseFrom(request.ToByteArray());

            Assert.Equal(request, parsed);
            Assert.Equal(ByteString.CopyFrom(new byte[] { 0x52, 0x49, 0x46, 0x46 }), parsed.Audio);
            Assert.True(parsed.EndOfStream);
            Assert.Equal("i_greeting", parsed.InitialIntentDisplayName);
        }

        [Fact]
        public void ControlMessageRoundTripsANestedMessageAndItsEnums()
        {
            var message = new ControlMessage
            {
                Service = ControlMessageServiceName.OndewoSip,
                Method = ControlMessageServiceMethod.TransferCall,
                Parameters = new ControlMessageServiceParameters
                {
                    TransferId = "transfer-1",
                    ConditionStart = new Condition { Type = ConditionType.Duration, Value = "10" },
                },
            };

            ControlMessage parsed = ControlMessage.Parser.ParseFrom(message.ToByteArray());

            Assert.Equal(message, parsed);
            Assert.Equal(ControlMessageServiceName.OndewoSip, parsed.Service);
            Assert.Equal(ControlMessageServiceMethod.TransferCall, parsed.Method);
            Assert.Equal("transfer-1", parsed.Parameters.TransferId);
            Assert.Equal(ConditionType.Duration, parsed.Parameters.ConditionStart.Type);
            Assert.Equal("10", parsed.Parameters.ConditionStart.Value);
        }

        [Fact]
        public void RepeatedFieldRoundTripsThroughAListResponse()
        {
            var response = new ListS2sPipelinesResponse();
            response.Pipelines.Add(new S2sPipeline { Id = "pipeline-1" });
            response.Pipelines.Add(new S2sPipeline { Id = "pipeline-2" });

            ListS2sPipelinesResponse parsed =
                ListS2sPipelinesResponse.Parser.ParseFrom(response.ToByteArray());

            Assert.Equal(response, parsed);
            Assert.Equal(new[] { "pipeline-1", "pipeline-2" }, parsed.Pipelines.Select(pipeline => pipeline.Id));
        }

        [Fact]
        public void UnsetScalarFieldsCarryTheProto3DefaultsAndStayOffTheWire()
        {
            var request = new S2sStreamRequest();

            Assert.Equal(string.Empty, request.PipelineId);
            Assert.Equal(ByteString.Empty, request.Audio);
            Assert.False(request.EndOfStream);
            Assert.Empty(request.ToByteArray());

            // The ondewo/csi protos declare no proto3 `optional` field (the vendored s2t/t2s ones
            // do), so there is no explicit-presence case to make about a CSI message: an unset
            // scalar and one set to its default are indistinguishable on the wire, by design.
            Assert.Empty(new S2sStreamRequest { EndOfStream = false }.ToByteArray());
        }

        /// <summary>
        /// The oneof in <see cref="ControlMessageServiceParameters"/> is what carries a per-service
        /// config, and setting one arm has to evict the other rather than add to it.
        /// </summary>
        [Fact]
        public void ServiceParametersOneofKeepsExactlyOneArmSet()
        {
            var parameters = new ControlMessageServiceParameters
            {
                T2SConfig = new global::Ondewo.T2S.RequestConfig { T2SPipelineId = "t2s-de-1" },
            };

            Assert.Equal(
                ControlMessageServiceParameters.ConfigOneofCase.T2SConfig,
                parameters.ConfigCase);

            parameters.S2TConfig = new global::Ondewo.S2T.TranscribeRequestConfig
            {
                S2TPipelineId = "s2t-de-1",
            };

            Assert.Equal(
                ControlMessageServiceParameters.ConfigOneofCase.S2TConfig,
                parameters.ConfigCase);
            Assert.Null(parameters.T2SConfig);

            ControlMessageServiceParameters parsed =
                ControlMessageServiceParameters.Parser.ParseFrom(parameters.ToByteArray());

            Assert.Equal(parameters, parsed);
            Assert.Equal("s2t-de-1", parsed.S2TConfig.S2TPipelineId);
        }

        /// <summary>
        /// Not every ONDEWO enum spells its zero value "UNSPECIFIED": the CSI control enums use
        /// <c>OK</c>, <c>UNKNOWNNAME</c>, <c>UNKNOWNMETHOD</c> and <c>UNKNOWTYPE</c>. What matters
        /// is that proto3's first-value-is-zero rule holds, and that the wire name is preserved.
        /// </summary>
        [Fact]
        public void EnumsStartAtTheirZeroValue()
        {
            Assert.Equal(0, (int)ControlStatus.Ok);
            Assert.Equal(ControlStatus.Ok, default(ControlStatus));
            Assert.Equal(0, (int)ControlMessageServiceName.Unknownname);
            Assert.Equal(ControlMessageServiceName.Unknownname, default(ControlMessageServiceName));
            Assert.Equal(0, (int)ControlMessageServiceMethod.Unknownmethod);
            Assert.Equal(0, (int)ConditionType.Unknowtype);
            Assert.Equal(0, (int)SipTrigger.Types.SipTriggerType.Unspecified);

            // The C# name is PascalCased; the wire/JSON name is the one the server speaks.
            Assert.Equal("OK", OriginalNameOf(ControlStatus.Ok));
            Assert.Equal("EMERGENCY_STOP", OriginalNameOf(ControlStatus.EmergencyStop));
            Assert.Equal("UNKNOWNNAME", OriginalNameOf(ControlMessageServiceName.Unknownname));
            Assert.Equal("ondewo_sip", OriginalNameOf(ControlMessageServiceName.OndewoSip));
        }

        [Fact]
        public void EnumFieldRoundTripsANonDefaultValue()
        {
            var response = new ControlStreamResponse
            {
                ControlStatus = ControlStatus.BargeIn,
                Epoch = 17UL,
            };

            ControlStreamResponse parsed = ControlStreamResponse.Parser.ParseFrom(response.ToByteArray());

            Assert.Equal(ControlStatus.BargeIn, parsed.ControlStatus);
            Assert.Equal(17UL, parsed.Epoch);
            Assert.NotEmpty(response.ToByteArray());
        }

        [Fact]
        public void ConversationsClientBindsToAChannelAndExposesTheDeclaredRpcs()
        {
            using GrpcChannel channel = GrpcChannel.ForAddress(DummyTarget);

            var client = new Conversations.ConversationsClient(channel);

            Assert.NotNull(client);
            Assert.Equal("ondewo.csi.Conversations", Conversations.Descriptor.FullName);
            Assert.Equal(
                UnaryRpcs.Concat(StreamingRpcs).OrderBy(name => name, StringComparer.Ordinal),
                Conversations.Descriptor.Methods
                    .Select(method => method.Name)
                    .OrderBy(name => name, StringComparer.Ordinal));

            string[] clientMethods = typeof(Conversations.ConversationsClient)
                .GetMethods()
                .Select(method => method.Name)
                .Distinct()
                .ToArray();

            foreach (string rpc in UnaryRpcs)
            {
                Assert.Contains(rpc, clientMethods);
                Assert.Contains(rpc + "Async", clientMethods);
            }

            // A streaming RPC gets exactly one client method - the async streaming call itself -
            // so asserting an `...Async` twin here would be asserting a bug.
            foreach (string rpc in StreamingRpcs)
            {
                Assert.Contains(rpc, clientMethods);
                Assert.DoesNotContain(rpc + "Async", clientMethods);
            }
        }

        /// <summary>
        /// CSI is a conversation orchestrator: its <c>S2sStream</c> is bidirectional and
        /// <c>GetControlStream</c> is server-streaming, and the generated call objects are what
        /// prove the plugin saw that in the .proto.
        /// </summary>
        [Fact]
        public void StreamingRpcsAreGeneratedAsStreamingCalls()
        {
            Assert.Equal(MethodType.DuplexStreaming, MethodTypeOf("S2sStream"));
            Assert.Equal(MethodType.ServerStreaming, MethodTypeOf("GetControlStream"));
            Assert.Equal(MethodType.Unary, MethodTypeOf("GetS2sPipeline"));

            Assert.Equal(
                typeof(AsyncDuplexStreamingCall<S2sStreamRequest, S2sStreamResponse>),
                ClientMethodReturning("S2sStream"));
            Assert.Equal(
                typeof(AsyncServerStreamingCall<ControlStreamResponse>),
                ClientMethodReturning("GetControlStream"));
        }

        /// <summary>
        /// CSI orchestrates NLU, S2T and T2S, so its API imports large parts of those products'
        /// protos and the compiler emits them into THIS assembly. That is correct, not bloat - the
        /// stubs below are reachable from a CSI consumer without a second package reference.
        /// </summary>
        [Fact]
        public void TheAssemblyAlsoShipsTheVendoredNluS2tAndT2sStubs()
        {
            string[] services = ProductStubs.Assembly.GetTypes()
                .Select(type => type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static))
                .Where(property => property != null && property.PropertyType == typeof(ServiceDescriptor))
                .Select(property => ((ServiceDescriptor)property.GetValue(null)).FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Contains("ondewo.csi.Conversations", services);
            Assert.Contains("ondewo.nlu.Sessions", services);
            Assert.Contains("ondewo.s2t.Speech2Text", services);
            Assert.Contains("ondewo.t2s.Text2Speech", services);

            // The vendored messages are the very types the CSI messages refer to.
            Assert.Same(
                ProductStubs.Assembly,
                typeof(global::Ondewo.T2S.RequestConfig).Assembly);
            Assert.Same(
                ProductStubs.Assembly,
                typeof(global::Google.Cloud.Dialogflow.V2.DetectIntentResponse).Assembly);
        }

        private static MethodType MethodTypeOf(string rpc)
        {
            FieldInfo field = typeof(Conversations).GetField(
                "__Method_" + rpc, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);

            return Assert.IsAssignableFrom<IMethod>(field.GetValue(null)).Type;
        }

        private static Type ClientMethodReturning(string rpc)
        {
            return typeof(Conversations.ConversationsClient)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == rpc)
                .Select(method => method.ReturnType)
                .Distinct()
                .Single();
        }

        private static string OriginalNameOf<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return typeof(TEnum)
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(OriginalNameAttribute), false)
                .Cast<OriginalNameAttribute>()
                .Single()
                .Name;
        }
    }
}

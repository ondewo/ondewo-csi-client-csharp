# Release History

*****************

## Release ONDEWO CSI Csharp Client 5.9.2

### New Features

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) Keycloak authentication with offline-token start-up, the same behaviour as ondewo-csi-client-python 5.9.2: `refreshToken` in `OndewoClientConfig` (`keycloakUrl`, `realm`, `clientId`, `username`, `password`, `tokenExpirationInS`, `verifySsl` and `refreshToken`, appended after the existing parameters, `refreshToken` last) hands the client a Keycloak *offline* refresh token instead of a password. The client then starts with one `grant_type=refresh_token` exchange and never sends a password grant, not even as a fallback; a refused token is never answered with a password login. Keycloak's brute-force detection counts failed LOGINS, so N processes booting at once with one technical user are a burst the realm penalises, while N refresh grants of one offline token are not.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A Keycloak configuration needs a password OR a refresh token, and the Keycloak URL, realm and client id are all-or-nothing; when both credentials are set the token wins. The password and the token are redacted from every rendering of the configuration and take part (hashed) in the shared-provider registry key, so two token-only configurations of different tenants never share a provider. The `token_expiration_in_s` window is armed on both start-up paths.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) `OndewoChannelFactory` attaches an auto-refreshed `authorization: Bearer <token>` to every call when the Keycloak fields are set, through a weak, SHA-256-keyed provider registry that never hands out a closed provider.

### Bug Fixes

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A failed token refresh never ends the background refresh: any failure of the token endpoint (a 502 from a proxy, a DNS failure, a restarting Keycloak, a dead offline session) is retried with one full-jitter backoff (5 s doubling up to 300 s, measured from the moment the failure is recorded) that the per-call path shares, and a success resets it. Concurrent callers during an outage share one in-flight refresh, so an outage costs one token request, not one per call.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A token that is still valid keeps being served when its refresh fails inside the 30 s expiry leeway; only an EXPIRED token raises `KeycloakAuthenticationException`, whatever failed underneath: a transport error, a non-2xx status, a body that is not JSON or not a JSON object, a missing `access_token` or a non-numeric `expires_in`, each with the original error as its cause. A malformed response changes no provider state, and a password login whose response carries no `refresh_token` keeps the previous one, as the python SDK does.

### Improvements

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) One Keycloak contract across every ONDEWO client SDK, with the python SDK as the reference: the same constants, log lines and error texts. The token request is bounded by 10 s, quoted Keycloak error bodies are cut at 500 characters, a token response is reported by its field names only (never its values), and each failure logs one WARNING with the attempt number and the retry delay, the first success afterwards one INFO; no credential appears in either line.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) Tests implement the cross-SDK test catalogue T01-T62 plus edge cases, on a fake token endpoint and clock, with 100 % coverage of the hand-written authentication code.
* Tracking API Version [5.9.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.9.0) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) ); the generated code is unchanged against 5.9.0.

*****************

## Release ONDEWO CSI Csharp Client 5.9.0

### Improvements

* Tracking API Version [5.9.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.9.0) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) )
* Generated with ondewo-proto-compiler 5.17.0 (protoc 35.1); new runtime floors: Google.Protobuf 3.35.1, Grpc.Core.Api / Grpc.Net.Client 2.85.0, Google.Api.CommonProtos 2.17.0 (protoc 35.1).

*****************

## Release ONDEWO CSI Csharp Client 5.7.0

### Improvements

* Tracking API Version [5.7.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.7.0) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) )

*****************

## Release ONDEWO CSI Csharp Client 5.6.2

### Improvements

* Tracking API Version [5.6.2](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.6.2) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) )

*****************

## Release ONDEWO CSI Csharp Client 5.6.0

### New Features

* Generated from [ONDEWO CSI API 5.6.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.6.0): call-scoped
  bot media control for live calls (see the API release notes for the semantics).
  * New RPC `Conversations.SetCallMediaControl` (`CallMediaControlLevel` in, `SetCallMediaControlResponse` out),
    with the unary and `Async` client methods.
  * `ControlStreamResponse.MediaControl`: set only on media-control messages of `GetControlStream`; such a
    message must not be handled as a `ControlStatus` change.
* The API change is purely additive: code built against 5.5.x compiles and stays wire-compatible.

### Improvements

* Tests: the CSI-specific suite lists `SetCallMediaControl` among the unary RPCs and round-trips the new
  messages and the `media_control` field.
* The stubs are generated by [ondewo-proto-compiler 5.15.5](https://github.com/ondewo/ondewo-proto-compiler/releases/tag/5.15.5)
  (previously 5.15.2).
* Tracking API Version [5.6.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.6.0) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) )

*****************

## Release ONDEWO CSI Csharp Client 5.5.1

### New Features

* TLS and mutual TLS: `OndewoClientConfig` and `OndewoChannelFactory.Create` (namespace
  `Ondewo.Csi.Client.Connection`) build the `GrpcChannel` the generated service clients take, with the rules of the
  ONDEWO Python client. **They exist only in the new `net10.0` target of the package**; the `netstandard2.0`
  target (.NET Framework, older .NET) does not contain them, because `SocketsHttpHandler`,
  `SslClientAuthenticationOptions` and `X509Certificate2.CreateFromPem` do not exist there.
  * `grpcCert`, `grpcClientCert` and `grpcClientKey` hold PEM content, not file paths; a certificate field
    without a PEM certificate in it is refused with `ArgumentException`.
  * With `grpcCert` set only those CAs are trusted, otherwise the platform trust store; the host is always
    checked against the certificate's SAN.
  * `grpcClientCert` / `grpcClientKey` are both-or-neither (refused in the constructor); empty strings on both
    mean plain TLS. The key must be an unencrypted PEM key (PKCS#8, PKCS#1 or SEC1).
  * `useSecureChannel: false` with a client identity is refused; a plaintext channel logs a warning naming
    `host:port` through `GrpcChannelOptions.LoggerFactory`.
  * No exception message renders a PEM or the key; `OndewoClientConfig.ToString()` redacts the key.
  * Bare IPv6 literal hosts are bracketed; CRLF PEMs work.
  * Channel defaults: keepalive ping every 30 s with a 20 s ping timeout, only while calls are active; 5 s maximum
    reconnect backoff; 2^31-1 maximum message size. Not configured (documented in the README): a per-method retry
    policy, `max_pings_without_data` and `TCP_USER_TIMEOUT`, which `Grpc.Net.Client` does not expose.

### Improvements

* The package now targets `netstandard2.0` and `net10.0`. The `netstandard2.0` target is unchanged: the
  generated stubs and `OndewoAuth`.
* README: new section "TLS, mutual TLS and certificates" (modes, rules, example, channel defaults, a test PKI
  with openssl, security notes, troubleshooting).
* Tests: real TLS and mutual TLS handshakes against an in-process Kestrel server with a PKI built at test time
  (custom CA, client chain, CRLF, IPv6, missing / unrelated client identity, wrong CA, wrong host name, system
  trust store, plaintext), and a test that pins the release-notes slice of `RELEASE.md` the GitHub release is
  cut from.
* The stubs are generated by [ondewo-proto-compiler 5.15.2](https://github.com/ondewo/ondewo-proto-compiler/releases/tag/5.15.2)
  (previously 5.15.1).
* Tracking API Version [5.5.0](https://github.com/ondewo/ondewo-csi-api/releases/tag/5.5.0) ( [Documentation](https://ondewo.github.io/ondewo-csi-api/) )

*****************

## Release ONDEWO CSI Csharp Client 5.5.0

### New Features

* Initial release of the ONDEWO CSI C# client. The package `Ondewo.CSI.Client` ships the
  gRPC message and client stubs generated from the [ONDEWO CSI API](https://github.com/ondewo/ondewo-csi-api)
  by the `ondewo-csharp-proto-compiler` image of
  [ondewo-proto-compiler 5.15.1](https://github.com/ondewo/ondewo-proto-compiler): one
  `<Message>.cs` per proto plus one `<Service>Grpc.cs` per proto that declares a service, nested by C# namespace
  under `api/` - 40 files in this release.
* The package targets `netstandard2.0`, so it is consumable from .NET Framework 4.6.1+, .NET Core 2.0+ and every
  modern .NET, and carries `Google.Protobuf`, `Grpc.Net.Client`, `Grpc.Core.Api` and `Google.Api.CommonProtos` as
  its only dependencies. The `google/api`, `google/rpc` and `google/type` descriptors the API imports come from
  `Google.Api.CommonProtos` rather than being generated a second time into this assembly.
* `make build` is the whole pipeline — submodule checkout at the pinned versions, compiler image build, stub
  generation and `dotnet pack` — and `make ondewo_release` cuts the GitHub and NuGet release from the same
  version number.
* The generated stubs are **committed** under `api/`, like every other ONDEWO client SDK, together with the
  generated `Ondewo.CSI.Client.csproj` and a `Directory.Build.props` carrying a fallback for every MSBuild pin
  that project file reads — which is what lets a clone without the `ondewo-proto-compiler` submodule build.
  `make check_dotnet_properties` fails the build if the two ever disagree.
* `auth/OndewoAuth.cs` is the hand-written bearer-token surface of the package: call metadata, an auth
  interceptor and call credentials for the `authorization: Bearer <token>` entry an ONDEWO server expects.
* `tests/` is an xUnit suite (1882 tests) over the committed stubs — a reflection-driven half that
  round-trips every generated message through its wire format, checks every enum starts at its zero value and
  binds every generated service client to a channel asserting it exposes every RPC its descriptor declares, plus
  concrete ONDEWO CSI cases and full coverage of `auth/`. `make test` gates on 100% line, branch and method
  coverage of the hand-written sources; `api/` is excluded from the metric but exercised by the suite.
* The CSI API imports large parts of the NLU, S2T and T2S APIs, so the assembly ships their stubs too
  (`Ondewo.Nlu`, `Ondewo.S2T`, `Ondewo.T2S` and `Google.Cloud.Dialogflow.V2`, 19 services in total).
  That is deliberate: a CSI consumer reaches them without a second package reference.
* `.github/workflows/ci.yml` builds, tests and packs the committed stubs on `ubuntu-latest` without the
  submodules and without the compiler image. Nothing in it is guarded by a directory check that could turn an
  empty repository into a green run: a missing `Ondewo.CSI.Client.csproj` or an empty `api/` fails the job.

*****************

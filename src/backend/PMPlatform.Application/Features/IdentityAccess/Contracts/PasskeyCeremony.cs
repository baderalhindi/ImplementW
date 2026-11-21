namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The start of a WebAuthn ceremony: the options the browser hands to <c>navigator.credentials</c> (JSON, binary values
/// base64url), and the sealed transaction the client sends back with the credential. Nothing is stored server-side.
/// </summary>
public sealed record PasskeyCeremony(string OptionsJson, string Transaction);

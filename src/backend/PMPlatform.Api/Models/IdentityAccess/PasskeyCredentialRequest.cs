using System.Text.Json.Nodes;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>The device's answer to a passkey sign-in and the transaction it answers.</summary>
public sealed record PasskeyCredentialRequest(string? Transaction, PasskeyCredential? Credential)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(Transaction, "transaction", PasskeyCredential.TransactionLength, errors);
        PasskeyCredential.Require(Credential, assertion: true, errors);
        return errors;
    }
}

/// <summary>A new passkey for the signed-in person: the device's answer, its transaction, and the device's name.</summary>
public sealed record PasskeyCreateRequest(string? Transaction, PasskeyCredential? Credential, string? Name)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(Transaction, "transaction", PasskeyCredential.TransactionLength, errors);
        PasskeyCredential.Require(Credential, assertion: false, errors);
        RequestValidation.RequireText(Name, "name", PasskeySummary.NameLength, errors);
        return errors;
    }
}

/// <summary>
/// A WebAuthn <c>PublicKeyCredential</c> as the browser serialises it (<c>toJSON()</c>): every binary value base64url. A
/// registration answers with <c>attestationObject</c>; a sign-in with <c>authenticatorData</c>, <c>signature</c> and <c>userHandle</c>.
/// </summary>
public sealed record PasskeyCredential(string? Id, string? RawId, string? Type, PasskeyCredentialResponse? Response)
{
    internal const int TransactionLength = 4096;

    /// <summary>Far above any real value (a few hundred bytes to a few kilobytes), within the request text limit.</summary>
    private const int ValueLength = 8192;

    /// <summary>The credential in the form the WebAuthn library reads.</summary>
    internal string ToJson()
    {
        JsonObject response = new() { ["clientDataJSON"] = Response!.ClientDataJson };
        AddIfSet(response, "attestationObject", Response.AttestationObject);
        AddIfSet(response, "authenticatorData", Response.AuthenticatorData);
        AddIfSet(response, "signature", Response.Signature);
        AddIfSet(response, "userHandle", Response.UserHandle);
        if (Response.Transports is { } transports)
        {
            response["transports"] = new JsonArray([.. transports.Select(t => (JsonNode?)t)]);
        }

        return new JsonObject
        {
            ["id"] = Id,
            ["rawId"] = RawId,
            ["type"] = Type,
            ["response"] = response,
            ["clientExtensionResults"] = new JsonObject(),
        }.ToJsonString();
    }

    internal static void Require(PasskeyCredential? credential, bool assertion, List<FieldError> errors)
    {
        if (credential is null)
        {
            errors.Add(new FieldError("credential", FieldError.Required));
            return;
        }

        RequestValidation.Require(credential.Id, "credential.id", ValueLength, errors);
        RequestValidation.Require(credential.RawId, "credential.rawId", ValueLength, errors);
        RequestValidation.Require(credential.Type, "credential.type", 50, errors);
        if (credential.Response is not { } response)
        {
            errors.Add(new FieldError("credential.response", FieldError.Required));
            return;
        }

        RequestValidation.Require(response.ClientDataJson, "credential.response.clientDataJSON", ValueLength, errors);
        if (assertion)
        {
            RequestValidation.Require(response.AuthenticatorData, "credential.response.authenticatorData", ValueLength, errors);
            RequestValidation.Require(response.Signature, "credential.response.signature", ValueLength, errors);
            RequestValidation.Optional(response.UserHandle, "credential.response.userHandle", ValueLength, errors);
        }
        else
        {
            RequestValidation.Require(response.AttestationObject, "credential.response.attestationObject", ValueLength, errors);
        }
    }

    private static void AddIfSet(JsonObject target, string name, string? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }
}

/// <summary>The authenticator's response inside a <see cref="PasskeyCredential"/>.</summary>
public sealed record PasskeyCredentialResponse(
    string? ClientDataJson,
    string? AttestationObject,
    string? AuthenticatorData,
    string? Signature,
    string? UserHandle,
    IReadOnlyList<string>? Transports);

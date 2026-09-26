namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>What a second-factor challenge is for.</summary>
public enum MultiFactorPurpose
{
    /// <summary>The person has no factor yet: the challenge registers one, and passing it proves the person holds it.</summary>
    Enrolment = 1,

    /// <summary>The person has a factor: the challenge checks they still hold it.</summary>
    Verification = 2,
}

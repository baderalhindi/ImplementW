namespace PMPlatform.Application.Common.Events;

/// <summary>A template parameter, referenced in a template as <c>{{name}}</c>.</summary>
public sealed record NotificationParameter(string Name, string Value);

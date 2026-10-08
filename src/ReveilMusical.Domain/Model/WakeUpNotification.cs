namespace ReveilMusical.Domain.Model;

/// <summary>Message de réveil, indépendant du canal qui le transportera.</summary>
public sealed record WakeUpNotification(string UserId, string Recipient, string Title, string Message, Track Track);

namespace NiTorrent.Application.Common;

public static class UserErrorMapper
{
    public static string ToMessage(Exception exception, string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is UserVisibleException visible && !string.IsNullOrWhiteSpace(visible.Message)
            ? visible.Message
            : fallbackMessage;
    }
}

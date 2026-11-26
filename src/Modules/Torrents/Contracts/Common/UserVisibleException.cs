namespace NiTorrent.Application.Common;

public sealed class UserVisibleException : Exception
{
    public UserVisibleException(string message) : base(message)
    {
    }

    public UserVisibleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

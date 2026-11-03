namespace Animaze.Client;

/// <summary>Where Animaze listens and how long to wait for it.</summary>
public sealed class AnimazeClientOptions
{
    /// <summary>The API endpoint; Animaze's default is port 9000, changeable in its settings.</summary>
    public Uri Endpoint { get; set; } = new("ws://localhost:9000");

    /// <summary>How long a request waits for its reply.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Size of the receive buffer; longer messages are assembled from several reads.</summary>
    public int ReceiveBufferSize { get; set; } = 16 * 1024;

    /// <summary>The clock for request timeouts.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}

/// <summary>Animaze refused a request, did not answer, or could not be reached.</summary>
public sealed class AnimazeException : Exception
{
    /// <summary>An exception.</summary>
    public AnimazeException() { }

    /// <summary>An exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public AnimazeException(string message)
        : base(message) { }

    /// <summary>An exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public AnimazeException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The action Animaze refused, when it refused one.</summary>
    public string? Action { get; init; }
}

/// <summary>An event Animaze pushed.</summary>
/// <param name="name">The event's name.</param>
/// <param name="body">The whole message.</param>
public sealed class AnimazeEventArgs(string name, System.Text.Json.JsonElement body) : EventArgs
{
    /// <summary>The event's name.</summary>
    public string Name { get; } = name;

    /// <summary>The whole message.</summary>
    public System.Text.Json.JsonElement Body { get; } = body;
}

/// <summary>The loaded avatar changed.</summary>
/// <param name="avatarName">The avatar now loaded.</param>
public sealed class AvatarChangedEventArgs(string avatarName) : EventArgs
{
    /// <summary>The avatar now loaded.</summary>
    public string AvatarName { get; } = avatarName;
}

namespace CoreBot.Core.Messaging;

using CoreBot.Core.Messages;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// In-memory message bus implementation using System.Threading.Channels
/// </summary>
public class MessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private readonly ConcurrentDictionary<Type, int> _subscriberCounts = new();
    private readonly int _channelCapacity = 1024; // Bounded channel capacity for backpressure

    /// <summary>
    /// Publish a message to all subscribers of its type
    /// </summary>
    public async ValueTask PublishAsync<T>(T message, CancellationToken ct = default) where T : IMessage
    {
        var messageType = typeof(T);

        // Get or create channel for this message type
        var channel = (Channel<T>)_channels.GetOrAdd(messageType, _ =>
        {
            return Channel.CreateBounded<T>(new BoundedChannelOptions(_channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        });

        // Publish message to the channel
        await channel.Writer.WriteAsync(message, ct);
    }

    /// <summary>
    /// Subscribe to messages of a specific type
    /// </summary>
    public IAsyncEnumerable<T> SubscribeAsync<T>(CancellationToken ct = default) where T : IMessage
    {
        var messageType = typeof(T);

        // Get or create channel for this message type
        var channel = (Channel<T>)_channels.GetOrAdd(messageType, _ =>
        {
            return Channel.CreateBounded<T>(new BoundedChannelOptions(_channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        });

        // Increment subscriber count
        _subscriberCounts.AddOrUpdate(messageType, 1, (_, count) => count + 1);

        // Return async enumerable for reading from the channel
        return SubscribeToChannelAsync(channel, ct);
    }

    private async IAsyncEnumerable<T> SubscribeToChannelAsync<T>(
        Channel<T> channel,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : IMessage
    {
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                yield return message;
            }
        }
        finally
        {
            // Decrement subscriber count when subscription is disposed
            var messageType = typeof(T);
            _subscriberCounts.AddOrUpdate(messageType, 0, (_, count) => Math.Max(0, count - 1));
        }
    }

    /// <summary>
    /// Get the number of active subscribers for a message type
    /// </summary>
    public int GetSubscriberCount<T>() where T : IMessage
    {
        var messageType = typeof(T);
        return _subscriberCounts.TryGetValue(messageType, out var count) ? count : 0;
    }

    /// <summary>
    /// Dispose of the message bus and complete all channels
    /// </summary>
    public ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
        {
            if (channel is Channel<dynamic> dynamicChannel)
            {
                dynamicChannel.Writer.TryComplete();
            }
        }

        _channels.Clear();
        _subscriberCounts.Clear();

        return default;
    }
}

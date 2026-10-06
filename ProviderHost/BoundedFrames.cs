namespace D200xDirect.Providers;

internal sealed class BoundedFrames(Stream stream)
{
    readonly byte[] input = new byte[4096];
    readonly byte[] frame = new byte[ProviderContract.MaximumBytes];
    int offset, available;

    public async Task<byte[]?> ReadAsync(CancellationToken token)
    {
        var length = 0;
        while (true)
        {
            if (offset == available)
            {
                available = await stream.ReadAsync(input, token).ConfigureAwait(false);
                offset = 0;
                if (available == 0)
                {
                    if (length != 0) throw new IOException("Incomplete provider frame at EOF.");
                    return null;
                }
            }
            var value = input[offset++];
            if (value == '\n')
            {
                if (length > 0 && frame[length - 1] == '\r') length--;
                if (length == 0) throw new IOException("Empty provider frame.");
                return frame.AsSpan(0, length).ToArray();
            }
            if (length == frame.Length) throw new IOException("Provider frame exceeds 64 KiB.");
            frame[length++] = value;
        }
    }
}

// © Microsoft Corporation. All rights reserved.

public interface IBackgroundTranscodeQueue
{
    void Enqueue(SymCacheKey key);
}

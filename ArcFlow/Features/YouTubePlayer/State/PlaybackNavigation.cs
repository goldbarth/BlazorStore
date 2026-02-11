using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.State;

internal static class PlaybackNavigation
{
    /// <summary>
    /// Computes the next video to play based on current state, shuffle, and repeat settings.
    /// Returns the decision and an updated QueueState (with PlaybackHistory pushed if advancing).
    /// </summary>
    public static (PlaybackDecision Decision, QueueState State) ComputeNext(QueueState queue)
    {
        if (queue.Videos.IsEmpty)
            return (new PlaybackDecision.Stop(), queue);

        if (queue.CurrentItemId is null)
            return (new PlaybackDecision.Stop(), queue);

        // RepeatOne: replay current
        if (queue.RepeatMode == RepeatMode.One)
            return (new PlaybackDecision.AdvanceTo(queue.CurrentItemId.Value), queue);

        var currentId = queue.CurrentItemId.Value;
        Guid? nextId;

        if (queue.ShuffleEnabled && !queue.ShuffleOrder.IsEmpty)
        {
            var idx = queue.ShuffleOrder.IndexOf(currentId);
            if (idx < 0)
                return (new PlaybackDecision.Stop(), queue);

            if (idx < queue.ShuffleOrder.Count - 1)
            {
                nextId = queue.ShuffleOrder[idx + 1];
            }
            else
            {
                // End of shuffle order
                nextId = queue.RepeatMode == RepeatMode.All
                    ? queue.ShuffleOrder[0]
                    : null;
            }
        }
        else
        {
            // Sequential mode
            var currentIndex = queue.Videos.FindIndex(v => v.Id == currentId);
            if (currentIndex < 0)
                return (new PlaybackDecision.Stop(), queue);

            if (currentIndex < queue.Videos.Count - 1)
            {
                nextId = queue.Videos[currentIndex + 1].Id;
            }
            else
            {
                // End of list
                nextId = queue.RepeatMode == RepeatMode.All
                    ? queue.Videos[0].Id
                    : null;
            }
        }

        if (nextId is null)
            return (new PlaybackDecision.Stop(), queue);

        // Push current to playback history
        var history = queue.PlaybackHistory.Add(currentId);
        if (history.Count > QueueState.PlaybackHistoryLimit)
            history = history.RemoveAt(0);

        var newQueue = queue with { PlaybackHistory = history };
        return (new PlaybackDecision.AdvanceTo(nextId.Value), newQueue);
    }

    /// <summary>
    /// Computes the previous video to play.
    /// Shuffle On: pops from PlaybackHistory. Shuffle Off: moves to previous index.
    /// </summary>
    public static (PlaybackDecision Decision, QueueState State) ComputePrev(QueueState queue)
    {
        if (queue.Videos.IsEmpty)
            return (new PlaybackDecision.NoOp(), queue);

        if (queue.CurrentItemId is null)
            return (new PlaybackDecision.NoOp(), queue);

        if (queue.ShuffleEnabled)
        {
            // Pop from playback history
            if (queue.PlaybackHistory.IsEmpty)
                return (new PlaybackDecision.NoOp(), queue);

            var prevId = queue.PlaybackHistory[^1];
            var newHistory = queue.PlaybackHistory.RemoveAt(queue.PlaybackHistory.Count - 1);
            var newQueue = queue with { PlaybackHistory = newHistory };
            return (new PlaybackDecision.AdvanceTo(prevId), newQueue);
        }
        else
        {
            // Sequential: go to previous index
            var currentIndex = queue.Videos.FindIndex(v => v.Id == queue.CurrentItemId.Value);
            if (currentIndex <= 0)
                return (new PlaybackDecision.NoOp(), queue);

            var prevId = queue.Videos[currentIndex - 1].Id;
            return (new PlaybackDecision.AdvanceTo(prevId), queue);
        }
    }

    /// <summary>
    /// Generates a Fisher-Yates shuffle order. Current item (if any) is placed at index 0.
    /// </summary>
    public static ImmutableList<Guid> GenerateShuffleOrder(
        ImmutableList<VideoItem> videos, Guid? currentItemId, int seed)
    {
        if (videos.IsEmpty)
            return ImmutableList<Guid>.Empty;

        var ids = videos.Select(v => v.Id).ToList();
        var rng = new Random(seed);

        // Fisher-Yates shuffle
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        // Move current item to front if present
        if (currentItemId.HasValue)
        {
            var idx = ids.IndexOf(currentItemId.Value);
            if (idx > 0)
            {
                ids.RemoveAt(idx);
                ids.Insert(0, currentItemId.Value);
            }
        }

        return ids.ToImmutableList();
    }

    /// <summary>
    /// Repairs playback structures after queue mutations:
    /// - Filters ShuffleOrder/PlaybackHistory to valid video IDs
    /// - Appends new video IDs to ShuffleOrder
    /// - Trims PlaybackHistory to limit
    /// - Fixes CurrentItemId if removed, syncs CurrentIndex
    /// </summary>
    public static QueueState RepairPlaybackStructures(QueueState queue)
    {
        var validIds = queue.Videos.Select(v => v.Id).ToHashSet();

        // Filter ShuffleOrder to valid IDs, append new ones only if order was already established
        var filteredShuffle = queue.ShuffleOrder.Where(id => validIds.Contains(id)).ToImmutableList();
        if (queue.ShuffleEnabled && !filteredShuffle.IsEmpty)
        {
            var existingInShuffle = filteredShuffle.ToHashSet();
            var newIds = queue.Videos
                .Where(v => !existingInShuffle.Contains(v.Id))
                .Select(v => v.Id);
            filteredShuffle = filteredShuffle.AddRange(newIds);
        }

        // Filter PlaybackHistory to valid IDs and trim
        var filteredHistory = queue.PlaybackHistory
            .Where(id => validIds.Contains(id))
            .ToImmutableList();
        if (filteredHistory.Count > QueueState.PlaybackHistoryLimit)
            filteredHistory = filteredHistory.RemoveRange(0, filteredHistory.Count - QueueState.PlaybackHistoryLimit);

        // Fix CurrentItemId
        var currentItemId = queue.CurrentItemId;
        if (currentItemId.HasValue && !validIds.Contains(currentItemId.Value))
            currentItemId = null;

        // Sync CurrentIndex from CurrentItemId
        int? currentIndex = queue.CurrentIndex;
        if (currentItemId.HasValue)
        {
            var idx = queue.Videos.FindIndex(v => v.Id == currentItemId.Value);
            currentIndex = idx >= 0 ? idx : null;
            if (idx < 0) currentItemId = null;
        }

        return queue with
        {
            ShuffleOrder = filteredShuffle,
            PlaybackHistory = filteredHistory,
            CurrentItemId = currentItemId,
            CurrentIndex = currentIndex
        };
    }
}

internal static class ImmutableListExtensions
{
    public static int FindIndex<T>(this ImmutableList<T> list, Func<T, bool> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
                return i;
        }
        return -1;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

public record MatchedSegment(long StartMs, long EndMs, string Strength, double Score);
public record TimeClip(string StartTimecode, string EndTimecode);

class Program
{
    private static string MsToTimecode(long totalMs)
    {
        var totalSec = (totalMs + 500) / 1000;
        var sec = (int)(totalSec % 60);
        var min = (int)(totalSec / 60 % 60);
        var hr = (int)(totalSec / 3_600);
        return $"{hr:D2}:{min:D2}:{sec:D2}:00";
    }

    static void Main()
    {
        // Randomly generate tests to find if StartTimecode can be <= previous
        var rand = new Random(42);
        for(int i=0; i<100000; i++)
        {
            var segments = new List<MatchedSegment>();
            for(int j=0; j<4; j++) {
                long start = rand.Next(0, 3600000); // up to 1 hr
                long end = start + rand.Next(100, 10000);
                segments.Add(new MatchedSegment(start, end, "test", 0.9));
            }
            
            var sortedMatches = segments.OrderBy(s => s.StartMs).ToList();
            var mergedMatches = new List<MatchedSegment>();
            foreach (var seg in sortedMatches)
            {
                if (mergedMatches.Count == 0) { mergedMatches.Add(seg); continue; }
                var last = mergedMatches[^1];
                if (seg.StartMs - last.EndMs <= 1000)
                {
                    mergedMatches[^1] = last with { EndMs = Math.Max(last.EndMs, seg.EndMs) };
                }
                else
                {
                    mergedMatches.Add(seg);
                }
            }

            var timeClips = mergedMatches
                .Select(seg => new TimeClip(MsToTimecode(seg.StartMs), MsToTimecode(seg.EndMs)))
                .Where(t => t.StartTimecode != t.EndTimecode)
                .ToList();

            for(int k=1; k<timeClips.Count; k++) {
                if (string.Compare(timeClips[k].StartTimecode, timeClips[k-1].StartTimecode) <= 0) {
                    Console.WriteLine($"FAILED!");
                    Console.WriteLine($"Prev: {timeClips[k-1].StartTimecode} (from {mergedMatches[k-1].StartMs})");
                    Console.WriteLine($"Curr: {timeClips[k].StartTimecode} (from {mergedMatches[k].StartMs})");
                    return;
                }
            }
        }
        Console.WriteLine("All 100,000 tests passed!");
    }
}

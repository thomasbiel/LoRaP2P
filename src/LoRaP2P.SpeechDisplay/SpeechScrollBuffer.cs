using LoRaP2P.Display.RadioBonnet;

namespace LoRaP2P.SpeechDisplay;

internal sealed class SpeechScrollBuffer
{
    private const int MaximumPendingLines = 256;
    private readonly Queue<string> pending = new();
    private readonly List<string> visible = [];
    private readonly Lock sync = new();

    public void Append(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (this.sync)
        {
            foreach (var line in RadioBonnetTextLayout.Wrap(text))
            {
                if (this.pending.Count == MaximumPendingLines)
                {
                    this.pending.Dequeue();
                }

                this.pending.Enqueue(line);
            }
        }
    }

    public IReadOnlyList<string>? Advance()
    {
        lock (this.sync)
        {
            if (!this.pending.TryDequeue(out var line))
            {
                return null;
            }

            if (this.visible.Count == RadioBonnetTextLayout.VisibleLines)
            {
                this.visible.RemoveAt(0);
            }

            this.visible.Add(line);
            return [.. this.visible];
        }
    }
}
namespace LoRaP2P.Display.RadioBonnet;

internal sealed class RadioBonnetFrameBuffer
{
    public const int Width = 128;
    public const int Height = 32;
    public byte[] Pixels { get; } = new byte[Width * Height / 8];

    public void Render(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Array.Clear(this.Pixels);
        for (var line = 0; line < Math.Min(lines.Count, RadioBonnetTextLayout.VisibleLines); line++)
        {
            var position = 0;
            foreach (var character in lines[line])
            {
                var glyph = RadioBonnetFont.Glyphs.GetValueOrDefault(character)
                    ?? RadioBonnetFont.Glyphs['?'];
                if (position + glyph.Length > Width)
                {
                    break;
                }

                glyph.CopyTo(this.Pixels, line * Width + position);
                position += glyph.Length + 1;
            }
        }
    }
}
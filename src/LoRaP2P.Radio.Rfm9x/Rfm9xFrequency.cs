namespace LoRaP2P.Radio.Rfm9x;

public static class Rfm9xFrequency
{
    private const double FrequencyStepHertz = 32_000_000.0 / 524_288.0;

    public static int ToRegisterValue(double frequencyHertz)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequencyHertz);

        int registerValue = checked((int)Math.Round(frequencyHertz / FrequencyStepHertz));
        if (registerValue > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(frequencyHertz));
        }

        return registerValue;
    }

    public static double FromRegisterValue(int registerValue)
    {
        if (registerValue is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(registerValue));
        }

        return registerValue * FrequencyStepHertz;
    }
}
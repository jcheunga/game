/// <summary>Deterministic hashing for the atlas geography: zone seeds and per-cell noise, so layouts never change.</summary>
public static class AdventureTerrain
{
    public static uint Seed(string text)
    {
        uint value = 2166136261;
        foreach (var ch in text) value = (value ^ ch) * 16777619;
        return value;
    }
    public static uint Hash(uint seed, int cell)
    {
        uint value = seed ^ ((uint)cell * 747796405u + 2891336453u);
        value = ((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u;
        return (value >> 22) ^ value;
    }
}

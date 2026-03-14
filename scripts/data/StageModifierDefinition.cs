using System;

public sealed class StageModifierDefinition
{
    public string Type { get; set; } = "";
    public float Value { get; set; }
    public string Label { get; set; } = "";

    public string NormalizedType =>
        string.IsNullOrWhiteSpace(Type)
            ? ""
            : Type.Trim().ToLowerInvariant();

    public StageModifierDefinition Clone()
    {
        return new StageModifierDefinition
        {
            Type = Type,
            Value = Value,
            Label = Label
        };
    }
}

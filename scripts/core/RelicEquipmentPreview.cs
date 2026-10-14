using System.Collections.Generic;

public sealed record RelicEquipmentPreview(
    UnitDefinition Unit,
    int Slot,
    EquipmentDefinition Replaced,
    EquipmentDefinition Candidate,
    UnitStats Current,
    UnitStats After,
    IReadOnlyList<UnitBonusLimit> Limits,
    bool CanApply,
    string Reason);

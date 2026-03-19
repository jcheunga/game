using System.Collections.Generic;
using Godot;

public static class UnitPool
{
	private static readonly Queue<Unit> Pool = new();
	private const int MaxPoolSize = 64;

	public static Unit Acquire()
	{
		if (Pool.Count > 0)
		{
			var unit = Pool.Dequeue();
			if (IsInstanceValid(unit))
			{
				return unit;
			}
		}

		return new Unit();
	}

	public static void Release(Unit unit)
	{
		if (unit == null || !IsInstanceValid(unit))
			return;

		if (Pool.Count >= MaxPoolSize)
		{
			unit.QueueFree();
			return;
		}

		unit.Visible = false;
		if (unit.IsInsideTree())
		{
			unit.GetParent()?.RemoveChild(unit);
		}
		unit.ResetForPool();
		Pool.Enqueue(unit);
	}

	public static void Clear()
	{
		while (Pool.Count > 0)
		{
			var unit = Pool.Dequeue();
			if (IsInstanceValid(unit))
			{
				unit.QueueFree();
			}
		}
	}

	private static bool IsInstanceValid(GodotObject obj)
	{
		return GodotObject.IsInstanceValid(obj);
	}
}

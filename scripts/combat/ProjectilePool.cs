using System.Collections.Generic;
using Godot;

public static class ProjectilePool
{
	private static readonly Queue<Projectile> Pool = new();
	private const int MaxPoolSize = 48;

	public static Projectile Acquire()
	{
		while (Pool.Count > 0)
		{
			var proj = Pool.Dequeue();
			if (GodotObject.IsInstanceValid(proj))
				return proj;
		}

		return new Projectile();
	}

	public static void Release(Projectile proj)
	{
		if (proj == null || !GodotObject.IsInstanceValid(proj))
			return;

		if (Pool.Count >= MaxPoolSize)
		{
			proj.QueueFree();
			return;
		}

		proj.Visible = false;
		proj.ResetForPool();
		var parent = proj.GetParent();
		parent?.RemoveChild(proj);
		Pool.Enqueue(proj);
	}

	public static void Clear()
	{
		while (Pool.Count > 0)
		{
			var proj = Pool.Dequeue();
			if (GodotObject.IsInstanceValid(proj))
				proj.QueueFree();
		}
	}
}

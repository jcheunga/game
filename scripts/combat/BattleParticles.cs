using Godot;

/// <summary>
/// Factory for spawning CpuParticles2D effects during battle.
/// Each method returns a one-shot particle emitter that auto-frees after its lifetime.
/// </summary>
public static class BattleParticles
{
	public static CpuParticles2D SpawnDeployBurst(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 24);
		particles.Lifetime = 0.4f;
		particles.Explosiveness = 0.92f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = 60f;
		particles.InitialVelocityMax = 140f;
		particles.Gravity = new Vector2(0f, 180f);
		particles.ScaleAmountMin = 2.5f;
		particles.ScaleAmountMax = 5f;
		particles.Color = color.Lightened(0.2f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color.Lightened(0.3f), 0.9f));
		gradient.AddPoint(0.5f, new Color(color, 0.6f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.2f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.6f);
		return particles;
	}

	public static CpuParticles2D SpawnDeathBurst(Node parent, Vector2 position, Color color, bool isBoss)
	{
		var count = isBoss ? 32 : 16;
		var particles = CreateBase(parent, position, count);
		particles.Lifetime = isBoss ? 0.6f : 0.4f;
		particles.Explosiveness = 0.95f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = 40f;
		particles.InitialVelocityMax = isBoss ? 180f : 110f;
		particles.Gravity = new Vector2(0f, 220f);
		particles.ScaleAmountMin = 2f;
		particles.ScaleAmountMax = isBoss ? 6f : 4f;
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color.Lightened(0.15f), 0.85f));
		gradient.AddPoint(0.4f, new Color(color.Darkened(0.1f), 0.5f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.4f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, isBoss ? 0.8f : 0.6f);
		return particles;
	}

	public static CpuParticles2D SpawnImpactSparks(Node parent, Vector2 position, Color color, float damage)
	{
		var count = Mathf.Clamp(Mathf.RoundToInt(damage * 0.4f), 4, 16);
		var particles = CreateBase(parent, position, count);
		particles.Lifetime = 0.25f;
		particles.Explosiveness = 1f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 140f;
		particles.InitialVelocityMin = 50f;
		particles.InitialVelocityMax = 120f;
		particles.Gravity = new Vector2(0f, 340f);
		particles.ScaleAmountMin = 1.5f;
		particles.ScaleAmountMax = 3f;
		particles.Color = color.Lightened(0.35f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.95f));
		gradient.AddPoint(0.3f, new Color(color.Lightened(0.3f), 0.7f));
		gradient.AddPoint(1f, new Color(color, 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.4f);
		return particles;
	}

	public static CpuParticles2D SpawnBaseHitDebris(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 18);
		particles.Lifetime = 0.5f;
		particles.Explosiveness = 0.9f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 120f;
		particles.InitialVelocityMin = 30f;
		particles.InitialVelocityMax = 100f;
		particles.Gravity = new Vector2(0f, 260f);
		particles.ScaleAmountMin = 2f;
		particles.ScaleAmountMax = 5f;
		particles.Color = color.Darkened(0.15f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color, 0.8f));
		gradient.AddPoint(0.3f, new Color(color.Darkened(0.2f), 0.5f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.5f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.7f);
		return particles;
	}

	public static CpuParticles2D SpawnFireballParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 28);
		particles.Lifetime = 0.5f;
		particles.Explosiveness = 0.85f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = radius * 0.6f;
		particles.InitialVelocityMax = radius * 1.4f;
		particles.Gravity = new Vector2(0f, -40f);
		particles.ScaleAmountMin = 3f;
		particles.ScaleAmountMax = 7f;
		var orange = new Color(1f, 0.55f, 0.1f);
		particles.Color = orange;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.95f));
		gradient.AddPoint(0.15f, new Color(new Color(1f, 0.85f, 0.2f), 0.9f));
		gradient.AddPoint(0.45f, new Color(orange, 0.6f));
		gradient.AddPoint(0.75f, new Color(new Color(0.8f, 0.2f, 0.05f), 0.3f));
		gradient.AddPoint(1f, new Color(new Color(0.2f, 0.1f, 0.05f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.7f);
		return particles;
	}

	public static CpuParticles2D SpawnHealSparkles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 20);
		particles.Lifetime = 0.6f;
		particles.Explosiveness = 0.3f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 60f;
		particles.InitialVelocityMin = 30f;
		particles.InitialVelocityMax = 80f;
		particles.Gravity = new Vector2(0f, -20f);
		particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
		particles.EmissionSphereRadius = Mathf.Max(12f, radius * 0.4f);
		particles.ScaleAmountMin = 2f;
		particles.ScaleAmountMax = 4.5f;
		particles.Color = color.Lightened(0.15f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.85f));
		gradient.AddPoint(0.3f, new Color(color.Lightened(0.2f), 0.7f));
		gradient.AddPoint(0.7f, new Color(color, 0.35f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.1f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.8f);
		return particles;
	}

	public static CpuParticles2D SpawnFrostParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 24);
		particles.Lifetime = 0.55f;
		particles.Explosiveness = 0.88f;
		particles.Direction = new Vector2(0f, 0f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = radius * 0.4f;
		particles.InitialVelocityMax = radius * 1.1f;
		particles.Gravity = new Vector2(0f, 30f);
		particles.ScaleAmountMin = 2f;
		particles.ScaleAmountMax = 5f;
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.9f));
		gradient.AddPoint(0.2f, new Color(new Color(0.8f, 0.95f, 1f), 0.75f));
		gradient.AddPoint(0.5f, new Color(color, 0.45f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.2f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.7f);
		return particles;
	}

	public static CpuParticles2D SpawnLightningParticles(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 14);
		particles.Lifetime = 0.3f;
		particles.Explosiveness = 1f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 30f;
		particles.InitialVelocityMin = 80f;
		particles.InitialVelocityMax = 200f;
		particles.Gravity = new Vector2(0f, 600f);
		particles.ScaleAmountMin = 1.5f;
		particles.ScaleAmountMax = 3f;
		particles.Color = color.Lightened(0.3f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 1f));
		gradient.AddPoint(0.2f, new Color(color.Lightened(0.4f), 0.8f));
		gradient.AddPoint(0.6f, new Color(color, 0.4f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.2f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.45f);
		return particles;
	}

	public static CpuParticles2D SpawnWardParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 16);
		particles.Lifetime = 0.7f;
		particles.Explosiveness = 0.2f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = 10f;
		particles.InitialVelocityMax = 40f;
		particles.Gravity = new Vector2(0f, -15f);
		particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
		particles.EmissionSphereRadius = Mathf.Max(14f, radius * 0.5f);
		particles.ScaleAmountMin = 2.5f;
		particles.ScaleAmountMax = 5f;
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color.Lightened(0.15f), 0.7f));
		gradient.AddPoint(0.4f, new Color(color, 0.5f));
		gradient.AddPoint(0.8f, new Color(color.Lightened(0.08f), 0.2f));
		gradient.AddPoint(1f, new Color(color, 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.9f);
		return particles;
	}

	public static CpuParticles2D SpawnStoneBarricadeParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 20);
		particles.Lifetime = 0.5f;
		particles.Explosiveness = 0.8f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 90f;
		particles.InitialVelocityMin = radius * 0.4f;
		particles.InitialVelocityMax = radius * 1.0f;
		particles.Gravity = new Vector2(0f, -60f);
		particles.ScaleAmountMin = 2.5f;
		particles.ScaleAmountMax = 5.5f;
		var brown = new Color(0.72f, 0.58f, 0.36f);
		particles.Color = brown;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(new Color(0.82f, 0.72f, 0.52f), 0.9f));
		gradient.AddPoint(0.3f, new Color(brown, 0.7f));
		gradient.AddPoint(0.7f, new Color(brown.Darkened(0.2f), 0.35f));
		gradient.AddPoint(1f, new Color(brown.Darkened(0.4f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.7f);
		return particles;
	}

	public static CpuParticles2D SpawnWarCryParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 24);
		particles.Lifetime = 0.6f;
		particles.Explosiveness = 0.9f;
		particles.Direction = new Vector2(0f, 0f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = radius * 0.5f;
		particles.InitialVelocityMax = radius * 1.4f;
		particles.Gravity = new Vector2(0f, 20f);
		particles.ScaleAmountMin = 3f;
		particles.ScaleAmountMax = 6f;
		var redOrange = new Color(0.95f, 0.35f, 0.1f);
		particles.Color = redOrange;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(new Color(1f, 0.7f, 0.2f), 0.95f));
		gradient.AddPoint(0.25f, new Color(redOrange, 0.8f));
		gradient.AddPoint(0.6f, new Color(new Color(0.8f, 0.2f, 0.05f), 0.4f));
		gradient.AddPoint(1f, new Color(redOrange.Darkened(0.4f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.8f);
		return particles;
	}

	public static CpuParticles2D SpawnEarthquakeParticles(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 30);
		particles.Lifetime = 0.55f;
		particles.Explosiveness = 0.85f;
		particles.Direction = new Vector2(0f, 1f);
		particles.Spread = 160f;
		particles.InitialVelocityMin = radius * 0.3f;
		particles.InitialVelocityMax = radius * 1.2f;
		particles.Gravity = new Vector2(0f, 420f);
		particles.ScaleAmountMin = 3f;
		particles.ScaleAmountMax = 7f;
		var darkBrown = new Color(0.55f, 0.4f, 0.22f);
		particles.Color = darkBrown;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(new Color(0.7f, 0.55f, 0.3f), 0.9f));
		gradient.AddPoint(0.3f, new Color(darkBrown, 0.7f));
		gradient.AddPoint(0.65f, new Color(darkBrown.Darkened(0.2f), 0.35f));
		gradient.AddPoint(1f, new Color(darkBrown.Darkened(0.5f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.75f);
		return particles;
	}

	public static CpuParticles2D SpawnPolymorphParticles(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 16);
		particles.Lifetime = 0.65f;
		particles.Explosiveness = 0.3f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = 20f;
		particles.InitialVelocityMax = 60f;
		particles.Gravity = new Vector2(0f, -10f);
		particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
		particles.EmissionSphereRadius = 16f;
		particles.ScaleAmountMin = 2f;
		particles.ScaleAmountMax = 4.5f;
		var purple = new Color(0.7f, 0.3f, 0.9f);
		particles.Color = purple;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.9f));
		gradient.AddPoint(0.25f, new Color(purple.Lightened(0.2f), 0.75f));
		gradient.AddPoint(0.6f, new Color(purple, 0.4f));
		gradient.AddPoint(1f, new Color(purple.Darkened(0.2f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.85f);
		return particles;
	}

	public static CpuParticles2D SpawnResurrectParticles(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 18);
		particles.Lifetime = 0.7f;
		particles.Explosiveness = 0.4f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 50f;
		particles.InitialVelocityMin = 40f;
		particles.InitialVelocityMax = 100f;
		particles.Gravity = new Vector2(0f, -120f);
		particles.ScaleAmountMin = 2.5f;
		particles.ScaleAmountMax = 5f;
		var golden = new Color(1f, 0.85f, 0.3f);
		particles.Color = golden;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.95f));
		gradient.AddPoint(0.2f, new Color(golden, 0.85f));
		gradient.AddPoint(0.55f, new Color(golden.Darkened(0.1f), 0.5f));
		gradient.AddPoint(1f, new Color(golden.Darkened(0.3f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.9f);
		return particles;
	}

	public static CpuParticles2D SpawnDeathBurstExplosion(Node parent, Vector2 position, Color color, float radius)
	{
		var particles = CreateBase(parent, position, 22);
		particles.Lifetime = 0.45f;
		particles.Explosiveness = 0.95f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = radius * 0.5f;
		particles.InitialVelocityMax = radius * 1.6f;
		particles.Gravity = new Vector2(0f, 120f);
		particles.ScaleAmountMin = 3f;
		particles.ScaleAmountMax = 6f;
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color.Lightened(0.2f), 0.9f));
		gradient.AddPoint(0.3f, new Color(color, 0.6f));
		gradient.AddPoint(0.7f, new Color(color.Darkened(0.3f), 0.25f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.5f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.65f);
		return particles;
	}

	public static CpuParticles2D SpawnProjectileTrail(Node parent, Color color)
	{
		var particles = new CpuParticles2D
		{
			Amount = 8,
			OneShot = false,
			Lifetime = 0.18f,
			Direction = new Vector2(-1f, 0f),
			Spread = 20f,
			InitialVelocityMin = 10f,
			InitialVelocityMax = 30f,
			Gravity = Vector2.Zero,
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3f,
			ZIndex = 90,
			Emitting = true
		};
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color.Lightened(0.15f), 0.6f));
		gradient.AddPoint(0.5f, new Color(color, 0.3f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.2f), 0f));
		particles.ColorRamp = gradient;
		parent.AddChild(particles);
		return particles;
	}

	public static CpuParticles2D SpawnBossSpawnBurst(Node parent, Vector2 position, Color color)
	{
		var particles = CreateBase(parent, position, 36);
		particles.Lifetime = 0.7f;
		particles.Explosiveness = 0.6f;
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 180f;
		particles.InitialVelocityMin = 40f;
		particles.InitialVelocityMax = 160f;
		particles.Gravity = new Vector2(0f, 60f);
		particles.ScaleAmountMin = 3f;
		particles.ScaleAmountMax = 8f;
		particles.Color = color;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Colors.White, 0.9f));
		gradient.AddPoint(0.2f, new Color(color.Lightened(0.25f), 0.8f));
		gradient.AddPoint(0.5f, new Color(color, 0.5f));
		gradient.AddPoint(0.8f, new Color(color.Darkened(0.2f), 0.2f));
		gradient.AddPoint(1f, new Color(color.Darkened(0.4f), 0f));
		particles.ColorRamp = gradient;
		particles.Emitting = true;
		AutoFree(particles, 0.9f);
		return particles;
	}

	private static CpuParticles2D CreateBase(Node parent, Vector2 position, int amount, string textureId = "particle_soft")
	{
		var particles = new CpuParticles2D
		{
			Amount = Mathf.Max(1, amount),
			OneShot = true,
			Position = position,
			ZIndex = 100
		};

		var texture = ParticleTextureLoader.TryLoad(textureId);
		if (texture != null)
		{
			particles.Texture = texture;
		}

		parent.AddChild(particles);
		return particles;
	}

	private static async void AutoFree(CpuParticles2D particles, float delay)
	{
		if (!GodotObject.IsInstanceValid(particles))
		{
			return;
		}

		await particles.ToSignal(
			particles.GetTree().CreateTimer(delay),
			SceneTreeTimer.SignalName.Timeout);

		if (GodotObject.IsInstanceValid(particles))
		{
			particles.QueueFree();
		}
	}
}

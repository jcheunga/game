using Godot;

/// <summary>
/// Factory for spawning CpuParticles2D effects during battle.
/// Each method returns a one-shot particle emitter that auto-frees after its lifetime.
/// </summary>
public static class BattleParticles
{
	public static CpuParticles2D SpawnDeployBurst(Node parent, Vector2 position, Color color)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var particles = CreateBase(parent, position, 24, "particle_deploy");
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
		StartAndAutoFree(particles, 0.6f);
		return particles;
	}

	public static CpuParticles2D SpawnDeathBurst(Node parent, Vector2 position, Color color, bool isBoss)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var count = isBoss ? 32 : 16;
		var particles = CreateBase(parent, position, count, "particle_smoke");
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
		StartAndAutoFree(particles, isBoss ? 0.8f : 0.6f);
		return particles;
	}

	public static CpuParticles2D SpawnImpactSparks(Node parent, Vector2 position, Color color, float damage)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var count = Mathf.Clamp(Mathf.RoundToInt(damage * 0.4f), 4, 16);
		var particles = CreateBase(parent, position, count, "particle_spark");
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
		StartAndAutoFree(particles, 0.4f);
		return particles;
	}

	public static CpuParticles2D SpawnBaseHitDebris(Node parent, Vector2 position, Color color)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var particles = CreateBase(parent, position, 18, "particle_stone");
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
		StartAndAutoFree(particles, 0.7f);
		return particles;
	}

	/// <summary>A body raised by necromancy: grave-light flames licking up around it and, for the dead that come up
	/// out of the ground, clods of earth thrown aside.</summary>
	public static void SpawnResurrectRise(Node parent, Vector2 position, bool fromGround)
	{
		if (IsReducedMotionEnabled()) return;
		var soul = new Color(.45f, 1f, .55f);
		// grave-light pooling under the body, a few tongues of soul-fire licking up it, and wisps let go above
		SpellVfx.Flash(parent, position, soul, 30f, 1f, ground: true);
		// (the painted wisp is pale blue-white, so it takes the green; the painted flame is orange and would not)
		SpellVfx.Burst(parent, position + new Vector2(0, -6), "p_wisp", 10, .85f, SpellVfx.Fade(soul.Lightened(.35f), soul.Darkened(.25f)),
			8f, 15f, 14f, 38f, 16f, Vector2.Up, gravity: -40f, groundRadius: 13f, explosiveness: .35f);
		SpellVfx.Burst(parent, position + new Vector2(0, -22), "p_wisp", 3, 1.2f, SpellVfx.Fade(new Color(soul, .7f), soul.Darkened(.2f)),
			6f, 10f, 10f, 26f, 30f, Vector2.Up, gravity: -25f, delay: .25f);
		if (!fromGround) return;
		var dirt = CreateBase(parent, position, 16, "particle_stone");
		dirt.Lifetime = .6f;
		dirt.Explosiveness = .9f;
		dirt.Direction = new Vector2(0f, -1f);
		dirt.Spread = 70f;
		dirt.InitialVelocityMin = 50f;
		dirt.InitialVelocityMax = 120f;
		dirt.Gravity = new Vector2(0f, 420f);
		dirt.ScaleAmountMin = 1.6f;
		dirt.ScaleAmountMax = 3.6f;
		dirt.Color = new Color(.34f, .27f, .2f);
		StartAndAutoFree(dirt, .9f);
	}

	public static CpuParticles2D SpawnDeathBurstExplosion(Node parent, Vector2 position, Color color, float radius)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var particles = CreateBase(parent, position, 22, "particle_fire");
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
		StartAndAutoFree(particles, 0.65f);
		return particles;
	}

	public static CpuParticles2D SpawnBossSpawnBurst(Node parent, Vector2 position, Color color)
	{
		if (IsReducedMotionEnabled())
		{
			return null;
		}

		var particles = CreateBase(parent, position, 36, "particle_smoke");
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
		StartAndAutoFree(particles, 0.9f);
		return particles;
	}

	private static CpuParticles2D CreateBase(Node parent, Vector2 position, int amount, string textureId = "particle_soft")
	{
		var particles = new CpuParticles2D
		{
			Amount = Mathf.Max(1, amount),
			OneShot = true,
			Emitting = false,
			Position = position,
			ZIndex = 100
		};

		var texture = ParticleTextureLoader.TryLoad(textureId) ?? ParticleTextureLoader.SoftTexture;
		if (texture != null)
		{
			particles.Texture = texture;
		}

		parent.AddChild(particles);
		return particles;
	}

	// The authored sizes are world-pixel diameters, not multiples of a 128px
	// texture. Keep sparks and trails local to the weapon, even at close zoom.
	private static void NormalizeTextureSize(CpuParticles2D particles)
	{
		if (particles.Texture == null) return;
		var pixels = Mathf.Max(1, Mathf.Max(particles.Texture.GetWidth(), particles.Texture.GetHeight()));
		particles.ScaleAmountMin /= pixels;
		particles.ScaleAmountMax /= pixels;
	}

	private static bool IsReducedMotionEnabled()
	{
		return GameState.Instance != null && GameState.Instance.ReducedMotion;
	}

	private static async void StartAndAutoFree(CpuParticles2D particles, float delay)
	{
		if (!GodotObject.IsInstanceValid(particles))
		{
			return;
		}

		NormalizeTextureSize(particles);
		particles.Emitting = true;

		await particles.ToSignal(
			particles.GetTree().CreateTimer(delay),
			SceneTreeTimer.SignalName.Timeout);

		if (GodotObject.IsInstanceValid(particles))
		{
			particles.QueueFree();
		}
	}
}

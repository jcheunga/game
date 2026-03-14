using Godot;

/// <summary>
/// Spawns persistent ambient particle effects based on terrain type.
/// Attach to the BattleController as a child and call Setup with the terrain ID.
/// </summary>
public partial class BattleAmbientParticles : Node2D
{
	private CpuParticles2D _primary;
	private CpuParticles2D _secondary;

	public void Setup(string terrainId, float left, float right, float top, float bottom)
	{
		var width = right - left;
		var height = bottom - top;
		var center = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);

		switch ((terrainId ?? "urban").ToLowerInvariant())
		{
			case "foundry":
			case "smelter":
			case "railyard":
				_primary = CreateEmbers(center, width, height);
				break;
			case "pass":
			case "watchfort":
				_primary = CreateSnowDrift(center, width, height);
				break;
			case "marsh":
			case "ferry":
			case "swamp":
				_primary = CreateMist(center, width, height);
				break;
			case "cathedral":
			case "reliquary":
			case "shrine":
				_primary = CreateDustMotes(center, width, height);
				break;
			case "ossuary":
				_primary = CreateDustMotes(center, width, height);
				_secondary = CreateAshFall(center, width, height);
				break;
			case "grove":
			case "timberroad":
				_primary = CreateLeafDrift(center, width, height);
				break;
			case "witchcircle":
				_primary = CreateMist(center, width, height);
				_secondary = CreateEmbers(center, width, height);
				break;
			case "grassland":
			case "waystation":
			case "siegecamp":
				_primary = CreateWindDust(center, width, height);
				break;
			case "bridgefort":
			case "breachyard":
			case "innerkeep":
				_primary = CreateAshFall(center, width, height);
				break;
			case "checkpoint":
			case "decon":
			case "lab":
			case "blacksite":
				_primary = CreateDustMotes(center, width, height);
				break;
			case "night":
				_primary = CreateFireflies(center, width, height);
				break;
			case "shipyard":
				_primary = CreateMist(center, width, height);
				break;
		}
	}

	private CpuParticles2D CreateEmbers(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 18,
			Lifetime = 2.5f,
			Position = new Vector2(center.X, center.Y + height * 0.3f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.45f, height * 0.1f),
			Direction = new Vector2(0.3f, -1f),
			Spread = 25f,
			InitialVelocityMin = 15f,
			InitialVelocityMax = 45f,
			Gravity = new Vector2(8f, -12f),
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3.5f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1f, 0.6f, 0.15f, 0f));
		gradient.AddPoint(0.15f, new Color(1f, 0.55f, 0.1f, 0.7f));
		gradient.AddPoint(0.6f, new Color(1f, 0.35f, 0.05f, 0.5f));
		gradient.AddPoint(1f, new Color(0.6f, 0.15f, 0.05f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateSnowDrift(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 24,
			Lifetime = 4f,
			Position = new Vector2(center.X, center.Y - height * 0.5f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.55f, 10f),
			Direction = new Vector2(0.4f, 1f),
			Spread = 35f,
			InitialVelocityMin = 8f,
			InitialVelocityMax = 22f,
			Gravity = new Vector2(6f, 14f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.95f, 0.97f, 1f, 0f));
		gradient.AddPoint(0.1f, new Color(0.95f, 0.97f, 1f, 0.45f));
		gradient.AddPoint(0.7f, new Color(0.9f, 0.94f, 1f, 0.3f));
		gradient.AddPoint(1f, new Color(0.9f, 0.94f, 1f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateMist(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 10,
			Lifetime = 5f,
			Position = new Vector2(center.X, center.Y + height * 0.15f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.5f, height * 0.25f),
			Direction = new Vector2(1f, 0f),
			Spread = 15f,
			InitialVelocityMin = 4f,
			InitialVelocityMax = 12f,
			Gravity = Vector2.Zero,
			ScaleAmountMin = 12f,
			ScaleAmountMax = 28f,
			ZIndex = 45,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.7f, 0.85f, 0.75f, 0f));
		gradient.AddPoint(0.2f, new Color(0.75f, 0.88f, 0.78f, 0.08f));
		gradient.AddPoint(0.5f, new Color(0.8f, 0.9f, 0.82f, 0.06f));
		gradient.AddPoint(1f, new Color(0.8f, 0.9f, 0.82f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateDustMotes(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 12,
			Lifetime = 3.5f,
			Position = center,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.45f, height * 0.35f),
			Direction = new Vector2(0f, -1f),
			Spread = 180f,
			InitialVelocityMin = 2f,
			InitialVelocityMax = 8f,
			Gravity = new Vector2(2f, -3f),
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1f, 0.96f, 0.85f, 0f));
		gradient.AddPoint(0.15f, new Color(1f, 0.96f, 0.85f, 0.25f));
		gradient.AddPoint(0.6f, new Color(1f, 0.94f, 0.8f, 0.15f));
		gradient.AddPoint(1f, new Color(1f, 0.94f, 0.8f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateAshFall(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 14,
			Lifetime = 3f,
			Position = new Vector2(center.X, center.Y - height * 0.45f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.5f, 10f),
			Direction = new Vector2(0.2f, 1f),
			Spread = 30f,
			InitialVelocityMin = 6f,
			InitialVelocityMax = 18f,
			Gravity = new Vector2(4f, 10f),
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.6f, 0.55f, 0.5f, 0f));
		gradient.AddPoint(0.1f, new Color(0.65f, 0.6f, 0.55f, 0.35f));
		gradient.AddPoint(0.6f, new Color(0.5f, 0.48f, 0.45f, 0.2f));
		gradient.AddPoint(1f, new Color(0.4f, 0.38f, 0.35f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateLeafDrift(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 10,
			Lifetime = 4f,
			Position = new Vector2(center.X, center.Y - height * 0.4f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.5f, 10f),
			Direction = new Vector2(0.5f, 1f),
			Spread = 40f,
			InitialVelocityMin = 8f,
			InitialVelocityMax = 20f,
			Gravity = new Vector2(6f, 8f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.55f, 0.7f, 0.3f, 0f));
		gradient.AddPoint(0.1f, new Color(0.6f, 0.72f, 0.32f, 0.45f));
		gradient.AddPoint(0.5f, new Color(0.7f, 0.6f, 0.25f, 0.35f));
		gradient.AddPoint(1f, new Color(0.65f, 0.5f, 0.2f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateWindDust(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 8,
			Lifetime = 2.5f,
			Position = new Vector2(center.X - width * 0.4f, center.Y + height * 0.2f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(10f, height * 0.3f),
			Direction = new Vector2(1f, -0.1f),
			Spread = 12f,
			InitialVelocityMin = 20f,
			InitialVelocityMax = 50f,
			Gravity = new Vector2(0f, 6f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 5f,
			ZIndex = 45,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.85f, 0.75f, 0.55f, 0f));
		gradient.AddPoint(0.1f, new Color(0.85f, 0.75f, 0.55f, 0.2f));
		gradient.AddPoint(0.5f, new Color(0.8f, 0.7f, 0.5f, 0.12f));
		gradient.AddPoint(1f, new Color(0.8f, 0.7f, 0.5f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}

	private CpuParticles2D CreateFireflies(Vector2 center, float width, float height)
	{
		var p = new CpuParticles2D
		{
			Amount = 8,
			Lifetime = 3f,
			Position = center,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.4f, height * 0.35f),
			Direction = new Vector2(0f, -1f),
			Spread = 180f,
			InitialVelocityMin = 3f,
			InitialVelocityMax = 10f,
			Gravity = new Vector2(0f, -2f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 3.5f,
			ZIndex = 50,
			Emitting = true
		};
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.7f, 1f, 0.5f, 0f));
		gradient.AddPoint(0.15f, new Color(0.75f, 1f, 0.55f, 0.5f));
		gradient.AddPoint(0.5f, new Color(0.7f, 0.95f, 0.4f, 0.35f));
		gradient.AddPoint(0.85f, new Color(0.65f, 0.9f, 0.35f, 0.2f));
		gradient.AddPoint(1f, new Color(0.6f, 0.85f, 0.3f, 0f));
		p.ColorRamp = gradient;
		AddChild(p);
		return p;
	}
}

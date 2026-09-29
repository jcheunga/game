using Godot;

// One bounded, additive pose reaction; it never moves the combatant's feet.
// Hits inside 70 ms share a weighted direction and the strongest amplitude.
// The window is not extended by more hits, and opposite hits can cancel out.
internal sealed class HitReactionMotion
{
    internal const float MergeSeconds = .07f;
    internal float Amount { get; private set; }
    private float _velocity, _remaining, _weightedDirection, _weight, _peak;

    internal void Add(float direction, float strength)
    {
        strength = Mathf.Clamp(strength, 0, 1);
        if (strength <= 0 || Mathf.IsZeroApprox(direction)) return;
        if (_remaining <= 0)
        {
            _remaining = MergeSeconds;
            _weightedDirection = _weight = _peak = 0;
        }
        _weightedDirection += Mathf.Clamp(direction, -1, 1) * strength;
        _weight += strength;
        _peak = Mathf.Max(_peak, strength);
    }

    internal void Advance(float delta)
    {
        delta = Mathf.Max(0, delta);
        if (_remaining > 0)
        {
            var step = Mathf.Min(delta, _remaining);
            Spring(_weightedDirection / _weight * _peak, step, 55);
            _remaining -= step;
            delta -= step;
        }
        if (delta > 0) Spring(0, delta, 32);
    }

    private void Spring(float target, float delta, float speed)
    {
        // Exact critically damped integration remains stable after a slow frame.
        var difference = Amount - target;
        var decay = Mathf.Exp(-speed * delta);
        var change = (_velocity + speed * difference) * delta;
        Amount = target + (difference + change) * decay;
        _velocity = (_velocity - speed * change) * decay;
        if (Mathf.Abs(Amount) > 1) { Amount = Mathf.Sign(Amount); _velocity = 0; }
    }

    internal void Reset()
    {
        Amount = _velocity = _remaining = _weightedDirection = _weight = _peak = 0;
    }
}

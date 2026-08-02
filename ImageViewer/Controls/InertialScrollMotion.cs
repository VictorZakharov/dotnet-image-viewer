using System;

namespace ImageViewer.Controls;

internal sealed class InertialScrollMotion
{
    private const double WheelTravel = 72;
    private const double VelocityImpulse = 420;
    private const double BurstWindowSeconds = 0.18;
    private const int MaximumBurstLevel = 6;

    private int _burstLevel;
    private int _inputDirection;

    public bool IsActive { get; private set; }
    public double TargetOffset { get; private set; }
    public double Velocity { get; private set; }

    public bool AddWheelInput(
        double currentOffset,
        double maximumOffset,
        double wheelDelta,
        double secondsSinceLastInput)
    {
        currentOffset = ClampOffset(currentOffset, maximumOffset);
        maximumOffset = Math.Max(0, maximumOffset);
        if (maximumOffset <= 0 || Math.Abs(wheelDelta) < 0.001)
        {
            Reset(currentOffset);
            return false;
        }

        var direction = -Math.Sign(wheelDelta);
        if (_inputDirection != 0 && direction != _inputDirection)
            Reset(currentOffset);
        else if (!IsActive)
            TargetOffset = currentOffset;

        _burstLevel = double.IsFinite(secondsSinceLastInput)
            && secondsSinceLastInput <= BurstWindowSeconds
            ? Math.Min(MaximumBurstLevel, _burstLevel + 1)
            : 0;
        _inputDirection = direction;

        var units = Math.Clamp(Math.Abs(wheelDelta), 0.25, 4);
        var burstBoost = 1 + (_burstLevel * 0.22);
        TargetOffset = Math.Clamp(
            TargetOffset + (direction * WheelTravel * units * burstBoost),
            0,
            maximumOffset);

        if (Math.Abs(TargetOffset - currentOffset) < 0.01)
        {
            Reset(currentOffset);
            return false;
        }

        Velocity += direction * VelocityImpulse * units * burstBoost;
        Velocity = Math.Clamp(Velocity, -4200, 4200);
        IsActive = true;
        return true;
    }

    public double Advance(
        double currentOffset,
        double maximumOffset,
        double viewportHeight,
        double elapsedSeconds)
    {
        maximumOffset = Math.Max(0, maximumOffset);
        currentOffset = ClampOffset(currentOffset, maximumOffset);
        TargetOffset = ClampOffset(TargetOffset, maximumOffset);

        var remaining = TargetOffset - currentOffset;
        if (!IsActive || Math.Abs(remaining) < 0.01)
            return Complete(TargetOffset);

        var direction = Math.Sign(remaining);
        if (Math.Sign(Velocity) != direction)
            Velocity = 0;

        var dt = Math.Clamp(elapsedSeconds, 1d / 240, 0.05);
        var viewport = Math.Max(240, viewportHeight);
        var maximumSpeed = Math.Max(1400, viewport * 3.5);
        var acceleration = Math.Max(10000, viewport * 16);
        var deceleration = Math.Max(12000, viewport * 20);
        var stoppingSpeed = Math.Sqrt(2 * deceleration * Math.Abs(remaining));
        var desiredVelocity = direction * Math.Min(maximumSpeed, stoppingSpeed);
        var rate = Math.Abs(desiredVelocity) > Math.Abs(Velocity)
            ? acceleration
            : deceleration;

        Velocity = MoveTowards(Velocity, desiredVelocity, rate * dt);
        var step = Velocity * dt;
        if (Math.Sign(step) != direction || Math.Abs(step) >= Math.Abs(remaining))
            return Complete(TargetOffset);

        var nextOffset = ClampOffset(currentOffset + step, maximumOffset);
        if (Math.Abs(TargetOffset - nextOffset) <= 0.35 && Math.Abs(Velocity) <= 30)
            return Complete(TargetOffset);

        return nextOffset;
    }

    public void Reset(double currentOffset)
    {
        TargetOffset = currentOffset;
        Velocity = 0;
        IsActive = false;
        _burstLevel = 0;
        _inputDirection = 0;
    }

    private double Complete(double offset)
    {
        Velocity = 0;
        IsActive = false;
        return offset;
    }

    private static double ClampOffset(double offset, double maximumOffset) =>
        Math.Clamp(offset, 0, Math.Max(0, maximumOffset));

    private static double MoveTowards(double current, double target, double maximumChange)
    {
        var change = target - current;
        return Math.Abs(change) <= maximumChange
            ? target
            : current + (Math.Sign(change) * maximumChange);
    }
}

﻿namespace Nalix.Game.Domain.Models.Attacks;

using Nalix.Game.Domain.Models.Combat;

public sealed class RangedAttack : IAttackBehavior
{
    public AttackType Type => AttackType.Ranged;

    public void Execute(ICombatant attacker, ICombatant target)
    {
        long damage = attacker.CalculateDamage(target) - 2; // Tầm xa có thể yếu hơn một chút
        target.TakeDamage(damage);
    }
}
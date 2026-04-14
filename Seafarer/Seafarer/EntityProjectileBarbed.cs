using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace SaltAndSand
{
    public class EntityProjectileBarbed : EntityProjectile
    {
        protected override void ImpactOnEntity(Entity entity)
        {
            base.ImpactOnEntity(entity);

            if (World.Side != EnumAppSide.Server) return;
            if (entity == null || !entity.Alive) return;

            var attrs = Properties.Attributes;
            float bleedDamage = attrs?["bleedDamage"].AsFloat(1.5f) ?? 1.5f;
            float bleedDurationSec = attrs?["bleedDurationSec"].AsFloat(6f) ?? 6f;
            int bleedTicks = attrs?["bleedTicks"].AsInt(3) ?? 3;

            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                Type = EnumDamageType.Injury,
                SourceEntity = this,
                CauseEntity = FiredBy,
                Duration = TimeSpan.FromSeconds(bleedDurationSec),
                TicksPerDuration = bleedTicks,
                DamageOverTimeTypeEnum = EnumDamageOverTimeEffectType.Bleeding
            }, bleedDamage);
        }
    }
}

#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Actor's turret rises from the ground before attacking.")]
	class AttackPopupTurretedInfo : AttackTurretedInfo, Requires<BuildingInfo>, Requires<RenderBuildingInfo>
	{
		[Desc("How many game ticks should pass before closing the actor's turret.")]
		public int CloseDelay = 125;
		public int DefaultFacing = 0;

		[Desc("The percentage of damage that is received while this actor is closed.")]
		public int ClosedDamageMultiplier = 50;

		public override object Create(ActorInitializer init) { return new AttackPopupTurreted(init, this); }
	}

	class AttackPopupTurreted : AttackTurreted, INotifyBuildComplete, INotifyIdle, IDamageModifier
	{
		enum PopupState { Open, Rotating, Transitioning, Closed }

		readonly AttackPopupTurretedInfo info;
		RenderBuilding rb;

		int idleTicks = 0;
		PopupState state = PopupState.Open;
		Turreted turret;
		bool skippedMakeAnimation;

		public AttackPopupTurreted(ActorInitializer init, AttackPopupTurretedInfo info)
			: base(init.Self, info)
		{
			this.info = info;
			turret = turrets.FirstOrDefault();
			rb = init.Self.Trait<RenderBuilding>();
			skippedMakeAnimation = init.Contains<SkipMakeAnimsInit>();
		}

		protected override bool CanAttack(Actor self, Target target)
		{
			if (state == PopupState.Transitioning || !building.Value.BuildComplete)
				return false;

			if (!base.CanAttack(self, target))
				return false;

			idleTicks = 0;
			if (state == PopupState.Closed)
			{
				state = PopupState.Transitioning;
				rb.PlayCustomAnimThen(self, "opening", () =>
				{
					state = PopupState.Open;
					rb.PlayCustomAnimRepeating(self, "idle");
				});
				return false;
			}

			return turret.FaceTarget(self, target);
		}

		public void TickIdle(Actor self)
		{
			if (state == PopupState.Open && idleTicks++ > info.CloseDelay)
			{
				turret.DesiredFacing = info.DefaultFacing;
				state = PopupState.Rotating;
			}
			else if (state == PopupState.Rotating && turret.TurretFacing == info.DefaultFacing)
			{
				state = PopupState.Transitioning;
				rb.PlayCustomAnimThen(self, "closing", () =>
				{
					state = PopupState.Closed;
					rb.PlayCustomAnimRepeating(self, "closed-idle");
					turret.DesiredFacing = null;
				});
			}
		}

		public void BuildingComplete(Actor self)
		{
			if (skippedMakeAnimation)
			{
				state = PopupState.Closed;
				rb.PlayCustomAnimRepeating(self, "closed-idle");
				turret.DesiredFacing = null;
			}
		}

		public int GetDamageModifier(Actor attacker, DamageWarhead warhead)
		{
			return state == PopupState.Closed ? info.ClosedDamageMultiplier : 100;
		}
	}
}

using Content.Server.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;

namespace Content.Server.Repairable
{
    public sealed class RepairableSystem : SharedRepairableSystem
    {
        [Dependency] private readonly SharedToolSystem _toolSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger= default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<RepairableComponent, InteractUsingEvent>(Repair);
            SubscribeLocalEvent<RepairableComponent, RepairFinishedEvent>(OnRepairFinished);
        }

        private void OnRepairFinished(EntityUid uid, RepairableComponent component, RepairFinishedEvent args)
        {
            if (args.Cancelled)
                return;

            if (!EntityManager.TryGetComponent(uid, out DamageableComponent? damageable) || damageable.TotalDamage == 0)
                return;

            if (component.Damage != null)
            {
                var damageChanged = _damageableSystem.TryChangeDamage(uid, component.Damage, true, false, origin: args.User);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(args.User):user} repaired {ToPrettyString(uid):target} by {damageChanged?.Total}");
            }

            else
            {
                // Repair all damage
                _damageableSystem.SetAllDamage(uid, damageable, 0);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(args.User):user} repaired {ToPrettyString(uid):target} back to full health");
            }

            uid.PopupMessage(args.User,
                Loc.GetString("comp-repairable-repair",
                    ("target", uid),
                    ("tool", args.Used!)));
        }

        public async void Repair(EntityUid uid, RepairableComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            // Only try repair the target if it is damaged
            if (!EntityManager.TryGetComponent(uid, out DamageableComponent? damageable) || damageable.TotalDamage == 0)
                return;

            float delay = component.DoAfterDelay;

            // Add a penalty to how long it takes if the user is repairing itself
            if (args.User == args.Target)
                delay *= component.SelfRepairPenalty;

            // Can the tool actually repair this, does it have enough fuel?
            args.Handled = !_toolSystem.UseTool(args.Used, args.User, uid, delay, component.QualityNeeded, new RepairFinishedEvent(), component.FuelCost);
        }
    }
}

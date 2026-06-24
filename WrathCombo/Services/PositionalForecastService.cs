using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using System;
using System.Diagnostics;
using System.Linq;
using WrathCombo.Combos.PvE;
using WrathCombo.Combos.PvE.Enums;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Services;

internal enum PositionalRequirement
{
    None = 0,
    Rear = 1,
    Flank = 2,
}

internal readonly record struct PositionalForecast(
    uint ActionId,
    PositionalRequirement Requirement,
    int GcdsUntil,
    ulong TargetId)
{
    internal static readonly PositionalForecast Empty = new(0, PositionalRequirement.None, 0, 0);
}

/// <summary>
/// Publishes Wrath's upcoming positional decision without exposing Wrath's
/// internal rotation classes to another plugin.
/// </summary>
internal sealed class PositionalForecastService : IDisposable
{
    private const string GetterChannel = "WrathCombo.PositionalForecast.Get.V1";
    private const string ChangedChannel = "WrathCombo.PositionalForecast.Changed.V1";
    private const long ObservationLifetimeMs = 2500;
    private const long RefreshIntervalMs = 1000;

    private static readonly Stopwatch ObservationAge = new();
    private static Preset? lastObservedPreset;
    private static uint lastResolvedAction;
    private static ulong lastObservedTarget;

    private readonly ICallGateProvider<string> getter;
    private readonly ICallGateProvider<uint, int, int, ulong, long, object> changed;
    private readonly Stopwatch refreshAge = Stopwatch.StartNew();

    private PositionalForecast current = PositionalForecast.Empty;
    private long generation;

    internal PositionalForecastService()
    {
        getter = Svc.PluginInterface.GetIpcProvider<string>(GetterChannel);
        changed = Svc.PluginInterface.GetIpcProvider<uint, int, int, ulong, long, object>(ChangedChannel);
        getter.RegisterFunc(GetSerializedForecast);
    }

    internal static void ObserveResolvedAction(
        Preset preset,
        uint resolvedAction,
        IGameObject? targetOverride)
    {
        var attributes = preset.Attributes();
        if (attributes.AutoAction is not { IsAoE: false, IsHeal: false })
            return;

        lastObservedPreset = preset;
        lastResolvedAction = resolvedAction;
        lastObservedTarget = (targetOverride ?? CurrentTarget)?.GameObjectId ?? 0;
        ObservationAge.Restart();
    }

    internal void Update()
    {
        var next = BuildForecast();
        if (next != current || refreshAge.ElapsedMilliseconds >= RefreshIntervalMs)
            Publish(next);
    }

    private static PositionalForecast BuildForecast()
    {
        if (!Player.Available ||
            Player.IsDead ||
            !ObservationAge.IsRunning ||
            ObservationAge.ElapsedMilliseconds > ObservationLifetimeMs ||
            lastObservedPreset is null)
            return PositionalForecast.Empty;

        var target = CurrentTarget;
        var targetId = target?.GameObjectId ?? lastObservedTarget;
        if (targetId == 0)
            return PositionalForecast.Empty;

        if (TryGetOpenerForecast(targetId, out var openerForecast))
            return openerForecast;

        if (TryMap(lastResolvedAction, out var immediate))
            return new PositionalForecast(lastResolvedAction, immediate, 1, targetId);

        if (!TryGetSustainedForecast(lastResolvedAction, out var actionId, out var gcdsUntil) ||
            !TryMap(actionId, out var requirement))
            return PositionalForecast.Empty;

        return new PositionalForecast(actionId, requirement, gcdsUntil, targetId);
    }

    private static bool TryGetOpenerForecast(
        ulong targetId,
        out PositionalForecast forecast)
    {
        forecast = PositionalForecast.Empty;
        var opener = WrathOpener.CurrentOpener;
        if (opener is null ||
            opener == WrathOpener.Dummy ||
            opener.CurrentState is not (OpenerState.OpenerReady or OpenerState.InOpener) ||
            opener.OpenerStep < 1)
            return false;

        var gcdsUntil = 0;
        for (var step = opener.OpenerStep; step <= opener.OpenerActions.Count; step++)
        {
            if (opener.SkipSteps.Any(x => x.Steps.Contains(step) && x.Condition()))
                continue;

            var actionId = opener.OpenerActions[step - 1];
            if (opener.AllowUpgradeSteps.Contains(step))
                actionId = OriginalHook(actionId);

            foreach (var substitution in opener.SubstitutionSteps.Where(x => x.Steps.Contains(step)))
            {
                if (substitution.Condition())
                {
                    actionId = substitution.NewAction;
                    break;
                }
            }

            if (ActionWatching.ActionSheet.TryGetValue(actionId, out var action) &&
                action.ActionCategory.RowId == (uint)ActionWatching.ActionAttackType.Weaponskill)
                gcdsUntil++;

            if (TryMap(actionId, out var requirement))
            {
                forecast = new PositionalForecast(
                    actionId,
                    requirement,
                    Math.Max(1, gcdsUntil),
                    targetId);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSustainedForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        return Player.Job switch
        {
            ECommons.ExcelServices.Job.MNK => MNK.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            ECommons.ExcelServices.Job.DRG => DRG.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            ECommons.ExcelServices.Job.NIN => NIN.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            ECommons.ExcelServices.Job.SAM => SAM.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            ECommons.ExcelServices.Job.RPR => RPR.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            ECommons.ExcelServices.Job.VPR => VPR.TryGetPositionalForecast(resolvedAction, out actionId, out gcdsUntil),
            _ => NoForecast(out actionId, out gcdsUntil),
        };
    }

    private static bool NoForecast(out uint actionId, out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        return false;
    }

    internal static bool TryMap(
        uint actionId,
        out PositionalRequirement requirement)
    {
        requirement = actionId switch
        {
            MNK.Demolish or
            DRG.ChaosThrust or DRG.ChaoticSpring or DRG.WheelingThrust or
            NIN.TrickAttack or NIN.AeolianEdge or
            SAM.Gekko or
            RPR.Gallows or RPR.ExecutionersGallows or
            VPR.HindstingStrike or VPR.HindsbaneFang or VPR.SwiftskinsCoil
                => PositionalRequirement.Rear,

            MNK.SnapPunch or MNK.PouncingCoeurl or
            DRG.FangAndClaw or
            NIN.ArmorCrush or
            SAM.Kasha or
            RPR.Gibbet or RPR.ExecutionersGibbet or
            VPR.FlankstingStrike or VPR.FlanksbaneFang or VPR.HuntersCoil
                => PositionalRequirement.Flank,

            _ => PositionalRequirement.None,
        };

        return requirement != PositionalRequirement.None;
    }

    private void Publish(PositionalForecast forecast)
    {
        current = forecast;
        generation++;
        refreshAge.Restart();

        if (changed.SubscriptionCount > 0)
        {
            changed.SendMessage(
                current.ActionId,
                (int)current.Requirement,
                current.GcdsUntil,
                current.TargetId,
                generation);
        }
    }

    private string GetSerializedForecast() =>
        $"1|{current.ActionId}|{(int)current.Requirement}|{current.GcdsUntil}|{current.TargetId}|{generation}";

    public void Dispose()
    {
        Publish(PositionalForecast.Empty);
        getter.UnregisterFunc();
    }
}

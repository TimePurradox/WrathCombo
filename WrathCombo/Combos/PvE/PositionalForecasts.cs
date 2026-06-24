using WrathCombo.Core;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class MNK
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        if ((!PresetStorage.IsEnabled(Preset.MNK_ST_SimpleMode) &&
             !PresetStorage.IsEnabled(Preset.MNK_ST_AdvancedMode)) ||
            HasStatusEffect(Buffs.PerfectBalance))
            return false;

        actionId = CoeurlStacks is 0 && LevelChecked(Demolish)
            ? Demolish
            : OriginalHook(SnapPunch);

        if (HasStatusEffect(Buffs.CoeurlForm))
            gcdsUntil = 1;
        else if (HasStatusEffect(Buffs.RaptorForm))
            gcdsUntil = 2;
        else if (HasStatusEffect(Buffs.OpoOpoForm) ||
                 HasStatusEffect(Buffs.FormlessFist))
            gcdsUntil = 3;

        return gcdsUntil > 0;
    }
}

internal partial class DRG
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        if ((!PresetStorage.IsEnabled(Preset.DRG_ST_SimpleMode) &&
             !PresetStorage.IsEnabled(Preset.DRG_ST_AdvancedMode)) ||
            ComboTimer <= 0)
            return false;

        if (ComboAction == OriginalHook(Disembowel))
        {
            actionId = OriginalHook(ChaosThrust);
            gcdsUntil = 1;
        }
        else if (ComboAction == OriginalHook(ChaosThrust))
        {
            actionId = WheelingThrust;
            gcdsUntil = 1;
        }
        else if (ComboAction == OriginalHook(FullThrust))
        {
            actionId = FangAndClaw;
            gcdsUntil = 1;
        }
        else if (ComboAction == OriginalHook(VorpalThrust))
        {
            actionId = FangAndClaw;
            gcdsUntil = 2;
        }
        else if (ComboAction is TrueThrust or RaidenThrust)
        {
            var chaosRoute =
                LevelChecked(Disembowel) &&
                (LevelChecked(ChaosThrust) &&
                 ChaosDebuff is null &&
                 CanApplyStatus(CurrentTarget, ChaoticList[OriginalHook(ChaosThrust)]) ||
                 GetStatusEffectRemainingTime(Buffs.PowerSurge) < 15);

            actionId = chaosRoute ? OriginalHook(ChaosThrust) : FangAndClaw;
            gcdsUntil = chaosRoute ? 2 : 3;
        }

        return gcdsUntil > 0;
    }
}

internal partial class NIN
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        if ((!PresetStorage.IsEnabled(Preset.NIN_ST_SimpleMode) &&
             !PresetStorage.IsEnabled(Preset.NIN_ST_AdvancedMode)) ||
            MudraPhase)
            return false;

        if (ComboAction is not (SpinningEdge or GustSlash))
            return false;

        var burnThreshold = STSimpleMode
            ? 10
            : (int)Config.NIN_ST_AdvancedMode_BurnKazematoi;

        if (GetTargetHPPercent() <= burnThreshold && gauge.Kazematoi > 0)
            actionId = AeolianEdge;
        else
            actionId = gauge.Kazematoi switch
            {
                0 => ArmorCrush,
                >= 4 => AeolianEdge,
                _ => OnTargetsFlank() || !TargetNeedsPositionals()
                    ? ArmorCrush
                    : AeolianEdge,
            };

        gcdsUntil = ComboAction == GustSlash ? 1 : 2;
        return true;
    }
}

internal partial class SAM
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        var simpleMode = PresetStorage.IsEnabled(Preset.SAM_ST_SimpleMode);
        if (!simpleMode &&
            !PresetStorage.IsEnabled(Preset.SAM_ST_AdvancedMode))
            return false;

        if (HasStatusEffect(Buffs.MeikyoShisui))
        {
            if ((simpleMode || IsEnabled(Preset.SAM_ST_Gekko)) &&
                LevelChecked(Gekko) &&
                (!LevelChecked(Kasha) ||
                 !HasStatusEffect(Buffs.Fugetsu) ||
                 (OnTargetsRear() || OnTargetsFront()) && !HasGetsu ||
                 OnTargetsFlank() && HasKa))
                actionId = Gekko;
            else if ((simpleMode || IsEnabled(Preset.SAM_ST_Kasha)) &&
                     LevelChecked(Kasha) &&
                     (!HasStatusEffect(Buffs.Fuka) ||
                      (OnTargetsFlank() || OnTargetsFront()) && !HasKa ||
                      OnTargetsRear() && HasGetsu))
                actionId = Kasha;

            gcdsUntil = actionId == 0 ? 0 : 1;
            return gcdsUntil > 0;
        }

        if (ComboAction == Jinpu && LevelChecked(Gekko))
        {
            actionId = Gekko;
            gcdsUntil = 1;
            return true;
        }

        if (ComboAction == Shifu && LevelChecked(Kasha))
        {
            actionId = Kasha;
            gcdsUntil = 1;
            return true;
        }

        if (ComboAction is not (Hakaze or Gyofu))
            return false;

        if ((simpleMode || IsEnabled(Preset.SAM_ST_Yukikaze)) &&
            !HasSetsu &&
            LevelChecked(Yukikaze) &&
            (GetStatusEffectRemainingTime(Buffs.Fugetsu) > 7 ||
             IsNotEnabled(Preset.SAM_ST_Gekko) ||
             !LevelChecked(Kasha)) &&
            (GetStatusEffectRemainingTime(Buffs.Fuka) > 7 ||
             IsNotEnabled(Preset.SAM_ST_Kasha) ||
             !LevelChecked(Kasha)))
            return false;

        if ((simpleMode || IsEnabled(Preset.SAM_ST_Kasha)) &&
            LevelChecked(Shifu) &&
            ((OnTargetsFlank() || OnTargetsFront()) && !HasKa && LevelChecked(Kasha) ||
             OnTargetsRear() && HasGetsu && LevelChecked(Kasha) ||
             !HasStatusEffect(Buffs.Fuka) ||
             SenCount is 3 && RefreshFuka))
            actionId = Kasha;
        else if ((simpleMode || IsEnabled(Preset.SAM_ST_Gekko)) &&
                 LevelChecked(Jinpu) &&
                 (!LevelChecked(Kasha) && LevelChecked(Gekko) ||
                  (OnTargetsRear() || OnTargetsFront()) && !HasGetsu && LevelChecked(Gekko) ||
                  OnTargetsFlank() && HasKa && LevelChecked(Gekko) ||
                  !HasStatusEffect(Buffs.Fugetsu) ||
                  SenCount is 3 && RefreshFugetsu))
            actionId = Gekko;

        gcdsUntil = actionId == 0 ? 0 : 2;
        return gcdsUntil > 0;
    }
}

internal partial class RPR
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        if (!PresetStorage.IsEnabled(Preset.RPR_ST_SimpleMode) &&
            !PresetStorage.IsEnabled(Preset.RPR_ST_AdvancedMode))
            return false;

        var executioner = HasStatusEffect(Buffs.Executioner);
        if (HasStatusEffect(Buffs.EnhancedGibbet))
            actionId = executioner ? ExecutionersGibbet : Gibbet;
        else if (HasStatusEffect(Buffs.EnhancedGallows) ||
                 HasStatusEffect(Buffs.SoulReaver) ||
                 executioner)
            actionId = executioner ? ExecutionersGallows : Gallows;
        else if (resolvedAction is Gluttony or BloodStalk or UnveiledGibbet or UnveiledGallows)
            actionId = Gallows;

        gcdsUntil = actionId == 0 ? 0 : 1;
        return gcdsUntil > 0;
    }
}

internal partial class VPR
{
    internal static bool TryGetPositionalForecast(
        uint resolvedAction,
        out uint actionId,
        out int gcdsUntil)
    {
        actionId = 0;
        gcdsUntil = 0;
        if (!PresetStorage.IsEnabled(Preset.VPR_ST_SimpleMode) &&
            !PresetStorage.IsEnabled(Preset.VPR_ST_AdvancedMode))
            return false;

        if (UsedVicewinder || UsedHuntersCoil || UsedSwiftskinsCoil)
        {
            if (UsedHuntersCoil ||
                UsedVicewinder &&
                (!HasStatusEffect(Buffs.Swiftscaled) ||
                 HasBothBuffs && (!OnTargetsFlank() || !TargetNeedsPositionals()) ||
                 Config.VPR_VicewinderBuffPrio &&
                 GetStatusEffectRemainingTime(Buffs.Swiftscaled) < GCD * 6))
                actionId = SwiftskinsCoil;
            else
                actionId = HuntersCoil;

            gcdsUntil = 1;
            return true;
        }

        if (resolvedAction == Vicewinder)
        {
            actionId = !HasStatusEffect(Buffs.Swiftscaled) ||
                       HasBothBuffs && (!OnTargetsFlank() || !TargetNeedsPositionals())
                ? SwiftskinsCoil
                : HuntersCoil;
            gcdsUntil = 2;
            return true;
        }

        if (ComboAction is HuntersSting or SwiftskinsSting)
        {
            if (HasStatusEffect(Buffs.FlanksbaneVenom) ||
                HasStatusEffect(Buffs.HindsbaneVenom))
                actionId = OriginalHook(ReavingFangs);
            else if (HasStatusEffect(Buffs.FlankstungVenom) ||
                     HasStatusEffect(Buffs.HindstungVenom))
                actionId = OriginalHook(SteelFangs);

            gcdsUntil = actionId == 0 ? 0 : 1;
        }

        return gcdsUntil > 0;
    }
}

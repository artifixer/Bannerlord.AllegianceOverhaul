﻿using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using AllegianceOverhaul.Extensions;
using AllegianceOverhaul.Helpers;

namespace AllegianceOverhaul.LoyaltyRebalance.EnsuredLoyalty
{
  public static class LoyaltyManager
  {
    //BB49Q65v,xJ9hsFLS,xCyQmsJJ,QH0E2w6p,yttWp6ez,4bAtdf3F,wpPwwxHp,CMYzDghV,h3RHyeGO,s3RAXuiB,CYUKnxOH,rdpdUqkQ,uNS0QtqU,zmR6sT03
    private const string TooltipOathLoyal = "{=vhJZj4an}Under {?HERO_CLAN.UNDER_CONTRACT}mercenary service{?}oath of fealty{\\?} at least for {REMAINING_DAYS} {?REMAINING_DAYS.PLURAL_FORM}days{?}day{\\?}";
    private const string TooltipLoyal = "{=f0u5ZyFj}Loyal";
    private const string TooltipRatherLoyal = "{=oeJtbs1u}Rather loyal";
    private const string TooltipSomewhatLoyal = "{=zI5tJrm5}Somewhat loyal";
    private const string TooltipNotLoyal = "{=9jecyxIV}Not loyal";

    internal const string TransitionFromSame = "{=5EjuUUvH}Furthermore,";
    internal const string TransitionFromDifferent = "{=PKqNif5j}But";

    private const string ResultTrue = "{=rpZFLb2V}loyalty will be";
    private const string ResultFalse = "{=9Ml0rRQw}loyalty won't be";
    private const string ResultDepends = "{=adoPNE7R}loyalty might be";

    private const string RelationLow = "{=nixfDSWU}too low";
    private const string RelationHigh = "{=GYctexwu}high enough";

    private const string LeaderHasResources = "{=qGEalMT7} and {LEAVING_CLAN_KINGDOM_LEADER.NAME} possesses resourceses to withhold the clan";
    private const string LeaderHasNoResources = "{=DQ2ATMdL} but {LEAVING_CLAN_KINGDOM_LEADER.NAME} does not possess resources to withhold the clan";

    private const string ReasonIsNotEnabled = "{=ZNEdXaUc}it is not enabled";
    private const string ReasonOutOfScope = "{=RrhbXipK}faction is out of scope";
    private const string ReasonRelationEnabled = "{=TJBHPB3s}clan leader's relationship with {LEAVING_CLAN_KINGDOM_LEADER.NAME} is {CHECK_RESULT} ({CURRENT_RELATION} out of required {REQUIRED_RELATION}){WITHHOLD_PRICE_INFO}";
    private const string ReasonRelationDisabled = "{=jPA16DTJ}clan leader's relationship with {LEAVING_CLAN_KINGDOM_LEADER.NAME} does not affect it and clan fulfilled minimal obligations";
    private const string ReasonServicePeriod = "{=7jtTAw2k}clan is under {?LEAVING_CLAN.UNDER_CONTRACT}mercenary service{?}oath of fealty{\\?} for {DAYS_UNDER_SERVICE} {?DAYS_UNDER_SERVICE.PLURAL_FORM}days{?}day{\\?} out of required {REQUIRED_DAYS_UNDER_SERVICE}";

    private const string Debug_EnsuredLoyalty = "{=4R4kwdpa} {TRANSITION_PART} {LOYALTY_CHECK_RESULT} ensured, as {REASON}.";

    private static int GetKingdomFortificationsCount(Kingdom kingdom)
    {
      int Count = 0;
      foreach (Clan clan in kingdom.Clans)
      {
        Count += clan.Fortifications?.Count ?? 0;
      }
      return Count;
    }

    private static int GetHonorModifier(Hero leader, bool Defecting = false)
    {
      int HonorLevel = leader.GetTraitLevel(DefaultTraits.Honor);
      return HonorLevel < 0
          ? -HonorLevel * (Defecting ? Settings.Instance.NegativeHonorEnsuredLoyaltyModifier_Defecting : Settings.Instance.NegativeHonorEnsuredLoyaltyModifier_Leaving)
          : -HonorLevel * (Defecting ? Settings.Instance.PositiveHonorEnsuredLoyaltyModifier_Defecting : Settings.Instance.PositiveHonorEnsuredLoyaltyModifier_Leaving);
    }

    private static bool SetDebugResult(ELState state, TextObject DebugTextObject, Clan clan = null, Kingdom kingdom = null, int DaysWithKingdom = 0, int RequiredDays = 0)
    {
      switch (state)
      {
        case ELState.SystemDisabled:
          DebugTextObject.SetTextVariable("LOYALTY_CHECK_RESULT", ResultFalse);
          DebugTextObject.SetTextVariable("REASON", ReasonIsNotEnabled);
          return false;
        case ELState.FactionOutOfScope:
          DebugTextObject.SetTextVariable("LOYALTY_CHECK_RESULT", ResultFalse);
          DebugTextObject.SetTextVariable("REASON", ReasonOutOfScope);
          return false;
        case ELState.UnderRequiredService:
          TextObject ReasonPeriod = new TextObject(ReasonServicePeriod);
          StringHelper.SetNumericVariable(ReasonPeriod, "DAYS_UNDER_SERVICE", DaysWithKingdom);
          StringHelper.SetNumericVariable(ReasonPeriod, "REQUIRED_DAYS_UNDER_SERVICE", RequiredDays);
          DebugTextObject.SetTextVariable("LOYALTY_CHECK_RESULT", ResultTrue);
          DebugTextObject.SetTextVariable("REASON", ReasonPeriod.ToString());
          return true;
        case ELState.UnaffectedByRelations:
          DebugTextObject.SetTextVariable("LOYALTY_CHECK_RESULT", ResultFalse);
          TextObject ReasonDisabled = new TextObject(ReasonRelationDisabled);
          StringHelper.SetEntitiyProperties(ReasonDisabled, "LEAVING_CLAN", clan, true);
          DebugTextObject.SetTextVariable("REASON", ReasonDisabled);
          return false;
        case ELState.AffectedByRelations:
          int CurrentRelation = clan.Leader.GetRelation(clan.Kingdom.Ruler);
          int RequiredRelation = GetRelationThreshold(clan, kingdom);
          bool RelationCheckResult = CurrentRelation >= RequiredRelation;
          LoyaltyCostManager costManager = new LoyaltyCostManager(clan, kingdom);
          bool HaveResources = clan.Kingdom.RulingClan.Influence > (costManager.WithholdCost?.InfluenceCost ?? 0) && clan.Kingdom.Ruler.Gold > (costManager.WithholdCost?.GoldCost ?? 0);
          bool ShouldPay = Settings.Instance.UseWithholdPrice && Settings.Instance.WithholdToleranceLimit * 1000000 < costManager.BarterableSum;
          TextObject WithholdPrice = new TextObject(HaveResources ? LeaderHasResources : LeaderHasNoResources);
          StringHelper.SetEntitiyProperties(WithholdPrice, "LEAVING_CLAN", clan, true);
          TextObject ReasonRelation = new TextObject(ReasonRelationEnabled);
          ReasonRelation.SetTextVariable("CHECK_RESULT", RelationCheckResult ? RelationHigh : RelationLow);
          StringHelper.SetEntitiyProperties(ReasonRelation, "LEAVING_CLAN", clan, true);
          StringHelper.SetNumericVariable(ReasonRelation, "CURRENT_RELATION", CurrentRelation);
          StringHelper.SetNumericVariable(ReasonRelation, "REQUIRED_RELATION", RequiredRelation);
          ReasonRelation.SetTextVariable("WITHHOLD_PRICE_INFO", RelationCheckResult && ShouldPay ? WithholdPrice : TextObject.Empty);
          DebugTextObject.SetTextVariable("LOYALTY_CHECK_RESULT", RelationCheckResult ? (ShouldPay ? (HaveResources ? ResultDepends : ResultFalse) : ResultTrue) : ResultFalse);
          DebugTextObject.SetTextVariable("REASON", ReasonRelation.ToString());
          return RelationCheckResult && (!ShouldPay || HaveResources);
        default:
          return false;
      }
    }

    public static int GetRelationThreshold(Clan clan, Kingdom kingdom = null)
    {
      int RelationThreshold = Settings.Instance.EnsuredLoyaltyBaseline;

      if (Settings.Instance.UseContextForEnsuredLoyalty)
      {
        RelationThreshold -= RelativesHelper.BloodRelatives(clan.Kingdom.RulingClan, clan) ? Settings.Instance.BloodRelativesEnsuredLoyaltyModifier : 0;
        RelationThreshold +=
          kingdom != null && RelativesHelper.BloodRelatives(kingdom.RulingClan, clan) ? Settings.Instance.BloodRelativesEnsuredLoyaltyModifier : 0 +
          (clan.IsMinorFaction ? Settings.Instance.MinorFactionEnsuredLoyaltyModifier : 0) +
          (kingdom is null ? Settings.Instance.DefectionEnsuredLoyaltyModifier : 0) +
          (clan.Fortifications?.Count < 1 ? Settings.Instance.LandlessClanEnsuredLoyaltyModifier : 0) +
          (GetKingdomFortificationsCount(clan.Kingdom) < 1 ? Settings.Instance.LandlessKingdomEnsuredLoyaltyModifier : 0);
      }

      if (Settings.Instance.UseHonorForEnsuredLoyalty)
        RelationThreshold += GetHonorModifier(clan.Leader, kingdom is null);

      return RelationThreshold;
    }

    public static bool CheckLoyalty(Clan clan, out TextObject DebugTextObject, Kingdom kingdom = null)
    {
      DebugTextObject = new TextObject(Debug_EnsuredLoyalty);
      if (!Settings.Instance.UseEnsuredLoyalty)
      {
        return SetDebugResult(ELState.SystemDisabled, DebugTextObject);
      }
      else
      {
        if (!SettingsHelper.FactionInScope(clan, Settings.Instance.EnsuredLoyaltyScope))
        {
          return SetDebugResult(ELState.FactionOutOfScope, DebugTextObject);
        }
        else
        {
          int DaysWithKingdom = (int)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays;
          int RequiredDays = clan.IsUnderMercenaryService ? Settings.Instance.MinorFactionServicePeriod : (clan.IsMinorFaction ? Settings.Instance.MinorFactionOathPeriod : Settings.Instance.FactionOathPeriod);
          if (clan.Kingdom != null && DaysWithKingdom <= RequiredDays)
          {
            return SetDebugResult(ELState.UnderRequiredService, DebugTextObject, DaysWithKingdom: DaysWithKingdom, RequiredDays: RequiredDays);
          }
          else if (Settings.Instance.UseRelationForEnsuredLoyalty && !clan.IsUnderMercenaryService)
          {
            return SetDebugResult(ELState.AffectedByRelations, DebugTextObject, clan, kingdom);
          }
          else
          {
            return SetDebugResult(ELState.UnaffectedByRelations, DebugTextObject, clan);
          }
        }
      }
    }

    public static bool CheckLoyalty(Clan clan, Kingdom kingdom = null)
    {
      if (!SettingsHelper.SubSystemEnabled(SubSystemType.EnsuredLoyalty, clan))
        return false;

      int DaysWithKingdom = (int)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays;
      if
        (
          (clan.IsUnderMercenaryService && DaysWithKingdom <= Settings.Instance.MinorFactionServicePeriod) ||
          (!clan.IsUnderMercenaryService && clan.Kingdom != null && DaysWithKingdom <= (clan.IsMinorFaction ? Settings.Instance.MinorFactionOathPeriod : Settings.Instance.FactionOathPeriod))
        )
        return true;

      if (Settings.Instance.UseRelationForEnsuredLoyalty && !clan.IsUnderMercenaryService)
      {
        if (!(clan.Leader.GetRelation(clan.Kingdom.Ruler) >= GetRelationThreshold(clan, kingdom)))
          return false;
        else
        if (Settings.Instance.UseWithholdPrice)
        {
          LoyaltyCostManager costManager = new LoyaltyCostManager(clan, kingdom);
          if (clan.Kingdom.RulingClan == Clan.PlayerClan)
          {
            if (costManager.WithholdCost != null)
            {
              if (!(clan.Kingdom.RulingClan.Influence > costManager.WithholdCost.InfluenceCost) || !(clan.Kingdom.Ruler.Gold > costManager.WithholdCost.GoldCost))
                return false;
              costManager.AwaitPlayerDecision();
            }
            return true;
          }
          else
            return costManager.GetAIWithholdDecision();
        }
        else
          return true;
      }
      else
        return false;
    }

    public static void GetLoyaltyTooltipInfo(Clan clan, out string text, out Color color)
    {
      if (clan.Kingdom is null)
      {
        GetBlankLoyaltyTooltip(out text, out color);
        return;
      }

      int DaysWithKingdom = (int)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays;
      int RequiredDays = clan.IsUnderMercenaryService ? Settings.Instance.MinorFactionServicePeriod : (clan.IsMinorFaction ? Settings.Instance.MinorFactionOathPeriod : Settings.Instance.FactionOathPeriod);
      if (clan.Kingdom != null && DaysWithKingdom <= RequiredDays)
      {
        TextObject ReasonPeriod = new TextObject(TooltipOathLoyal);
        StringHelper.SetEntitiyProperties(ReasonPeriod, "HERO_CLAN", clan);
        StringHelper.SetNumericVariable(ReasonPeriod, "REMAINING_DAYS", RequiredDays - DaysWithKingdom);
        text = ReasonPeriod.ToString();
        color = Colors.Green;
        return;
      }
      if (!Settings.Instance.UseRelationForEnsuredLoyalty)
      {
        GetBlankLoyaltyTooltip(out text, out color);
        return;
      }
      GetAveragedLoyaltyStatus(clan, out text, out color);
    }

    private static void GetBlankLoyaltyTooltip(out string text, out Color color)
    {
      text = "-";
      color = ViewModels.TooltipHelper.DefaultTooltipColor;
    }

    private static void GetRelationThresholds(Clan clan, out int MinRelationThreshold, out int MaxRelationThreshold, out int LeaveRelationThreshold)
    {
      MinRelationThreshold = -101;
      MaxRelationThreshold = 101;
      LeaveRelationThreshold = 0;
      foreach (Kingdom kingdom in Kingdom.All)
      {
        if (kingdom == clan.Kingdom)
          LeaveRelationThreshold = GetRelationThreshold(clan, null);
        else
        {
          MinRelationThreshold = MinRelationThreshold < -100 ? GetRelationThreshold(clan, kingdom) : Math.Min(MinRelationThreshold, GetRelationThreshold(clan, kingdom));
          MaxRelationThreshold = MaxRelationThreshold > 100 ? GetRelationThreshold(clan, kingdom) : Math.Max(MaxRelationThreshold, GetRelationThreshold(clan, kingdom));
        }
      }
    }

    private static void GetAveragedLoyaltyStatus(Clan clan, out string text, out Color color)
    {
      GetRelationThresholds(clan, out int MinRelationThreshold, out int MaxRelationThreshold, out int LeaveRelationThreshold);
      int RelationWithLiege = clan.Leader.GetRelation(clan.Kingdom.Ruler);
      if (RelationWithLiege > LeaveRelationThreshold && RelationWithLiege > MaxRelationThreshold)
      {
        text = TooltipLoyal.ToLocalizedString();
        color = Colors.Green;
      }
      else
      if (RelationWithLiege > LeaveRelationThreshold && RelationWithLiege > MinRelationThreshold)
      {
        text = TooltipRatherLoyal.ToLocalizedString();
        color = ViewModels.TooltipHelper.DefaultTooltipColor;
      }
      else
      if (RelationWithLiege > MinRelationThreshold)
      {
        text = TooltipSomewhatLoyal.ToLocalizedString();
        color = ViewModels.TooltipHelper.DefaultTooltipColor;
      }
      else
      {
        text = TooltipNotLoyal.ToLocalizedString();
        color = Colors.Red;
      }
    }

    public enum ELState : byte
    {
      SystemDisabled,
      FactionOutOfScope,
      UnderRequiredService,
      UnaffectedByRelations,
      AffectedByRelations
    }
  }
}

﻿using System;
using MCM.Abstractions.Data;
using MCM.Abstractions.Settings.Base.Global;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using TaleWorlds.Localization;
using AllegianceOverhaul.Extensions;

namespace AllegianceOverhaul
{
  public partial class Settings : AttributeGlobalSettings<Settings>
  {
    private const string HeadingRelationOverhaul = "{=}Relation overhaul";

    //Relation overhaul
    [SettingPropertyBool("{=}Relation overhaul", RequireRestart = true, HintText = "{=}Enables personalized asymmetric hero relation system and specifying various other adjustments regarding hero relations, including new sources of relation shifts.")]
    [SettingPropertyGroup(HeadingRelationOverhaul, GroupOrder = 2, IsMainToggle = true)]
    public bool UseRelationOverhaul { get; set; } = false;

    [SettingPropertyDropdown("{=yul4vp54}Applies to", RequireRestart = false, HintText = "{=y3ClkEcy}Specify if below rules should affect all kingdoms, or just the player's one. Default is [All kingdoms].")]
    [SettingPropertyGroup(HeadingRelationOverhaul)]
    public DefaultDropdown<string> RelationOverhaulScope { get; set; } = new DefaultDropdown<string>(new string[]
    {
      DropdownValueAllFactions,
      DropdownValuePlayers,
      DropdownValueRuledBy
    }, 0);
  }
}

﻿using HugsLib;
using HugsLib.Settings;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BurnItForFuel
{
    public class ModBaseBurnItForFuel : ModBase
    {
        public ModBaseBurnItForFuel()
        {
            Settings.EntryName = "Burn It For Fuel";
        }
        
        public override string ModIdentifier
        {
            get
            {
                return "JPT_BurnItForFuel";
            }
        }

        public ThingFilter PossibleFuels
        {
            get
            {
                var filter = new ThingFilter();
                ThingDef[] fuelDefs = (from def in DefDatabase<ThingDef>.AllDefsListForReading
                                       where def.IsWithinCategory(ThingCategoryDefOf.Root)
                                       select def).ToArray();
                foreach (ThingDef def in fuelDefs)
                {
                    filter.SetAllow(def, true);
                }
                return filter;
            }
        }

        private static ThingFilter DefaultFuels
        {
            get
            {
                var filter = new ThingFilter();
                filter.CopyAllowancesFrom(ThingDef.Named("BurnItForFuel").building.fixedStorageSettings.filter);
                return filter;
            }
            set
            {

            }
        }

        private static Vector2 scrollPosition;

        private SettingHandle<bool> HasEverBeenSet;

        public static SettingHandle<FuelSettingsHandle> Fuels;

        public override void DefsLoaded()
        {
            HasEverBeenSet = Settings.GetHandle<bool>("HasEverBeenSet", null, null, false);
            HasEverBeenSet.NeverVisible = true;
            var fuels = Settings.GetHandle<FuelSettingsHandle>("FuelSettings", "", null, null);
            if (fuels.Value == null) fuels.Value = new FuelSettingsHandle();
            //Log.Message("baseFuelSettings has " + fuels.Value.baseFuelSettings.AllowedDefCount+" defs");
            if (fuels.Value.masterFuelSettings.AllowedDefCount == 0 && !HasEverBeenSet)
            {
                Log.Message("[BurnItForFuel] Populating fuel settings for the first time. Default fuels are: "+DefaultFuels.AllowedThingDefs.ToStringSafeEnumerable()+".");
                fuels.Value.masterFuelSettings = DefaultFuels;
                HasEverBeenSet.Value = true;
            }
            fuels.CustomDrawerHeight = 320f;
            fuels.CustomDrawer = rect => SettingsUI.CustomDrawer_ThingFilter(rect, ref scrollPosition, ref fuels.Value.masterFuelSettings, PossibleFuels);
        }

        public class FuelSettingsHandle : SettingHandleConvertible
        {
            public ThingFilter masterFuelSettings = new ThingFilter();

            public override void FromString(string settingValue)
            {
                List<ThingDef> defList = settingValue.Replace(", ", ",").Split(',').ToList().ConvertAll(e => DefDatabase<ThingDef>.GetNamed(e));
                foreach (ThingDef def in defList)
                { 
                    if(def != null) masterFuelSettings.SetAllow(def, true); 
                }
            }

            public override string ToString()
            {
                return masterFuelSettings.AllowedThingDefs.ToStringSafeEnumerable();
            }

        }

    }
}
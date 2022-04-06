// Project:         MeriTamas's (Mostly) Magic Mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2022 meritamas
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          meritamas (meritamas@outlook.com)

// This class was based on the DaggerfallSpellBookWindow class which is part of the Daggerfall Tools For Unity project - many parts retained unchanged
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Lypyl (lypyldf@gmail.com), Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Allofich, Hazelnut


using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DaggerfallConnect.Save;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;   //required for modding features
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Guilds;

namespace MTMMM
{
    public class MTSpellBookWindow : DaggerfallSpellBookWindow
    {
        public static bool spellLearning;
        public string[] onSpellSale1 = { "I understand. You must know and also understand that I cannot", "offer to teach you this spell for free.", "The guild fee needs to be covered, among other things.",
            "In addition, it will take some time and you will need to", "expend some spell points practicing the spell.", "goldcost={0}, timecost={1}, magickacost={2}, fatiguecost={3}",
            "Shall I teach you the spell now?"};        
        int actualSpellPointCost;
        int actualSpellLearningTimeCost;
        int actualSpellPointCostOfLearning;

        public MTSpellBookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {  
            if (buyMode)
            {
                uint factionID = GameManager.Instance.PlayerEnterExit.FactionID;
                MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("MTSpellBookWindow being created in buy mode, in object with a factionID of "+ factionID);
                IGuild ownerGuild = GameManager.Instance.GuildManager.GetGuild((int)factionID);
                int playerRankInGuild = ownerGuild.Rank;
                MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("Guild object successfully obtained for " + factionID+", player rank in guild: "+playerRankInGuild);
            }
        }

        protected override void PopulateSpellsList(List<EffectBundleSettings> spells, int? availableSpellPoints = null)
        {
            foreach (EffectBundleSettings spell in spells)
            {
                var effectBroker = GameManager.Instance.EntityEffectBroker;

                // Get spell costs
                // Costs can change based on player skills and stats so must be calculated each time
                FormulaHelper.SpellCost tmpSpellCost = FormulaHelper.CalculateTotalEffectCosts(spell.Effects, spell.TargetType, null, spell.MinimumCastingCost);              

                // Lycanthropy is a free spell, even though it shows a cost in classic
                // Setting cost to 0 so it displays correctly in spellbook
                if (spell.Tag == PlayerEntity.lycanthropySpellTag)
                    tmpSpellCost.spellPointCost = 0;
                                    
                int spellConfidentialityLevel = -1;

                if (spell.Effects!=null)
                    for (int i=0; i < spell.Effects.Count(); i++)
                    {
                        string effectKey = spell.Effects[i].Key;
                        IEntityEffect effect = effectBroker.GetEffectTemplate(effectKey);

                        int duration = 0;
                        if (effect.Properties.SupportDuration) duration = 1;                        

                        int magnitude = 0;
                        if (effect.Properties.SupportMagnitude) magnitude = 1;                        

                        int chance = 0;
                        if (effect.Properties.SupportChance) chance = 1;                        

                        spellConfidentialityLevel += duration + magnitude+ chance;
                    }

                if (spell.LegacyEffects != null)
                    spellConfidentialityLevel += spell.LegacyEffects.Count();

                        // TODO: here comes the code to add aditional aspects fo Spell Confidentiality Level

                // Display spell name and cost - confidentiality hidden - its role will be in the exclusion from the list of spells that are too confidential for the PC 
                ListBox.ListItem listItem;
                string spellListString = string.Format("{0}-{1}", tmpSpellCost.spellPointCost, spell.Name);
                spellsListBox.AddItem(spellListString, out listItem);
                MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("Populating SpellBox Spell List: spell '"+ spellListString+"' was found to have a confidentiality level of "+ spellConfidentialityLevel);
                                // for now, log confidentiality level to player.log

                if (availableSpellPoints != null && availableSpellPoints < tmpSpellCost.spellPointCost)         // TODO: think of other aspects based on which some spells could be shown differently
                {
                    // Desaturate unavailable spells
                    float desaturation = 0.75f;
                    listItem.textColor = Color.Lerp(listItem.textColor, Color.grey, desaturation);
                    listItem.selectedTextColor = Color.Lerp(listItem.selectedTextColor, Color.grey, desaturation);
                    listItem.highlightedTextColor = Color.Lerp(listItem.highlightedTextColor, Color.grey, desaturation);
                    listItem.highlightedSelectedTextColor = Color.Lerp(listItem.highlightedSelectedTextColor, Color.grey, desaturation);
                }
            }
        }

        protected override void LoadSpellsForSale()
        {
            // Load spells for sale
            offeredSpells.Clear();

            var effectBroker = GameManager.Instance.EntityEffectBroker;

            IEnumerable<SpellRecord.SpellRecordData> standardSpells = effectBroker.StandardSpells;
            if (standardSpells == null || standardSpells.Count() == 0)
            {
                Debug.LogError("Failed to load SPELLS.STD for spellbook in buy mode.");
                return;
            }

            // Add standard spell bundles to offer
            foreach (SpellRecord.SpellRecordData standardSpell in standardSpells)
            {
                // Filter internal spells starting with exclamation point '!'
                if (standardSpell.spellName.StartsWith("!"))
                    continue;

                // NOTE: Classic allows purchase of duplicate spells
                // If ever changing this, must ensure spell is an *exact* duplicate (i.e. not a custom spell with same name)
                // Just allowing duplicates for now as per classic and let user manage preference

                // Get effect bundle settings from classic spell
                EffectBundleSettings bundle;
                if (!effectBroker.ClassicSpellRecordDataToEffectBundleSettings(standardSpell, BundleTypes.Spell, out bundle))
                    continue;

                // Store offered spell and add to list box
                offeredSpells.Add(bundle);
            }

            // Add custom spells for sale bundles to list of offered spells
            offeredSpells.AddRange(effectBroker.GetCustomSpellBundles(EntityEffectBroker.CustomSpellBundleOfferUsage.SpellsForSale));

            // Sort spells for easier finding
            offeredSpells = offeredSpells.OrderBy(x => x.Name).ToList();
        }

        protected override void UpdateSelection()
        {
            MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("MTSpellBookWindow.UpdateSelection called.");
            if (spellLearning)
            {                
                // Update spell list scroller
                spellsListScrollBar.Reset(spellsListBox.RowsDisplayed, spellsListBox.Count, spellsListBox.ScrollIndex);
                spellsListScrollBar.TotalUnits = spellsListBox.Count;
                spellsListScrollBar.ScrollIndex = spellsListBox.ScrollIndex;

                // Get spell settings selected from player spellbook or offered spells
                EffectBundleSettings spellSettings;
                if (buyMode)
                {
                    spellSettings = offeredSpells[spellsListBox.SelectedIndex];

                    // In classic, the price shown in buy mode is the player casting cost * 4
                    // TODO: Here we could add a twist: add other relevant values (cost of instruction, potions etc) 
                    (int _, int spellPointCost) = FormulaHelper.CalculateTotalEffectCosts(spellSettings.Effects, spellSettings.TargetType);
                    presentedCost = spellPointCost * 4;
                    actualSpellPointCost = spellPointCost;
                    actualSpellPointCostOfLearning = actualSpellPointCost * 3;
                    actualSpellLearningTimeCost = MMMFormulaHelper.CalculateSpellLearningTimeCost(actualSpellPointCost);

                    // Presented cost is halved on Witches Festival holiday
                    uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
                    int holidayID = FormulaHelper.GetHolidayId(gameMinutes, 0);
                    if (holidayID == (int)DaggerfallConnect.DFLocation.Holidays.Witches_Festival)
                    {
                        presentedCost >>= 1;
                        if (presentedCost == 0)
                            presentedCost = 1;
                    }

                    spellCostLabel.Text = presentedCost.ToString();
                }
                else
                {
                    // Get spell and exit if spell index not found
                    if (!GameManager.Instance.PlayerEntity.GetSpell(spellsListBox.SelectedIndex, out spellSettings))
                    {
                        spellNameLabel.Text = string.Empty;
                        ClearEffectLabels();
                        ShowIcons(false);
                        return;
                    }
                }

                // Update spell name label
                spellNameLabel.Text = spellSettings.Name;

                // Update effect labels
                if (spellSettings.Effects != null && spellSettings.Effects.Length > 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (i < spellSettings.Effects.Length)
                            SetEffectLabels(spellSettings.Effects[i].Key, i);
                        else
                            SetEffectLabels(string.Empty, i);
                    }
                }
                else
                {
                    SetEffectLabels(string.Empty, 0);
                    SetEffectLabels(string.Empty, 1);
                    SetEffectLabels(string.Empty, 2);
                }

                // Update spell icons
                spellIconPanel.BackgroundTexture = GetSpellIcon(spellSettings.Icon);
                spellTargetIconPanel.BackgroundTexture = GetSpellTargetIcon(spellSettings.TargetType);
                spellTargetIconPanel.ToolTipText = GetTargetTypeDescription(spellSettings.TargetType);
                spellElementIconPanel.BackgroundTexture = GetSpellElementIcon(spellSettings.ElementType);
                spellElementIconPanel.ToolTipText = GetElementDescription(spellSettings.ElementType);
                ShowIcons(true);
            }
            else
                base.UpdateSelection();
        }


        protected override void BuyButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (spellLearning)
            {
                DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                const int tradeMessageBaseId = 260;
                const int notEnoughGoldId = 454;
                int tradePrice = GetTradePrice();
                int msgOffset = 0;
                EffectBundleSettings spell = offeredSpells[spellsListBox.SelectedIndex];
                FormulaHelper.SpellCost spellCost = FormulaHelper.CalculateTotalEffectCosts(spell.Effects, spell.TargetType, null, spell.MinimumCastingCost);
                actualSpellPointCost = spellCost.spellPointCost;
                actualSpellLearningTimeCost = MMMFormulaHelper.CalculateSpellLearningTimeCost(actualSpellPointCost);
                
                if (!GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.MiscItems, (int)MiscItems.Spellbook))
                {
                    DaggerfallUI.MessageBox(noSpellBook);
                }
                else if (GameManager.Instance.PlayerEntity.GetGoldAmount() < tradePrice)
                {
                    DaggerfallUI.MessageBox(notEnoughGoldId);
                }
                else if (GameManager.Instance.PlayerEntity.Stats.LiveIntelligence < actualSpellPointCost)
                {
                    DaggerfallUI.MessageBox("At present, you seem unable to master this spell. Perhaps if you were more intelligent or had more experience in the relevant school of magic.");
                }
                else
                if ((GameManager.Instance.PlayerEntity.CurrentFatigue < PlayerEntity.DefaultFatigueLoss * (actualSpellLearningTimeCost / 60+1)) ||
                    (GameManager.Instance.PlayerEntity.CurrentMagicka < actualSpellPointCost))
                {
                    DaggerfallUI.MessageBox("You are too tired to learn this spell right now. Return when you are well rested.");
                }
                else
                {
                    if (presentedCost >> 1 <= tradePrice)
                    {
                        if (presentedCost - (presentedCost >> 2) <= tradePrice)
                            msgOffset = 2;
                        else
                            msgOffset = 1;
                    }                               

                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, this);

                    string[] acturalSpellTeachBoxText = { onSpellSale1[0], onSpellSale1[1], onSpellSale1[2], onSpellSale1[3], onSpellSale1[4],
                    string.Format(onSpellSale1[5], tradePrice, actualSpellLearningTimeCost, actualSpellPointCostOfLearning, PlayerEntity.DefaultFatigueLoss * actualSpellLearningTimeCost / 60), onSpellSale1[6]};                    

                    messageBox.SetText(acturalSpellTeachBoxText, this);
                    messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
                    messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
                    messageBox.OnButtonClick += ConfirmTrade_OnButtonClick;
                    uiManager.PushWindow(messageBox);
                }
            }
            else
            {
                base.BuyButton_OnMouseClick(sender, position);
            }
        }

        protected override int GetTradePrice()          // TODO: can wait - add calculation for magicka/stamina/health potions sold
                                                        // TODO: can wait - add calculation for food provided if instruction takes longer than 4 hours - check how Ralzar's mod handles things                                                        
        {
            int classicTradePrice = FormulaHelper.CalculateTradePrice(presentedCost, buildingDiscoveryData.quality, false);

            if (!spellLearning)
                return classicTradePrice;           // if the relevant mod preference is not ticked, return as classic would, else continue and calculate our own price

            int hoursOfInstructionCompleted = (actualSpellLearningTimeCost / 60);

            int ourPresentedCost = classicTradePrice + 500 * hoursOfInstructionCompleted;      // just add a flat 500 gold fee for each hour of instruction completed
                                                // TODO: consider changing it so presented cost included the extra charges

            int tradePriceToReturn = FormulaHelper.CalculateTradePrice(ourPresentedCost, buildingDiscoveryData.quality, false);
            MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage(
                string.Format("MTSpellBookWindow.GetTradePrice: Classic Trade Price={0}, Hours Of Instruction Completed={1}, Our Presented Cost={2}, Trade Price To Return={3}.",
                classicTradePrice, hoursOfInstructionCompleted, ourPresentedCost, tradePriceToReturn));

            return tradePriceToReturn;
        }

        protected override void ConfirmTrade_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (!spellLearning)
                base.ConfirmTrade_OnButtonClick(sender, messageBoxButton);
            else
            {
                if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                {
                    // Deduct gold - adding gold sound for additional feedback
                    MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("MTSpellBookWindow.ConfirmTrade_OnButtonClickPlayer: player fatigue before change: "+ GameManager.Instance.PlayerEntity.CurrentFatigue);
                    int goldCost = GetTradePrice();
                    GameManager.Instance.PlayerEntity.DeductGoldAmount(goldCost);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);

                    actualSpellPointCostOfLearning = actualSpellPointCost * 3;      // you need to be able to cast the spell 3 times in order to learn it
                    int numberOfFatiguePointsToSubtract = PlayerEntity.DefaultFatigueLoss * actualSpellLearningTimeCost;    // '/60' divisor removed  
                    int numberOfSpellointsToSubtract = actualSpellPointCostOfLearning;

                    GameManager.Instance.PlayerEntity.DecreaseFatigue(numberOfFatiguePointsToSubtract);
                    GameManager.Instance.PlayerEntity.DecreaseMagicka(numberOfSpellointsToSubtract);

                    DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now;
                    now.RaiseTime(DaggerfallDateTime.SecondsPerMinute * actualSpellLearningTimeCost); //  actualSpellLearningTimeCost is in minutes and you have to give the value here in seconds

                    MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage(
                    string.Format("MTSpellBookWindow.ConfirmTrade_OnButtonClick: Spell learning costs incurred. Gold={0}, SpellPoints={1}, FatiguePoints={2}, Time={3} minutes.",
                    goldCost, numberOfSpellointsToSubtract, numberOfFatiguePointsToSubtract, actualSpellLearningTimeCost));
                    MMMFormulaHelper.MMMFormulaHelperSilentInfoMessage("MTSpellBookWindow.ConfirmTrade_OnButtonClickPlayer: player fatigue after change: " + GameManager.Instance.PlayerEntity.CurrentFatigue);

                    /* TODO: consider to have spell learning train the relevant magic skill too
                     * int skillAdvancementMultiplier = DaggerfallSkills.GetAdvancementMultiplier(skillToTrain);
                     * short tallyAmount = (short)(UnityEngine.Random.Range(10, 20 + 1) * skillAdvancementMultiplier);
                     * playerEntity.TallySkill(skillToTrain, tallyAmount);               */

                    // Add to player entity spellbook
                    GameManager.Instance.PlayerEntity.AddSpell(offeredSpells[spellsListBox.SelectedIndex]);
                    UpdateGold();
                }
                CloseWindow();
            }            
        }
    }
}
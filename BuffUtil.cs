﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WindowsInput;
using WindowsInput.Native;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using ExileCore.Shared.Enums;

namespace BuffUtil
{
    public class BuffUtil : BaseSettingsPlugin<BuffUtilSettings>
    {
        private readonly HashSet<Entity> loadedMonsters = new HashSet<Entity>();
        private readonly object loadedMonstersLock = new object();

        private List<Buff> buffs;
        private List<ActorSkill> skills;
        private DateTime? currentTime;
        private InputSimulator inputSimulator;
        private Random rand;
        private DateTime? lastBloodRageCast;
        private DateTime? lastPhaseRunCast;
        private DateTime? lastWitheringStepCast;
        private DateTime? lastSteelSkinCast;
        private DateTime? lastImmortalCallCast;
        private DateTime? lastMoltenShellCast;
        private DateTime? lastVaalMoltenShellCast;
        private float HPPercent;
        private float MPPercent;
        private int? nearbyMonsterCount;
        private bool showErrors = true;
        private Stopwatch movementStopwatch { get; set; } = new Stopwatch();

        public override bool Initialise()
        {
            inputSimulator = new InputSimulator();
            rand = new Random();

            showErrors = !Settings.SilenceErrors;
            Settings.SilenceErrors.OnValueChanged += delegate { showErrors = !Settings.SilenceErrors; };
            return base.Initialise();
        }

        public override void OnPluginDestroyForHotReload()
        {
            if (loadedMonsters != null)
                lock (loadedMonstersLock)
                {
                    loadedMonsters.Clear();
                }

            base.OnPluginDestroyForHotReload();
        }

        public override void Render()
        {
            // Should move to Tick?
            if (OnPreExecute())
                OnExecute();
            OnPostExecute();
        }

        private void OnExecute()
        {
            try
            {
                HandleBladeFlurry();
                HandleScourgeArrow();
                HandleBloodRage();
                HandleSteelSkin();
                HandleImmortalCall();
                HandleMoltenShell();
                HandleVaalMoltenShell();
                HandlePhaseRun();
                HandleWitheringStep();
            }
            catch (Exception ex)
            {
                if (showErrors)
                {
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(OnExecute)}: {ex.StackTrace}", 3f);
                }
            }
        }

        private void HandleBladeFlurry()
        {
            try
            {
                if (!Settings.BladeFlurry)
                    return;

                var stacksBuff = GetBuff(C.BladeFlurry.BuffName);
                if (stacksBuff == null)
                    return;

                var charges = stacksBuff.Charges;
                if (charges < Settings.BladeFlurryMinCharges.Value)
                    return;

                if (Settings.BladeFlurryWaitForInfused)
                {
                    var hasInfusedBuff = HasBuff(C.InfusedChanneling.BuffName);
                    if (!hasInfusedBuff.HasValue || !hasInfusedBuff.Value)
                        return;
                }

                if (Settings.Debug)
                    LogMessage($"Releasing Blade Flurry at {charges} charges.", 1);

                if (Settings.BladeFlurryUseLeftClick)
                {
                    inputSimulator.Mouse.LeftButtonUp();
                    inputSimulator.Mouse.LeftButtonDown();
                }
                else
                {
                    inputSimulator.Mouse.RightButtonUp();
                    inputSimulator.Mouse.RightButtonDown();
                }
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleBloodRage)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleScourgeArrow()
        {
            try
            {
                if (!Settings.ScourgeArrow)
                    return;

                var stacksBuff = GetBuff(C.ScourgeArrow.BuffName);
                if (stacksBuff == null)
                    return;

                var charges = stacksBuff.Charges;
                if (charges < Settings.ScourgeArrowMinCharges.Value)
                    return;

                if (Settings.ScourgeArrowWaitForInfused)
                {
                    var hasInfusedBuff = HasBuff(C.InfusedChanneling.BuffName);
                    if (!hasInfusedBuff.HasValue || !hasInfusedBuff.Value)
                        return;
                }

                if (Settings.Debug)
                    LogMessage($"Releasing Scourge Arrow at {charges} charges.", 1);

                if (Settings.ScourgeArrowUseLeftClick)
                {
                    inputSimulator.Mouse.LeftButtonUp();
                    inputSimulator.Mouse.LeftButtonDown();
                }
                else
                {
                    inputSimulator.Mouse.RightButtonUp();
                    inputSimulator.Mouse.RightButtonDown();
                }
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleScourgeArrow)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleBloodRage()
        {
            try
            {
                if (!Settings.BloodRage)
                    return;

                if (lastBloodRageCast.HasValue && currentTime - lastBloodRageCast.Value <
                    C.BloodRage.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.BloodRageMaxHP.Value || MPPercent > Settings.BloodRageMaxMP)
                    return;

                var hasBuff = HasBuff(C.BloodRage.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var skill = GetUsableSkill(C.BloodRage.Name, C.BloodRage.InternalName,
                    Settings.BloodRageConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Blood Rage - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Blood Rage", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.BloodRageKey.Value);
                lastBloodRageCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleBloodRage)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleSteelSkin()
        {
            try
            {
                if (!Settings.SteelSkin)
                    return;

                if (lastSteelSkinCast.HasValue && currentTime - lastSteelSkinCast.Value <
                    C.SteelSkin.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.SteelSkinMaxHP.Value)
                    return;

                var hasBuff = HasBuff(C.SteelSkin.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var skill = GetUsableSkill(C.SteelSkin.Name, C.SteelSkin.InternalName,
                    Settings.SteelSkinConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Steel Skin - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Steel Skin", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.SteelSkinKey.Value);
                lastSteelSkinCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleSteelSkin)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleImmortalCall()
        {
            try
            {
                if (!Settings.ImmortalCall)
                    return;

                if (lastImmortalCallCast.HasValue && currentTime - lastImmortalCallCast.Value <
                    C.ImmortalCall.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.ImmortalCallMaxHP.Value)
                    return;

                var hasBuff = HasBuff(C.ImmortalCall.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var skill = GetUsableSkill(C.ImmortalCall.Name, C.ImmortalCall.InternalName,
                    Settings.ImmortalCallConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Immortal Call - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Immortal Call", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.ImmortalCallKey.Value);
                lastImmortalCallCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleImmortalCall)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleMoltenShell()
        {
            try
            {
                if (!Settings.MoltenShell)
                    return;

                if (lastMoltenShellCast.HasValue && currentTime - lastMoltenShellCast.Value <
                    C.MoltenShell.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.MoltenShellMaxHP.Value)
                    return;

                var hasBuff = HasBuff(C.MoltenShell.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var skill = GetUsableSkill(C.MoltenShell.Name, C.MoltenShell.InternalName,
                    Settings.MoltenShellConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Molten Shell - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Molten Shell", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.MoltenShellKey.Value);
                lastMoltenShellCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleMoltenShell)}: {ex.StackTrace}", 3f);
            }
        }
        private void HandleVaalMoltenShell()
        {
            try
            {
                if (!Settings.VaalMoltenShell)
                    return;

                if (lastVaalMoltenShellCast.HasValue && currentTime - lastVaalMoltenShellCast.Value <
                    C.VaalMoltenShell.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.MoltenShellMaxHP.Value)
                    return;

                var hasBuff = HasBuff(C.MoltenShell.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;
                //hasBuff = HasBuff(C.VaalMoltenShell.BuffName);
                //if (!hasBuff.HasValue || hasBuff.Value)
                //    return;

                var skill = GetUsableSkill(C.VaalMoltenShell.Name, C.VaalMoltenShell.InternalName,
                    Settings.MoltenShellConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Vaal Molten Shell - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Vaal Molten Shell", 1);

                inputSimulator.Keyboard.KeyPress((VirtualKeyCode)Settings.VaalMoltenShellKey.Value);
                lastVaalMoltenShellCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleVaalMoltenShell)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandlePhaseRun()
        {
            try
            {
                if (!Settings.PhaseRun)
                    return;

                if (lastPhaseRunCast.HasValue && currentTime - lastPhaseRunCast.Value <
                    C.PhaseRun.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.PhaseRunMaxHP.Value)
                    return;

                if (movementStopwatch.ElapsedMilliseconds < Settings.PhaseRunMinMoveTime)
                    return;

                var hasBuff = HasBuff(C.PhaseRun.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var requiredBVStacks = Settings.PhaseRunMinBVStacks.Value;
                if (requiredBVStacks > 0)
                {
                    var bvBuff = GetBuff(C.BladeVortex.BuffName);
                    if (bvBuff == null || bvBuff.Charges < requiredBVStacks)
                        return;
                }

                var skill = GetUsableSkill(C.PhaseRun.Name, C.PhaseRun.InternalName,
                    Settings.PhaseRunConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Phase Run - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Phase Run", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.PhaseRunKey.Value);
                lastPhaseRunCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleSteelSkin)}: {ex.StackTrace}", 3f);
            }
        }

        private void HandleWitheringStep()
        {
            try
            {
                if (!Settings.WitheringStep)
                    return;

                if (lastWitheringStepCast.HasValue && currentTime - lastWitheringStepCast.Value <
                    C.WitheringStep.TimeBetweenCasts)
                    return;

                if (HPPercent > Settings.WitheringStepMaxHP.Value)
                    return;

                if (movementStopwatch.ElapsedMilliseconds < Settings.WitheringStepMinMoveTime)
                    return;

                var hasBuff = HasBuff(C.WitheringStep.BuffName);
                if (!hasBuff.HasValue || hasBuff.Value)
                    return;

                var skill = GetUsableSkill(C.WitheringStep.Name, C.WitheringStep.InternalName,
                    Settings.WitheringStepConnectedSkill.Value);
                if (skill == null)
                {
                    if (Settings.Debug)
                        LogMessage("Can not cast Withering Step - not found in usable skills.", 1);
                    return;
                }

                if (!NearbyMonsterCheck())
                    return;

                if (Settings.Debug)
                    LogMessage("Casting Withering Step", 1);
                inputSimulator.Keyboard.KeyPress((VirtualKeyCode) Settings.WitheringStepKey.Value);
                lastWitheringStepCast = currentTime + TimeSpan.FromSeconds(rand.NextDouble(0, 0.2));
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(HandleSteelSkin)}: {ex.StackTrace}", 3f);
            }
        }

        private bool OnPreExecute()
        {
            try
            {
                if (!Settings.Enable)
                    return false;
                var inTown = GameController.Area.CurrentArea.IsTown;
                if (inTown)
                    return false;
                if (Settings.DisableInHideout && GameController.Area.CurrentArea.IsHideout)
                    return false;
                var player = GameController.Game.IngameState.Data.LocalPlayer;
                var playerLife = player.GetComponent<Life>();
                var isDead = playerLife.CurHP <= 0;
                if (isDead)
                    return false;

                buffs = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>().Buffs;
                if (buffs == null)
                    return false;

                var gracePeriod = HasBuff(C.GracePeriod.BuffName);
                if (!gracePeriod.HasValue || gracePeriod.Value)
                    return false;

                skills = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Actor>().ActorSkills;
                if (skills == null || skills.Count == 0)
                    return false;

                currentTime = DateTime.UtcNow;

                HPPercent = 100f * playerLife.HPPercentage;
                MPPercent = 100f * playerLife.MPPercentage;
                
                var playerActor = player.GetComponent<Actor>();
                if (player != null && player.Address != 0 && playerActor.isMoving)
                {
                    if (!movementStopwatch.IsRunning)
                        movementStopwatch.Start();
                }
                else
                {
                    movementStopwatch.Reset();
                }
                

                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(OnPreExecute)}: {ex.StackTrace}", 3f);
                return false;
            }
        }

        private void OnPostExecute()
        {
            try
            {
                buffs = null;
                skills = null;
                currentTime = null;
                nearbyMonsterCount = null;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(OnPostExecute)}: {ex.StackTrace}", 3f);
            }
        }

        private bool? HasBuff(string buffName)
        {
            if (buffs == null)
            {
                if (showErrors)
                    LogError("Requested buff check, but buff list is empty.", 1);
                return null;
            }

            return buffs.Any(b => string.Compare(b.Name, buffName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private Buff GetBuff(string buffName)
        {
            if (buffs == null)
            {
                if (showErrors)
                    LogError("Requested buff retrieval, but buff list is empty.", 1);
                return null;
            }

            return buffs.FirstOrDefault(b => string.Compare(b.Name, buffName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private ActorSkill GetUsableSkill(string skillName, string skillInternalName, int skillSlotIndex)
        {
            if (skills == null)
            {
                if (showErrors)
                    LogError("Requested usable skill, but skill list is empty.", 1);
                return null;
            }

            return skills.FirstOrDefault(s => s.CanBeUsed &&
                (s.Name == skillName || s.InternalName == skillInternalName));
        }

        private bool NearbyMonsterCheck()
        {
            if (!Settings.RequireMinMonsterCount.Value)
                return true;

            if (nearbyMonsterCount.HasValue)
                return nearbyMonsterCount.Value >= Settings.NearbyMonsterCount;

            var playerPosition = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;

            List<Entity> localLoadedMonsters;
            lock (loadedMonstersLock)
            {
                localLoadedMonsters = new List<Entity>(loadedMonsters);
            }

            var maxDistance = Settings.NearbyMonsterMaxDistance.Value;
            var maxDistanceSquared = maxDistance * maxDistance;
            
            nearbyMonsterCount = GetMonsterWithin(maxDistanceSquared, playerPosition);

            var result = nearbyMonsterCount.Value >= Settings.NearbyMonsterCount;
            if (Settings.Debug.Value && !result)
                LogMessage("NearbyMonstersCheck failed.", 1);
            if(result)
                return result;

            if (Settings.NearbyMagicMonsterCount > 0)
            {
                nearbyMonsterCount = GetMonsterWithin(maxDistanceSquared, playerPosition, MonsterRarity.Magic );
                if (nearbyMonsterCount >= Settings.NearbyMagicMonsterCount)
                    return true;
            }
            if (Settings.NearbyRareMonsterCount > 0)
            {
                nearbyMonsterCount = GetMonsterWithin(maxDistanceSquared, playerPosition, MonsterRarity.Rare);
                if (nearbyMonsterCount >= Settings.NearbyRareMonsterCount)
                    return true;
            }
            if (Settings.NearbyUniqueMonsterCount > 0)
            {
                nearbyMonsterCount = GetMonsterWithin(maxDistanceSquared, playerPosition, MonsterRarity.Unique);
                if (nearbyMonsterCount >= Settings.NearbyUniqueMonsterCount)
                    return true;
            }
            return false;

        }

        private bool IsValidNearbyMonster(Entity monster, Vector3 playerPosition, int maxDistanceSquared)
        {
            try
            {
                if (!monster.IsTargetable || !monster.IsAlive || !monster.IsHostile || monster.IsHidden ||
                    !monster.IsValid)
                    return false;

                var monsterPosition = monster.Pos;

                var xDiff = playerPosition.X - monsterPosition.X;
                var yDiff = playerPosition.Y - monsterPosition.Y;
                var monsterDistanceSquare = xDiff * xDiff + yDiff * yDiff;

                return monsterDistanceSquare <= maxDistanceSquared;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    LogError($"Exception in {nameof(BuffUtil)}.{nameof(IsValidNearbyMonster)}: {ex.StackTrace}", 3f);
                return false;
            }
        }
        public int GetMonsterWithin(float maxDistance, Vector3 playerPosition, MonsterRarity rarity = MonsterRarity.White)
        {
            int count = 0;
            float maxDistanceSquare = maxDistance * maxDistance;
            foreach (var monster in loadedMonsters.Where(x=> x.Rarity>=rarity && (x.IsValid && x.IsHostile && !x.IsHidden && x.IsTargetable)))
            {              
                var monsterPosition = monster.Pos;

                var xDiff = playerPosition.X - monsterPosition.X;
                var yDiff = playerPosition.Y - monsterPosition.Y;
                var monsterDistanceSquare = (xDiff * xDiff + yDiff * yDiff);

                if (monsterDistanceSquare <= maxDistanceSquare)
                {
                    count++;
                }

            }
            return count;
        }

        private bool IsMonster(Entity entity) => entity != null && entity.HasComponent<Monster>();

        public override void EntityAdded(Entity entity)
        {
            if (!IsMonster(entity))
                return;

            lock (loadedMonstersLock)
            {
                loadedMonsters.Add(entity);
            }
        }

        public override void EntityRemoved(Entity entity)
        {
            lock (loadedMonstersLock)
            {
                loadedMonsters.Remove(entity);
            }
        }
    }
}